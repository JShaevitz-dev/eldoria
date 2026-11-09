using Eldoira.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Eldoira.EditorTools
{
    /// <summary>
    /// Creates the infantry model profiles and placeholder soldier prefabs (built from primitives,
    /// human-sized in meters), and places a 10-soldier test squad (9 troopers + 1 sergeant) in the open scene.
    /// Menu: Eldoira > Spawn Test Squad.
    /// </summary>
    public static class SquadBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ProfileDir = Root + "/Data/Profiles";
        private const string PrefabDir = Root + "/Prefabs/Units";
        private const string MaterialDir = Root + "/Materials";

        private const string SquadObjectName = "Squad_Player_Infantry";
        private const int SquadSize = 10;
        private const float Spacing = 2f; // meters between soldiers in the starting formation

        [MenuItem("Eldoira/Spawn Test Squad")]
        public static void SpawnInOpenScene()
        {
            SpawnTestSquad();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static Squad SpawnTestSquad()
        {
            EnsureFolder(Root, "Data");
            EnsureFolder(Root + "/Data", "Profiles");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root + "/Prefabs", "Units");
            EnsureFolder(Root, "Materials");

            var trooperProfile = GetOrCreateProfile("Trooper", "Trooper");
            var sergeantProfile = GetOrCreateProfile("Sergeant", "Sergeant");

            var armor = GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f));
            var skin = GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f));
            var weapon = GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f));
            var gold = GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f));

            var trooperPrefab = BuildSoldierPrefab("Trooper", trooperProfile, false, armor, skin, weapon, gold);
            var sergeantPrefab = BuildSoldierPrefab("Sergeant", sergeantProfile, true, armor, skin, weapon, gold);

            // Replace any previous copy of the test squad.
            var existing = GameObject.Find(SquadObjectName);
            if (existing != null) Object.DestroyImmediate(existing);

            var squadGo = new GameObject(SquadObjectName);
            // Deployed near the south (player) side, facing north.
            squadGo.transform.position = new Vector3(0f, 0f, -16f);
            var squad = squadGo.AddComponent<Squad>();
            var so = new SerializedObject(squad);
            so.FindProperty("squadName").stringValue = "Infantry Squad Alpha";
            so.FindProperty("side").enumValueIndex = (int)Side.Player;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Two ranks of five; sergeant at the centre of the front rank.
            int trooperNum = 1;
            for (int i = 0; i < SquadSize; i++)
            {
                int rank = i / 5;
                int file = i % 5;
                bool isSergeant = rank == 0 && file == 2;
                var prefab = isSergeant ? sergeantPrefab : trooperPrefab;

                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, squadGo.transform);
                model.name = isSergeant ? "Sergeant" : $"Trooper_{trooperNum++:00}";
                model.transform.localPosition = new Vector3((file - 2) * Spacing, 0f, -rank * Spacing);
                model.transform.localRotation = Quaternion.identity;
            }

            squad.RefreshModels();
            Debug.Log($"[Eldoira] Spawned {squad.Models.Count}-soldier squad '{squad.SquadName}', in coherency: {squad.IsInCoherency()}");
            return squad;
        }

        private static GameObject BuildSoldierPrefab(string name, ModelProfile profile, bool isLeader,
            Material armor, Material skin, Material weapon, Material gold)
        {
            float h = profile.height;          // 1.8m
            float headSize = 0.26f;
            float bodyHeight = h - headSize;   // capsule from feet to neck
            float bodyRadius = 0.25f;

            var root = new GameObject(name);
            var model = root.AddComponent<UnitModel>();
            var mso = new SerializedObject(model);
            mso.FindProperty("profile").objectReferenceValue = profile;
            mso.FindProperty("isLeader").boolValue = isLeader;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // One collider on the root for selection / line-of-sight later.
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = profile.radius;
            col.height = h;
            col.center = new Vector3(0f, h / 2f, 0f);

            // Body (capsule primitive is 2 tall, 1 wide at scale 1).
            Part(root, "Body", PrimitiveType.Capsule, armor,
                new Vector3(0f, bodyHeight / 2f, 0f), new Vector3(bodyRadius * 2f, bodyHeight / 2f, bodyRadius * 2f * 0.8f));
            Part(root, "Head", PrimitiveType.Sphere, skin,
                new Vector3(0f, bodyHeight + headSize / 2f - 0.02f, 0f), Vector3.one * headSize);
            // Rifle held forward; also shows which way the soldier is facing.
            Part(root, "Weapon", PrimitiveType.Cube, weapon,
                new Vector3(0.18f, h * 0.68f, 0.32f), new Vector3(0.07f, 0.1f, 0.85f));

            if (isLeader)
            {
                // Sergeant markings: helmet crest, shoulder plates and a sash.
                Part(root, "Crest", PrimitiveType.Cube, gold,
                    new Vector3(0f, h + 0.04f, -0.02f), new Vector3(0.05f, 0.12f, 0.28f));
                Part(root, "Pauldron_L", PrimitiveType.Sphere, gold,
                    new Vector3(-0.26f, h * 0.78f, 0f), Vector3.one * 0.2f);
                Part(root, "Pauldron_R", PrimitiveType.Sphere, gold,
                    new Vector3(0.26f, h * 0.78f, 0f), Vector3.one * 0.2f);
                Part(root, "Sash", PrimitiveType.Cylinder, gold,
                    new Vector3(0f, h * 0.5f, 0f), new Vector3(bodyRadius * 2.1f, 0.04f, bodyRadius * 2.1f * 0.8f));
            }

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Part(GameObject parent, string name, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static ModelProfile GetOrCreateProfile(string file, string displayName)
        {
            string path = $"{ProfileDir}/{file}.asset";
            var p = AssetDatabase.LoadAssetAtPath<ModelProfile>(path);
            if (p != null) return p; // keep any tweaks made in the inspector
            p = ScriptableObject.CreateInstance<ModelProfile>();
            p.displayName = displayName;
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.35f);
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
