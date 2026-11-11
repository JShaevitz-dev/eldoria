using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The main menu (Scenes/MainMenu, the first scene the game opens):
    ///   Play - opens the army builder; confirming a valid army starts a game on the demo map against an AI army.
    ///   Quit - closes the program (in the Unity editor, stops Play mode).
    ///   Turn timer - On/Off (off by default) and its length (10 minutes by default); remembered between sessions.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        /// <summary>The scene names (they must be in the build settings).</summary>
        public const string MenuScene = "MainMenu";
        public const string GameScene = "UrbanDemo";

        private GUIStyle titleStyle, subtitleStyle, buttonStyle, optionStyle, optionLabelStyle;

        /// <summary>The army builder, while it's open (null on the main menu itself).</summary>
        public ArmyBuilderScreen Builder { get; private set; }

        /// <summary>The player's last confirmed army and its faction: the builder starts from it when that faction is chosen again.</summary>
        private static List<UnitTemplate> lastArmy;
        private static Faction lastFaction;

        /// <summary>Forgets the last confirmed army: the builder starts from the demo army again.</summary>
        public static void ForgetLastArmy()
        {
            lastArmy = null;
            lastFaction = null;
        }

        /// <summary>Play: opens the army builder.</summary>
        public void OpenArmyBuilder() => Builder = new ArmyBuilderScreen(UnitCatalog.Load(), lastFaction, lastArmy);

        /// <summary>
        /// Starts a battle with this faction and army, if it's valid: the AI picks a random faction and builds a random valid
        /// army from it, and both go to the demo map. Returns why the army isn't valid (empty if the battle started).
        /// </summary>
        public static List<string> StartBattle(Faction faction, IReadOnlyList<UnitTemplate> army)
        {
            var problems = faction == null ? new List<string> { "Choose a faction first." } : ArmyRules.Problems(army, faction);
            if (problems.Count > 0) return problems;
            var catalog = UnitCatalog.Load();
            var rng = Core.GameRandom.Shared;
            var factions = catalog != null ? catalog.Factions.ToList() : new List<Faction>();
            var aiFaction = factions.Count > 0 ? factions[rng.Next(factions.Count)] : faction;
            var aiArmy = ArmyRules.RandomArmy(catalog != null ? catalog.UnitsFor(aiFaction) : army, rng);
            lastArmy = new List<UnitTemplate>(army);
            lastFaction = faction;
            ArmySelection.Set(army, aiArmy, faction, aiFaction);
            SceneManager.LoadScene(GameScene);
            return problems;
        }

        /// <summary>Closes the program (in the editor: stops Play mode).</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Back to this menu (e.g. from the end-of-battle banner).</summary>
        public static void BackToMenu() => SceneManager.LoadScene(MenuScene);

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                titleStyle.normal.textColor = new Color(0.95f, 0.78f, 0.3f);
                subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                subtitleStyle.normal.textColor = new Color(0.75f, 0.77f, 0.82f);
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
                optionStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
                optionLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            }

            if (Builder != null)
            {
                var result = Builder.Draw();
                if (result == ArmyBuilderScreen.Result.Back) Builder = null;
                else if (result == ArmyBuilderScreen.Result.Confirmed) StartBattle(Builder.Faction, Builder.Army);
                return;
            }

            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            GUI.Label(new Rect(cx - 400f, cy - 220f, 800f, 100f), "ELDORIA", titleStyle);
            GUI.Label(new Rect(cx - 400f, cy - 125f, 800f, 30f), "Turn-based tactics — urban demo", subtitleStyle);

            const float w = 260f, h = 60f;
            if (GUI.Button(new Rect(cx - w / 2f, cy - 20f, w, h), "Play", buttonStyle)) OpenArmyBuilder();
            if (GUI.Button(new Rect(cx - w / 2f, cy + 60f, w, h), "Quit", buttonStyle)) Quit();

            // Options: the turn timer (off by default; 10 minutes by default).
            bool timerOn = TurnTimerSettings.Enabled;
            if (GUI.Button(new Rect(cx - w / 2f, cy + 160f, w, 40f), $"Turn timer: {(timerOn ? "On" : "Off")}", optionStyle))
                TurnTimerSettings.Enabled = !timerOn;
            GUI.enabled = timerOn;
            if (GUI.Button(new Rect(cx - w / 2f, cy + 210f, 50f, 36f), "<", optionStyle)) StepMinutes(-1);
            GUI.Label(new Rect(cx - w / 2f + 50f, cy + 210f, w - 100f, 36f), $"{TurnTimerSettings.Minutes:0.#} min per turn", optionLabelStyle);
            if (GUI.Button(new Rect(cx + w / 2f - 50f, cy + 210f, 50f, 36f), ">", optionStyle)) StepMinutes(+1);
            GUI.enabled = true;
        }

        /// <summary>Moves the turn timer length to the next shorter or longer option.</summary>
        public static void StepMinutes(int direction)
        {
            var options = TurnTimerSettings.MinuteOptions;
            int i = System.Array.FindIndex(options, m => Mathf.Approximately(m, TurnTimerSettings.Minutes));
            if (i < 0) i = System.Array.IndexOf(options, TurnTimerSettings.DefaultMinutes);
            TurnTimerSettings.Minutes = options[Mathf.Clamp(i + direction, 0, options.Length - 1)];
        }
    }
}
