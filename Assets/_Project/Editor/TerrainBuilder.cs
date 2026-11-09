using Eldoira.Terrain;
using UnityEditor;
using UnityEngine;

namespace Eldoira.EditorTools
{
    /// <summary>
    /// Builds the test terrain for the cube room:
    ///  Structure: ground mesh with a hill and a ditch; a roofless building with a doorway;
    ///             a raised platform reached by a slope.
    ///  Objects:   low walls (cover, mantle), crates (cover, mantle, climb onto), a barricade (Half-cover keyword),
    ///             a shipping container (cover, climb onto, blocks movement, Full-cover), a rock pillar (cover, blocks, Full-cover).
    /// </summary>
    public static class TerrainBuilder
    {
        private const string Root = "Assets/_Project";
        private const string MeshDir = Root + "/Meshes";
        private const string MaterialDir = Root + "/Materials";
        private const float GroundResolution = 0.5f;

        // Landforms (meters).
        public static readonly Vector3 HillCenter = new Vector3(-14f, 0f, 4f);
        public const float HillRadius = 7.5f;
        public const float HillHeight = 3f;

        public static readonly Vector3 DitchStart = new Vector3(3f, 0f, -4f);
        public static readonly Vector3 DitchEnd = new Vector3(21f, 0f, -4f);
        public const float DitchHalfWidth = 2.5f;
        public const float DitchDepth = 1.2f;

        public static readonly Vector3 BuildingCenter = new Vector3(14f, 0f, 9f);
        public const float BuildingWidth = 8f, BuildingDepth = 6f, BuildingWallHeight = 3f, BuildingWallThickness = 0.3f, DoorWidth = 1.6f;

        public static readonly Vector3 BarricadeCenter = new Vector3(6f, 0.55f, -13f);

        // Raised platform reached by a slope on its south side.
        public static readonly Vector3 PlatformCenter = new Vector3(2f, 0f, 14f);
        public const float PlatformWidth = 6f, PlatformDepth = 4f, PlatformHeight = 2f;
        public const float SlopeRun = 3.5f, SlopeWidth = 2f;

