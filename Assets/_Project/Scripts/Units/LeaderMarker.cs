using UnityEngine;

namespace Eldoira.Units
{
    /// <summary>Floating, spinning gold diamond above whichever soldier currently leads the squad.</summary>
    [RequireComponent(typeof(Unit))]
    public class LeaderMarker : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private float heightAboveHead = 0.45f;
        [SerializeField] private float size = 0.22f;

        private Unit squad;
        private Transform marker;

        private void Awake()
        {
            squad = GetComponent<Unit>();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LeaderMarker";
            Destroy(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size, size * 1.6f, size);
            marker = go.transform;
        }

        private void LateUpdate()
        {
            var leader = squad.Leader;
            bool show = leader != null && leader.IsAlive;
            marker.gameObject.SetActive(show);
            if (!show) return;

            float bob = Mathf.Sin(Time.time * 2.5f) * 0.05f;
            marker.position = leader.Position + Vector3.up * (leader.Height + heightAboveHead + bob);
            marker.rotation = Quaternion.Euler(0f, Time.time * 90f, 45f);
        }
    }
}
