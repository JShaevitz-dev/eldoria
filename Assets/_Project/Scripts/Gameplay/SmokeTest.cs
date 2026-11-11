using System.Collections;
using System.Linq;
using Eldoria.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// A quick end-to-end check of a built game, off unless the program is started with <c>-smokeTest</c>:
    /// from the main menu it starts a game, selects a unit and previews a move, lets the AI play three turns, checks the
    /// runtime shaders and visuals, then quits with exit code 0 (passed) or 1 (an error was logged or a check failed).
    /// Usage: Eldoria.exe -batchmode -nographics -smokeTest -logFile smoke.log
    /// With a window (no -batchmode) and <c>-smokeScreenshots folder</c>, it also saves screenshots of the main menu and
    /// the game (HUD and labels included) into that folder.
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

        /// <summary>Saves a screenshot (with the UI) if -smokeScreenshots was given and there is a window to capture.</summary>
        private IEnumerator Screenshot(string file)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-smokeScreenshots");
            if (i < 0 || i + 1 >= args.Length || Application.isBatchMode) yield break;
            for (int f = 0; f < 5; f++) yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(args[i + 1], file), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log($"[Eldoria] Screenshot saved: {file}");
        }

        private void Check(bool ok, string what)
        {
            if (ok) Debug.Log($"[Eldoria] Smoke test ok: {what}");
            else
            {
                problems++;
                Debug.LogWarning($"[Eldoria] Smoke test FAILED: {what}");
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
            yield return Screenshot("menu.png");
            Check(FindFirstObjectByType<MainMenu>() != null, "the main menu is there");

            var menu = FindFirstObjectByType<MainMenu>();
            menu.OpenMapChoice();
            yield return Screenshot("maps.png");
            Check(Battlefields.All.Count == 2, "the map choice offers Boxed in and Open Season");
            menu.ChooseMap(Battlefields.EnglishCountryside);
            yield return Screenshot("factions.png");
            Check(menu.Builder.Factions.Count == 3, $"the army builder offers the factions ({menu.Builder.Factions.Count})");
            menu.Builder.ChooseFaction(menu.Builder.Factions[0]);
            yield return Screenshot("army.png");
            Check(menu.Builder != null && menu.Builder.Available.Count >= 6, $"the army builder lists every unit ({menu.Builder?.Available.Count})");
            Check(menu.Builder != null && menu.Builder.Confirm(), "the starting army is valid");
            var army = new System.Collections.Generic.List<Eldoria.Units.UnitTemplate>(menu.Builder.Army);
            MainMenu.StartBattle(menu.Builder.Faction, army);
            for (int i = 0; i < 10 && SceneManager.GetActiveScene().name != Battlefields.EnglishCountryside.Scene; i++) yield return null;
            yield return null;
            Check(SceneManager.GetActiveScene().name == Battlefields.EnglishCountryside.Scene, "Play starts a game on the chosen map");

            var controller = FindFirstObjectByType<MovementController>();
            Check(controller != null && controller.PlayerUnits.Count == army.Count, "the player's army is deployed");
            Check(ArmySelection.HasArmies && FindObjectsByType<Eldoria.Units.Unit>(FindObjectsSortMode.None).Count(u => u.Side == Eldoria.Units.Side.AI) == ArmySelection.AIArmy.Count,
                $"the AI's army is deployed ({ArmySelection.AIArmy?.Count} units)");
            if (controller != null)
            {
                var unit = controller.PlayerUnits[0];
                Check(controller.SelectUnit(unit), "a unit can be selected");
                controller.PreviewAt(unit.Leader.Position + Vector3.forward * 8f);
                yield return Screenshot("game.png");
                for (int turn = 0; turn < 3 && !Victory.IsOver; turn++) controller.EndTurn(instant: true);
                Check(Turns.Current >= 2, $"the AI played its turns (now turn {Turns.Current})");
            }

            KeywordSparkles.RefreshNow();
            CloseRangeMarkers.RefreshNow();
            foreach (var shader in new[] { "Sprites/Default", "Universal Render Pipeline/Unlit" })
                Check(Shader.Find(shader) != null, $"shader '{shader}' is in the build");
            Check(Resources.Load<AudioClip>(CombatAudio.VolleySoundPath) != null, "the gunshot sound is in the build");

            yield return null;
            Debug.Log(problems == 0 ? "[Eldoria] Smoke test PASSED" : $"[Eldoria] Smoke test found {problems} problem(s)");
            Application.Quit(problems == 0 ? 0 : 1);
        }
    }
}
