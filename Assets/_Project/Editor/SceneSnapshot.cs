using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Eldoira.EditorTools
{
    /// <summary>
    /// Renders the main camera of the CubeRoom scene to a PNG (handy for quick visual checks
    /// from the command line): -executeMethod Eldoira.EditorTools.SceneSnapshot.Capture -snapshotOut path.png
    /// </summary>
    public static class SceneSnapshot
    {
        public static void Capture()
        {
            string outPath = "Snapshot.png";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-snapshotOut") outPath = args[i + 1];

            EditorSceneManager.OpenScene("Assets/_Project/Scenes/CubeRoom.unity");
            var cam = Camera.main;
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
            Debug.Log($"[Eldoira] Snapshot written to {outPath}");
        }
    }
}
