using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The area of the map where one side sets up its army before the battle (a rectangle on the ground,
    /// centred on this object, measured in meters).
    /// </summary>
    public class DeploymentZone : MonoBehaviour
    {
        [SerializeField] private Side side = Side.Player;
        [SerializeField] private Vector2 size = new Vector2(80f, 8f);

        public Side Side => side;
        public Vector2 Size => size;

        public void Configure(Side newSide, Vector2 newSize)
        {
            side = newSide;
            size = newSize;
        }

        /// <summary>True if a point lies inside the zone (horizontally).</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 c = transform.position;
            return Mathf.Abs(point.x - c.x) <= size.x / 2f && Mathf.Abs(point.z - c.z) <= size.y / 2f;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = side == Side.Player ? new Color(0.3f, 0.6f, 1f, 0.8f) : new Color(1f, 0.3f, 0.25f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.05f, new Vector3(size.x, 0.1f, size.y));
        }
    }
}
