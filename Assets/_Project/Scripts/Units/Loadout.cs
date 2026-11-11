using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>A weapon a soldier type can swap in the army builder, and what it can be swapped for.</summary>
    [Serializable]
    public class WeaponOption
    {
        [Tooltip("The weapon the soldier normally carries.")]
        public WeaponTemplate weapon;
        [Tooltip("What it can be swapped for.")]
        public List<WeaponTemplate> alternatives = new List<WeaponTemplate>();

        public bool IsValid => weapon != null && alternatives.Any(a => a != null && a != weapon);
    }

    /// <summary>The army builder choices for one soldier type in one unit: weapons swapped, and equipment given.</summary>
    public class SoldierLoadout
    {
        /// <summary>Normal weapon → the weapon chosen instead.</summary>
        public readonly Dictionary<WeaponTemplate, WeaponTemplate> Swaps = new Dictionary<WeaponTemplate, WeaponTemplate>();
        public readonly List<Equipment> Equipment = new List<Equipment>();

        public bool IsEmpty => Swaps.Count == 0 && Equipment.Count == 0;

        /// <summary>The weapon carried in place of <paramref name="weapon"/> (itself if it isn't swapped).</summary>
        public WeaponTemplate Swap(WeaponTemplate weapon) => weapon != null && Swaps.TryGetValue(weapon, out var to) ? to : weapon;

        public SoldierLoadout Clone()
        {
            var c = new SoldierLoadout();
            foreach (var kv in Swaps) c.Swaps[kv.Key] = kv.Value;
            c.Equipment.AddRange(Equipment);
            return c;
        }
    }

    /// <summary>
    /// The army builder choices for one unit in an army list: a loadout per soldier type (every soldier of that type in
    /// the unit carries the same). Applied when the unit deploys.
    /// </summary>
    public class UnitLoadout
    {
        private readonly Dictionary<SoldierTemplate, SoldierLoadout> soldiers = new Dictionary<SoldierTemplate, SoldierLoadout>();

        public bool IsEmpty => soldiers.Values.All(l => l.IsEmpty);

        /// <summary>The loadout for a soldier type (null if nothing was chosen for it).</summary>
        public SoldierLoadout Get(SoldierTemplate soldier) =>
            soldier != null && soldiers.TryGetValue(soldier, out var l) && !l.IsEmpty ? l : null;

        /// <summary>The loadout for a soldier type, created if needed (to change it).</summary>
        public SoldierLoadout For(SoldierTemplate soldier)
        {
            if (!soldiers.TryGetValue(soldier, out var l)) soldiers[soldier] = l = new SoldierLoadout();
            return l;
        }

        public UnitLoadout Clone()
        {
            var c = new UnitLoadout();
            foreach (var kv in soldiers) c.soldiers[kv.Key] = kv.Value.Clone();
            return c;
        }

        /// <summary>A short summary for the army list, e.g. "Trooper: SMG; Sergeant: Armor Plating". Empty if nothing changed.</summary>
        public string Describe(Func<WeaponTemplate, string> weaponName = null)
        {
            weaponName ??= w => w.displayName;
            var parts = new List<string>();
            foreach (var kv in soldiers.Where(kv => kv.Key != null && !kv.Value.IsEmpty))
            {
                var items = kv.Value.Swaps.Values.Where(w => w != null).Select(weaponName)
                    .Concat(kv.Value.Equipment.Where(e => e != null).Select(e => e.displayName));
                parts.Add($"{kv.Key.displayName}: {string.Join(", ", items)}");
            }
            return string.Join("; ", parts);
        }
    }
}
