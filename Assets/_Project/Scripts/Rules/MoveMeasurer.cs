using System.Collections.Generic;
using UnityEngine;

namespace Eldoria.Rules
{
    /// <summary>A measured route between two points.</summary>
    public class MovePath
    {
        public bool Reachable;
        /// <summary>Length in meters, measured along structure terrain and straight through objects.</summary>
        public float Length;
        /// <summary>Points along the route, following the ground (for drawing and walking).</summary>
        public List<Vector3> Points = new List<Vector3>();
        /// <summary>How many times the route climbs onto an object (each costs <see cref="MovementRules.ClimbCost"/>).</summary>
        public int Climbs;
        public string Reason;

        public static MovePath Unreachable(string reason) => new MovePath { Reachable = false, Reason = reason };
    }

    /// <summary>How the rules find where a soldier can stand and how far a move is.</summary>
    public interface IMoveMeasurer
    {
        /// <summary>
        /// Converts a point (e.g. under the cursor) into the spot a soldier would stand on there.
        /// Returns false with a reason if no soldier can stand there.
        /// </summary>
        bool TryGetStandPoint(Vector3 point, float radius, float height, out Vector3 stand, out string reason);

        /// <summary>Finds and measures the route from one standing spot to another.</summary>
        MovePath Measure(Vector3 from, Vector3 to);
    }

    /// <summary>Flat, empty battlefield: anywhere is standable and every route is a straight line.</summary>
    public class StraightLineMeasurer : IMoveMeasurer
    {
        public static readonly StraightLineMeasurer Instance = new StraightLineMeasurer();

        public bool TryGetStandPoint(Vector3 point, float radius, float height, out Vector3 stand, out string reason)
        {
            stand = point;
            reason = null;
            return true;
        }

        public MovePath Measure(Vector3 from, Vector3 to)
        {
            return new MovePath
            {
                Reachable = true,
                Length = Vector3.Distance(from, to),
                Points = new List<Vector3> { from, to }
            };
        }
    }
}
