using System.Collections.Generic;
using Eldoria.CameraControl;
using Eldoria.Gameplay;
using Eldoria.Terrain;
using Eldoria.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eldoria.EditorTools
{
    /// <summary>
    /// Builds the demo map "Urban Demo": an 80 x 60 m town district with 180° rotational symmetry
    /// (every feature in the south half has a twin at (-x, -z), turned round), so both sides face the same choices.
    ///
    ///   North: AI deployment zone (8 m deep)
    ///   Centre: an open plaza around a fountain, ringed by planters and low walls
    ///   Around it: an apartment block, a corner shop with a loading dock, a garage and a ruined house per half,
    ///              joined by streets with cars, sandbags, crates and a shipping container for cover
    ///   South: player deployment zone
    ///
    /// Buildings are roofless (so the camera can see in), with doorways and windows. Window sills are low objects:
    /// soldiers can see, shoot and climb through a window (it costs no extra movement), and its sill gives half-cover.
    /// Menu: Eldoria > Build Urban Demo, or -executeMethod Eldoria.EditorTools.UrbanMapBuilder.Build
    /// </summary>
    public static class UrbanMapBuilder
    {
        private const string Root = "Assets/_Project";
        public const string ScenePath = Root + "/Scenes/UrbanDemo.unity";
        private const string MaterialDir = Root + "/Materials";
        private const string MeshDir = Root + "/Meshes";

        public const float Width = 80f, Depth = 60f;
        public const float DeployDepth = 8f;
        private const float PerimeterHeight = 4f, PerimeterThickness = 0.5f;

        private const float WallHeight = 3f, WallThickness = 0.3f;
        private const float SillHeight = 1f, LintelBottom = 2.2f;
        private const float DoorWidth = 2f, WindowWidth = 2f;

        private static Dictionary<string, Keyword> keywords;
        private static Material plaster, brick, concrete, ruinMat, floorMat, stone, wood, sandbag, rust;

        /// <summary>A gap in a wall: a doorway (walk through) or a window (see and shoot through, half-cover, no walking).</summary>
        private struct Opening
        {
            public float Offset, Width;
            public bool Window;
            public static Opening Door(float offset, float width = DoorWidth) => new Opening { Offset = offset, Width = width };
            public static Opening Win(float offset, float width = WindowWidth) => new Opening { Offset = offset, Width = width, Window = true };
        }

        [MenuItem("Eldoria/Build Urban Demo")]
        public static void Build()
        {
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Meshes");
            keywords = SquadBuilder.EnsureKeywords();
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var map = new GameObject("UrbanMap");
            BuildGround(map.transform);
            BuildPerimeter(map.transform);
            BuildDecor(map.transform);

            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(map.transform, false);
            // The fountain sits on the centre point: it is its own twin.
            Prop(terrain.transform, "Fountain", PrimitiveType.Cylinder, stone, new Vector3(0f, 0.45f, 0f), new Vector3(5f, 0.45f, 5f), 0f,
                cover: true, mantle: false, climb: false, "Half-cover");
            Prop(terrain.transform, "Fountain Statue", PrimitiveType.Cylinder, stone, new Vector3(0f, 1.6f, 0f), new Vector3(0.8f, 1.6f, 0.8f), 0f,
                cover: true, mantle: false, climb: false, "Full-cover");

            foreach (bool north in new[] { false, true })
            {
                var half = new GameObject(north ? "North Half" : "South Half");
                half.transform.SetParent(terrain.transform, false);
                // The north half is the south half turned 180° about the centre.
                if (north) half.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                BuildHalf(half.transform);
            }

            map.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            var battlefield = map.AddComponent<BattlefieldNavigation>();
            battlefield.SetBoardSize(new Vector2(Width, Depth));

            BuildDeploymentZones(map.transform);
            var cam = BuildLightingAndCamera();

            Physics.SyncTransforms();
            SpawnArmies();

            var gameController = new GameObject("GameController");
            var mover = gameController.AddComponent<MovementController>();
            var mso = new SerializedObject(mover);
            mso.FindProperty("cam").objectReferenceValue = cam;
            mso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Eldoria] Urban demo map built at {ScenePath}");
        }

        // ------------------------------------------------------------------ One half (twinned by rotation)

        /// <summary>Everything in the south half, in world coordinates of the south half (z &lt; 0 mostly).</summary>
        private static void BuildHalf(Transform half)
        {
            // --- Apartment block: two rooms, windows on every side, a door toward the plaza and one toward the street.
            var apartment = Building(half, "Apartment Block", new Vector3(-27f, 0f, -12f), 0f, 14f, 10f, WallHeight, brick,
                north: new[] { Opening.Door(-3.5f), Opening.Win(3f) },
                east: new[] { Opening.Door(2f), Opening.Win(-2.5f) },
                south: new[] { Opening.Win(-3.5f), Opening.Win(3.5f) },
                west: new[] { Opening.Win(0f) });
            WallLine(apartment, new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, 5f), WallHeight, brick, new[] { Opening.Door(0.5f) }, extend: false);
            Prop(apartment, "Table", PrimitiveType.Cube, wood, Local(-3.5f, 0.4f, -2f), new Vector3(2f, 0.8f, 1f), 0f, true, true, true, "Half-cover");
            Prop(apartment, "Crate", PrimitiveType.Cube, wood, Local(3.5f, 0.6f, 2.5f), Vector3.one * 1.2f, 15f, true, true, true, "Half-cover");

            // --- Corner shop: a shop front of windows facing the plaza, a counter inside.
            var shop = Building(half, "Corner Shop", new Vector3(13f, 0f, -15f), 0f, 12f, 8f, WallHeight, plaster,
                north: new[] { Opening.Door(-3f), Opening.Win(1f), Opening.Win(4f) },
                east: new Opening[0],
                south: new[] { Opening.Win(-2.5f), Opening.Door(3f) },
                west: new[] { Opening.Win(0f) });
            Prop(shop, "Counter", PrimitiveType.Cube, wood, Local(1.5f, 0.5f, 1.2f), new Vector3(4f, 1f, 0.6f), 0f, true, true, false, "Half-cover");
            Prop(shop, "Crate", PrimitiveType.Cube, wood, Local(-4f, 0.6f, -2.5f), Vector3.one * 1.2f, -10f, true, true, true, "Half-cover");

            // --- Loading dock next to the shop: a raised platform with a slope down to the south.
            LoadingDock(half, new Vector3(22f, 0f, -14f), width: 4f, depth: 5f, height: 1.2f, slopeRun: 3.5f, slopeWidth: 2.5f);

            // --- Garage: a wide door onto the east street.
            var garage = Building(half, "Garage", new Vector3(31f, 0f, -8f), 0f, 8f, 8f, WallHeight, concrete,
                north: new[] { Opening.Win(0f) },
                east: new Opening[0],
                south: new[] { Opening.Door(1.5f) },
                west: new[] { Opening.Door(0f, 3.5f) });
            Prop(garage, "Car", PrimitiveType.Cube, Mat("Car_White", new Color(0.82f, 0.82f, 0.8f)), Local(0.5f, 0.7f, 0f), new Vector3(1.9f, 1.4f, 4.2f), 0f,
                true, false, true, "Half-cover");

            // --- Ruined house: low broken walls with wide breaches, rubble inside and out.
            var ruin = Building(half, "Ruined House", new Vector3(-10f, 0f, -17f), 0f, 8f, 6f, 2.2f, ruinMat,
                north: new[] { Opening.Door(1f, 3f) },
                east: new[] { Opening.Win(0f) },
                south: new[] { Opening.Door(-1.5f) },
                west: new[] { Opening.Door(-0.5f, 2.5f) });
            Prop(ruin, "Rubble", PrimitiveType.Cube, ruinMat, Local(-1.5f, 0.35f, 0.5f), new Vector3(2f, 0.7f, 1.4f), 25f, true, true, false, "Half-cover");
            Prop(half, "Rubble", PrimitiveType.Cube, ruinMat, new Vector3(-16f, 0.35f, -16.5f), new Vector3(2.2f, 0.7f, 1.5f), -20f, true, true, false, "Half-cover");

            // --- Plaza edge: planters and low walls (the other two planters/walls are the north half's twins).
            Prop(half, "Planter", PrimitiveType.Cube, stone, new Vector3(-9f, 0.4f, -5f), new Vector3(2f, 0.8f, 2f), 0f, true, true, true, "Half-cover");
            Prop(half, "Planter", PrimitiveType.Cube, stone, new Vector3(9f, 0.4f, -5f), new Vector3(2f, 0.8f, 2f), 0f, true, true, true, "Half-cover");
            Prop(half, "Low Wall", PrimitiveType.Cube, stone, new Vector3(-5.5f, 0.5f, -9f), new Vector3(4f, 1f, 0.3f), 0f, true, true, false, "Half-cover");
            Prop(half, "Low Wall", PrimitiveType.Cube, stone, new Vector3(5.5f, 0.5f, -9f), new Vector3(4f, 1f, 0.3f), 0f, true, true, false, "Half-cover");

            // --- South avenue (between the ruin and the shop) into the plaza.
            Prop(half, "Sandbags", PrimitiveType.Cube, sandbag, new Vector3(0.5f, 0.45f, -14f), new Vector3(3f, 0.9f, 0.8f), 0f, true, true, false, "Half-cover");
            Prop(half, "Car", PrimitiveType.Cube, Mat("Car_Teal", new Color(0.15f, 0.45f, 0.45f)), new Vector3(4f, 0.7f, -18f), new Vector3(1.9f, 1.4f, 4.2f), 5f,
                true, false, true, "Half-cover");

            // --- West street (between the apartment block and the ruin).
            Prop(half, "Car", PrimitiveType.Cube, Mat("Car_Yellow", new Color(0.8f, 0.65f, 0.15f)), new Vector3(-17f, 0.7f, -9f), new Vector3(1.9f, 1.4f, 4.2f), -8f,
                true, false, true, "Half-cover");
            Prop(half, "Sandbags", PrimitiveType.Cube, sandbag, new Vector3(-27f, 0.45f, -3.5f), new Vector3(3f, 0.9f, 0.8f), 0f, true, true, false, "Half-cover");

            // --- East street (between the shop and the garage) and the open east side of the centre line.
            Prop(half, "Car", PrimitiveType.Cube, Mat("Car_Grey", new Color(0.35f, 0.36f, 0.4f)), new Vector3(23.5f, 0.7f, -6f), new Vector3(1.9f, 1.4f, 4.2f), 75f,
                true, false, true, "Half-cover");
            Prop(half, "Low Wall", PrimitiveType.Cube, stone, new Vector3(33f, 0.5f, 1f), new Vector3(0.3f, 1f, 4f), 0f, true, true, false, "Half-cover");
            Prop(half, "Sandbags", PrimitiveType.Cube, sandbag, new Vector3(17f, 0.45f, -2f), new Vector3(3f, 0.9f, 0.8f), 90f, true, true, false, "Half-cover");

            // --- Near the deployment zone: crates on the west flank, a container on the east flank.
            Prop(half, "Crate", PrimitiveType.Cube, wood, new Vector3(-18f, 0.6f, -20f), Vector3.one * 1.2f, 10f, true, true, true, "Half-cover");
            Prop(half, "Crate", PrimitiveType.Cube, wood, new Vector3(-16.6f, 0.6f, -20.3f), Vector3.one * 1.2f, -5f, true, true, true, "Half-cover");
            Prop(half, "Crate", PrimitiveType.Cube, wood, new Vector3(-17.3f, 1.8f, -20.15f), Vector3.one * 1.2f, 25f, true, true, true, "Half-cover");
            Prop(half, "Shipping Container", PrimitiveType.Cube, rust, new Vector3(33f, 1.3f, -18.5f), new Vector3(2.4f, 2.6f, 6f), 0f,
                true, false, true, "Full-cover");
        }

        private static Vector3 Local(float x, float y, float z) => new Vector3(x, y, z);

        // ------------------------------------------------------------------ Buildings

        /// <summary>A roofless building centred at <paramref name="centre"/>; openings are listed per side (offset along the side from its middle).</summary>
        private static Transform Building(Transform parent, string name, Vector3 centre, float yaw, float w, float d, float height, Material mat,
            Opening[] north, Opening[] east, Opening[] south, Opening[] west)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.layer = TerrainLayers.Structure;
            go.AddComponent<StructureTerrain>().Configure(StructureKind.Building);
            var t = go.transform;

            float hw = w / 2f, hd = d / 2f;
            WallLine(t, new Vector3(-hw, 0f, hd), new Vector3(hw, 0f, hd), height, mat, north, extend: true);
            WallLine(t, new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd), height, mat, south, extend: true);
            WallLine(t, new Vector3(hw, 0f, -hd), new Vector3(hw, 0f, hd), height, mat, east, extend: false);
            WallLine(t, new Vector3(-hw, 0f, -hd), new Vector3(-hw, 0f, hd), height, mat, west, extend: false);

            // Interior floor (visual only).
            Decor(t, "Floor", new Vector3(0f, 0.012f, 0f), new Vector2(w - WallThickness, d - WallThickness), floorMat);
            return t;
        }

        /// <summary>
        /// A straight wall from a to b (local to the parent) with doorways and windows cut into it.
        /// <paramref name="extend"/> lengthens the wall by half its thickness at both ends to close the corners.
        /// </summary>
        private static void WallLine(Transform parent, Vector3 a, Vector3 b, float height, Material mat, Opening[] openings, bool extend)
        {
            Vector3 dir = (b - a).normalized;
            float length = Vector3.Distance(a, b);
            Vector3 mid = (a + b) / 2f;
            float start = -length / 2f - (extend ? WallThickness / 2f : 0f);
            float end = length / 2f + (extend ? WallThickness / 2f : 0f);

            var sorted = new List<Opening>(openings ?? new Opening[0]);
            sorted.Sort((x, y) => x.Offset.CompareTo(y.Offset));
            float cursor = start;
            foreach (var o in sorted)
            {
                float o0 = o.Offset - o.Width / 2f, o1 = o.Offset + o.Width / 2f;
                WallPiece(parent, "Wall", mid, dir, cursor, o0, 0f, height, mat);
                if (o.Window)
                {
                    // Sill: a low object (see, shoot and climb through the window; half-cover); lintel above.
                    var sill = WallPiece(parent, "Window Sill", mid, dir, o0, o1, 0f, SillHeight, mat);
                    sill.layer = TerrainLayers.Object;
                    Object.DestroyImmediate(sill.GetComponent<StructureTerrain>());
                    sill.AddComponent<TerrainObject>().Configure("Window Sill", cover: true, mantle: true, climbOnto: false,
                        new[] { keywords["Half-cover"] });
                    if (height > LintelBottom) WallPiece(parent, "Lintel", mid, dir, o0, o1, LintelBottom, height, mat);
                }
                cursor = o1;
            }
            WallPiece(parent, "Wall", mid, dir, cursor, end, 0f, height, mat);
        }

        /// <summary>One solid box of wall between two distances along the wall line, from y0 to y1.</summary>
        private static GameObject WallPiece(Transform parent, string name, Vector3 mid, Vector3 dir, float from, float to, float y0, float y1, Material mat)
        {
            float len = to - from;
            if (len < 0.05f) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = mid + dir * ((from + to) / 2f) + Vector3.up * ((y0 + y1) / 2f);
            go.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            go.transform.localScale = new Vector3(WallThickness, y1 - y0, len);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.layer = TerrainLayers.Structure;
            go.AddComponent<StructureTerrain>().Configure(StructureKind.Building);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>A raised platform (structure) with a slope down from its south edge. Maps use slopes, never steps.</summary>
        private static void LoadingDock(Transform parent, Vector3 centre, float width, float depth, float height, float slopeRun, float slopeWidth)
        {
            var dock = new GameObject("Loading Dock");
            dock.transform.SetParent(parent, false);
            dock.transform.localPosition = centre;

            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Platform";
            top.transform.SetParent(dock.transform, false);
            top.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            top.transform.localScale = new Vector3(width, height, depth);
            top.GetComponent<Renderer>().sharedMaterial = concrete;
            top.layer = TerrainLayers.Structure;
            top.AddComponent<StructureTerrain>().Configure(StructureKind.Building);

            Vector3 slopeTop = new Vector3(0f, height, -depth / 2f);
            Vector3 slopeBottom = slopeTop - new Vector3(0f, height, slopeRun);
            Vector3 along = slopeTop - slopeBottom;
            const float thickness = 0.2f;
            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "Slope";
            slope.transform.SetParent(dock.transform, false);
            var rot = Quaternion.LookRotation(along.normalized, Vector3.up);
            slope.transform.localRotation = rot;
            slope.transform.localPosition = (slopeTop + slopeBottom) / 2f - rot * Vector3.up * (thickness / 2f);
            slope.transform.localScale = new Vector3(slopeWidth, thickness, along.magnitude);
            slope.GetComponent<Renderer>().sharedMaterial = concrete;
            slope.layer = TerrainLayers.Structure;
            slope.AddComponent<StructureTerrain>().Configure(StructureKind.Slope);
        }

        // ------------------------------------------------------------------ Objects

        private static void Prop(Transform parent, string name, PrimitiveType shape, Material mat, Vector3 pos, Vector3 scale, float yaw,
            bool cover, bool mantle, bool climb, string keyword = null)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (shape == PrimitiveType.Cylinder)
            {
                // Cylinder primitives come with a capsule collider; use the exact mesh instead.
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().convex = true;
            }
            go.layer = TerrainLayers.Object;
            go.AddComponent<TerrainObject>().Configure(name, cover, mantle, climb,
                keyword != null ? new[] { keywords[keyword] } : null);
        }

        // ------------------------------------------------------------------ Ground, edges, decoration

        private static void BuildGround(Transform parent)
        {
            // Flat ground with world-space UVs so the 1 m / 5 m measuring grid lines up across the map.
            const float step = 5f;
            int nx = Mathf.RoundToInt(Width / step) + 1, nz = Mathf.RoundToInt(Depth / step) + 1;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = -Width / 2f + ix * step, z = -Depth / 2f + iz * step;
                verts[iz * nx + ix] = new Vector3(x, 0f, z);
                uvs[iz * nx + ix] = new Vector2(x / 5f, z / 5f);
            }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int k = 0;
            for (int iz = 0; iz < nz - 1; iz++)
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int i0 = iz * nx + ix, i1 = i0 + 1, i2 = i0 + nx, i3 = i2 + 1;
                tris[k++] = i0; tris[k++] = i2; tris[k++] = i1;
                tris[k++] = i1; tris[k++] = i2; tris[k++] = i3;
            }

            string path = $"{MeshDir}/UrbanGround.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "UrbanGround";
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);

            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial =
                Mat("Asphalt", new Color(0.36f, 0.37f, 0.39f), AssetDatabase.LoadAssetAtPath<Texture2D>($"{MaterialDir}/FloorGrid.png"));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.layer = TerrainLayers.Structure;
            go.AddComponent<StructureTerrain>().Configure(StructureKind.Ground);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
        }

        private static void BuildPerimeter(Transform parent)
        {
            var edge = new GameObject("Perimeter");
            edge.transform.SetParent(parent, false);
            float w = Width, d = Depth, h = PerimeterHeight, t = PerimeterThickness;
            var mat = Mat("Wall", new Color(0.55f, 0.55f, 0.58f));
            PerimeterWall(edge.transform, "Edge_North", new Vector3(0f, h / 2f, d / 2f + t / 2f), new Vector3(w + t * 2f, h, t), mat);
            PerimeterWall(edge.transform, "Edge_South", new Vector3(0f, h / 2f, -d / 2f - t / 2f), new Vector3(w + t * 2f, h, t), mat);
            PerimeterWall(edge.transform, "Edge_East", new Vector3(w / 2f + t / 2f, h / 2f, 0f), new Vector3(t, h, d), mat);
            PerimeterWall(edge.transform, "Edge_West", new Vector3(-w / 2f - t / 2f, h / 2f, 0f), new Vector3(t, h, d), mat);
        }

        private static void PerimeterWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = mat;
            wall.layer = TerrainLayers.Structure;
            wall.AddComponent<StructureTerrain>().Configure(StructureKind.Building);
            GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Paving and painted markings: visual only (no colliders, no effect on the rules).</summary>
        private static void BuildDecor(Transform parent)
        {
            var decor = new GameObject("Decor");
            decor.transform.SetParent(parent, false);
            Decor(decor.transform, "Plaza Paving", new Vector3(0f, 0.01f, 0f), new Vector2(24f, 16f), Mat("Paving", new Color(0.62f, 0.58f, 0.52f)));
            var line = Mat("Road Marking", new Color(0.85f, 0.8f, 0.55f));
            // Centre line of the east-west road through the middle (broken by the plaza).
            Decor(decor.transform, "Road Line", new Vector3(-26f, 0.011f, 0f), new Vector2(28f, 0.2f), line);
            Decor(decor.transform, "Road Line", new Vector3(26f, 0.011f, 0f), new Vector2(28f, 0.2f), line);
        }

        private static void Decor(Transform parent, string name, Vector3 localPos, Vector2 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ Deployment, armies, camera

        private static void BuildDeploymentZones(Transform parent)
        {
            float z = Depth / 2f - DeployDepth / 2f;
            Zone(parent, "Deployment (Player)", Side.Player, new Vector3(0f, 0f, -z), Mat("Deploy_Player", new Color(0.33f, 0.38f, 0.47f)));
            Zone(parent, "Deployment (AI)", Side.AI, new Vector3(0f, 0f, z), Mat("Deploy_AI", new Color(0.47f, 0.35f, 0.34f)));
        }

        private static void Zone(Transform parent, string name, Side side, Vector3 centre, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.AddComponent<DeploymentZone>().Configure(side, new Vector2(Width, DeployDepth));
            Decor(go.transform, "Marking", new Vector3(0f, 0.008f, 0f), new Vector2(Width, DeployDepth), mat);
        }

        /// <summary>
        /// Both sides field the same army: three Infantry Squads, a Field Commander and a Battlefield Priest.
        /// Squads deploy across the zone (west, centre, east) with the officers behind the gaps between them.
        /// </summary>
        private static void SpawnArmies()
        {
            var infantry = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Root}/Data/Units/InfantrySquad.asset");
            var commander = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Root}/Data/Units/FieldCommander.asset");
            var priest = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Root}/Data/Units/BattlefieldPriest.asset");

            var armies = new GameObject("Armies");
            foreach (var side in new[] { Side.Player, Side.AI })
            {
                float s = side == Side.Player ? 1f : -1f;       // AI positions are the player's turned 180°
                var facing = Quaternion.Euler(0f, side == Side.Player ? 0f : 180f, 0f);
                string suffix = side == Side.Player ? "" : " (Enemy)";
                var units = new[]
                {
                    SquadBuilder.SpawnUnit(infantry, side, new Vector3(-22f, 0f, -24f) * s, facing, "Infantry Squad 1" + suffix),
                    SquadBuilder.SpawnUnit(infantry, side, new Vector3(0f, 0f, -24f) * s, facing, "Infantry Squad 2" + suffix),
                    SquadBuilder.SpawnUnit(infantry, side, new Vector3(22f, 0f, -24f) * s, facing, "Infantry Squad 3" + suffix),
                    SquadBuilder.SpawnUnit(commander, side, new Vector3(-11f, 0f, -27f) * s, facing, "Field Commander" + suffix),
                    SquadBuilder.SpawnUnit(priest, side, new Vector3(11f, 0f, -27f) * s, facing, "Battlefield Priest" + suffix),
                };
                // Units sit side by side under "Armies", never inside one another (a unit owns only its own soldiers).
                foreach (var u in units) u.transform.SetParent(armies.transform, true);
            }
        }

        private static Camera BuildLightingAndCamera()
        {
            var sun = new GameObject("Directional Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);

            // Camera starts over the player's side, looking north across the map.
            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(0f, 0f, -16f);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();
            var controller = rig.AddComponent<TacticalCameraController>();
            var so = new SerializedObject(controller);
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("boundsMin").vector2Value = new Vector2(-Width / 2f, -Depth / 2f);
            so.FindProperty("boundsMax").vector2Value = new Vector2(Width / 2f, Depth / 2f);
            so.FindProperty("maxDistance").floatValue = 85f;
            so.ApplyModifiedPropertiesWithoutUndo();
            camGo.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            camGo.transform.localPosition = camGo.transform.localRotation * new Vector3(0f, 0f, -40f);
            return cam;
        }

        // ------------------------------------------------------------------ Assets

        private static void CreateMaterials()
        {
            plaster = Mat("Building", new Color(0.72f, 0.68f, 0.6f));
            brick = Mat("Brick", new Color(0.55f, 0.32f, 0.26f));
            concrete = Mat("Concrete", new Color(0.6f, 0.6f, 0.58f));
            ruinMat = Mat("Ruin", new Color(0.47f, 0.45f, 0.42f));
            floorMat = Mat("Interior_Floor", new Color(0.45f, 0.4f, 0.35f));
            stone = Mat("Stone", new Color(0.5f, 0.48f, 0.45f));
            wood = Mat("Wood", new Color(0.55f, 0.4f, 0.22f));
            sandbag = Mat("Sandbag", new Color(0.62f, 0.56f, 0.4f));
            rust = Mat("Container", new Color(0.6f, 0.25f, 0.18f));
        }

        private static Material Mat(string name, Color color, Texture2D tex = null)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (isNew) mat.SetFloat("_Smoothness", 0.15f);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
            }
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Main menu, demo map, test room (see <see cref="ProjectScenes"/>).</summary>
        private static void AddToBuildSettings() => ProjectScenes.Apply();

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
