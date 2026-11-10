using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// Visual only: a red marker floats over the head of every soldier who is in close range (a Biological enemy less
    /// than 2 m away - see <see cref="CloseRange"/>), and disappears as soon as they aren't. It sits above the gold
    /// leader marker so the two never overlap. Starts itself when play begins; checks a few times a second.
    /// </summary>
    public class CloseRangeMarkers : MonoBehaviour
    {
        private const float RefreshInterval = 0.15f;
        private const float HeightAboveHead = 1.0f;
        private const float Size = 0.26f;
        private static readonly Color Red = new Color(0.95f, 0.12f, 0.1f, 1f);

        private static CloseRangeMarkers instance;
        private static Material material;

        private readonly Dictionary<Soldier, Transform> markers = new Dictionary<Soldier, Transform>();
        private float nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("CloseRangeMarkers");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CloseRangeMarkers>();
        }

        /// <summary>True if the soldier's red close-range marker is showing.</summary>
        public static bool IsShowing(Soldier soldier) =>
            instance != null && soldier != null && instance.markers.TryGetValue(soldier, out var m) && m != null && m.gameObject.activeSelf;

        /// <summary>Updates every marker now (instead of waiting for the next check).</summary>
        public static void RefreshNow()
        {
            if (instance == null) Bootstrap();
            instance.Refresh();
        }

        private void LateUpdate()
        {
            if (Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + RefreshInterval;
                Refresh();
            }
            // Bob and spin the visible markers.
            float bob = Mathf.Sin(Time.time * 4f) * 0.05f;
            foreach (var kv in markers)
            {
                if (kv.Key == null || kv.Value == null || !kv.Value.gameObject.activeSelf) continue;
                kv.Value.position = kv.Key.Position + Vector3.up * (kv.Key.Height + HeightAboveHead + bob);
                kv.Value.rotation = Quaternion.Euler(45f, Time.time * 120f, 45f);
            }
        }

        private void Refresh()
        {
            foreach (var gone in markers.Keys.Where(s => s == null || markers[s] == null).ToList()) markers.Remove(gone);

            var soldiers = FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var s in soldiers)
            {
                bool inCloseRange = CloseRange.EnemiesInCloseRange(s, soldiers).Count > 0;
                markers.TryGetValue(s, out var marker);
                if (inCloseRange && marker == null) markers[s] = marker = Create(s);
                if (marker != null)
                {
                    marker.gameObject.SetActive(inCloseRange);
                    if (inCloseRange) marker.position = s.Position + Vector3.up * (s.Height + HeightAboveHead);
                }
            }
        }

        private static Transform Create(Soldier soldier)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CloseRangeMarker";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(soldier.transform, true);
            go.transform.localScale = Vector3.one * Size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Material();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Flat bright red, unaffected by lighting so it reads at any angle.</summary>
        private static Material Material()
        {
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "CloseRangeMarker" };
            material.SetColor("_BaseColor", Red);
            material.color = Red;
            return material;
        }
    }
}
