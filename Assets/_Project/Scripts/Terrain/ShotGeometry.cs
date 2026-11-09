using System.Collections.Generic;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Terrain
{
    /// <summary>
    /// Geometry for shooting and explosions:
    ///  - Line of sight: a soldier can see another if nothing on the Structure layer (walls, hill crests,
    ///    platforms) blocks the line from their eyes to the target's chest or head. Objects don't block sight;
    ///    they give cover through their keywords instead.
    ///  - Objects in the line of fire: terrain objects the line between two soldiers passes through,
    ///    seen from above (so a low barricade counts even though a soldier can see over it).
    ///  - Explosions: whether a blast reaches a soldier (terrain in between stops it).
    /// </summary>
    public static class ShotGeometry
    {
        private const float EyeHeight = 0.9f;   // fraction of soldier height
        private const float ChestHeight = 0.6f;

        /// <summary>Height above the ground (meters) at which an explosion's path is traced against structure terrain.</summary>
        public const float BlastHeight = 0.3f;

        /// <summary>
        /// Can an explosion at <paramref name="point"/> reach the soldier? Under normal circumstances it can't go through terrain:
        ///  - Structure terrain (building walls, hill crests, platforms): blocked if a straight line just above the ground
        ///    from the blast to the soldier hits it.
        ///  - Object terrain (crates, low walls, cars, window sills...): blocked if the line between them, seen from above,
        ///    crosses the object - whatever its height. The object the blast sits on, and one the soldier stands on, don't count.
        /// </summary>
        public static bool BlastReaches(Vector3 point, Soldier soldier)
        {
            if (soldier == null) return false;
            Vector3 target = soldier.Position;
            Vector3 a = point + Vector3.up * BlastHeight, b = target + Vector3.up * BlastHeight;
            if ((b - a).sqrMagnitude > 1e-6f && Physics.Linecast(a, b, TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore))
                return false;

            var restingOn = ObjectUnder(point);
            var standingOn = ObjectUnder(target);
            foreach (var obj in Object.FindObjectsByType<TerrainObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (obj == restingOn || obj == standingOn) continue;
                if (Crosses(point, target, obj)) return false;
            }
            return true;
        }

        /// <summary>The terrain object directly beneath a point (e.g. the crate a soldier stands on), if any.</summary>
        private static TerrainObject ObjectUnder(Vector3 p)
        {
            return Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out var hit, 0.6f, TerrainLayers.ObjectMask, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<TerrainObject>()
                : null;
        }

        /// <summary>Could a soldier of this height standing at <paramref name="standingAt"/> see the target? (Same test as <see cref="CanSee"/>.)</summary>
        public static bool CanSeeFrom(Vector3 standingAt, float height, Soldier target)
        {
            if (target == null) return false;
            Vector3 eye = standingAt + Vector3.up * height * EyeHeight;
            Vector3 chest = target.Position + Vector3.up * target.Height * ChestHeight;
            Vector3 head = target.Position + Vector3.up * target.Height * EyeHeight;
            return !Blocked(eye, chest) || !Blocked(eye, head);
        }

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
