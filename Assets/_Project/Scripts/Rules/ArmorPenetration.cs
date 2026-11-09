using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Rules
{
    public enum PenetrationResult
    {
        /// <summary>Penetration is 2 or more below Armor Level: no damage, no bonus effects.</summary>
        Blocked,
        /// <summary>Penetration is 1 below Armor Level: 50% damage.</summary>
        Reduced,
        /// <summary>Penetration equals Armor Level: 100% damage.</summary>
        Normal,
        /// <summary>Penetration is above Armor Level by any amount: 150% damage.</summary>
        Overpenetrating
    }

    public readonly struct PenetrationOutcome
    {
        public readonly PenetrationResult Result;
        /// <summary>Multiplier applied to the final damage calculation (0, 0.5, 1 or 1.5).</summary>
        public readonly float Multiplier;
        /// <summary>Damage after the multiplier, rounded down.</summary>
        public readonly int Damage;
        /// <summary>False when the attack is blocked: the weapon's bonus effects (e.g. keywords) don't apply.</summary>
        public readonly bool BonusEffectsApply;

        public PenetrationOutcome(PenetrationResult result, float multiplier, int damage, bool bonusEffectsApply)
        {
            Result = result;
            Multiplier = multiplier;
            Damage = damage;
            BonusEffectsApply = bonusEffectsApply;
        }
    }

    /// <summary>
    /// When shooting, the weapon's Penetration is compared with the target's Armor Level:
    ///   Penetration > Armor          → 150% of the final damage
    ///   Penetration = Armor          → 100%
    ///   Penetration = Armor − 1      → 50%
    ///   Penetration ≤ Armor − 2      → 0%, and the attack's bonus effects don't apply
    /// The multiplier is applied to the final damage calculation (the last step); fractions are rounded down.
    /// </summary>
    public static class ArmorPenetration
    {
        public static PenetrationResult Compare(int penetration, int armorLevel)
        {
            int diff = penetration - armorLevel;
            if (diff > 0) return PenetrationResult.Overpenetrating;
            if (diff == 0) return PenetrationResult.Normal;
            if (diff == -1) return PenetrationResult.Reduced;
            return PenetrationResult.Blocked;
        }

        public static float Multiplier(PenetrationResult result)
        {
            switch (result)
            {
                case PenetrationResult.Overpenetrating: return 1.5f;
                case PenetrationResult.Normal: return 1f;
                case PenetrationResult.Reduced: return 0.5f;
                default: return 0f;
            }
        }

        /// <summary>Applies penetration vs armor to an attack's final damage.</summary>
        public static PenetrationOutcome Resolve(int finalDamage, int penetration, int armorLevel)
        {
            var result = Compare(penetration, armorLevel);
            float multiplier = Multiplier(result);
            int damage = Mathf.FloorToInt(Mathf.Max(0, finalDamage) * multiplier);
            return new PenetrationOutcome(result, multiplier, damage, result != PenetrationResult.Blocked);
        }

        /// <summary>Applies a weapon's Penetration against a soldier's Armor Level to an attack's final damage.</summary>
        public static PenetrationOutcome Resolve(int finalDamage, Weapon weapon, Soldier target)
        {
            return Resolve(finalDamage, weapon.Penetration, target.ArmorLevel);
        }
    }
}
