using System;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// A faction's change to a weapon's stats, added to the weapon's own (negative values subtract). Several changes to
    /// the same weapon add up.
    /// </summary>
    [Serializable]
    public class WeaponAdjustment
    {
        [Tooltip("The weapon (stat block) this changes, under whatever name it appears.")]
        public WeaponTemplate weapon;
        public int damage;
        public int shots;
        [Tooltip("Ignored for weapons with no penetration value (they always do 100% damage).")]
        public int penetration;
        public int accuracyBonus;
        [Tooltip("Meters. Ignored for weapons with unlimited range.")]
        public float range;
    }

    /// <summary>The name a faction uses for a weapon (an alias): same stat block, different name.</summary>
    [Serializable]
    public class WeaponName
    {
        public WeaponTemplate weapon;
        public string name;
    }

    /// <summary>
    /// A weapon's stats for a given faction: the weapon template's own stats plus the faction's adjustments, under the
    /// faction's name for it. With no faction (or none that changes it) these are just the template's stats.
    /// Limits: damage at least 0, shots at least 1, penetration at least 0 (a weapon with no penetration value keeps
    /// none), range at least 1 m (unlimited range stays unlimited).
    /// </summary>
    public readonly struct WeaponStats
    {
        public readonly string Name;
        public readonly int BaseDamage;
        public readonly int Shots;
        public readonly int? PenetrationValue;
        public readonly int AccuracyBonus;
        /// <summary>Meters (horizontal); 0 = unlimited.</summary>
        public readonly float Range;

        private WeaponStats(string name, int damage, int shots, int? penetration, int accuracy, float range)
        {
            Name = name;
            BaseDamage = damage;
            Shots = shots;
            PenetrationValue = penetration;
            AccuracyBonus = accuracy;
            Range = range;
        }

        public static WeaponStats For(WeaponTemplate weapon, Faction faction)
        {
            if (weapon == null) return new WeaponStats("(none)", 0, 1, 0, 0, 0f);
            var adj = faction != null ? faction.AdjustmentFor(weapon) : null;
            int? pen = weapon.PenetrationValue;
            if (pen.HasValue && adj != null) pen = Mathf.Max(0, pen.Value + adj.penetration);
            float range = weapon.range;
            if (range > 0f && adj != null) range = Mathf.Max(1f, range + adj.range);
            return new WeaponStats(
                faction != null ? faction.NameFor(weapon) : weapon.displayName,
                Mathf.Max(0, weapon.baseDamage + (adj?.damage ?? 0)),
                Mathf.Max(1, Mathf.Max(1, weapon.shots) + (adj?.shots ?? 0)),
                pen,
                weapon.accuracyBonus + (adj?.accuracyBonus ?? 0),
                range);
        }
    }
}
