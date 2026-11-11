using Eldoria.Rules;
using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// Sound only. Starts itself when play begins.
    ///  - Gunshot (Resources/Audio/Gunshot) when a unit finishes a volley in which at least one soldier fired,
    ///    for any unit, player or AI.
    ///  - An ability's cast sound (e.g. the Battle Priest's spells: Hallelujah) when it is used successfully.
    ///  - A bell when the player's turn starts (Resources/Audio/Bell if present, otherwise a synthesised bell).
    ///  - Error sound when an order is refused, e.g. Divine Healing on a soldier at full health
    ///    (Resources/Audio/Error if present, otherwise a short synthesised "denied" buzz).
    /// </summary>
    public class CombatAudio : MonoBehaviour
    {
        public const string VolleySoundPath = "Audio/Gunshot";
        public const string ErrorSoundPath = "Audio/Error";
        public const string BellSoundPath = "Audio/Bell";

        private static CombatAudio instance;

        private AudioSource source;
        private AudioClip volleySound;
        private AudioClip errorSound;
        private AudioClip bellSound;

        /// <summary>How many times the volley sound has been played (for tests).</summary>
        public static int VolleySoundsPlayed { get; private set; }
        /// <summary>How many ability cast sounds have been played (for tests).</summary>
        public static int CastSoundsPlayed { get; private set; }
        /// <summary>The last ability cast sound played (for tests).</summary>
        public static AudioClip LastCastSound { get; private set; }
        /// <summary>How many times the your-turn bell has rung (for tests).</summary>
        public static int BellsPlayed { get; private set; }
        /// <summary>How many times the error sound has been played (for tests).</summary>
        public static int ErrorSoundsPlayed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("CombatAudio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CombatAudio>();
        }

        /// <summary>Plays the error sound: an order was refused and nothing happened.</summary>
        public static void PlayError()
        {
            if (instance == null) Bootstrap();
            instance.source.PlayOneShot(instance.errorSound);
            ErrorSoundsPlayed++;
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // heard the same wherever the camera is
            volleySound = Resources.Load<AudioClip>(VolleySoundPath);
            if (volleySound == null) Debug.LogWarning($"[Eldoria] No volley sound at Resources/{VolleySoundPath}.");
            errorSound = Resources.Load<AudioClip>(ErrorSoundPath);
            if (errorSound == null) errorSound = SynthesiseErrorSound();
            bellSound = Resources.Load<AudioClip>(BellSoundPath);
            if (bellSound == null) bellSound = SynthesiseBell();
            UnitShooting.VolleyFinished += OnVolleyFinished;
            Turns.TurnStarted += OnTurnStarted;
            ActiveAbilities.AbilityUsed += OnAbilityUsed;
        }

        private void OnDestroy()
        {
            UnitShooting.VolleyFinished -= OnVolleyFinished;
            ActiveAbilities.AbilityUsed -= OnAbilityUsed;
            Turns.TurnStarted -= OnTurnStarted;
            if (instance == this) instance = null;
        }

        private void OnVolleyFinished(ShootingResult result)
        {
            if (result == null || !result.AnyShotsFired || volleySound == null) return;
            source.PlayOneShot(volleySound);
            VolleySoundsPlayed++;
        }

        private void OnAbilityUsed(Units.Soldier user, Units.Ability ability, AbilityResult result)
        {
            if (ability == null || ability.castSound == null) return;
            source.PlayOneShot(ability.castSound);
            CastSoundsPlayed++;
            LastCastSound = ability.castSound;
        }

        /// <summary>The player goes first every turn, so a new turn means it's the player's turn: ring the bell.</summary>
        private void OnTurnStarted(int turn)
        {
            source.PlayOneShot(bellSound, 0.8f);
            BellsPlayed++;
        }

        /// <summary>A bright bell: a few inharmonic partials with a quick strike and a long ring-out.</summary>
        private static AudioClip SynthesiseBell()
        {
            const int rate = 44100;
            const float length = 1.6f, fundamental = 880f;
            // (frequency ratio, loudness, how fast it dies away)
            var partials = new[] { (1f, 1f, 2.2f), (2f, 0.55f, 3f), (2.76f, 0.4f, 4f), (5.4f, 0.25f, 6f), (8.9f, 0.12f, 9f) };
            var data = new float[(int)(length * rate)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float strike = Mathf.Min(1f, t / 0.003f);
                float v = 0f;
                foreach (var (ratio, amp, decay) in partials)
                    v += amp * Mathf.Exp(-decay * t) * Mathf.Sin(2f * Mathf.PI * fundamental * ratio * t);
                data[i] = v * strike * 0.22f;
            }
            var clip = AudioClip.Create("Bell", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Two short low buzzes, the second lower: a "denied" sound.</summary>
        private static AudioClip SynthesiseErrorSound()
        {
            const int rate = 44100;
            const float tone = 0.11f, gap = 0.04f;
            int toneSamples = (int)(tone * rate), gapSamples = (int)(gap * rate);
            var data = new float[toneSamples * 2 + gapSamples];
            void Tone(int start, float freq)
            {
                for (int i = 0; i < toneSamples; i++)
                {
                    float t = (float)i / rate;
                    float envelope = Mathf.Min(1f, i / (0.005f * rate)) * Mathf.Min(1f, (toneSamples - i) / (0.02f * rate));
                    // A softened square wave: buzzy but not harsh.
                    float wave = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * freq * t) * 3f, -1f, 1f);
                    data[start + i] = wave * envelope * 0.35f;
                }
            }
            Tone(0, 220f);
            Tone(toneSamples + gapSamples, 165f);
            var clip = AudioClip.Create("Error", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