        /// <summary>Ground height at a point: flat at 0 except for the hill and the ditch.</summary>
        public static float GroundHeight(float x, float z)
        {
            float h = 0f;

            float dh = new Vector2(x - HillCenter.x, z - HillCenter.z).magnitude;
            if (dh < HillRadius) h += HillHeight * 0.5f * (1f + Mathf.Cos(Mathf.PI * dh / HillRadius));

            // Ditch: cosine trough across its width, tapered at both ends.
            Vector2 a = new Vector2(DitchStart.x, DitchStart.z), b = new Vector2(DitchEnd.x, DitchEnd.z), p = new Vector2(x, z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float across = (p - (a + ab * t)).magnitude;
            float along = t * ab.magnitude;
            float endTaper = Mathf.Clamp01(Mathf.Min(along, ab.magnitude - along) / 3f);
            if (across < DitchHalfWidth)
                h -= DitchDepth * endTaper * 0.5f * (1f + Mathf.Cos(Mathf.PI * across / DitchHalfWidth));

            return h;
        }

        public static GameObject BuildGround(Transform parent, Material mat)
        {
            EnsureFolder(Root, "Meshes");
            float w = Core.BattlefieldScale.DefaultWidth, d = Core.BattlefieldScale.DefaultDepth;
            int nx = Mathf.RoundToInt(w / GroundResolution) + 1;
            int nz = Mathf.RoundToInt(d / GroundResolution) + 1;

            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = -w / 2f + ix * GroundResolution;
                float z = -d / 2f + iz * GroundResolution;
                verts[iz * nx + ix] = new Vector3(x, GroundHeight(x, z), z);
                uvs[iz * nx + ix] = new Vector2(x / 5f, z / 5f); // grid texture tile = 5 m
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

            string path = $"{MeshDir}/Ground.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "Ground";
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
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.layer = TerrainLayers.Structure;
            go.AddComponent<StructureTerrain>().Configure(StructureKind.Ground);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            // Markers so the hill and ditch show up by name in the hierarchy (the shape is in the ground mesh).
            Marker(go.transform, "Hill", HillCenter + Vector3.up * HillHeight, StructureKind.Hill);
            Marker(go.transform, "Ditch", (DitchStart + DitchEnd) / 2f + Vector3.down * DitchDepth, StructureKind.Ditch);
            return go;
        }

        public static void BuildTestTerrain(Transform parent)
        {
            var stone = Mat("Stone", new Color(0.5f, 0.48f, 0.45f));
            var wood = Mat("Wood", new Color(0.55f, 0.4f, 0.22f));
            var rust = Mat("Container", new Color(0.6f, 0.25f, 0.18f));
            var plaster = Mat("Building", new Color(0.72f, 0.68f, 0.6f));

            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(parent, false);

            // --- Structure: roofless building, doorway in the south wall ---
            var building = new GameObject("Building");
            building.transform.SetParent(terrain.transform, false);
            building.transform.position = BuildingCenter;
            float hw = BuildingWidth / 2f, hd = BuildingDepth / 2f, t = BuildingWallThickness, wh = BuildingWallHeight;
            float sideLen = (BuildingWidth - DoorWidth) / 2f;
            BuildingWall(building.transform, "Wall_N", new Vector3(0, wh / 2, hd), new Vector3(BuildingWidth + t, wh, t), plaster);
            BuildingWall(building.transform, "Wall_E", new Vector3(hw, wh / 2, 0), new Vector3(t, wh, BuildingDepth), plaster);
            BuildingWall(building.transform, "Wall_W", new Vector3(-hw, wh / 2, 0), new Vector3(t, wh, BuildingDepth), plaster);
            BuildingWall(building.transform, "Wall_S_Left", new Vector3(-hw + sideLen / 2f, wh / 2, -hd), new Vector3(sideLen + t, wh, t), plaster);
            BuildingWall(building.transform, "Wall_S_Right", new Vector3(hw - sideLen / 2f, wh / 2, -hd), new Vector3(sideLen + t, wh, t), plaster);
            building.AddComponent<StructureTerrain>().Configure(StructureKind.Building);
            building.layer = TerrainLayers.Structure;

            // --- Structure: raised platform reached by a slope on its south side ---
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.SetParent(terrain.transform, false);
            platform.transform.position = PlatformCenter + Vector3.up * (PlatformHeight / 2f);
            platform.transform.localScale = new Vector3(PlatformWidth, PlatformHeight, PlatformDepth);
            platform.GetComponent<Renderer>().sharedMaterial = plaster;
            platform.layer = TerrainLayers.Structure;
            platform.AddComponent<StructureTerrain>().Configure(StructureKind.Building);

            Vector3 slopeTop = PlatformCenter + new Vector3(0f, PlatformHeight, -PlatformDepth / 2f);
            Vector3 slopeBottom = slopeTop - new Vector3(0f, PlatformHeight, SlopeRun);
            Vector3 along = slopeTop - slopeBottom;
            const float slopeThickness = 0.2f;
            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "Slope";
            slope.transform.SetParent(terrain.transform, false);
            var slopeRot = Quaternion.LookRotation(along.normalized, Vector3.up);
            slope.transform.rotation = slopeRot;
            // Top face of the slab runs from the ground to the platform's edge.
            slope.transform.position = (slopeTop + slopeBottom) / 2f - slopeRot * Vector3.up * (slopeThickness / 2f);
            slope.transform.localScale = new Vector3(SlopeWidth, slopeThickness, along.magnitude);
            slope.GetComponent<Renderer>().sharedMaterial = stone;
            slope.layer = TerrainLayers.Structure;
            slope.AddComponent<StructureTerrain>().Configure(StructureKind.Slope);

            // --- Objects ---
            var keywords = SquadBuilder.EnsureKeywords();
            var objects = new GameObject("Objects");
            objects.transform.SetParent(terrain.transform, false);

            Prop(objects.transform, "Low Wall", PrimitiveType.Cube, stone, new Vector3(0f, 0.5f, -10f), new Vector3(4f, 1f, 0.3f), 0f,
                cover: true, mantle: true, climb: false);
            Prop(objects.transform, "Low Wall", PrimitiveType.Cube, stone, new Vector3(-6f, 0.5f, -10f), new Vector3(4f, 1f, 0.3f), 0f,
                cover: true, mantle: true, climb: false);
            Prop(objects.transform, "Crate", PrimitiveType.Cube, wood, new Vector3(-3f, 0.6f, -2f), Vector3.one * 1.2f, 10f,
                cover: true, mantle: true, climb: true);
            Prop(objects.transform, "Crate", PrimitiveType.Cube, wood, new Vector3(-1.6f, 0.6f, -2.3f), Vector3.one * 1.2f, -5f,
                cover: true, mantle: true, climb: true);
            Prop(objects.transform, "Crate", PrimitiveType.Cube, wood, new Vector3(-2.3f, 1.8f, -2.15f), Vector3.one * 1.2f, 25f,
                cover: true, mantle: true, climb: true);
            Prop(objects.transform, "Shipping Container", PrimitiveType.Cube, rust, new Vector3(3f, 1.3f, 3f), new Vector3(6f, 2.6f, 2.4f), 0f,
                cover: true, mantle: false, climb: true, objectKeywords: new[] { keywords["Full-cover"] });
            // Barricade with Half-cover: soldiers within 5 m gain In half-cover.
            Prop(objects.transform, "Barricade", PrimitiveType.Cube, wood, BarricadeCenter, new Vector3(2.5f, 1.1f, 0.4f), 0f,
                cover: true, mantle: true, climb: false, objectKeywords: new[] { keywords["Half-cover"] });
            Prop(objects.transform, "Rock Pillar", PrimitiveType.Cylinder, stone, new Vector3(-9f, 1.5f, -13f), new Vector3(1.6f, 1.5f, 1.6f), 0f,
                cover: true, mantle: false, climb: false, objectKeywords: new[] { keywords["Full-cover"] });
        }

        private static void BuildingWall(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.layer = TerrainLayers.Structure;
            go.AddComponent<StructureTerrain>().Configure(StructureKind.Building);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static void Prop(Transform parent, string name, PrimitiveType shape, Material mat, Vector3 pos, Vector3 scale, float yaw,
            bool cover, bool mantle, bool climb, Units.Keyword[] objectKeywords = null)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (shape == PrimitiveType.Cylinder)
            {
                // Cylinder primitives come with a capsule collider; use the exact mesh instead.
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().convex = true;
            }
            go.layer = TerrainLayers.Object;
            go.AddComponent<TerrainObject>().Configure(name, cover, mantle, climb, objectKeywords);
        }

        private static void Marker(Transform parent, string name, Vector3 pos, StructureKind kind)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = TerrainLayers.Structure;
            go.AddComponent<StructureTerrain>().Configure(kind);
        }

        private static Material Mat(string name, Color color)
        {
            EnsureFolder(Root, "Materials");
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
