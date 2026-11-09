using Eldoira.CameraControl;
using Eldoira.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eldoira.EditorTools
{
    /// <summary>
    /// Generates the "cube room" test scene: a 60m x 44m battlefield (with test terrain) enclosed by
    /// four walls (open top so the tactical camera can see in), lighting and a camera rig.
    /// Menu: Eldoira > Build Cube Room. Also callable from the command line via
    /// -executeMethod Eldoira.EditorTools.CubeRoomBuilder.Build
    /// </summary>
    public static class CubeRoomBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/CubeRoom.unity";
        private const string MaterialDir = Root + "/Materials";

        private const float WallHeight = 4f;
        private const float WallThickness = 0.5f;

        [MenuItem("Eldoira/Build Cube Room")]
        public static void Build()
        {
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Materials");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floorMat = GetOrCreateMaterial("Floor", new Color(0.32f, 0.36f, 0.30f), CreateGridTexture());
            Material wallMat = GetOrCreateMaterial("Wall", new Color(0.55f, 0.55f, 0.58f), null);

            float w = BattlefieldScale.DefaultWidth;
            float d = BattlefieldScale.DefaultDepth;

            // --- Room ---
            var room = new GameObject("CubeRoom");

            // Ground (with a hill and a ditch) is structure terrain.
            TerrainBuilder.BuildGround(room.transform, floorMat);

            CreateWall(room.transform, "Wall_North", new Vector3(0f, WallHeight / 2f, d / 2f + WallThickness / 2f), new Vector3(w + WallThickness * 2f, WallHeight, WallThickness), wallMat);
            CreateWall(room.transform, "Wall_South", new Vector3(0f, WallHeight / 2f, -d / 2f - WallThickness / 2f), new Vector3(w + WallThickness * 2f, WallHeight, WallThickness), wallMat);
            CreateWall(room.transform, "Wall_East", new Vector3(w / 2f + WallThickness / 2f, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, d), wallMat);
            CreateWall(room.transform, "Wall_West", new Vector3(-w / 2f - WallThickness / 2f, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, d), wallMat);

            // Test terrain: a building (structure) and props (objects).
            TerrainBuilder.BuildTestTerrain(room.transform);

            // Walkable-area map, built when the scene starts.
            room.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            room.AddComponent<Eldoira.Terrain.BattlefieldNavigation>();

            // --- Lighting ---
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

            // --- Camera rig ---
            var rig = new GameObject("CameraRig");
            rig.transform.position = Vector3.zero;
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
            so.FindProperty("boundsMin").vector2Value = new Vector2(-w / 2f, -d / 2f);
            so.FindProperty("boundsMax").vector2Value = new Vector2(w / 2f, d / 2f);
            so.ApplyModifiedPropertiesWithoutUndo();
            // Position the camera for the editor view as well (Awake runs only in play mode).
            camGo.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            camGo.transform.localPosition = camGo.transform.localRotation * new Vector3(0f, 0f, -40f);

            SquadBuilder.SpawnTestSquad();
            SquadBuilder.SpawnEnemySquad();
            SquadBuilder.SpawnFieldCommander();
            SquadBuilder.SpawnBattlefieldPriest();

            // --- Game controller (player movement orders for the test squad) ---
            var gameController = new GameObject("GameController");
            var mover = gameController.AddComponent<Eldoira.Gameplay.MovementController>();
            var mso = new SerializedObject(mover);
            mso.FindProperty("cam").objectReferenceValue = cam;
            mso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetAsOnlyBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Eldoira] Cube room built at {ScenePath}");
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = mat;
            wall.layer = Eldoira.Terrain.TerrainLayers.Structure;
            wall.AddComponent<Eldoira.Terrain.StructureTerrain>().Configure(Eldoira.Terrain.StructureKind.Building);
            GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
        }

        private static Material GetOrCreateMaterial(string name, Color color, Texture2D tex)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
                // Ground UVs are already in 5 m units: one tile = thin line every meter, heavier every 5 m.
                var tiling = Vector2.one;
                mat.SetTextureScale("_BaseMap", tiling);
                mat.mainTextureScale = tiling;
            }
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Subtle measuring grid: thin line every meter, heavier line every 5 meters.</summary>
        private static Texture2D CreateGridTexture()
        {
            string path = $"{MaterialDir}/FloorGrid.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                const int size = 320; // 5 meters * 64 px
                const int pxPerMeter = size / 5;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool major = x < 3 || y < 3;
                    bool minor = x % pxPerMeter == 0 || y % pxPerMeter == 0;
                    byte v = major ? (byte)150 : minor ? (byte)215 : (byte)255;
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Keeps the full scene list (main menu, demo map, test room) rather than just this room.</summary>
        private static void SetAsOnlyBuildScene(string path) => ProjectScenes.Apply();

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
