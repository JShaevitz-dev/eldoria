using UnityEngine;
using UnityEngine.Rendering;

namespace Eldoira.Gameplay
{
    /// <summary>A flat circle or line drawn on the ground (range rings, selection, path preview).</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RangeRing : MonoBehaviour
    {
        private const int Segments = 96;
        private const float GroundOffset = 0.03f;

        private static Material sharedMaterial;

        /// <summary>Ground height under a point, so rings follow hills and ditches. Flat by default.</summary>
        public static System.Func<Vector3, float> SurfaceHeight = _ => 0f;
        private LineRenderer line;

        public static RangeRing Create(string name, Transform parent, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // Lines are laid flat on the ground: TransformZ alignment with Z pointing down.
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var ring = go.AddComponent<RangeRing>();
            var lr = go.GetComponent<LineRenderer>();
            ring.line = lr;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            if (sharedMaterial == null) sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.sharedMaterial = sharedMaterial;
            go.SetActive(false);
            return ring;
        }

        public void ShowCircle(Vector3 center, float radius, Color color)
        {
            gameObject.SetActive(true);
            line.loop = true;
            line.positionCount = Segments;
            line.startColor = line.endColor = color;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var p = new Vector3(center.x + Mathf.Cos(a) * radius, 0f, center.z + Mathf.Sin(a) * radius);
                p.y = SurfaceHeight(p) + GroundOffset;
                line.SetPosition(i, p);
            }
        }

        public void ShowLine(Vector3 from, Vector3 to, Color color)
        {
            gameObject.SetActive(true);
            line.loop = false;
            line.positionCount = 2;
            line.startColor = line.endColor = color;
            line.SetPosition(0, from + Vector3.up * GroundOffset);
            line.SetPosition(1, to + Vector3.up * GroundOffset);
        }

        /// <summary>Draws a route through the given points (already on the ground).</summary>
        public void ShowPath(System.Collections.Generic.IReadOnlyList<Vector3> points, Color color)
        {
            gameObject.SetActive(true);
            line.loop = false;
            line.positionCount = points.Count;
            line.startColor = line.endColor = color;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i] + Vector3.up * (GroundOffset + 0.02f));
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
