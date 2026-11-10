using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// Visual only: soldiers who have a keyword marked "sparkles" (e.g. Divine Sanctuary) glitter with sparkles of that
    /// keyword's colour for exactly as long as they have it — the effect stops when the keyword wears off, is lost, or
    /// the soldier dies. Starts itself when play begins; checks every soldier a few times a second.
    /// </summary>
    public class KeywordSparkles : MonoBehaviour
    {
        private const float RefreshInterval = 0.2f;

        private static KeywordSparkles instance;
        private static Material material;

        private readonly Dictionary<Soldier, ParticleSystem> effects = new Dictionary<Soldier, ParticleSystem>();
        private float nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("KeywordSparkles");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KeywordSparkles>();
        }

        /// <summary>The sparkle colour a soldier should show right now, or null for none.</summary>
        public static Color? SparkleColorFor(Soldier soldier)
        {
            if (soldier == null || !soldier.IsAlive) return null;
            var k = soldier.Keywords.FirstOrDefault(kw => kw != null && kw.sparkles);
            return k != null ? k.sparkleColor : (Color?)null;
        }

        /// <summary>True if the soldier's sparkles are currently being emitted.</summary>
        public static bool IsSparkling(Soldier soldier) =>
            instance != null && soldier != null && instance.effects.TryGetValue(soldier, out var ps) && ps != null && ps.isEmitting;

        /// <summary>Updates every soldier's sparkles now (instead of waiting for the next check).</summary>
        public static void RefreshNow()
        {
            if (instance == null) Bootstrap();
            instance.Refresh();
        }

        private void LateUpdate()
        {
            if (Time.time < nextRefresh) return;
            nextRefresh = Time.time + RefreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            foreach (var dead in effects.Keys.Where(s => s == null || effects[s] == null).ToList()) effects.Remove(dead);

            foreach (var soldier in FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var color = SparkleColorFor(soldier);
                effects.TryGetValue(soldier, out var ps);
                if (color.HasValue)
                {
                    if (ps == null) effects[soldier] = ps = Create(soldier);
                    var main = ps.main;
                    main.startColor = color.Value;
                    if (!ps.isEmitting) ps.Play(true);
                }
                else if (ps != null && ps.isEmitting)
                {
                    // Stop making new sparkles; the ones in the air fade out.
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private static ParticleSystem Create(Soldier soldier)
        {
            var go = new GameObject("Sparkles");
            go.transform.SetParent(soldier.transform, false);
            go.transform.localPosition = new Vector3(0f, soldier.Height * 0.5f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.04f;            // drift slowly upward
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            var emission = ps.emission;
            emission.rateOverTime = 32f;

            // Sparkles appear all over the soldier's body.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(soldier.Radius * 2.4f, soldier.Height * 0.95f, soldier.Radius * 2.4f);

            // Twinkle: fade and grow in, then shrink and fade out.
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>A soft four-pointed star, generated once.</summary>
        private static Material Material()
        {
            if (material != null) return material;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                float rays = Mathf.Clamp01(1f - dx * 6f) * Mathf.Clamp01(1f - dy) + Mathf.Clamp01(1f - dy * 6f) * Mathf.Clamp01(1f - dx);
                float a = Mathf.Clamp01(core + rays * 0.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            material = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, name = "Sparkles" };
            return material;
        }
    }
}
