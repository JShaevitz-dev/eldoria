using System.Collections.Generic;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Terrain
{
    /// <summary>
    /// Geometry for shooting:
    ///  - Line of sight: a soldier can see another if nothing on the Structure layer (walls, hill crests,
    ///    platforms) blocks the line from their eyes to the target's chest or head. Objects don't block sight;
    ///    they give cover through their keywords instead.
    ///  - Objects in the line of fire: terrain objects the line between two soldiers passes through,
    ///    seen from above (so a low barricade counts even though a soldier can see over it).
    /// </summary>
    public static class ShotGeometry
    {
        private const float EyeHeight = 0.9f;   // fraction of soldier height
        private const float ChestHeight = 0.6f;

        public static bool CanSee(Soldier attacker, Soldier target)
        {
            if (attacker == null || target == null) return true;
            Vector3 eye = attacker.Position + Vector3.up * attacker.Height * EyeHeight;
            Vector3 chest = target.Position + Vector3.up * target.Height * ChestHeight;
            Vector3 head = target.Position + Vector3.up * target.Height * EyeHeight;
            return !Blocked(eye, chest) || !Blocked(eye, head);
        }

        private static bool Blocked(Vector3 from, Vector3 to)
        {
            return Physics.Linecast(from, to, TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Terrain objects the line between two points crosses (looking down from above).</summary>
        public static List<TerrainObject> ObjectsBetween(Vector3 from, Vector3 to)
        {
            var result = new List<TerrainObject>();
            foreach (var obj in Object.FindObjectsByType<TerrainObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (Crosses(from, to, obj)) result.Add(obj);
            return result;
        }

        /// <summary>True if <paramref name="source"/> (a terrain object) lies between the two points.</summary>
        public static bool IsBetween(Vector3 from, Vector3 to, Component source)
        {
            var obj = source as TerrainObject ?? (source != null ? source.GetComponentInParent<TerrainObject>() : null);
            return obj != null && Crosses(from, to, obj);
        }

        /// <summary>Does the line from a to b, seen from above, pass through the object's footprint?</summary>
        public static bool Crosses(Vector3 a, Vector3 b, TerrainObject obj)
        {
            foreach (var c in obj.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled) continue;
                // Cast at the object's own mid-height so its height doesn't matter.
                float y = c.bounds.center.y;
                Vector3 start = new Vector3(a.x, y, a.z), end = new Vector3(b.x, y, b.z);
                Vector3 dir = end - start;
                float length = dir.magnitude;
                if (length < 0.001f) continue;
                if (c.Raycast(new Ray(start, dir / length), out _, length)) return true;
                // A point inside the object (e.g. standing right against it) also counts.
                if (c.bounds.Contains(start)) return true;
            }
            return false;
        }
    }
}
