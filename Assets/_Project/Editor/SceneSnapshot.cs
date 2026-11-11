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
        /// -executeMethod Eldoria.EditorTools.SceneSnapshot.CaptureModels -snapshotOut models.png [-snapshotModels A,B]
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

            // Optional: -snapshotModels Name1,Name2 (prefab names) to show only those.
            string[] only = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotModels") only = args[i + 1].Split(',');

            // Side by side, each spaced by its own width, so big models (tanks, the Colossus) don't hide the rest.
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Units" });
            float x = 0f, tallest = 1.8f;
            var placed = new System.Collections.Generic.List<GameObject>();
            foreach (var guid in guids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (only != null && System.Array.IndexOf(only, prefab.name) < 0) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
                var bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                float width = Mathf.Max(0.8f, bounds.size.x);
                go.transform.position = new Vector3(x + width / 2f, 0f, 0f);
                x += width + 0.4f;
                tallest = Mathf.Max(tallest, bounds.size.y);
                placed.Add(go);
                Debug.Log($"[Eldoria] Model: {prefab.name}");
            }
            float rowWidth = Mathf.Max(1f, x - 0.4f);
            foreach (var go in placed) go.transform.position -= new Vector3(rowWidth / 2f, 0f, 0f);
            floor.transform.localScale = new Vector3(Mathf.Max(3f, rowWidth / 10f + 1f), 1f, Mathf.Max(1f, tallest / 5f));

            // Frame the whole row (and the tallest model) from slightly above.
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float fitHeight = tallest * 0.65f / Mathf.Tan(halfFov);
            float fitWidth = rowWidth * 0.55f / (Mathf.Tan(halfFov) * 1280f / 720f);
            float dist = Mathf.Max(4.6f, fitHeight, fitWidth);
            cam.transform.position = new Vector3(0f, tallest * 0.75f, -dist);
            cam.transform.LookAt(new Vector3(0f, tallest * 0.45f, 0f));
            cam.farClipPlane = dist * 4f;
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
