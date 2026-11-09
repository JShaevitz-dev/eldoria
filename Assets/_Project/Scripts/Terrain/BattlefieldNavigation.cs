using Eldoira.Rules;
using Unity.AI.Navigation;
using UnityEngine;

namespace Eldoira.Terrain
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
        /// <summary>Measurer for the active battlefield, or null if there is none.</summary>
        public static IMoveMeasurer Measurer { get; private set; }

        private void Awake()
        {
            Build();
            Measurer = new TerrainMeasurer();
        }

        private void OnDestroy() => Measurer = null;

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

            var surface = GetComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = TerrainLayers.TerrainMask;
            surface.BuildNavMesh();
        }
    }
}
