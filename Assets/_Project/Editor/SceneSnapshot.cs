using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Eldoria.EditorTools
{
    /// <summary>
    /// Renders the main camera of a scene to a PNG (handy for quick visual checks from the command line):
    /// -executeMethod Eldoria.EditorTools.SceneSnapshot.Capture -snapshotOut path.png
    /// Options: -snapshotScene Assets/.../Scene.unity (default CubeRoom), -snapshotTop size (straight-down map view
    /// showing size meters top to bottom), -snapshotLookAt x,y,z -snapshotDistance d (close-up).
    /// </summary>
    public static class SceneSnapshot
    {
        /// <summary>
        /// Renders every soldier model (Prefabs/Units) side by side on a plain floor, to check how they look:
        /// -executeMethod Eldoria.EditorTools.SceneSnapshot.CaptureModels -snapshotOut models.png
        /// </summary>
        public static void CaptureModels()
        {
            string outPath = "Models.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotOut") outPath = args[i + 1];

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(3f, 1f, 1f);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor.mat");
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Units" });
            float x = -(guids.Length - 1) * 1.2f / 2f;
            foreach (var guid in guids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetPositionAndRotation(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 200f, 0f));
                Debug.Log($"[Eldoria] Model at x={x:0.0}: {prefab.name}");
                x += 1.2f;
            }

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            cam.transform.position = new Vector3(0f, 1.6f, -4.6f);
            cam.transform.LookAt(new Vector3(0f, 0.95f, 0f));
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Debug.Log($"[Eldoria] Models snapshot written to {outPath}");
        }

        public static void Capture()
        {
            string outPath = "Snapshot.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotOut") outPath = args[i + 1];

            string scenePath = "Assets/_Project/Scenes/CubeRoom.unity";
            float topSize = 0f;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-snapshotScene") scenePath = args[i + 1];
                if (args[i] == "-snapshotTop") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out topSize);
            }
            EditorSceneManager.OpenScene(scenePath);
            var cam = Camera.main;
            if (topSize > 0f)
            {
                cam.orthographic = true;
                cam.orthographicSize = topSize / 2f;
                cam.transform.SetPositionAndRotation(new Vector3(0f, 80f, 0f), Quaternion.Euler(90f, 0f, 0f));
            }
            // Optional close-up: -snapshotLookAt x,y,z -snapshotDistance d
            string lookAt = null; float dist = 10f;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-snapshotLookAt") lookAt = args[i + 1];
                if (args[i] == "-snapshotDistance") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dist);
            }
            if (lookAt != null)
            {
                var p = lookAt.Split(',');
                var target = new Vector3(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
                                         float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
                                         float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture));
                cam.transform.position = target + new Vector3(0.35f, 0.55f, 0.75f).normalized * dist;
                cam.transform.LookAt(target);
            }
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Debug.Log($"[Eldoria] Snapshot written to {outPath}");
        }
    }
}
