using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Eldoria.Units
{
    /// <summary>
    /// Every unit a player can put in an army list (Resources/UnitCatalog, built by SquadBuilder), the factions, plus the
    /// materials needed to deploy units at runtime (the primary colour material, the unit leader's marker).
    /// </summary>
    public class UnitCatalog : ScriptableObject
    {
        public const string ResourcePath = "UnitCatalog";

        [Tooltip("Units available for army lists, in the order the army builder shows them.")]
        public List<UnitTemplate> units = new List<UnitTemplate>();

        [Tooltip("The factions a player can choose, in the order the army builder shows them.")]
        public List<Faction> factions = new List<Faction>();

        [Tooltip("The demo army: what the army builder starts with.")]
        public List<UnitTemplate> defaultArmy = new List<UnitTemplate>();

        [Header("Deploying at runtime")]
        [Tooltip("The material soldier models use for their primary colour (body, cap...). Deployed units tint it with their faction's colour.")]
        [FormerlySerializedAs("playerArmor")]
        public Material primaryColorMaterial;
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

        public IEnumerable<Faction> Factions => factions.Where(f => f != null);

        /// <summary>The units a faction can take, in catalog order.</summary>
        public IEnumerable<UnitTemplate> UnitsFor(Faction faction) =>
            faction == null ? Available : Available.Where(faction.Has);
    }
}
