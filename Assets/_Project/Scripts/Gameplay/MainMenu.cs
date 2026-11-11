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
    ///   Play - choose a map, then build an army; confirming a valid army starts a game on that map against an AI army.
    ///   Quit - closes the program (in the Unity editor, stops Play mode).
    ///   Turn timer - On/Off (off by default) and its length (10 minutes by default); remembered between sessions.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        /// <summary>The scene names (they must be in the build settings).</summary>
        public const string MenuScene = "MainMenu";
        public const string GameScene = "UrbanDemo";

        private GUIStyle titleStyle, subtitleStyle, buttonStyle, optionStyle, optionLabelStyle, mapButtonStyle;

        /// <summary>True while the player is choosing a map (after Play, before the army builder).</summary>
        public bool ChoosingMap { get; private set; }

        /// <summary>The map the next battle is fought on (the Boxed in unless another is chosen).</summary>
        public static Battlefield ChosenMap { get; private set; } = Battlefields.UrbanDemo;

        /// <summary>Play: choose a map first.</summary>
        public void OpenMapChoice()
        {
            Builder = null;
            ChoosingMap = true;
        }

        /// <summary>Chooses the map for the next battle and opens the army builder.</summary>
        public void ChooseMap(Battlefield map)
        {
            ChosenMap = map ?? Battlefields.UrbanDemo;
            ChoosingMap = false;
            OpenArmyBuilder();
        }

        /// <summary>The army builder, while it's open (null on the main menu itself).</summary>
        public ArmyBuilderScreen Builder { get; private set; }

        /// <summary>The player's last confirmed army and its faction: the builder starts from it when that faction is chosen again.</summary>
        private static List<UnitTemplate> lastArmy;
        private static List<UnitLoadout> lastLoadouts;
        private static Faction lastFaction;

        /// <summary>Forgets the last confirmed army: the builder starts from the demo army again.</summary>
        public static void ForgetLastArmy()
        {
            lastArmy = null;
            lastLoadouts = null;
            lastFaction = null;
            ChosenMap = Battlefields.UrbanDemo;
        }

        /// <summary>Opens the army builder (after the map is chosen).</summary>
        public void OpenArmyBuilder() => Builder = new ArmyBuilderScreen(UnitCatalog.Load(), lastFaction, lastArmy, lastLoadouts);

        /// <summary>
        /// Starts a battle with this faction and army, if it's valid: the AI picks a random faction and builds a random valid
        /// army from it, and both go to the chosen map. Returns why the army isn't valid (empty if the battle started).
        /// </summary>
        /// <param name="loadouts">Each unit's weapon swaps and equipment (same order as <paramref name="army"/>), if any.</param>
        public static List<string> StartBattle(Faction faction, IReadOnlyList<UnitTemplate> army, IReadOnlyList<UnitLoadout> loadouts = null)
        {
            var problems = faction == null ? new List<string> { "Choose a faction first." } : ArmyRules.Problems(army, faction);
            if (problems.Count > 0) return problems;
            var catalog = UnitCatalog.Load();
            var rng = Core.GameRandom.Shared;
            var factions = catalog != null ? catalog.Factions.ToList() : new List<Faction>();
            var aiFaction = factions.Count > 0 ? factions[rng.Next(factions.Count)] : faction;
            var aiArmy = ArmyRules.RandomArmy(catalog != null ? catalog.UnitsFor(aiFaction) : army, rng);
            lastArmy = new List<UnitTemplate>(army);
            lastLoadouts = loadouts?.Select(l => l?.Clone()).ToList();
            lastFaction = faction;
            ArmySelection.Set(army, aiArmy, faction, aiFaction, loadouts);
            SceneManager.LoadScene(ChosenMap.Scene);
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
                mapButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, wordWrap = true, richText = true, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(20, 20, 12, 12) };
            }

            if (Builder != null)
            {
                var result = Builder.Draw();
                if (result == ArmyBuilderScreen.Result.Back) OpenMapChoice();   // back to the map choice
                else if (result == ArmyBuilderScreen.Result.Confirmed) StartBattle(Builder.Faction, Builder.Army, Builder.Loadouts);
                return;
            }
            if (ChoosingMap)
            {
                DrawMapChoice();
                return;
            }

            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            GUI.Label(new Rect(cx - 400f, cy - 220f, 800f, 100f), "ELDORIA", titleStyle);
            GUI.Label(new Rect(cx - 400f, cy - 125f, 800f, 30f), "Turn-based tactics", subtitleStyle);

            const float w = 260f, h = 60f;
            if (GUI.Button(new Rect(cx - w / 2f, cy - 20f, w, h), "Play", buttonStyle)) OpenMapChoice();
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

        /// <summary>The map choice: one button per map, with its description.</summary>
        private void DrawMapChoice()
        {
            float w = Mathf.Min(760f, Screen.width - 40f), h = Mathf.Min(560f, Screen.height - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h));
            var heading = new GUIStyle(titleStyle) { fontSize = 36 };
            GUILayout.Label("CHOOSE A BATTLEFIELD", heading);
            GUILayout.Space(16f);
            foreach (var map in Battlefields.All)
            {
                if (GUILayout.Button($"<size=24><b>{map.Name}</b></size>\n{map.Description}", mapButtonStyle, GUILayout.MinHeight(110f)))
                    ChooseMap(map);
                GUILayout.Space(10f);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", buttonStyle, GUILayout.Height(50f))) ChoosingMap = false;
            GUILayout.EndArea();
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
