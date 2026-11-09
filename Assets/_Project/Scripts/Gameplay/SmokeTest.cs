using System.Collections;
using System.Linq;
using Eldoira.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eldoira.Gameplay
{
    /// <summary>
    /// A quick end-to-end check of a built game, off unless the program is started with <c>-smokeTest</c>:
    /// from the main menu it starts a game, selects a unit and previews a move, lets the AI play three turns, checks the
    /// runtime shaders and visuals, then quits with exit code 0 (passed) or 1 (an error was logged or a check failed).
    /// Usage: Eldoira.exe -batchmode -nographics -smokeTest -logFile smoke.log
    /// </summary>
    public class SmokeTest : MonoBehaviour
    {
        private int problems;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-smokeTest")) return;
            var go = new GameObject("SmokeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<SmokeTest>();
        }

        private void Check(bool ok, string what)
        {
            if (ok) Debug.Log($"[Eldoira] Smoke test ok: {what}");
            else
            {
                problems++;
                Debug.LogWarning($"[Eldoira] Smoke test FAILED: {what}");
            }
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) problems++;
            };

            yield return null;
            Check(SceneManager.GetActiveScene().name == MainMenu.MenuScene, "the game opens on the main menu");
            Check(FindFirstObjectByType<MainMenu>() != null, "the main menu is there");

            MainMenu.Play();
            for (int i = 0; i < 10 && SceneManager.GetActiveScene().name != MainMenu.GameScene; i++) yield return null;
            yield return null;
            Check(SceneManager.GetActiveScene().name == MainMenu.GameScene, "Play starts a game");

            var controller = FindFirstObjectByType<MovementController>();
            Check(controller != null && controller.PlayerUnits.Count == 5, "the player's army is deployed");
            if (controller != null)
            {
                var unit = controller.PlayerUnits[0];
                Check(controller.SelectUnit(unit), "a unit can be selected");
                controller.PreviewAt(unit.Leader.Position + Vector3.forward * 8f);
                for (int turn = 0; turn < 3 && !Victory.IsOver; turn++) controller.EndTurn(instant: true);
                Check(Turns.Current >= 2, $"the AI played its turns (now turn {Turns.Current})");
            }

            KeywordSparkles.RefreshNow();
            CloseRangeMarkers.RefreshNow();
            foreach (var shader in new[] { "Sprites/Default", "Universal Render Pipeline/Unlit" })
                Check(Shader.Find(shader) != null, $"shader '{shader}' is in the build");
            Check(Resources.Load<AudioClip>(CombatAudio.VolleySoundPath) != null, "the gunshot sound is in the build");

            yield return null;
            Debug.Log(problems == 0 ? "[Eldoira] Smoke test PASSED" : $"[Eldoira] Smoke test found {problems} problem(s)");
            Application.Quit(problems == 0 ? 0 : 1);
        }
    }
}
