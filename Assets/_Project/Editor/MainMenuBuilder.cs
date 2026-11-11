using System.Linq;
using Eldoria.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Eldoria.EditorTools
{
    /// <summary>
    /// Builds the main menu scene (Scenes/MainMenu): a camera and the <see cref="MainMenu"/> (Play / Quit).
    /// Menu: Eldoria > Build Main Menu, or -executeMethod Eldoria.EditorTools.MainMenuBuilder.Build
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";

        [MenuItem("Eldoria/Build Main Menu")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
            camGo.AddComponent<AudioListener>();

            new GameObject("MainMenu").AddComponent<MainMenu>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectScenes.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Eldoria] Main menu built at {ScenePath}");
        }
    }

    /// <summary>
    /// The scenes in the build, in order: the main menu first (what the game opens), then the maps, then the test
    /// room (used by the tests). Scenes that haven't been built yet are left out.
    /// </summary>
    public static class ProjectScenes
    {
        public static readonly string[] InOrder =
        {
            MainMenuBuilder.ScenePath,
            UrbanMapBuilder.ScenePath,
            CountrysideMapBuilder.ScenePath,
            "Assets/_Project/Scenes/CubeRoom.unity",
        };

        public static void Apply()
        {
            EditorBuildSettings.scenes = InOrder
                .Where(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null)
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();
        }
    }
}
