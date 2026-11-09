using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Eldoira.Units
{
    /// <summary>
    /// A single soldier on the battlefield. Belongs to a <see cref="Squad"/>.
    /// Its pivot sits at its feet, on the ground.
    /// </summary>
    public class UnitModel : MonoBehaviour
    {
        [SerializeField] private ModelProfile profile;
        [SerializeField] private bool isLeader;

        private Squad squad;

        public ModelProfile Profile => profile;
        public bool IsLeader => isLeader;
        public Squad Squad => squad != null ? squad : squad = GetComponentInParent<Squad>();

        /// <summary>Physical footprint radius in meters.</summary>
        public float Radius => profile != null ? profile.radius : 0.35f;

        /// <summary>Move characteristic in meters.</summary>
        public float Move => profile != null ? profile.move : 0f;

        public Vector3 Position => transform.position;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Maximum reach of a normal move, measured from the soldier's position.
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Handles.DrawWireDisc(Position + Vector3.up * 0.02f, Vector3.up, Move);
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.08f);
            Handles.DrawSolidDisc(Position + Vector3.up * 0.01f, Vector3.up, Move);
        }
#endif
    }
}
