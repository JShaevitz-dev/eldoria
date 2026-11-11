using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Eldoria.Terrain
{
    /// <summary>
    /// Builds the walkable-area maps (NavMeshes) for the battlefield when the scene starts, from structure terrain and
    /// blocking objects. Mantleable objects are left out so routes pass through them; water costs double.
    ///  - The standard map is for soldier-sized bodies.
    ///  - Soldiers wider than that (e.g. the Medium Tank's hull) get a map of their own, built for their size: it only
    ///    covers ground with room for them, so their routes never squeeze through gaps narrower than they are, and they
    ///    can only stop where they fit. Built when such a soldier is on the battlefield (or first needs a route).
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

        /// <summary>Sizes are rounded up to this step (meters) so similar soldiers share one map.</summary>
        private const float SizeStep = 0.1f;

        /// <summary>Wide-body maps built so far: rounded radius → agent type and its NavMesh data.</summary>
        private readonly Dictionary<int, (int agentType, NavMeshDataInstance data)> wideMaps = new Dictionary<int, (int, NavMeshDataInstance)>();
        private int standardAgentType;
        private float standardRadius = 0.5f;

        private void Awake()
        {
            active = this;
            Core.BattlefieldScale.SetBoard(boardSize.x, boardSize.y);
            Build();
            Measurer = new TerrainMeasurer();
        }

        private void Start()
        {
            // Every soldier is on the battlefield by now (armies deploy in Awake): build maps for any wide ones up front.
            foreach (var r in FindObjectsByType<Soldier>(FindObjectsSortMode.None).Select(s => s.Radius).Distinct())
                AgentTypeFor(r);
        }

        private void OnDestroy()
        {
            foreach (var (agentType, data) in wideMaps.Values)
            {
                NavMesh.RemoveNavMeshData(data);
                NavMesh.RemoveSettings(agentType);
            }
            wideMaps.Clear();
            // Only if no newer battlefield has taken over (scene loads can overlap).
            if (active != this) return;
            active = null;
            Measurer = null;
            Core.BattlefieldScale.ResetBoard();
        }

        private static BattlefieldNavigation active;

        /// <summary>Rebuilds the standard NavMesh (call after terrain changes).</summary>
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
            NavMesh.SetAreaCost(WaterArea, MovementRules.WadingCostMultiplier);

            var surface = GetComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = TerrainLayers.TerrainMask;
            surface.BuildNavMesh();
            standardAgentType = surface.agentTypeID;
            standardRadius = NavMesh.GetSettingsByID(standardAgentType).agentRadius;
        }

        /// <summary>
        /// The NavMesh agent type for a soldier of this radius: the standard one for soldier-sized bodies, otherwise one
        /// built for that size (building its map the first time it's asked for).
        /// </summary>
        public static int AgentTypeFor(float radius) => active != null ? active.AgentType(radius) : 0;

        private int AgentType(float radius)
        {
            if (radius <= standardRadius + 0.01f) return standardAgentType;
            int key = Mathf.CeilToInt(radius / SizeStep - 0.001f);
            if (wideMaps.TryGetValue(key, out var built)) return built.agentType;

            // A map for this body size: same terrain, same rules, built for a wider agent.
            var standard = NavMesh.GetSettingsByID(standardAgentType);
            var settings = NavMesh.CreateSettings();
            settings.agentRadius = key * SizeStep;
            settings.agentHeight = standard.agentHeight;
            settings.agentClimb = standard.agentClimb;
            settings.agentSlope = standard.agentSlope;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.2f;

            var markups = new List<NavMeshBuildMarkup>();
            foreach (var mod in FindObjectsByType<NavMeshModifier>(FindObjectsSortMode.None))
                markups.Add(new NavMeshBuildMarkup
                {
                    root = mod.transform,
                    applyToChildren = mod.applyToChildren,
                    overrideArea = mod.overrideArea,
                    area = mod.area,
                    overrideIgnore = mod.ignoreFromBuild,
                    ignoreFromBuild = mod.ignoreFromBuild,
                });
            var bounds = new Bounds(Vector3.zero, new Vector3(boardSize.x + 20f, 200f, boardSize.y + 20f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, TerrainLayers.TerrainMask, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            var instance = NavMesh.AddNavMeshData(data);
            wideMaps[key] = (settings.agentTypeID, instance);
            return settings.agentTypeID;
        }

        /// <summary>Route query settings for a soldier of this radius: their map, every area, water at wading cost.</summary>
        public static NavMeshQueryFilter FilterFor(float radius)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = AgentTypeFor(radius), areaMask = NavMesh.AllAreas };
            filter.SetAreaCost(WaterArea, MovementRules.WadingCostMultiplier);
            return filter;
        }
    }
}
