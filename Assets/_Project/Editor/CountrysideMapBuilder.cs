using System.Collections.Generic;
using System.Linq;
using Eldoria.CameraControl;
using Eldoria.Gameplay;
using Eldoria.Terrain;
using Eldoria.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using L = Eldoria.Terrain.CountrysideLayout;

namespace Eldoria.EditorTools
{
    /// <summary>
    /// Builds the Open Season map (Scenes/Countryside): 100 × 100 m, not symmetrical. Rolling fields with a knoll
    /// (north-west), a long ridge (south-west) and a low rise (south field) whose crests block line of sight; a river
    /// meandering across the middle (wade at double
    /// movement cost) with a stone bridge and a shallow ford; a farmhouse, a barn and a stone shed; farm props giving
    /// half-cover (hay bales, haystacks, carts, fences, a well). The shape comes from <see cref="CountrysideLayout"/>.
    /// Menu: Eldoria > Build Open Season.
    /// </summary>
    public static class CountrysideMapBuilder
    {
        private const string Root = "Assets/_Project";
        public const string ScenePath = Root + "/Scenes/Countryside.unity";
        private const string MeshDir = Root + "/Meshes";
        private const float Resolution = 0.5f;

        private static Material grass, gravel, mud, water, farmStone, barnWood, hay;

