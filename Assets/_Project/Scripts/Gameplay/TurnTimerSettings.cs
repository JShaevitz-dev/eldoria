using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The turn timer option (set on the main menu, remembered between sessions): when on, the player has a limited time
    /// for each of their turns; when it runs out, the turn ends automatically. Off by default; 10 minutes by default.
    /// </summary>
    public static class TurnTimerSettings
    {
        public const bool DefaultEnabled = false;
        public const float DefaultMinutes = 10f;

        /// <summary>The lengths the menu offers, in minutes.</summary>
        public static readonly float[] MinuteOptions = { 1f, 2f, 3f, 5f, 10f, 15f, 20f, 30f };

        private const string EnabledKey = "Eldoria.TurnTimer.Enabled";
        private const string MinutesKey = "Eldoria.TurnTimer.Minutes";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, DefaultEnabled ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Length of each of the player's turns, in minutes.</summary>
        public static float Minutes
        {
            get => Mathf.Max(0.01f, PlayerPrefs.GetFloat(MinutesKey, DefaultMinutes));
            set { PlayerPrefs.SetFloat(MinutesKey, Mathf.Max(0.01f, value)); PlayerPrefs.Save(); }
        }

        public static float Seconds => Minutes * 60f;

        /// <summary>Back to the defaults (off, 10 minutes).</summary>
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(EnabledKey);
            PlayerPrefs.DeleteKey(MinutesKey);
            PlayerPrefs.Save();
        }

        /// <summary>Format seconds as m:ss.</summary>
        public static string Format(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
