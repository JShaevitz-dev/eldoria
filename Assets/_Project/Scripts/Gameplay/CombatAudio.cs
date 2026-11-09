using Eldoira.Rules;
using UnityEngine;

namespace Eldoira.Gameplay
{
    /// <summary>
    /// Sound only: plays the gunshot (Resources/Audio/Gunshot) when a unit finishes a volley in which at least one
    /// soldier fired. Works for any unit, player or AI. Starts itself when play begins.
    /// </summary>
    public class CombatAudio : MonoBehaviour
    {
        public const string VolleySoundPath = "Audio/Gunshot";

        private static CombatAudio instance;

        private AudioSource source;
        private AudioClip volleySound;

        /// <summary>How many times the volley sound has been played (for tests).</summary>
        public static int VolleySoundsPlayed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("CombatAudio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CombatAudio>();
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // heard the same wherever the camera is
            volleySound = Resources.Load<AudioClip>(VolleySoundPath);
            if (volleySound == null) Debug.LogWarning($"[Eldoira] No volley sound at Resources/{VolleySoundPath}.");
            UnitShooting.VolleyFinished += OnVolleyFinished;
        }

        private void OnDestroy()
        {
            UnitShooting.VolleyFinished -= OnVolleyFinished;
            if (instance == this) instance = null;
        }

        private void OnVolleyFinished(ShootingResult result)
        {
            if (result == null || !result.AnyShotsFired || volleySound == null) return;
            source.PlayOneShot(volleySound);
            VolleySoundsPlayed++;
        }
    }
}
