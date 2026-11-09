using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Rules
{
    /// <summary>
    /// Distance measuring for rules. All results are in meters, measured horizontally
    /// between soldiers' positions (centre to centre).
    /// </summary>
    public static class Measure
    {
        public static float Horizontal(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public static float Between(Soldier a, Soldier b) => Horizontal(a.Position, b.Position);

        public static float ToPoint(Soldier a, Vector3 point) => Horizontal(a.Position, point);
    }
}
