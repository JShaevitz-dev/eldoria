using System.Collections.Generic;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.AI
{
    public enum AIActionKind
    {
        None,
        Shoot,
        Ability
    }

    /// <summary>What an AI unit has decided to do with its action (after moving).</summary>
    public class AIAction
    {
        public AIActionKind Kind;
        public Unit ShootAt;
        /// <summary>For units with Sniper Precision soldiers: the enemy soldier they shoot.</summary>
        public Soldier ShootAtSoldier;
        public Ability Ability;
        public Soldier User;
        public Soldier TargetSoldier;
        public Unit TargetUnit;
        public Vector3? TargetPoint;
        /// <summary>Why, for the log.</summary>
        public string Reason = "";

        public static readonly AIAction Nothing = new AIAction { Kind = AIActionKind.None, Reason = "nothing useful to do" };
    }

    /// <summary>
    /// The AI opponent's decisions. Uses only the rules (no visuals or input), so it can be tested on its own.
    /// Each unit, in turn:
    ///  1. Moves (or holds). Fighting units (infantry squads) push toward the enemy, preferring spots in cover from which
    ///     enemies are in sight and within weapon range, avoiding close range and avoiding sprinting (it costs accuracy). Support units
    ///     (Field Commander, Battle Priest) stay a few meters behind their own squads, in cover where they can; the
    ///     priest never sprints, so it can still cast.
    ///  2. Acts: heals a wounded friendly soldier (2+ health missing) if it can; otherwise blesses a friendly unit that
    ///     enemies can see with a unit buff (Divine Sanctuary: it lasts until the AI's next turn, so through the player's);
    ///     otherwise shoots the enemy unit the most of its soldiers can target (a sniper picks a soldier: Solo soldiers and
    ///     sergeants first, otherwise the closest); otherwise throws a grenade (once per
    ///     battle) at a spot hitting enemies but no friends.
    /// </summary>
    public static class AIPlanner
    {
        private const float CloseRangePenalty = 40f;
        private const float SprintPenalty = 6f;
        /// <summary>A move must score this much better than holding position to be worth making.</summary>
        private const float MoveThreshold = 1f;
        private static readonly float[] Radii = { 3f, 6f, 9f, 12f, 16f, 20f };
        private const int Directions = 12;

        /// <summary>The order the side's units act in: the Field Commander first (so its aura is in place), then
        /// fighting units, then the other support units (healers act on what the fighting has left).</summary>
        public static List<Unit> ActivationOrder(Side side)
        {
            var units = Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.Side == side && !u.IsDestroyed)
                .OrderBy(u => u.transform.GetSiblingIndex()).ToList();
            int Rank(Unit u) => HasAura(u) ? 0 : IsSupport(u) ? 2 : 1;
            return units.OrderBy(Rank).ToList();
        }

        private static bool HasAura(Unit u) => u.LivingSoldiers.Any(s => s.Abilities.Any(a => a != null && a.IsAura));

        /// <summary>A unit of one officer (Field Commander, Battle Priest) that supports the squads rather than leading the attack.</summary>
        public static bool IsSupport(Unit u) => u.LivingSoldiers.Count() == 1 && u.LivingSoldiers.First().HasKeyword("Officer");

        private static bool HasSpells(Unit u) => u.LivingSoldiers.Any(s => s.Abilities.Any(a => a != null && a.IsSpell));

        public static List<Soldier> Enemies(Unit unit) =>
            Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(s => s.IsAlive && s.Unit != null && s.Unit.Side != unit.Side).ToList();

        // ------------------------------------------------------------------ 1. Moving

        /// <summary>
        /// Where the unit leader should move this activation, or null to hold position.
        /// <paramref name="activation"/> is the unit's fresh move (nothing moved yet); candidates are checked with it.
        /// </summary>
        public static Vector3? ChooseLeaderMove(Unit unit, UnitMoveActivation activation)
        {
            var leader = activation.Leader;
            var enemies = Enemies(unit);
            if (leader == null || enemies.Count == 0) return null;

            bool support = IsSupport(unit);
            bool noSprint = support && HasSpells(unit);
            Vector3? anchor = support ? FriendlyAnchor(unit) : null;

            float holdScore = Score(unit, leader, leader.Position, MoveKind.Hold, enemies, anchor);
            float bestScore = holdScore + MoveThreshold;
            Vector3? best = null;

            foreach (var radius in Radii)
            {
                if (radius > leader.Sprint + 0.01f) continue;
                for (int i = 0; i < Directions; i++)
                {
                    float a = (i + (radius % 2f)) * Mathf.PI * 2f / Directions;   // stagger rings
                    var point = leader.Position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                    var check = activation.CheckLeaderMove(point);
                    if (!check.Ok) continue;
                    if (noSprint && check.Kind == MoveKind.Sprint) continue;
                    float s = Score(unit, leader, check.Destination, check.Kind, enemies, anchor);
                    if (s > bestScore)
                    {
                        bestScore = s;
                        best = check.Destination;
                    }
                }
            }
            return best;
        }

        /// <summary>How good a spot is for the unit leader (higher is better).</summary>
        public static float Score(Unit unit, Soldier leader, Vector3 at, MoveKind kind, List<Soldier> enemies, Vector3? supportAnchor)
        {
            float nearest = enemies.Min(e => Measure.Horizontal(e.Position, at));
            var nearestEnemy = enemies.OrderBy(e => Measure.Horizontal(e.Position, at)).First();
            // Enemies the leader could shoot from here: in sight and within the weapon's range.
            var weapon = leader.PrimaryWeapon;
            float range = weapon != null && weapon.HasRangeLimit ? weapon.Range : float.MaxValue;
            int visible = enemies.Count(e => Measure.Horizontal(e.Position, at) <= range && ShotGeometry.CanSeeFrom(at, leader.Height, e));
            int cover = CoverAt(at, nearestEnemy);
            bool closeRange = leader.HasKeyword("Biological") &&
                              enemies.Any(e => e.HasKeyword("Biological") && Measure.Horizontal(e.Position, at) < CloseRange.Distance);

            float score;
            if (supportAnchor.HasValue)
            {
                // Stay about 5 m from the squads, and no closer to the enemy than they are.
                float fromSquads = Measure.Horizontal(at, supportAnchor.Value);
                float anchorToEnemy = enemies.Min(e => Measure.Horizontal(e.Position, supportAnchor.Value));
                score = -Mathf.Abs(fromSquads - 5f) + cover * 6f - Mathf.Max(0f, anchorToEnemy - nearest) * 1.5f;
            }
            else
            {
                // Push toward the enemy; having enemies in sight and in range (to shoot) and being in cover from them
                // are worth a lot.
                score = -0.6f * nearest + (visible > 0 ? 12f + Mathf.Min(visible, 10) : 0f) + cover * 10f;
            }
            if (closeRange) score -= CloseRangePenalty;
            if (kind == MoveKind.Sprint) score -= SprintPenalty;
            return score;
        }

        /// <summary>Cover at a spot against an enemy: 0 none, 1 half-cover, 2 full cover (the covering object must be between).</summary>
        public static int CoverAt(Vector3 at, Soldier enemy)
        {
            int best = 0;
            foreach (var obj in Object.FindObjectsByType<TerrainObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                foreach (var k in obj.Keywords)
                {
                    if (k == null || k.grantsNearby == null) continue;
                    if (!k.grantsWhenBetween && obj.HorizontalDistanceTo(at) > k.grantRadius) continue;
                    if (!ShotGeometry.Crosses(enemy.Position, at, obj)) continue;
                    best = Mathf.Max(best, k.grantsNearby.Is("In full cover") ? 2 : 1);
                }
            }
            return best;
        }

        /// <summary>Where a support unit should stay near: the middle of the side's other (fighting) units.</summary>
        private static Vector3? FriendlyAnchor(Unit unit)
        {
            var friends = Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u != unit && u.Side == unit.Side && !u.IsDestroyed && !IsSupport(u))
                .SelectMany(u => u.LivingSoldiers).ToList();
            if (friends.Count == 0) return null;
            var sum = Vector3.zero;
            foreach (var s in friends) sum += s.Position;
            return sum / friends.Count;
        }

        // ------------------------------------------------------------------ 2. Acting

        /// <summary>What the unit should do with its action, from where its soldiers stand now.</summary>
        public static AIAction ChooseAction(Unit unit)
        {
            if (unit == null || unit.IsDestroyed) return AIAction.Nothing;

            var heal = ChooseHeal(unit);
            if (heal != null) return heal;

            var buff = ChooseUnitBuff(unit);
            if (buff != null) return buff;

            // A unit that only has Sniper Precision soldiers (e.g. the Sniper) shoots a soldier, not a unit: pick the
            // soldier from every enemy unit, then shoot at their unit.
            if (unit.LivingSoldiers.All(s => s.HasKeyword(UnitShooting.SniperPrecisionKeyword)))
            {
                var pick = ChooseSniperTarget(unit, null);
                return pick == null
                    ? new AIAction { Kind = AIActionKind.None, Reason = "no valid target for its sniper" }
                    : new AIAction
                    {
                        Kind = AIActionKind.Shoot, ShootAt = pick.Unit, ShootAtSoldier = pick,
                        Reason = $"sniper picks {pick.name}{(IsValuableTarget(pick) ? " (a valuable target)" : "")}"
                    };
            }

            var unitAttack = ChooseUnitAttack(unit);
            if (unitAttack != null) return unitAttack;

            var aimed = ChooseAimedAttack(unit);
            if (aimed != null) return aimed;

            var shoot = ChooseShootTarget(unit, out int shooters);
            if (shoot != null)
                return new AIAction
                {
                    Kind = AIActionKind.Shoot, ShootAt = shoot, ShootAtSoldier = ChooseSniperTarget(unit, shoot),
                    Reason = $"{shooters} soldier(s) can target {shoot.DisplayName}"
                };

            var grenade = ChooseAreaAttack(unit);
            if (grenade != null) return grenade;

            return AIAction.Nothing;
        }

        /// <summary>
        /// A unit-attack ability (e.g. Firestorm) on the enemy unit in reach with the most soldiers it would affect, if that's
        /// at least 2. Null otherwise.
        /// </summary>
        private static AIAction ChooseUnitAttack(Unit unit)
        {
            foreach (var (user, ability) in ActiveAbilities.UnitAbilities(unit))
            {
                if (user == null || !ability.IsUnitAttack || ActiveAbilities.WhyCannotCast(user, ability) != null) continue;
                Unit best = null;
                int bestCount = 1;
                foreach (var enemy in Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (enemy.Side == unit.Side || enemy.IsDestroyed) continue;
                    if (ActiveAbilities.WhyCannotUse(user, ability, targetUnit: enemy) != null) continue;
                    int count = enemy.LivingSoldiers.Count(s => ability.targetMustHave == null || s.HasKeyword(ability.targetMustHave.displayName));
                    if (count > bestCount) { best = enemy; bestCount = count; }
                }
                if (best != null)
                    return new AIAction
                    {
                        Kind = AIActionKind.Ability, Ability = ability, User = user, TargetUnit = best,
                        Reason = $"{ability.displayName} on {best.DisplayName} ({bestCount} soldiers)"
                    };
            }
            return null;
        }

        /// <summary>
        /// An aimed-attack ability. With a weapon that armor stops (e.g. the Rocket Launcher) it's saved for heavily armored
        /// targets (Armor Level 2 or more, where rifles do little or nothing): tanks first, then the most heavily armored,
        /// then the closest. With one that ignores armor (no penetration value, e.g. Fireball) it's used on anyone: valuable
        /// targets (Solo, sergeants) first, then the closest. Otherwise the unit shoots as usual. Null if there's no target.
        /// </summary>
        private static AIAction ChooseAimedAttack(Unit unit)
        {
            foreach (var (user, ability) in ActiveAbilities.UnitAbilities(unit))
            {
                if (user == null || !ability.IsAimedAttack || ActiveAbilities.WhyCannotCast(user, ability) != null) continue;
                bool ignoresArmor = !ability.attackWeapon.PenetrationValue.HasValue;
                var weapon = user.AbilityWeapon(ability.attackWeapon);
                var valid = Enemies(unit).Where(s => ActiveAbilities.WhyCannotUse(user, ability, s) == null
                    && ArmorPenetration.Resolve(weapon.BaseDamage, weapon.PenetrationValue, s.ArmorLevel).Damage > 0);   // it would hurt them
                var target = ignoresArmor
                    ? valid.OrderByDescending(IsValuableTarget).ThenBy(s => Measure.Between(user, s)).FirstOrDefault()
                    : valid.Where(s => s.ArmorLevel >= 2)
                        .OrderByDescending(s => s.HasKeyword("Tank"))
                        .ThenByDescending(s => s.ArmorLevel)
                        .ThenBy(s => Measure.Between(user, s))
                        .FirstOrDefault();
                if (target != null)
                    return new AIAction
                    {
                        Kind = AIActionKind.Ability, Ability = ability, User = user, TargetSoldier = target,
                        Reason = $"{ability.displayName} at {target.name} (Armor {target.ArmorLevel})"
                    };
            }
            return null;
        }

        /// <summary>Heal the friendly soldier missing the most health (2 or more), if a heal ability can reach one.</summary>
        private static AIAction ChooseHeal(Unit unit)
        {
            foreach (var (user, ability) in ActiveAbilities.UnitAbilities(unit))
            {
                if (user == null || !ActiveAbilities.IsHealOnly(ability) || ability.targets != AbilityTarget.Soldier) continue;
                var target = Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(s => s.IsAlive && s.MaxHealth - s.Health >= 2 && ActiveAbilities.WhyCannotUse(user, ability, s) == null)
                    .OrderByDescending(s => s.MaxHealth - s.Health).FirstOrDefault();
                if (target != null)
                    return new AIAction
                    {
                        Kind = AIActionKind.Ability, Ability = ability, User = user, TargetSoldier = target,
                        Reason = $"{target.name} is missing {target.MaxHealth - target.Health} health"
                    };
            }
            return null;
        }

        /// <summary>
        /// A keyword-granting ability aimed at a friendly unit (e.g. Divine Sanctuary): on the unit in range that the most
        /// enemy soldiers can see (ties: the bigger unit), unless it already has the keyword. Null if no unit is exposed.
        /// </summary>
        private static AIAction ChooseUnitBuff(Unit unit)
        {
            foreach (var (user, ability) in ActiveAbilities.UnitAbilities(unit))
            {
                if (user == null || ability.targets != AbilityTarget.Unit || ability.grantsKeyword == null) continue;
                var enemies = Enemies(unit);
                Unit best = null;
                int bestSeen = 0, bestSize = 0;
                foreach (var friend in Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (friend.Side != unit.Side || friend.IsDestroyed) continue;
                    if (ActiveAbilities.WhyCannotUse(user, ability, targetUnit: friend) != null) continue;
                    var soldiers = friend.LivingSoldiers.ToList();
                    if (soldiers.All(s => s.HasKeyword(ability.grantsKeyword.displayName))) continue;   // already blessed
                    int seen = enemies.Count(e => soldiers.Any(s => ShotGeometry.CanSee(e, s)));
                    if (seen > bestSeen || (seen == bestSeen && seen > 0 && soldiers.Count > bestSize))
                    {
                        best = friend;
                        bestSeen = seen;
                        bestSize = soldiers.Count;
                    }
                }
                if (best != null)
                    return new AIAction
                    {
                        Kind = AIActionKind.Ability, Ability = ability, User = user, TargetUnit = best,
                        Reason = $"{best.DisplayName} is in sight of {bestSeen} enemy soldier(s)"
                    };
            }
            return null;
        }

        /// <summary>
        /// The enemy soldier the unit's Sniper Precision soldiers shoot (null if it has none, or nothing to shoot).
        /// Valuable targets first - soldiers with the Solo keyword, or sergeants - the closest of them; otherwise the
        /// closest soldier the sniper can target. <paramref name="enemy"/>: only soldiers of that unit (a mixed unit shooting
        /// at it); null: any enemy soldier (a unit of snipers).
        /// </summary>
        public static Soldier ChooseSniperTarget(Unit unit, Unit enemy)
        {
            var sniper = unit.LivingSoldiers.FirstOrDefault(s => s.HasKeyword(UnitShooting.SniperPrecisionKeyword) && s.WeaponFor(null) != null);
            if (sniper == null) return null;
            var weapon = sniper.WeaponFor(null);
            var pool = enemy != null
                ? enemy.LivingSoldiers
                : Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(t => t.IsAlive && t.Unit != null && t.Unit.Side != unit.Side);
            return pool.Where(t => UnitShooting.IsValidTarget(sniper, weapon, t))
                .OrderBy(t => IsValuableTarget(t) ? 0 : 1)
                .ThenBy(t => Measure.Between(sniper, t))
                .FirstOrDefault();
        }

        /// <summary>
        /// A target the AI's snipers prefer: a soldier with the Solo keyword, or a sergeant (a sergeant soldier type, e.g.
        /// "Sergeant" or "Sergeant in Powered Armor"; a trooper who has taken over command doesn't count).
        /// </summary>
        public static bool IsValuableTarget(Soldier s) =>
            s.HasKeyword("Solo") || (s.Template != null && s.Template.displayName.Contains("Sergeant"));

        /// <summary>The enemy unit the most of this unit's soldiers can target (ties: the nearest). Null if none.</summary>
        public static Unit ChooseShootTarget(Unit unit, out int shooters)
        {
            shooters = 0;
            Unit best = null;
            float bestDist = float.MaxValue;
            var enemyUnits = Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(u => u.Side != unit.Side && !u.IsDestroyed);
            foreach (var enemy in enemyUnits)
            {
                int count = 0;
                foreach (var s in unit.LivingSoldiers)
                {
                    var weapon = s.WeaponFor(null);
                    if (weapon == null) continue;
                    if (enemy.LivingSoldiers.Any(t => UnitShooting.IsValidTarget(s, weapon, t))) count++;
                }
                if (count == 0) continue;
                float dist = enemy.LivingSoldiers.Min(t => Measure.Horizontal(t.Position, unit.Leader != null ? unit.Leader.Position : t.Position));
                if (count > shooters || (count == shooters && dist < bestDist))
                {
                    best = enemy;
                    shooters = count;
                    bestDist = dist;
                }
            }
            return best;
        }

        /// <summary>
        /// An area attack (e.g. Frag Grenade) at the spot that hits the most enemies and no friendly soldiers. Null if no
        /// spot hits an enemy without a friend.
        /// </summary>
        private static AIAction ChooseAreaAttack(Unit unit)
        {
            foreach (var (user, ability) in ActiveAbilities.UnitAbilities(unit))
            {
                if (user == null || !ability.IsAreaAttack || ActiveAbilities.WhyCannotCast(user, ability) != null) continue;
                var all = Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(s => s.IsAlive).ToList();
                var enemies = all.Where(s => s.Unit != null && s.Unit.Side != unit.Side).ToList();

                // Candidate spots: on each enemy in range, and between pairs of nearby enemies.
                var spots = new List<Vector3>();
                foreach (var e in enemies)
                {
                    spots.Add(e.Position);
                    foreach (var f in enemies)
                        if (f != e && Measure.Horizontal(e.Position, f.Position) < ability.areaRadius * 2f) spots.Add((e.Position + f.Position) / 2f);
                }

                Vector3? bestSpot = null;
                int bestHits = 0;
                foreach (var spot in spots)
                {
                    if (ActiveAbilities.WhyCannotUse(user, ability, targetPoint: spot) != null) continue;
                    var caught = all.Where(s => Measure.ToPoint(s, spot) <= ability.areaRadius && BlastCheck.Reaches(spot, s)).ToList();
                    if (caught.Any(s => s.Unit != null && s.Unit.Side == unit.Side)) continue;   // never hit our own
                    int hits = caught.Count;
                    if (hits > bestHits)
                    {
                        bestHits = hits;
                        bestSpot = spot;
                    }
                }
                if (bestSpot.HasValue)
                    return new AIAction
                    {
                        Kind = AIActionKind.Ability, Ability = ability, User = user, TargetPoint = bestSpot,
                        Reason = $"{bestHits} enemy soldier(s) in the blast"
                    };
            }
            return null;
        }

        /// <summary>Moves soldiers' transforms to where an activation has placed them (instant movement; tests and Skip).</summary>
        public static void ApplyPositions(UnitMoveActivation activation)
        {
            foreach (var s in activation.Unit.LivingSoldiers)
                if (activation.Positions.TryGetValue(s, out var p)) s.transform.position = p;
        }
    }
}
