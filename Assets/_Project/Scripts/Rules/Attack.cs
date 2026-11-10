using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Rules
{
    /// <summary>Everything that happened in one soldier's attack, step by step.</summary>
    public class AttackResult
    {
        public KeywordOutcome Keywords;
        /// <summary>The target couldn't be targeted (e.g. in full cover, or out of sight); the attack wasn't made.</summary>
        public bool NotTargetable;
        /// <summary>Why the target couldn't be targeted.</summary>
        public string NotTargetableReason;
        /// <summary>Step 7: keywords prevented the attack (e.g. Toxic vs a target that isn't Biological).</summary>
        public bool NoEffect;
        /// <summary>Step 8: Base Damage adjusted by keyword bonuses/penalties.</summary>
        public int FinalDamage;
        /// <summary>Step 9: the Armor Level the weapon's Penetration was compared against (stat + keyword changes).</summary>
        public int TargetArmor;
        /// <summary>Steps 9 and 11: penetration vs armor, then the crit bonus.</summary>
        public DamageResult Damage;
        /// <summary>Step 10: accuracy and the hit roll (and step 11's crit roll).</summary>
        public AccuracyResult Accuracy;
        public AttackRoll Roll;
        /// <summary>Step 12: damage the target actually took (0 on a miss or no effect).</summary>
        public int DamageDealt;

        public bool Hit => !NotTargetable && !NoEffect && Roll.Hit;
        public bool Crit => Hit && Roll.Crit;
        /// <summary>Whether the weapon's bonus effects (e.g. Explosive) apply: a hit that armor didn't block.</summary>
        public bool BonusEffectsApply => Hit && Damage.BonusEffectsApply;
    }

    /// <summary>
    /// One soldier's attack on one target, following the shooting order (steps 7–12; see <see cref="UnitShooting"/>
    /// for the whole sequence):
    ///    —  Can the target be targeted at all? Not if in full cover behind it or out of line of sight,
    ///       unless the attack ignores line of sight (e.g. Indirect fire).
    ///    7. Do keywords prevent the attack? (e.g. Toxic vs a target that isn't Biological) → if so, it ends.
    ///    8. Damage: Base Damage − keyword penalties (not below 0) + keyword bonuses.
    ///    9. Penetration vs armor modifies that damage (×1.5 / ×1 / ×0.5 / ×0, rounded down).
    ///   10. Accuracy (stat − penalties + bonuses, or an override), then the hit roll.
    ///   11. If accuracy is over 100%, a crit roll; a crit adds 100% of the post-penetration damage.
    ///   12. Damage is applied to the target.
    /// </summary>
    public static class Attack
    {
        public static AttackResult Resolve(Soldier attacker, Weapon weapon, Soldier target, bool attackerSprinted,
            System.Random rng, bool applyDamage = true)
        {
            var result = new AttackResult { Keywords = KeywordRules.Evaluate(attacker, weapon, target) };

            string reason = WhyNotTargetable(attacker, weapon, target, result.Keywords);
            if (reason != null)
            {
                result.NotTargetable = true;
                result.NotTargetableReason = reason;
                return result;
            }

            // 7. Keywords preventing the attack.
            if (result.Keywords.NoEffect)
            {
                result.NoEffect = true;
                return result;
            }

            // 8. Damage.
            result.FinalDamage = FinalDamage(weapon.BaseDamage, result.Keywords);

            // 9. Armor penetration (against the target's Armor Level, adjusted by keywords such as Divine Sanctuary).
            result.TargetArmor = ArmorAgainst(target, result.Keywords);
            var penetration = ArmorPenetration.Resolve(result.FinalDamage, weapon.PenetrationValue, result.TargetArmor);

            // 10. Accuracy and hit roll.
            result.Accuracy = Rules.Accuracy.ForAttack(attacker, weapon, attackerSprinted, result.Keywords.AccuracyModifiers,
                ignoreModifiers: result.Keywords.IgnoresAccuracyModifiers);
            result.Roll = Rules.Accuracy.Roll(result.Accuracy, rng);
            if (!result.Roll.Hit)
            {
                result.Damage = new DamageResult(result.FinalDamage, penetration, false, 0);
                return result;
            }

            // 11. Crit roll (made only when accuracy is over 100%) and crit bonus on the post-penetration damage.
            int critBonus = result.Roll.Crit ? penetration.Damage : 0;
            result.Damage = new DamageResult(result.FinalDamage, penetration, result.Roll.Crit, critBonus);

            // 12. Apply damage.
            result.DamageDealt = result.Damage.Total;
            if (applyDamage && result.DamageDealt > 0 && target.IsAlive) target.TakeDamage(result.DamageDealt);
            else if (!target.IsAlive) result.DamageDealt = 0; // shooting a soldier already down does nothing
            return result;
        }

        /// <summary>Whether the attacker can choose this target with this weapon (in range, in sight, not in full cover…).</summary>
        public static bool CanTarget(Soldier attacker, Weapon weapon, Soldier target)
        {
            if (target == null || !target.CanBeTargeted) return false;
            return WhyNotTargetable(attacker, weapon, target, KeywordRules.Evaluate(attacker, weapon, target)) == null;
        }

        /// <summary>Is the target within the weapon's range (horizontal distance; weapons without a range have no limit)?</summary>
        public static bool InRange(Soldier attacker, Weapon weapon, Soldier target) =>
            weapon == null || !weapon.HasRangeLimit || Measure.Between(attacker, target) <= weapon.Range + RangeTolerance;

        private const float RangeTolerance = 0.01f;

        /// <summary>Null if the target can be attacked; otherwise the reason it can't.</summary>
        private static string WhyNotTargetable(Soldier attacker, Weapon weapon, Soldier target, KeywordOutcome keywords)
        {
            if (!InRange(attacker, weapon, target)) return $"Out of range ({weapon.Range:0} m)";
            if (keywords.CannotBeTargeted) return "Target is in full cover";
            if (!keywords.IgnoresLineOfSight && !LineOfSight.CanSee(attacker, target)) return "No line of sight";
            return null;
        }

        /// <summary>The target's Armor Level for this attack: their stat plus keyword armor bonuses/penalties (not below 0).</summary>
        public static int ArmorAgainst(Soldier target, KeywordOutcome keywords) => Mathf.Max(0, target.ArmorLevel + keywords.ArmorModifier);

        /// <summary>Step 8: Base Damage, minus keyword penalties (not below 0), plus keyword bonuses.</summary>
        public static int FinalDamage(int baseDamage, KeywordOutcome keywords)
        {
            int damage = Mathf.Max(0, baseDamage - keywords.DamagePenalty);
            return damage + keywords.DamageBonus;
        }
    }
}
