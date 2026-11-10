using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Rules
{
    /// <summary>What happened when an active ability was used.</summary>
    public class AbilityResult
    {
        public bool Used;
        public string Reason;
        public readonly List<string> Log = new List<string>();
        /// <summary>For area attacks: everyone caught in the area, and the damage each took (0 if armor or keywords stopped it).</summary>
        public readonly List<(Soldier soldier, int damage)> AreaHits = new List<(Soldier soldier, int damage)>();
        /// <summary>For area attacks: soldiers within the radius whom terrain shielded from the blast.</summary>
        public readonly List<Soldier> Shielded = new List<Soldier>();
        /// <summary>For area attacks: where it landed.</summary>
        public Vector3? Point;
    }

    /// <summary>
    /// Whether an explosion at a spot reaches a soldier. Under normal circumstances it can't go through object terrain or
    /// structure terrain (see <see cref="Terrain.ShotGeometry.BlastReaches"/>). Replaceable (tests, special rules).
    /// </summary>
    public static class BlastCheck
    {
        public static System.Func<Vector3, Soldier, bool> Reaches = Terrain.ShotGeometry.BlastReaches;
    }

    /// <summary>
    /// Using active abilities: the user picks a target soldier, unit, or spot on the ground within range (horizontal; for a
    /// unit, its closest soldier). A target soldier must be the right side (e.g. friendly) and have any required keyword
    /// (e.g. Biological). Each soldier can use each ability a limited number of times per turn (once by default); some
    /// abilities can also only be used a number of times per battle by each unit (e.g. Frag Grenade: once).
    /// When several soldiers of a unit have the same ability, the unit uses it once, through <see cref="UserFor"/>.
    /// </summary>
    public static class ActiveAbilities
    {
        private const float Epsilon = 0.01f;

        /// <summary>Raised after an ability has been used successfully (user, ability, result), for sound and effects.</summary>
        public static event System.Action<Soldier, Ability, AbilityResult> AbilityUsed;

        /// <summary>
        /// Which soldier uses an ability on the unit's behalf: the unit leader if they have it and can use it right now,
        /// otherwise the first living soldier who has it and can (e.g. one who isn't in close range, for a Frag Grenade);
        /// if nobody can, the leader or first soldier who has it (so the reason can be shown). Null if nobody has it.
        /// Ranges are measured from this soldier.
        /// </summary>
        public static Soldier UserFor(Unit unit, Ability ability)
        {
            if (unit == null || ability == null) return null;
            var leader = unit.Leader;
            var holders = unit.LivingSoldiers.Where(s => s.Abilities.Contains(ability)).ToList();
            if (leader != null && holders.Contains(leader)) holders.Insert(0, leader);
            var able = holders.FirstOrDefault(s => WhyCannotCast(s, ability) == null);
            return able ?? holders.FirstOrDefault();
        }

        /// <summary>
        /// Every active ability the unit's soldiers have, once each (duplicates condensed), with the soldier who would use it.
        /// </summary>
        public static List<(Soldier user, Ability ability)> UnitAbilities(Unit unit)
        {
            var list = new List<(Soldier, Ability)>();
            if (unit == null) return list;
            foreach (var s in unit.LivingSoldiers)
            foreach (var a in s.Abilities)
                if (a != null && a.IsActive && !list.Any(e => e.Item2 == a))
                    list.Add((UserFor(unit, a), a));
            return list;
        }

        /// <summary>
        /// Null if the soldier can use the ability at all right now (whatever the target); otherwise why not.
        /// Spells can't be cast by a soldier whose unit sprinted this turn.
        /// </summary>
        public static string WhyCannotCast(Soldier user, Ability ability)
        {
            if (user == null || !user.IsAlive) return "The user can't act";
            if (ability == null || !ability.IsActive) return "Not an active ability";
            if (!user.Abilities.Contains(ability)) return $"{user.name} doesn't have {ability.displayName}";
            if (user.TimesUsedThisTurn(ability) >= ability.usesPerTurn) return $"{ability.displayName} has already been used this turn";
            if (user.Unit != null && user.Unit.UsesLeftThisBattle(ability) <= 0)
                return $"{ability.displayName} has already been used this battle";
            if (ability.IsSpell && user.Unit != null && user.Unit.SprintedThisTurn)
                return $"{ability.displayName} is a Spell and can't be cast after sprinting";
            // Abilities that use a weapon (e.g. Frag Grenade) follow the close range rule; magic and others don't.
            if (ability.areaWeapon != null && WeaponUse.WhyCannotUse(user, ability.areaWeapon) is string why)
                return $"{user.name} can't use {ability.displayName}: {why}";
            return null;
        }

        /// <summary>Null if the ability can be used on this target; otherwise why not.</summary>
        /// <param name="targetPoint">For abilities aimed at the ground (e.g. Frag Grenade): the chosen spot.</param>
        public static string WhyCannotUse(Soldier user, Ability ability, Soldier targetSoldier = null, Unit targetUnit = null,
            Vector3? targetPoint = null)
        {
            string cannotCast = WhyCannotCast(user, ability);
            if (cannotCast != null) return cannotCast;

            if (ability.targets == AbilityTarget.Point)
            {
                // No line of sight needed: grenades are lobbed over walls.
                if (!targetPoint.HasValue) return "Choose a spot on the ground";
                if (!MovementRules.IsInsideBattlefield(targetPoint.Value, 0f)) return "Outside the battlefield";
                if (Measure.ToPoint(user, targetPoint.Value) > ability.range + Epsilon) return $"Out of range ({ability.range:0} m)";
                return null;
            }

            if (ability.targets == AbilityTarget.Soldier)
            {
                if (targetSoldier == null) return "Choose a soldier";
                if (!targetSoldier.IsAlive) return $"{targetSoldier.name} is down";
                if (!SideOk(ability, user, targetSoldier)) return $"{targetSoldier.name} isn't a valid target";
                if (Measure.Between(user, targetSoldier) > ability.range + Epsilon) return $"Out of range ({ability.range:0} m)";
                if (ability.targetMustHave != null && !targetSoldier.HasKeyword(ability.targetMustHave.displayName))
                    return $"Target must be {ability.targetMustHave.displayName}";
                // A purely healing ability (e.g. Divine Healing) would do nothing to a soldier at full health.
                if (IsHealOnly(ability) && targetSoldier.Health >= targetSoldier.MaxHealth)
                    return $"{targetSoldier.name} is already at full health";
                return null;
            }

            if (targetUnit == null || targetUnit.IsDestroyed) return "Choose a unit";
            var living = targetUnit.LivingSoldiers.ToList();
            if (!SideOk(ability, user, living[0])) return $"{targetUnit.DisplayName} isn't a valid target";
            if (targetUnit == user.Unit && !ability.includeSelf) return "Can't target its own unit";
            if (living.Min(s => Measure.Between(user, s)) > ability.range + Epsilon) return $"Out of range ({ability.range:0} m)";
            return null;
        }

        /// <summary>The ability's only effect is restoring health.</summary>
        public static bool IsHealOnly(Ability ability) => ability != null && ability.heal > 0 && ability.grantsKeyword == null;

        private static bool SideOk(Ability ability, Soldier user, Soldier target)
        {
            if (target == user) return ability.includeSelf;
            bool sameSide = user.Unit != null && target.Unit != null && user.Unit.Side == target.Unit.Side;
            return ability.affects == AuraAffects.Everyone
                   || (ability.affects == AuraAffects.Allies && sameSide)
                   || (ability.affects == AuraAffects.Enemies && !sameSide);
        }

        /// <summary>Uses the ability on a target soldier, unit, or spot on the ground.</summary>
        public static AbilityResult Use(Soldier user, Ability ability, Soldier targetSoldier = null, Unit targetUnit = null,
            Vector3? targetPoint = null)
        {
            var result = new AbilityResult { Reason = WhyCannotUse(user, ability, targetSoldier, targetUnit, targetPoint) };
            if (result.Reason != null) return result;

            if (ability.targets == AbilityTarget.Point)
            {
                result.Point = targetPoint;
                if (ability.IsAreaAttack) AreaAttack(user, ability, targetPoint.Value, result);
            }
            else
            {
                var affected = ability.targets == AbilityTarget.Soldier
                    ? new List<Soldier> { targetSoldier }
                    : targetUnit.LivingSoldiers.ToList();
                foreach (var s in affected)
                {
                    if (ability.targetMustHave != null && !s.HasKeyword(ability.targetMustHave.displayName)) continue;
                    if (ability.heal > 0)
                    {
                        int restored = s.Heal(ability.heal);
                        result.Log.Add($"{user.name}'s {ability.displayName}: {s.name} restores {restored} health ({s.Health}/{s.MaxHealth}).");
                    }
                    if (ability.grantsKeyword != null)
                    {
                        s.GainKeyword(ability.grantsKeyword, ability.grantUntilNextTurn ? Turns.NextTurnOf(user) : 0);   // the caster's next turn
                        result.Log.Add($"{user.name}'s {ability.displayName}: {s.name} gains {ability.grantsKeyword.displayName}.");
                    }
                }
            }

            user.RecordAbilityUse(ability);
            if (user.Unit != null) user.Unit.RecordBattleUse(ability);
            result.Used = true;
            AbilityUsed?.Invoke(user, ability, result);
            return result;
        }

        /// <summary>
        /// An area attack (e.g. Frag Grenade): every soldier within the radius of the spot - friend or foe - is hit by the
        /// ability's weapon, unless object or structure terrain lies between them and the blast (see <see cref="BlastCheck"/>;
        /// cover keywords themselves don't matter). There is no hit roll. For each soldier hit, as for shooting:
        /// keywords (e.g. "no effect"), damage (Base Damage − penalties + bonuses), then penetration vs armor (armor
        /// keywords such as Divine Sanctuary count). Soldiers who take damage fire their "when damaged" triggers.
        /// Anyone killed is removed when the blast is over.
        /// </summary>
        private static void AreaAttack(Soldier user, Ability ability, Vector3 point, AbilityResult result)
        {
            var blast = AreaAttacks.Resolve(user, ability.areaWeapon, point, ability.areaRadius);
            int caught = blast.Hits.Count;
            result.Log.Add($"{user.name}'s {ability.displayName} lands {Measure.ToPoint(user, point):0.0} m away: " +
                           (caught == 0 ? "no one is caught in the blast." : $"{caught} caught in the blast."));
            result.Log.AddRange(blast.Log);
            result.AreaHits.AddRange(blast.Hits);
            result.Shielded.AddRange(blast.Shielded);
        }
    }
}
