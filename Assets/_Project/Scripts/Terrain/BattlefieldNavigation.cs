using Eldoria.Rules;
using Unity.AI.Navigation;
using UnityEngine;

namespace Eldoria.Terrain
{
    /// <summary>
    /// Builds the walkable-area map (NavMesh) for the battlefield when the scene starts, from
    /// structure terrain and blocking objects. Mantleable objects are left out so routes pass through them.
    /// Exposes the terrain-aware <see cref="IMoveMeasurer"/> the movement rules use.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    [DefaultExecutionOrder(-100)]
    public class BattlefieldNavigation : MonoBehaviour
    {
        /// <summary>The NavMesh area for water (riverbeds); it costs <see cref="MovementRules.WadingCostMultiplier"/> to cross.</summary>
        public const int WaterArea = 3;

        /// <summary>Measurer for the active battlefield, or null if there is none.</summary>
        public static IMoveMeasurer Measurer { get; private set; }

        [Tooltip("Size of this map in meters (east-west, north-south), centred on the origin. Soldiers can't end a move outside it.")]
        [SerializeField] private Vector2 boardSize = new Vector2(Core.BattlefieldScale.DefaultWidth, Core.BattlefieldScale.DefaultDepth);

        public Vector2 BoardSize => boardSize;

        public void SetBoardSize(Vector2 size) => boardSize = size;

        private void Awake()
        {
            active = this;
            Core.BattlefieldScale.SetBoard(boardSize.x, boardSize.y);
            Build();
            Measurer = new TerrainMeasurer();
        }

        private void OnDestroy()
        {
            // Only if no newer battlefield has taken over (scene loads can overlap).
            if (active != this) return;
            active = null;
            Measurer = null;
            Core.BattlefieldScale.ResetBoard();
        }

        private static BattlefieldNavigation active;

        /// <summary>Rebuilds the NavMesh (call after terrain changes).</summary>
        public void Build()
        {
            foreach (var obj in FindObjectsByType<TerrainObject>(FindObjectsSortMode.None))
            {
                var mod = obj.GetComponent<NavMeshModifier>();
                if (mod == null) mod = obj.gameObject.AddComponent<NavMeshModifier>();
                mod.applyToChildren = true;
                mod.ignoreFromBuild = obj.CanMantle;     // routes pass straight through
                mod.overrideArea = !obj.CanMantle;       // routes go around
                mod.area = 1;                            // "Not Walkable"
            }

            // Water: walkable, but route finding treats each meter as WadingCostMultiplier meters.
            foreach (var st in FindObjectsByType<StructureTerrain>(FindObjectsSortMode.None))
            {
                if (st.Kind != StructureKind.Water) continue;
                var mod = st.GetComponent<NavMeshModifier>();
                if (mod == null) mod = st.gameObject.AddComponent<NavMeshModifier>();
                mod.overrideArea = true;
                mod.area = WaterArea;
            }
            UnityEngine.AI.NavMesh.SetAreaCost(WaterArea, MovementRules.WadingCostMultiplier);

            var surface = GetComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = TerrainLayers.TerrainMask;
            surface.BuildNavMesh();
        }
    }
}
