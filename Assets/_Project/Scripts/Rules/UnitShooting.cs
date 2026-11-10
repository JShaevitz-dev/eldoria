using System.Collections.Generic;
using System.Linq;
using Eldoria.Terrain;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Rules
{
    /// <summary>One soldier's shot during a unit's shooting.</summary>
    public class SoldierShot
    {
        public Soldier Shooter;
        public Weapon Weapon;
        /// <summary>Picked in step 6 (null if the shooter had no valid target).</summary>
        public Soldier Target;
        /// <summary>Terrain objects in the line of fire between shooter and target.</summary>
        public List<TerrainObject> ObjectsInLine = new List<TerrainObject>();
        /// <summary>Steps 7–12 (null if the shot wasn't taken).</summary>
        public AttackResult Attack;
        /// <summary>The target was already dead when this soldier fired (the shot is still made, but does nothing).</summary>
        public bool TargetAlreadyDown;
        /// <summary>Enemy soldiers this shooter picked in step 6 but whose attack keywords prevented (step 7).</summary>
        public List<Soldier> Rejected = new List<Soldier>();
        public string Note;
    }

    /// <summary>The outcome of one unit shooting at another.</summary>
    public class ShootingResult
    {
        public Unit Attackers;
        public Unit Defenders;
        /// <summary>Step 5: at least one attacking soldier can see at least one defending soldier.</summary>
        public bool HasLineOfSight;
        /// <summary>Step 5: terrain objects in the lines of sight between the units.</summary>
        public readonly List<TerrainObject> ObjectsInLineOfSight = new List<TerrainObject>();
        public readonly List<SoldierShot> Shots = new List<SoldierShot>();
        /// <summary>Everything that happened, in order, for display.</summary>
        public readonly List<string> Log = new List<string>();

        public int ShotsTaken => Shots.Count(s => s.Attack != null && !s.Attack.NotTargetable && !s.Attack.NoEffect);
        public int Hits => Shots.Count(s => s.Attack != null && s.Attack.Hit);
        public int Crits => Shots.Count(s => s.Attack != null && s.Attack.Crit);
        public int TotalDamage => Shots.Sum(s => s.Attack?.DamageDealt ?? 0);
        public int Kills;
        /// <summary>At least one soldier fired a shot (a soldier with no valid target doesn't).</summary>
        public bool AnyShotsFired => Shots.Any(s => s.Attack != null);
    }

    /// <summary>
    /// A unit shooting at an enemy unit: one "volley". Steps 1–4 (select unit, optionally move, declare shooting, pick the enemy unit)
    /// happen in the controller; this resolves the rest:
    ///    5. Line of sight: can the attacking unit see the defending unit? Note any objects in the lines of sight.
    ///    6. Each attacking soldier picks the closest defending soldier they can target (several may pick the same one).
    ///       Picking fires the target's "when targeted" keyword triggers (even if no attack follows), resolved before
    ///       carrying on — at most once per soldier per volley (this unit's sequence of shooting).
    ///    7. If keywords would prevent that attack, the soldier goes back to step 6 for the next-closest soldier,
    ///       until it finds one it can shoot — or it doesn't shoot this turn.
    ///   8–12. Each soldier's attack in turn (see <see cref="Attack"/>): damage → penetration → accuracy and hit roll →
    ///       crit roll → damage applied. A soldier shoots the target it picked even if it has died in the meantime.
    ///   13. A soldier who took damage fires their "when damaged" keyword triggers.
    /// A soldier can only pick a target within their weapon's range (horizontal; e.g. Assault Rifle 20 m, Pistol 10 m).
    /// Soldiers shoot with their primary weapon - except in close range (a Biological enemy within 2 m), where only a
    /// Pistol can be used: soldiers with one switch to it, the rest don't shoot (see <see cref="CloseRange"/>).
    /// Otherwise soldiers shoot with their primary weapon. Soldiers killed in this volley stay targetable until it ends, then are
    /// removed from the battlefield (see <see cref="Casualties"/>), so the next attacking unit can't target them.
    /// </summary>
    public static class UnitShooting
    {
        /// <summary>Raised when a unit finishes a volley (after its casualties are removed), for sound and effects.</summary>
        public static event System.Action<ShootingResult> VolleyFinished;

        /// <summary>How many of the attacking unit's soldiers have at least one soldier of the defending unit they can
        /// target (in range, in sight, not prevented by keywords). 0 means the unit can't usefully shoot at it.</summary>
        /// <param name="chosenTarget">The enemy soldier the player picked: Sniper Precision soldiers can only shoot them.</param>
        public static int SoldiersAbleToShoot(Unit attackers, Unit defenders, Soldier chosenTarget = null)
        {
            int count = 0;
            foreach (var s in attackers.LivingSoldiers)
            {
                var weapon = s.WeaponFor(null);
                if (weapon == null) continue;
                bool able = s.HasKeyword(SniperPrecisionKeyword)
                    ? chosenTarget != null && defenders.Soldiers.Contains(chosenTarget) && IsValidTarget(s, weapon, chosenTarget)
                    : defenders.Soldiers.Any(t => IsValidTarget(s, weapon, t));
                if (able) count++;
            }
            return count;
        }

        /// <summary>Can this soldier shoot this target with this weapon (in range and sight, not in full cover, not prevented by keywords)?</summary>
        public static bool IsValidTarget(Soldier shooter, Weapon weapon, Soldier target) =>
            target != null && weapon != null && Attack.CanTarget(shooter, weapon, target) && !KeywordRules.Evaluate(shooter, weapon, target).NoEffect;

        /// <summary>
        /// Does the unit have anything to shoot at, anywhere (any of its soldiers, any enemy soldier)? If not, it can't
        /// shoot this turn: it can use an ability, if it has one, or do nothing.
        /// </summary>
        public static bool HasAnyTarget(Unit attackers)
        {
            var enemies = Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.CanBeTargeted && t.Unit != null && t.Unit.Side != attackers.Side).ToList();
            foreach (var s in attackers.LivingSoldiers)
            {
                var weapon = s.WeaponFor(null);
                if (weapon != null && enemies.Any(t => IsValidTarget(s, weapon, t))) return true;
            }
            return false;
        }

        /// <summary>Soldier keyword: when this soldier shoots, the player picks the specific enemy soldier they target.</summary>
        public const string SniperPrecisionKeyword = "Sniper Precision";

        /// <param name="chosenTarget">The enemy soldier the player picked (by clicking them). Soldiers with Sniper Precision
        /// shoot only this soldier (they make no choice of their own: with no pick, or one they can't target, they don't
        /// shoot). Everyone else picks the closest soldier they can target, as usual.</param>
        public static ShootingResult Resolve(Unit attackers, Unit defenders, bool attackersSprinted, System.Random rng,
            Soldier chosenTarget = null)
        {
            var result = new ShootingResult { Attackers = attackers, Defenders = defenders };
            Casualties.BeginVolley();
            try
            {
                ResolveVolley(result, attackers, defenders, attackersSprinted, rng, chosenTarget);
            }
            finally
            {
                // The round of shooting is over: soldiers killed in it are removed, so later volleys can't target them.
                var removed = Casualties.EndVolley();
                if (removed.Count > 0)
                    result.Log.Add("Removed from the battlefield: " + string.Join(", ", removed.Where(s => s != null).Select(s => s.name)) + ".");
            }
            VolleyFinished?.Invoke(result);
            return result;
        }

        private static void ResolveVolley(ShootingResult result, Unit attackers, Unit defenders, bool attackersSprinted, System.Random rng,
            Soldier chosenTarget)
        {
            var shooters = attackers.LivingSoldiers.ToList();
            var targets = defenders.Soldiers.Where(t => t != null && t.CanBeTargeted).ToList();

            // 5. Line of sight between the units, noting objects in the way.
            foreach (var s in shooters)
            foreach (var t in targets)
            {
                if (!LineOfSight.CanSee(s, t)) continue;
                result.HasLineOfSight = true;
                foreach (var obj in ShotGeometry.ObjectsBetween(s.Position, t.Position))
                    if (!result.ObjectsInLineOfSight.Contains(obj)) result.ObjectsInLineOfSight.Add(obj);
            }
            if (!result.HasLineOfSight)
            {
                // Indirect fire doesn't need line of sight: allow shooting if any soldier's weapon ignores it.
                bool anyIndirect = shooters.Any(s => s.WeaponFor(null) != null && targets.Any(t =>
                    KeywordRules.Evaluate(s, s.WeaponFor(null), t).IgnoresLineOfSight));
                if (!anyIndirect)
                {
                    result.Log.Add($"{attackers.DisplayName} can't see {defenders.DisplayName}.");
                    return;
                }
            }
            if (result.ObjectsInLineOfSight.Count > 0)
                result.Log.Add("In the line of sight: " + string.Join(", ", result.ObjectsInLineOfSight.Select(o => o.DisplayName).Distinct()));

            // 6–7. Each soldier picks the closest enemy it can shoot. Picking fires "when targeted" triggers (an interrupt).
            // If keywords would prevent the attack (step 7), it goes back to step 6 and tries the next closest,
            // until it finds a target it can shoot or runs out — then it doesn't shoot this turn.
            var candidates = defenders.Soldiers.Where(t => t != null && t.CanBeTargeted).ToList();
            // "When targeted" fires whenever a soldier is picked — even if no attack follows — but only once per volley
            // (one attacking unit's sequence of shooting), however many of that unit's soldiers pick them.
            // Another attacking unit's volley later in the turn can trigger it again.
            var alreadyTargeted = new HashSet<Soldier>();
            foreach (var shooter in shooters)
            {
                var shot = new SoldierShot { Shooter = shooter, Weapon = shooter.WeaponFor(null) };
                result.Shots.Add(shot);
                if (shot.Weapon == null)
                {
                    var primary = shooter.PrimaryWeapon;
                    shot.Note = primary == null ? "no weapon" : WeaponUse.WhyCannotUse(shooter, primary) ?? "no usable weapon";
                    if (primary != null) result.Log.Add($"{shooter.name} can't shoot: {shot.Note}.");
                    continue;
                }

                // Normally the closest first. Sniper Precision: only the soldier the player picked - the soldier makes no
                // decision of their own (no pick, or a pick they can't shoot: no shot).
                bool precise = shooter.HasKeyword(SniperPrecisionKeyword);
                var order = precise
                    ? (chosenTarget != null && candidates.Contains(chosenTarget) ? new List<Soldier> { chosenTarget } : new List<Soldier>())
                    : candidates.OrderBy(t => Measure.Between(shooter, t)).ToList();

                foreach (var t in order)
                {
                    if (!Attack.CanTarget(shooter, shot.Weapon, t)) continue;            // out of sight / in full cover

                    // 6. Picked: resolve any "when targeted" triggers now (once per soldier per volley).
                    if (alreadyTargeted.Add(t))
                        result.Log.AddRange(KeywordTriggers.Fire(TriggerEvent.Targeted, t, shooter));

                    // 7. Would keywords prevent this attack? Then look for another target.
                    var keywords = KeywordRules.Evaluate(shooter, shot.Weapon, t);
                    if (keywords.NoEffect || !Attack.CanTarget(shooter, shot.Weapon, t))
                    {
                        shot.Rejected.Add(t);
                        result.Log.Add($"{shooter.name} can't usefully shoot {t.name} ({string.Join("; ", keywords.Applied)}); looks for another target.");
                        continue;
                    }

                    shot.Target = t;
                    break;
                }

                if (shot.Target == null && precise)
                {
                    shot.Note = chosenTarget == null || !candidates.Contains(chosenTarget)
                        ? "Sniper Precision: no soldier picked in this unit"
                        : $"Sniper Precision: can't target {chosenTarget.name}";
                    result.Log.Add($"{shooter.name}: {shot.Note}; doesn't shoot.");
                    continue;
                }
                if (shot.Target == null)
                {
                    bool anyInRange = candidates.Any(t => Attack.InRange(shooter, shot.Weapon, t));
                    shot.Note = anyInRange ? "no target it can shoot" : $"no target within range ({shot.Weapon.Range:0} m)";
                    result.Log.Add($"{shooter.name} has {shot.Note} and won't shoot this turn.");
                    continue;
                }
                shot.ObjectsInLine = ShotGeometry.ObjectsBetween(shooter.Position, shot.Target.Position);
            }

            // 8–13. Resolve each soldier's attack in turn, at the target it picked — even if that soldier has died meanwhile.
            foreach (var shot in result.Shots)
            {
                if (shot.Target == null) continue;
                shot.TargetAlreadyDown = !shot.Target.IsAlive;

                bool wasAlive = shot.Target.IsAlive;
                shot.Attack = Attack.Resolve(shot.Shooter, shot.Weapon, shot.Target, attackersSprinted, rng);
                result.Log.Add(Describe(shot));

                // 13. Triggers from receiving damage (a soldier already dead takes no more damage).
                if (wasAlive && shot.Attack.DamageDealt > 0)
                    result.Log.AddRange(KeywordTriggers.Fire(TriggerEvent.Damaged, shot.Target, shot.Shooter));
                if (wasAlive && !shot.Target.IsAlive)
                {
                    result.Kills++;
                    result.Log.Add($"{shot.Target.name} is down.");
                }
            }
            // (the volley ends in Resolve: casualties are removed there)
        }

        private static string Describe(SoldierShot shot)
        {
            var a = shot.Attack;
            string who = $"{shot.Shooter.name} → {shot.Target.name}{(shot.TargetAlreadyDown ? " (already down)" : "")}";
            if (a.NotTargetable) return $"{who}: can't target ({a.NotTargetableReason}).";
            if (a.NoEffect) return $"{who}: no effect ({string.Join("; ", a.Keywords.Applied)}).";
            string chance = a.Accuracy.CritChance > 0 ? $"{a.Accuracy.HitChance}% (+{a.Accuracy.CritChance}% crit)" : $"{a.Accuracy.HitChance}%";
            if (!a.Hit) return $"{who}: miss at {chance}.";
            return $"{who}: {(a.Crit ? "CRIT" : "hit")} at {chance} for {a.DamageDealt}.";
        }
    }
}
