using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// A faction (e.g. "United States of Liberteria"). The faction a player picks when building their army list decides
    /// which units (Commanders included) they can take. A faction can also change the stats of weapons its soldiers
    /// use (added to the weapon's own stats) and give weapons its own names (aliases of the same stat block).
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoria/Faction", fileName = "NewFaction", order = 1)]
    public class Faction : ScriptableObject
    {
        public string displayName = "Faction";

        [Tooltip("The faction's colour: the primary colour of its units on the battlefield (see PrimaryColor).")]
        public Color color = new Color(0.5f, 0.5f, 0.5f);

        [Tooltip("The units this faction can put in an army list.")]
        public List<UnitTemplate> units = new List<UnitTemplate>();

        [Tooltip("Changes to the stats of weapons this faction's soldiers use, added to the weapon's own stats.")]
        public List<WeaponAdjustment> weaponAdjustments = new List<WeaponAdjustment>();

        [Tooltip("This faction's names for weapons (aliases): the same weapon and stats, shown under another name.")]
        public List<WeaponName> weaponNames = new List<WeaponName>();

        public IEnumerable<UnitTemplate> Units => units.Where(u => u != null);

        /// <summary>What this faction calls the weapon (its alias, or the weapon's own name).</summary>
        public string NameFor(WeaponTemplate weapon)
        {
            if (weapon == null) return "(none)";
            var alias = weaponNames.FirstOrDefault(n => n != null && n.weapon == weapon && !string.IsNullOrWhiteSpace(n.name));
            return alias != null ? alias.name.Trim() : weapon.displayName;
        }

        /// <summary>All of this faction's changes to the weapon, added together (null if there are none).</summary>
        public WeaponAdjustment AdjustmentFor(WeaponTemplate weapon)
        {
            WeaponAdjustment total = null;
            foreach (var a in weaponAdjustments)
            {
                if (a == null || a.weapon != weapon) continue;
                total ??= new WeaponAdjustment { weapon = weapon };
                total.damage += a.damage;
                total.shots += a.shots;
                total.penetration += a.penetration;
                total.accuracyBonus += a.accuracyBonus;
                total.range += a.range;
            }
            return total;
        }

        public bool Has(UnitTemplate unit) => unit != null && units.Contains(unit);
    }
}
