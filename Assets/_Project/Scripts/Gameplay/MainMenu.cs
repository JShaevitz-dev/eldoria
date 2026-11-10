using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The main menu (Scenes/MainMenu, the first scene the game opens):
    ///   Play - starts a game on the demo map.
    ///   Quit - closes the program (in the Unity editor, stops Play mode).
    ///   Turn timer - On/Off (off by default) and its length (10 minutes by default); remembered between sessions.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        /// <summary>The scene names (they must be in the build settings).</summary>
        public const string MenuScene = "MainMenu";
        public const string GameScene = "UrbanDemo";

        private GUIStyle titleStyle, subtitleStyle, buttonStyle, optionStyle, optionLabelStyle;

        /// <summary>Starts a game on the demo map.</summary>
        public static void Play() => SceneManager.LoadScene(GameScene);

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

            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            GUI.Label(new Rect(cx - 400f, cy - 220f, 800f, 100f), "ELDORIA", titleStyle);
            GUI.Label(new Rect(cx - 400f, cy - 125f, 800f, 30f), "Turn-based tactics — urban demo", subtitleStyle);

            const float w = 260f, h = 60f;
            if (GUI.Button(new Rect(cx - w / 2f, cy - 20f, w, h), "Play", buttonStyle)) Play();
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
