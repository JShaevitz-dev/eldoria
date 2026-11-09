using System.Collections.Generic;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Terrain
{
    /// <summary>
    /// Object terrain: modeled props a soldier can interact with (crates, low walls, barricades, containers...).
    /// Movement is measured straight through objects (they never add distance).
    /// Objects that cannot be mantled over block movement, so routes go around them.
    /// Objects can have keywords (e.g. Half-cover), and those keywords can grant keywords to soldiers nearby.
    /// Lives on the "TerrainObject" layer.
    /// </summary>
    public class TerrainObject : MonoBehaviour
    {
        [SerializeField] private string displayName = "Object";

        [Tooltip("Soldiers can use this object as cover (used by the shooting rules later).")]
        [SerializeField] private bool providesCover = true;

        [Tooltip("Soldiers can move over this object. If false, it blocks movement.")]
        [SerializeField] private bool canMantle = true;

        [Tooltip("Soldiers can end their move standing on top of this object.")]
        [SerializeField] private bool canClimbOnto;

        [Tooltip("Keywords this object has, e.g. Half-cover.")]
        [SerializeField] private List<Keyword> keywords = new List<Keyword>();

        [SerializeField] private KeywordSet keywordState = new KeywordSet();

        private Collider[] colliders;

        public string DisplayName => displayName;
        public bool ProvidesCover => providesCover;
        public bool CanMantle => canMantle;
        public bool CanClimbOnto => canClimbOnto;
        public bool BlocksMovement => !canMantle;

        /// <summary>Keywords the object has right now (its own, minus lost, plus gained).</summary>
        public IReadOnlyList<Keyword> Keywords => keywordState.Current(keywords);
        public bool HasKeyword(string name) => Keyword.Contains(Keywords, name);
        public void GainKeyword(Keyword keyword, int expiresAtTurn = 0) => keywordState.Gain(keyword, keywords, expiresAtTurn);
        public bool LoseKeyword(Keyword keyword) => keywordState.Lose(keyword, keywords);
        public void OnTurnStarted(int turn) => keywordState.ExpireAtTurnStart(turn);

        public void Configure(string name, bool cover, bool mantle, bool climbOnto, IEnumerable<Keyword> objectKeywords = null)
        {
            displayName = name;
            providesCover = cover;
            canMantle = mantle;
            canClimbOnto = climbOnto;
            keywords = objectKeywords != null ? new List<Keyword>(objectKeywords) : new List<Keyword>();
        }

        /// <summary>Horizontal distance (meters, ignoring height) from a point to the nearest part of this object.</summary>
        public float HorizontalDistanceTo(Vector3 point)
        {
            if (colliders == null || colliders.Length == 0) colliders = GetComponentsInChildren<Collider>();
            float best = float.MaxValue;
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled) continue;
                var probe = new Vector3(point.x, Mathf.Clamp(point.y, c.bounds.min.y, c.bounds.max.y), point.z);
                Vector3 closest = c.ClosestPoint(probe);
                best = Mathf.Min(best, Rules.Measure.Horizontal(closest, point));
            }
            return best == float.MaxValue ? Rules.Measure.Horizontal(transform.position, point) : best;
        }

        private void Reset() => gameObject.layer = TerrainLayers.Object;

        private void OnValidate() => Keyword.RemoveDuplicates(keywords);
    }
}
