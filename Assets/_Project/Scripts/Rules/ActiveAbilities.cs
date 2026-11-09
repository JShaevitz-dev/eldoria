using System.Collections.Generic;
using System.Linq;
using Eldoira.Units;

namespace Eldoira.Rules
{
    /// <summary>What happened when an active ability was used.</summary>
    public class AbilityResult
    {
        public bool Used;
        public string Reason;
        public readonly List<string> Log = new List<string>();
    }

    /// <summary>
    /// Using active abilities: the user picks a target soldier or unit that is the right side (e.g. friendly), within
    /// range (horizontal; for a unit, its closest soldier), and has any required keyword (e.g. Biological). Each
    /// soldier can use each ability a limited number of times per turn (once by default).
    /// </summary>
    public static class ActiveAbilities
    {
        private const float Epsilon = 0.01f;

        /// <summary>Null if the ability can be used on this target; otherwise why not.</summary>
        public static string WhyCannotUse(Soldier user, Ability ability, Soldier targetSoldier = null, Unit targetUnit = null)
        {
            if (user == null || !user.IsAlive) return "The user can't act";
            if (ability == null || !ability.IsActive) return "Not an active ability";
            if (!user.Abilities.Contains(ability)) return $"{user.name} doesn't have {ability.displayName}";
            if (user.TimesUsedThisTurn(ability) >= ability.usesPerTurn) return $"{ability.displayName} has already been used this turn";

            if (ability.targets == AbilityTarget.Soldier)
            {
                if (targetSoldier == null) return "Choose a soldier";
                if (!targetSoldier.IsAlive) return $"{targetSoldier.name} is down";
                if (!SideOk(ability, user, targetSoldier)) return $"{targetSoldier.name} isn't a valid target";
                if (Measure.Between(user, targetSoldier) > ability.range + Epsilon) return $"Out of range ({ability.range:0} m)";
                if (ability.targetMustHave != null && !targetSoldier.HasKeyword(ability.targetMustHave.displayName))
                    return $"Target must be {ability.targetMustHave.displayName}";
                return null;
            }

            if (targetUnit == null || targetUnit.IsDestroyed) return "Choose a unit";
            var living = targetUnit.LivingSoldiers.ToList();
            if (!SideOk(ability, user, living[0])) return $"{targetUnit.DisplayName} isn't a valid target";
            if (targetUnit == user.Unit && !ability.includeSelf) return "Can't target its own unit";
            if (living.Min(s => Measure.Between(user, s)) > ability.range + Epsilon) return $"Out of range ({ability.range:0} m)";
            return null;
        }

        private static bool SideOk(Ability ability, Soldier user, Soldier target)
        {
            if (target == user) return ability.includeSelf;
            bool sameSide = user.Unit != null && target.Unit != null && user.Unit.Side == target.Unit.Side;
            return ability.affects == AuraAffects.Everyone
                   || (ability.affects == AuraAffects.Allies && sameSide)
                   || (ability.affects == AuraAffects.Enemies && !sameSide);
        }

        /// <summary>Uses the ability on a target soldier (or unit, for unit-targeting abilities).</summary>
        public static AbilityResult Use(Soldier user, Ability ability, Soldier targetSoldier = null, Unit targetUnit = null)
        {
            var result = new AbilityResult { Reason = WhyCannotUse(user, ability, targetSoldier, targetUnit) };
            if (result.Reason != null) return result;

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
                    s.GainKeyword(ability.grantsKeyword, ability.grantUntilNextTurn ? Turns.NextTurn : 0);
                    result.Log.Add($"{user.name}'s {ability.displayName}: {s.name} gains {ability.grantsKeyword.displayName}.");
                }
            }

            user.RecordAbilityUse(ability);
            result.Used = true;
            return result;
        }
    }
}
