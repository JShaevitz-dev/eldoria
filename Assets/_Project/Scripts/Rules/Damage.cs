using Eldoira.Units;

namespace Eldoira.Rules
{
    /// <summary>Damage dealt by a hit, with each step for display.</summary>
    public readonly struct DamageResult
    {
        /// <summary>The final damage calculation (before armor and crits).</summary>
        public readonly int FinalDamage;
        public readonly PenetrationOutcome Penetration;
        public readonly bool Crit;
        /// <summary>Extra damage from a crit: 100% of the damage after penetration.</summary>
        public readonly int CritBonus;
        /// <summary>Damage the target actually takes.</summary>
        public readonly int Total;

        public DamageResult(int finalDamage, PenetrationOutcome penetration, bool crit, int critBonus)
        {
            FinalDamage = finalDamage;
            Penetration = penetration;
            Crit = crit;
            CritBonus = critBonus;
            Total = penetration.Damage + critBonus;
        }

        /// <summary>False when armor blocked the attack: the weapon's bonus effects don't apply.</summary>
        public bool BonusEffectsApply => Penetration.BonusEffectsApply;
    }

    /// <summary>
    /// Damage from a hit, in this order:
    ///   1. The final damage calculation.
    ///   2. Penetration vs armor (×1.5 / ×1 / ×0.5 / ×0, rounded down) — see <see cref="ArmorPenetration"/>.
    ///   3. A crit adds a bonus equal to 100% of the damage from step 2.
    /// </summary>
    public static class Damage
    {
        public static DamageResult Resolve(int finalDamage, int penetration, int armorLevel, bool crit)
        {
            var pen = ArmorPenetration.Resolve(finalDamage, penetration, armorLevel);
            int critBonus = crit ? pen.Damage : 0;
            return new DamageResult(finalDamage, pen, crit, critBonus);
        }

        public static DamageResult Resolve(int finalDamage, Weapon weapon, Soldier target, bool crit)
        {
            return Resolve(finalDamage, weapon.Penetration, target.ArmorLevel, crit);
        }
    }
}
