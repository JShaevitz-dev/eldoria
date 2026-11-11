using System.Collections.Generic;
using Eldoria.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Eldoria.Terrain
{
    /// <summary>
    /// Measures moves over real terrain:
    ///  - Routes use the shortest walkable path (NavMesh). Structure terrain (ground, hills, ditches,
    ///    building walls) shapes the path; objects that can't be mantled block it; mantleable objects don't.
    ///    Routes and standing spots are found on the map for the soldier's size, so a wide soldier (e.g. a tank) only
    ///    goes through gaps it fits through and only stops where it fits (see BattlefieldNavigation).
    ///  - Length is measured along the surface of structure terrain (up and down hills, into ditches),
    ///    and straight through objects.
    ///  - Soldiers may stand on walkable structure, or on top of objects that can be climbed onto;
    ///    climbing onto an object costs a flat <see cref="MovementRules.ClimbCost"/> (10 m) whatever its height.
    ///    Climbing down costs nothing.
    ///  - Wading through water (structure of kind Water) costs <see cref="MovementRules.WadingCostMultiplier"/>× per meter;
    ///    route finding knows this too (the water's NavMesh area costs the same), so routes use a bridge or ford when cheaper.
    /// </summary>
    public class TerrainMeasurer : IMoveMeasurer
    {
        private const float SampleStep = 0.25f;
        private const float NavSnap = 0.35f;
        private const float ClimbSearchRadius = 5f;
        /// <summary>How far from an object's side a soldier can be to climb onto it (body radius + a little).</summary>
        private const float ClimbReach = 0.8f;
        private const float RayHeight = 100f;

        public bool TryGetStandPoint(Vector3 point, float radius, float height, out Vector3 stand, out string reason)
        {
            stand = point;
            reason = null;

            // Look straight down from above to find the top-most surface at this spot.
            var origin = new Vector3(point.x, RayHeight, point.z);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, RayHeight * 2f, TerrainLayers.TerrainMask, QueryTriggerInteraction.Ignore))
            {
                reason = "Nothing to stand on";
                return false;
            }

            TerrainObject standingOn = null;
            if (hit.collider.gameObject.layer == TerrainLayers.Object)
            {
                standingOn = hit.collider.GetComponentInParent<TerrainObject>();
                string name = standingOn != null ? standingOn.DisplayName : hit.collider.name;
                if (standingOn == null || !standingOn.CanClimbOnto)
                {
                    reason = $"Can't stand on the {name}";
                    return false;
                }
                if (hit.normal.y < 0.7f)
                {
                    reason = $"Can't stand on the side of the {name}";
                    return false;
                }
            }
            else
            {
                // Structure: must be walkable ground (not a wall top or a too-steep slope).
                if (!NavMesh.SamplePosition(hit.point, out var navHit, NavSnap, BattlefieldNavigation.FilterFor(radius))
                    || Mathf.Abs(navHit.position.y - hit.point.y) > 0.3f)
                {
                    reason = radius > 1f ? "Not enough room to stand there" : "Can't stand there";
                    return false;
                }
            }

            stand = hit.point;

            // Room for the soldier's body: no object (other than the one stood on) in the way.
            var bottom = stand + Vector3.up * (radius + 0.05f);
            var top = stand + Vector3.up * Mathf.Max(radius + 0.06f, height - radius);
            foreach (var c in Physics.OverlapCapsule(bottom, top, radius * 0.9f, TerrainLayers.TerrainMask, QueryTriggerInteraction.Ignore))
            {
                var obj = c.GetComponentInParent<TerrainObject>();
                if (obj != null && obj == standingOn) continue;
                if (c.gameObject.layer == TerrainLayers.Structure)
                {
                    var st = c.GetComponentInParent<StructureTerrain>();
                    if (st == null || st.Kind != StructureKind.Building) continue;
                }
                reason = obj != null ? $"Blocked by the {obj.DisplayName}" : "Too close to a wall";
                return false;
            }
            return true;
        }

        public MovePath Measure(Vector3 from, Vector3 to, float radius = 0f)
        {
            var filter = BattlefieldNavigation.FilterFor(radius);
            // A soldier on top of an object can climb down on any side: try each side, keep the shortest route.
            var fromEnds = RouteEnds(from, filter);
            var toEnds = RouteEnds(to, filter);
            if (fromEnds.Count == 0) return MovePath.Unreachable("Start is not on walkable ground");
            if (toEnds.Count == 0) return MovePath.Unreachable("No route there");

            MovePath best = null;
            foreach (var a in fromEnds)
            foreach (var b in toEnds)
            {
                var path = MeasureVia(from, a, b, to, filter);
                if (path != null && (best == null || path.Length < best.Length)) best = path;
            }
            return best ?? MovePath.Unreachable("No route there");
        }

        /// <summary>Where a route can start/end for a soldier at a point.</summary>
        private struct RouteEnd
        {
            public Vector3 Nav;     // point on the NavMesh used for route finding
            public bool Climb;      // soldier is on top of an object and climbs down/up here
        }

        private static List<RouteEnd> RouteEnds(Vector3 p, NavMeshQueryFilter filter)
        {
            var ends = new List<RouteEnd>();
            if (NavMesh.SamplePosition(p, out var hit, NavSnap, filter) && Mathf.Abs(hit.position.y - p.y) < 0.3f)
            {
                ends.Add(new RouteEnd { Nav = hit.position, Climb = false });
                return ends;
            }

            // On top of an object: candidate climbing points on walkable ground all around its base.
            Vector3 below = p;
            if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out var ground, 50f, TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore))
                below = ground.point;

            // The object being stood on: soldiers can only climb up/down right next to it.
            Collider standingOn = null;
            if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out var objHit, 1f, TerrainLayers.ObjectMask, QueryTriggerInteraction.Ignore))
                standingOn = objHit.collider;

            for (float r = 0f; r <= ClimbSearchRadius; r += 0.75f)
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var probe = below + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!NavMesh.SamplePosition(probe, out hit, 0.5f, filter)) continue;
                if (Mathf.Abs(hit.position.y - below.y) > 1f) continue;
                if (standingOn != null && !IsBeside(standingOn, hit.position)) continue;
                bool duplicate = false;
                foreach (var e in ends)
                    if ((e.Nav - hit.position).sqrMagnitude < 0.5f * 0.5f) { duplicate = true; break; }
                if (!duplicate) ends.Add(new RouteEnd { Nav = hit.position, Climb = true });
                if (r == 0f) break;
            }
            return ends;
        }

        /// <summary>True if a ground point is right next to (within reach of) an object's base.</summary>
        private static bool IsBeside(Collider obj, Vector3 groundPoint)
        {
            var probe = new Vector3(groundPoint.x, Mathf.Clamp(groundPoint.y + 0.1f, obj.bounds.min.y, obj.bounds.max.y), groundPoint.z);
            Vector3 closest = obj.ClosestPoint(probe);
            closest.y = groundPoint.y;
            return Vector3.Distance(closest, groundPoint) <= ClimbReach;
        }

        private static MovePath MeasureVia(Vector3 from, RouteEnd a, RouteEnd b, Vector3 to, NavMeshQueryFilter filter)
        {
            var navPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.Nav, b.Nav, filter, navPath) || navPath.status != NavMeshPathStatus.PathComplete)
                return null;

            // Route corners laid exactly on the structure surface; ground-level ends are the soldier's own spots.
            var corners = navPath.corners;
            for (int i = 0; i < corners.Length; i++) corners[i] = OnSurface(corners[i]);
            if (!a.Climb) corners[0] = from;
            if (!b.Climb) corners[corners.Length - 1] = to;

            var result = new MovePath { Reachable = true };
            result.Points.Add(from);
            if (a.Climb)
            {
                // Climbing down off an object costs no movement (the soldier still counts as having moved).
                result.Points.Add(corners[0]);
            }

            for (int i = 1; i < corners.Length; i++)
                Drape(corners[i - 1], corners[i], result);

            if (b.Climb)
            {
                // Climbing onto an object costs a flat amount, whatever its height.
                result.Length += MovementRules.ClimbCost;
                result.Climbs++;
                result.Points.Add(to);
            }
            return result;
        }

        /// <summary>
        /// Walks a straight route segment along the structure surface, adding its length and points. Stretches through
        /// water count <see cref="MovementRules.WadingCostMultiplier"/> times.
        /// </summary>
        private static void Drape(Vector3 a, Vector3 b, MovePath path)
        {
            Vector3 flat = b - a;
            flat.y = 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(flat.magnitude / SampleStep));

            Vector3 prev = a;
            bool prevWet = InWater(a);
            for (int i = 1; i <= steps; i++)
            {
                bool wet = false;
                Vector3 p = i == steps ? b : OnSurface(Vector3.Lerp(a, b, (float)i / steps), out wet);
                if (i == steps) wet = InWater(b);
                float len = Vector3.Distance(prev, p);
                float wading = len * ((prevWet ? 0.5f : 0f) + (wet ? 0.5f : 0f));
                path.Length += len + wading * (MovementRules.WadingCostMultiplier - 1f);
                path.WadingMeters += wading;
                path.Points.Add(p);
                prev = p;
                prevWet = wet;
            }
        }

        /// <summary>Drops a point onto the structure surface just below it (objects are ignored).</summary>
        private static Vector3 OnSurface(Vector3 p) => OnSurface(p, out _);

        private static Vector3 OnSurface(Vector3 p, out bool water)
        {
            water = false;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 10f, TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore))
            {
                p.y = hit.point.y;
                water = IsWater(hit.collider);
            }
            return p;
        }

        /// <summary>True if the structure surface at this spot is water (a riverbed below the water surface).</summary>
        public static bool InWater(Vector3 p)
        {
            OnSurface(p, out bool water);
            return water;
        }

        private static bool IsWater(Collider c)
        {
            var st = c.GetComponentInParent<StructureTerrain>();
            return st != null && st.Kind == StructureKind.Water;
        }

        /// <summary>Height of the structure surface under a point (used to lay range rings on the ground).</summary>
        public static float SurfaceHeight(Vector3 p)
        {
            return Physics.Raycast(new Vector3(p.x, RayHeight, p.z), Vector3.down, out var hit, RayHeight * 2f,
                TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
        }
    }
}
