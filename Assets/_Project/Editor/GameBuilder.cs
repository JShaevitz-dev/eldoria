using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.Build;

namespace Eldoira.EditorTools
{
    /// <summary>
    /// Builds the playable Windows game: Builds/Eldoira/Eldoira.exe (with its Eldoira_Data folder next to it), opening on the
    /// main menu. Needs no Unity install to run.
    /// Menu: Eldoira > Build Game (Windows), or -executeMethod Eldoira.EditorTools.GameBuilder.BuildWindows
    /// (batchmode exits with code 1 if the build fails).
    /// </summary>
    public static class GameBuilder
    {
        public const string OutputDir = "Builds/Eldoira";
        public const string ExeName = "Eldoira.exe";

        /// <summary>Shaders the game loads by name at runtime (so the build must keep them).</summary>
        private static readonly string[] RuntimeShaders = { "Sprites/Default", "Universal Render Pipeline/Unlit" };

        [MenuItem("Eldoira/Build Game (Windows)")]
        public static void BuildWindows()
        {
            ProjectScenes.Apply();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || !scenes[0].EndsWith("MainMenu.unity"))
                Fail("The main menu must be the first scene in the build (run Eldoira > Build Main Menu).");

            EnsureShadersIncluded();

            PlayerSettings.companyName = "Eldoira";
            PlayerSettings.productName = "Eldoira";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, recursive: true);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                Fail($"Build {summary.result}: {summary.totalErrors} error(s).");
            Debug.Log($"[Eldoira] Built {summary.outputPath} ({summary.totalSize / (1024f * 1024f):0.0} MB) in {summary.totalTime.TotalSeconds:0} s.");
        }

        /// <summary>Adds the shaders the game finds by name to Graphics Settings > Always Included Shaders.</summary>
        private static void EnsureShadersIncluded()
        {
            var graphics = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(graphics);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in RuntimeShaders)
            {
                var shader = Shader.Find(name);
                if (shader == null) Fail($"Shader '{name}' not found.");
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void Fail(string message)
        {
            Debug.LogError("[Eldoira] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw new BuildFailedException(message);
        }
    }
}
