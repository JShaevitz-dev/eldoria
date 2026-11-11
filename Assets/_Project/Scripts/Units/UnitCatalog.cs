using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// Every unit a player can put in an army list (Resources/UnitCatalog, built by SquadBuilder), plus the materials
    /// needed to deploy them at runtime (the enemy's armor colour, the unit leader's marker).
    /// </summary>
    public class UnitCatalog : ScriptableObject
    {
        public const string ResourcePath = "UnitCatalog";

        [Tooltip("Units available for army lists, in the order the army builder shows them.")]
        public List<UnitTemplate> units = new List<UnitTemplate>();

        [Tooltip("The demo army: what the army builder starts with.")]
        public List<UnitTemplate> defaultArmy = new List<UnitTemplate>();

        [Header("Deploying at runtime")]
        [Tooltip("The player's armor colour on soldier models; swapped for Enemy Armor on the AI's soldiers.")]
        public Material playerArmor;
        public Material enemyArmor;
        [Tooltip("The marker floating over a unit's leader.")]
        public Material leaderMarker;

        private static UnitCatalog loaded;

        /// <summary>The catalog in Resources (null if it hasn't been built).</summary>
        public static UnitCatalog Load()
        {
            if (loaded == null) loaded = Resources.Load<UnitCatalog>(ResourcePath);
            return loaded;
        }

        public IEnumerable<UnitTemplate> Available => units.Where(u => u != null);
    }
}