        [MenuItem("Eldoria/Build Open Season")]
        public static void Build()
        {
            UrbanMapBuilder.PrepareShared();
            grass = UrbanMapBuilder.Mat("Pasture", new Color(0.52f, 0.55f, 0.34f),
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Materials/FloorGrid.png"));
            gravel = UrbanMapBuilder.Mat("Ford_Gravel", new Color(0.6f, 0.57f, 0.5f));
            mud = UrbanMapBuilder.Mat("Riverbed", new Color(0.33f, 0.3f, 0.24f));
            water = UrbanMapBuilder.Mat("River_Water", new Color(0.22f, 0.38f, 0.48f));
            farmStone = UrbanMapBuilder.Mat("Farm_Stone", new Color(0.74f, 0.7f, 0.6f));
            barnWood = UrbanMapBuilder.Mat("Barn_Wood", new Color(0.5f, 0.2f, 0.15f));
            hay = UrbanMapBuilder.Mat("Hay", new Color(0.86f, 0.74f, 0.4f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var map = new GameObject("CountrysideMap");
            BuildGround(map.transform);
            BuildWater(map.transform);

            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(map.transform, false);
            BuildBridge(terrain.transform);
            BuildFarm(terrain.transform);
            BuildProps(terrain.transform);

            map.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            map.AddComponent<BattlefieldNavigation>().SetBoardSize(new Vector2(L.Size, L.Size));

            var zones = BuildDeploymentZones(map.transform);
            var cam = BuildLightingAndCamera();

            Physics.SyncTransforms();
            SpawnArmies(zones);

            var gameController = new GameObject("GameController");
            var mover = gameController.AddComponent<MovementController>();
            var mso = new SerializedObject(mover);
            mso.FindProperty("cam").objectReferenceValue = cam;
            mso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectScenes.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Eldoria] Countryside map built at {ScenePath}");
        }

        // ------------------------------------------------------------------ Ground and river

        private enum Patch { Grass, Gravel, Riverbed }

        /// <summary>
        /// The ground as a height grid. Triangles below the water surface become the riverbed (structure of kind Water:
        /// wading) on its own object; the rest is dry ground (grass, and gravel at the ford).
        /// </summary>
        private static void BuildGround(Transform parent)
        {
            int n = Mathf.RoundToInt(L.Size / Resolution) + 1;
            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = -L.Half + ix * Resolution, z = -L.Half + iz * Resolution;
                verts[iz * n + ix] = new Vector3(x, L.GroundHeight(x, z), z);
                uvs[iz * n + ix] = new Vector2(x / 5f, z / 5f);   // 5 m grid tiles
            }

            var tris = new Dictionary<Patch, List<int>> { [Patch.Grass] = new List<int>(), [Patch.Gravel] = new List<int>(), [Patch.Riverbed] = new List<int>() };
            void Tri(int a, int b, int c)
            {
                Vector3 centre = (verts[a] + verts[b] + verts[c]) / 3f;
                Patch patch = L.IsUnderWater(centre.x, centre.z, centre.y) ? Patch.Riverbed
                    : L.DistanceToRiver(centre.x, centre.z) < L.RiverHalfWidth && Mathf.Abs(centre.x - L.FordX) < L.FordHalfLength + 3f ? Patch.Gravel
                    : Patch.Grass;
                tris[patch].Add(a); tris[patch].Add(b); tris[patch].Add(c);
            }
            for (int iz = 0; iz < n - 1; iz++)
            for (int ix = 0; ix < n - 1; ix++)
            {
                int i0 = iz * n + ix, i1 = i0 + 1, i2 = i0 + n, i3 = i2 + 1;
                Tri(i0, i2, i1);
                Tri(i1, i2, i3);
            }

            var land = SaveMesh("Countryside_Ground", verts, uvs, tris[Patch.Grass], tris[Patch.Gravel]);
            var landGo = Surface(parent, "Ground", land, new[] { grass, gravel }, StructureKind.Ground);
            GameObjectUtility.SetStaticEditorFlags(landGo, StaticEditorFlags.BatchingStatic);

            var bed = SaveMesh("Countryside_Riverbed", verts, uvs, tris[Patch.Riverbed]);
            Surface(parent, "Riverbed", bed, new[] { mud }, StructureKind.Water);
        }

        private static Mesh SaveMesh(string name, Vector3[] verts, Vector2[] uvs, params List<int>[] submeshes)
        {
            string path = $"{MeshDir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static GameObject Surface(Transform parent, string name, Mesh mesh, Material[] mats, StructureKind kind)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.layer = TerrainLayers.Structure;
            go.AddComponent<StructureTerrain>().Configure(kind);
            return go;
        }

        /// <summary>The water surface: a flat strip along the river at the water level (visual only: no collider).</summary>
        private static void BuildWater(Transform parent)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (float x = -L.Half; x <= L.Half + 0.01f; x += 1f)
            {
                var dir = L.RiverDirection(x);
                var across = new Vector3(-dir.z, 0f, dir.x) * (L.RiverHalfWidth * 0.85f);
                var centre = new Vector3(x, L.WaterLevel, L.RiverZ(x));
                verts.Add(centre - across);
                verts.Add(centre + across);
                uvs.Add(new Vector2(x / 5f, 0f));
                uvs.Add(new Vector2(x / 5f, 1f));
                int i = verts.Count - 2;
                if (i >= 2)
                {
                    // Wound to face up (same as the ground grid).
                    tris.AddRange(new[] { i - 2, i - 1, i });
                    tris.AddRange(new[] { i - 1, i + 1, i });
                }
            }
            var mesh = SaveMesh("Countryside_Water", verts.ToArray(), uvs.ToArray(), tris);
            var go = new GameObject("River Water");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = water;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>A stone bridge across the river (a dry, walkable deck) with low parapets giving half-cover.</summary>
        private static void BuildBridge(Transform parent)
        {
            var dir = L.RiverDirection(L.BridgeX);
            var across = new Vector3(-dir.z, 0f, dir.x);
            float length = 2f * L.RiverHalfWidth + 4f;

            var bridge = new GameObject("Stone Bridge");
            bridge.transform.SetParent(parent, false);
            bridge.transform.localPosition = new Vector3(L.BridgeX, 0f, L.RiverZ(L.BridgeX));
            bridge.transform.localRotation = Quaternion.LookRotation(across, Vector3.up);

            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Bridge Deck";
            deck.transform.SetParent(bridge.transform, false);
            deck.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            deck.transform.localScale = new Vector3(L.BridgeWidth, 0.4f, length);
            deck.GetComponent<Renderer>().sharedMaterial = UrbanMapBuilder.stone;
            deck.layer = TerrainLayers.Structure;
            deck.AddComponent<StructureTerrain>().Configure(StructureKind.Bridge);

            foreach (float side in new[] { -1f, 1f })
                UrbanMapBuilder.Prop(bridge.transform, "Bridge Parapet", PrimitiveType.Cube, UrbanMapBuilder.stone,
                    new Vector3(side * (L.BridgeWidth / 2f + 0.2f), 0.5f, 0f), new Vector3(0.4f, 0.9f, length - 2f), 0f,
                    cover: true, mantle: false, climb: false, "Half-cover");
        }

        // ------------------------------------------------------------------ Farm buildings

        private static Vector3 OnGround(float x, float z, float lift = 0f) => new Vector3(x, L.GroundHeight(x, z) + lift, z);

        private static Vector3 PadCentre(string name)
        {
            var pad = L.Pads.First(p => p.Name == name);
            return OnGround(pad.Centre.x, pad.Centre.y);
        }

        private static void BuildFarm(Transform parent)
        {
            var D = (System.Func<float, float, UrbanMapBuilder.Opening>)((o, w) => UrbanMapBuilder.Opening.Door(o, w));
            var W = (System.Func<float, UrbanMapBuilder.Opening>)(o => UrbanMapBuilder.Opening.Win(o));

            // Farmhouse (north-east): stone, two rooms' worth of windows, doors front and back.
            UrbanMapBuilder.Building(parent, "Farmhouse", PadCentre("Farmhouse"), 10f, 10f, 7f, 3f, farmStone,
                north: new[] { D(2f, 2f), W(-2.5f) }, east: new[] { W(0f) }, south: new[] { D(0f, 2f), W(-3f), W(3f) }, west: new[] { W(0.5f) });

            // Barn (south-east): big wooden barn with wide doors at both ends.
            UrbanMapBuilder.Building(parent, "Barn", PadCentre("Barn"), -6f, 9f, 14f, 3.5f, barnWood,
                north: new[] { D(0f, 4f) }, east: new[] { W(-3f), W(3f) }, south: new[] { D(0f, 4f) }, west: new[] { W(0f) });

            // Stone shed (south field): small and low, one door, one window.
            UrbanMapBuilder.Building(parent, "Stone Shed", PadCentre("Stone Shed"), 20f, 5f, 4f, 2.2f, UrbanMapBuilder.ruinMat,
                north: new[] { W(0f) }, east: null, south: new[] { D(0.8f, 1.6f) }, west: null);
        }

        // ------------------------------------------------------------------ Farm props (all Half-cover)

        private static void BuildProps(Transform parent)
        {
            var props = new GameObject("Farm Props").transform;
            props.SetParent(parent, false);

            // Farmyard around the farmhouse (north-east), with a paddock fence to its west.
            Well(props, 34f, 21f);
            Cart(props, 17.5f, 18.5f, 30f);
            HayBales(props, new Vector2(36f, 31f), new Vector2(37.6f, 31.8f), new Vector2(36.7f, 33.2f));
            Fence(props, new Vector2(8f, 21f), new Vector2(16f, 23f));
            Fence(props, new Vector2(16f, 23f), new Vector2(17f, 33f));

            // Around the barn.
            Haystack(props, 21f, -18f);
            HayBales(props, new Vector2(38f, -4f), new Vector2(39.4f, -5f));
            Fence(props, new Vector2(37f, -36f), new Vector2(44f, -21f));

            // Open fields.
            HayBales(props, new Vector2(5f, -23f), new Vector2(6.6f, -22.2f));
            HayBales(props, new Vector2(-30f, -36f));
            HayBales(props, new Vector2(-8f, 30f), new Vector2(-6.5f, 30.6f));
            HayBales(props, new Vector2(42f, 13f));
            Haystack(props, -12f, 18f);
            Fence(props, new Vector2(-1f, -15.5f), new Vector2(13f, -18f));
            Cart(props, 8.5f, 2.5f, -15f);   // left by the road to the bridge
        }

        private static GameObject PropAt(Transform parent, string name, PrimitiveType shape, Material mat, float x, float z, Vector3 scale, float yaw,
            bool mantle, bool climb)
        {
            float halfHeight = shape == PrimitiveType.Cube ? scale.y / 2f : scale.y;   // cylinders are 2 units tall
            return UrbanMapBuilder.Prop(parent, name, shape, mat, OnGround(x, z, halfHeight), scale, yaw,
                cover: true, mantle: mantle, climb: climb, "Half-cover");
        }

        /// <summary>Round hay bales (1.2 m tall): step over them or climb on top.</summary>
        private static void HayBales(Transform parent, params Vector2[] spots)
        {
            foreach (var p in spots)
                PropAt(parent, "Hay Bale", PrimitiveType.Cylinder, hay, p.x, p.y, new Vector3(1.4f, 0.6f, 1.4f), 0f, mantle: true, climb: true);
        }

        /// <summary>A big haystack (2.8 m tall): blocks movement.</summary>
        private static void Haystack(Transform parent, float x, float z)
        {
            var go = PropAt(parent, "Haystack", PrimitiveType.Cylinder, hay, x, z, new Vector3(3.5f, 1.4f, 3.5f), 0f, mantle: false, climb: false);
            Visual(go.transform, "Haystack Top", PrimitiveType.Sphere, hay, new Vector3(0f, 0.9f, 0f), new Vector3(1f, 0.6f, 1f));
        }

        /// <summary>A farm cart: blocks movement, but soldiers can climb onto it.</summary>
        private static void Cart(Transform parent, float x, float z, float yaw)
        {
            var go = PropAt(parent, "Cart", PrimitiveType.Cube, UrbanMapBuilder.wood, x, z, new Vector3(1.8f, 1.1f, 3.2f), yaw, mantle: false, climb: true);
            foreach (float side in new[] { -1f, 1f })
                Visual(go.transform, "Wheel", PrimitiveType.Cylinder, UrbanMapBuilder.wood, new Vector3(side * 0.55f, -0.2f, 0f),
                    new Vector3(0.05f, 0.5f, 0.5f), new Vector3(0f, 0f, 90f));
        }

        /// <summary>A stone well (1 m tall): blocks movement.</summary>
        private static void Well(Transform parent, float x, float z)
        {
            PropAt(parent, "Well", PrimitiveType.Cylinder, UrbanMapBuilder.stone, x, z, new Vector3(1.6f, 0.5f, 1.6f), 0f, mantle: false, climb: false);
        }

        /// <summary>A wooden fence (1 m tall) in short sections that follow the ground: soldiers can climb over it.</summary>
        private static void Fence(Transform parent, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            int pieces = Mathf.Max(1, Mathf.CeilToInt(ab.magnitude / 3f));
            float yaw = Mathf.Atan2(ab.x, ab.y) * Mathf.Rad2Deg;
            for (int i = 0; i < pieces; i++)
            {
                Vector2 mid = a + ab * ((i + 0.5f) / pieces);
                PropAt(parent, "Fence", PrimitiveType.Cube, UrbanMapBuilder.wood, mid.x, mid.y,
                    new Vector3(0.12f, 1f, ab.magnitude / pieces), yaw, mantle: true, climb: false);
            }
        }

        /// <summary>A purely visual part (no collider) under a prop, in the prop's local space.</summary>
        private static void Visual(Transform parent, string name, PrimitiveType shape, Material mat, Vector3 localPos, Vector3 localScale,
            Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.layer = parent.gameObject.layer;
        }

        // ------------------------------------------------------------------ Deployment, armies, camera

        private static List<DeploymentZone> BuildDeploymentZones(Transform parent)
        {
            float z = L.Half - L.DeployDepth / 2f;
            return new List<DeploymentZone>
            {
                Zone(parent, "Deployment (Player)", Side.Player, new Vector3(0f, 0f, -z), UrbanMapBuilder.Mat("Deploy_Player", new Color(0.33f, 0.38f, 0.47f))),
                Zone(parent, "Deployment (AI)", Side.AI, new Vector3(0f, 0f, z), UrbanMapBuilder.Mat("Deploy_AI", new Color(0.47f, 0.35f, 0.34f))),
            };
        }

        private static DeploymentZone Zone(Transform parent, string name, Side side, Vector3 centre, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var zone = go.AddComponent<DeploymentZone>();
            zone.Configure(side, new Vector2(L.Size, L.DeployDepth));
            UrbanMapBuilder.Decor(go.transform, "Marking", new Vector3(0f, 0.01f, 0f), new Vector2(L.Size, L.DeployDepth), mat);
            return zone;
        }

        /// <summary>The armies placed in the scene (used when the map is opened directly): the demo army on both sides.</summary>
        private static void SpawnArmies(List<DeploymentZone> zones)
        {
            UnitTemplate U(string file) => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Root}/Data/Units/{file}.asset");
            var army = new List<UnitTemplate> { U("InfantrySquad"), U("FieldCommander"), U("InfantrySquad"), U("BattlefieldPriest"), U("InfantrySquad") };
            var root = new GameObject("Armies");
            foreach (var zone in zones)
            {
                var side = zone.Side;
                var names = ArmyDeployment.UnitNames(army, side);
                var facing = Quaternion.Euler(0f, side == Side.Player ? 0f : 180f, 0f);
                for (int i = 0; i < army.Count; i++)
                {
                    var unit = SquadBuilder.SpawnUnit(army[i], side, ArmyDeployment.Slot(zone.transform.position, zone.Size, side, i), facing, names[i]);
                    unit.transform.SetParent(root.transform, true);
                }
            }
        }

        private static Camera BuildLightingAndCamera()
        {
            var sun = new GameObject("Directional Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -40f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.66f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.46f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.2f);

            // Camera starts over the player's side, looking north across the map.
            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(0f, 0f, -30f);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.66f, 0.76f);   // an overcast English sky
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            camGo.AddComponent<AudioListener>();
            var controller = rig.AddComponent<TacticalCameraController>();
            var so = new SerializedObject(controller);
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("boundsMin").vector2Value = new Vector2(-L.Half, -L.Half);
            so.FindProperty("boundsMax").vector2Value = new Vector2(L.Half, L.Half);
            so.FindProperty("maxDistance").floatValue = 110f;
            so.ApplyModifiedPropertiesWithoutUndo();
            camGo.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            camGo.transform.localPosition = camGo.transform.localRotation * new Vector3(0f, 0f, -45f);
            return cam;
        }
    }
}
