using UnityEngine;

namespace Eldoira.Units
{
    /// <summary>
    /// Stat line for a kind of soldier (e.g. "Trooper", "Sergeant").
    /// Only movement-related stats for now; combat stats come later. All distances in meters.
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoira/Model Profile", fileName = "NewModelProfile")]
    public class ModelProfile : ScriptableObject
    {
        public string displayName = "Soldier";

        [Tooltip("Maximum normal move, in meters.")]
        [Min(0f)] public float move = 10f;

        [Tooltip("Radius of the space the soldier physically occupies, in meters (used for spacing/collision).")]
        [Min(0.05f)] public float radius = 0.35f;

        [Tooltip("Standing height, in meters.")]
        [Min(0.1f)] public float height = 1.8f;
    }
}
