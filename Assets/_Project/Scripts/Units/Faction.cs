using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// A faction (e.g. "United States of Columbia"). The faction a player picks when building their army list decides
    /// which units (Commanders included) they can take.
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoria/Faction", fileName = "NewFaction", order = 1)]
    public class Faction : ScriptableObject
    {
        public string displayName = "Faction";

        [Tooltip("The faction's colour: the primary colour of its units on the battlefield (see PrimaryColor).")]
        public Color color = new Color(0.5f, 0.5f, 0.5f);

        [Tooltip("The units this faction can put in an army list.")]
        public List<UnitTemplate> units = new List<UnitTemplate>();

        public IEnumerable<UnitTemplate> Units => units.Where(u => u != null);

        public bool Has(UnitTemplate unit) => unit != null && units.Contains(unit);
    }
}
