using UnityEngine;

namespace Eldoria.Terrain
{
    public enum StructureKind
    {
        Ground,
        Hill,
        Ditch,
        Building,
        /// <summary>A man-made incline, e.g. a ramp up to a platform. (Maps don't use steps: elevation changes are slopes or climbable objects.)</summary>
        Slope,
        /// <summary>A riverbed under water: soldiers can wade through it, but every meter costs double (see MovementRules.WadingCostMultiplier).</summary>
        Water,
        /// <summary>A bridge deck: walkable, dry.</summary>
        Bridge,
    }

    /// <summary>
    /// Structure terrain: large, static landforms and buildings (ground, hills, ditches, slopes, building walls).
    /// Movement is measured along the surface of structure terrain, so climbing a hill costs the
    /// distance up and down it. Lives on the "Structure" layer.
    /// </summary>
    public class StructureTerrain : MonoBehaviour
    {
        [SerializeField] private StructureKind kind = StructureKind.Ground;

        public StructureKind Kind => kind;

        public void Configure(StructureKind newKind) => kind = newKind;

        private void Reset() => gameObject.layer = TerrainLayers.Structure;
    }
}
