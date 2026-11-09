using System;
using System.Collections.Generic;
using Eldoira.Core;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Rules
{
    public enum MoveKind
    {
        /// <summary>Destination is not reachable this activation.</summary>
        None,
        /// <summary>Unit leader stayed where they were.</summary>
        Hold,
        /// <summary>Within the squad leader's normal move (10 m).</summary>
        Normal,
        /// <summary>Beyond the normal move but within the sprint move (20 m).</summary>
        Sprint
    }

    public readonly struct MoveCheck
    {
        public readonly bool Ok;
        public readonly MoveKind Kind;
        /// <summary>Measured length of the route in meters (0 if no route was found).</summary>
        public readonly float Distance;
        public readonly string Reason;
        /// <summary>Where the soldier would actually stand (snapped onto the ground / object top).</summary>
        public readonly Vector3 Destination;
        /// <summary>The measured route, if one was found.</summary>
        public readonly MovePath Path;

        public MoveCheck(bool ok, MoveKind kind, float distance, string reason, Vector3 destination, MovePath path)
        {
            Ok = ok;
            Kind = kind;
            Distance = distance;
            Reason = reason;
            Destination = destination;
            Path = path;
        }

        public static MoveCheck Fail(float distance, string reason, Vector3 destination = default, MovePath path = null)
            => new MoveCheck(false, MoveKind.None, distance, reason, destination, path);
    }

    /// <summary>
    /// Unit movement rules (all distances in meters):
    ///  1. The squad leader moves first: up to their movement range (10 m), or sprint up to double that (20 m).
    ///     The distance is the route length, measured along structure terrain and through objects
    ///     (see <see cref="IMoveMeasurer"/>).
    ///  2. Then every other soldier may move any distance (as long as a route exists), but must end
    ///     within <see cref="CohesionRange"/> of the squad leader (the leader at the start of the move).
    /// Soldiers may walk through each other but not end overlapping another soldier, and may not end
    /// outside the battlefield.
    /// </summary>
    public static class MovementRules
    {
        public const float CohesionRange = 5f;

        /// <summary>Movement (meters) it costs to climb onto an object of any height.</summary>
        public const float ClimbCost = 10f;

        /// <summary>Sprinting covers this many times a soldier's movement range.</summary>
        public const float SprintMultiplier = 2f;

        // Small tolerance so a click exactly on a ring edge counts as inside.
        private const float Epsilon = 0.01f;

        public static MoveCheck CheckLeaderMove(Soldier leader, Vector3 from, Vector3 to,
            IReadOnlyDictionary<Soldier, Vector3> positions, IMoveMeasurer measurer)
        {
            if (!measurer.TryGetStandPoint(to, leader.Radius, leader.Height, out var stand, out var why))
                return MoveCheck.Fail(0f, why, to);
            if (!IsInsideBattlefield(stand, leader.Radius)) return MoveCheck.Fail(0f, "Outside the battlefield", stand);

            var path = measurer.Measure(from, stand);
            if (!path.Reachable) return MoveCheck.Fail(0f, path.Reason, stand);
            float d = path.Length;

            MoveKind kind;
            if (d <= leader.Move + Epsilon) kind = MoveKind.Normal;
            else if (d <= leader.Sprint + Epsilon) kind = MoveKind.Sprint;
            else return MoveCheck.Fail(d, $"Out of range (max {leader.Sprint:0.#} m sprint)", stand, path);

            if (!IsSpaceFree(leader, stand, positions)) return MoveCheck.Fail(d, "Space is occupied", stand, path);

            return new MoveCheck(true, kind, d, null, stand, path);
        }

        public static MoveCheck CheckFollowerMove(Soldier follower, Vector3 from, Vector3 to, Vector3 leaderPosition,
            IReadOnlyDictionary<Soldier, Vector3> positions, IMoveMeasurer measurer)
        {
            if (!measurer.TryGetStandPoint(to, follower.Radius, follower.Height, out var stand, out var why))
                return MoveCheck.Fail(0f, why, to);
            if (!IsWithinCohesion(leaderPosition, stand))
                return MoveCheck.Fail(0f, $"Must end in cohesion (within {CohesionRange:0.#} m of the unit leader)", stand);
            if (!IsInsideBattlefield(stand, follower.Radius)) return MoveCheck.Fail(0f, "Outside the battlefield", stand);
            if (!IsSpaceFree(follower, stand, positions)) return MoveCheck.Fail(0f, "Space is occupied", stand);

            var path = measurer.Measure(from, stand);
            if (!path.Reachable) return MoveCheck.Fail(0f, path.Reason, stand);

            return new MoveCheck(true, MoveKind.Normal, path.Length, null, stand, path);
        }

        public static bool IsWithinCohesion(Vector3 leaderPosition, Vector3 point)
        {
            return Measure.Horizontal(leaderPosition, point) <= CohesionRange + Epsilon;
        }

        public static bool IsInsideBattlefield(Vector3 point, float radius)
        {
            float halfW = BattlefieldScale.BoardWidth / 2f - radius;
            float halfD = BattlefieldScale.BoardDepth / 2f - radius;
            return point.x >= -halfW && point.x <= halfW && point.z >= -halfD && point.z <= halfD;
        }

        /// <summary>True if <paramref name="mover"/> standing at <paramref name="point"/> would not overlap anyone else.</summary>
        public static bool IsSpaceFree(Soldier mover, Vector3 point, IReadOnlyDictionary<Soldier, Vector3> positions)
        {
            foreach (var kv in positions)
            {
                if (kv.Key == mover || kv.Key == null) continue;
                bool sameLevel = Mathf.Abs(kv.Value.y - point.y) < Mathf.Min(mover.Height, kv.Key.Height);
                if (sameLevel && Measure.Horizontal(kv.Value, point) < mover.Radius + kv.Key.Radius - Epsilon) return false;
            }
            return true;
        }

        /// <summary>
        /// Finds the nearest legal spot for a follower: as close to where it already stands as possible,
        /// inside cohesion range. <paramref name="check"/> validates (and snaps) each candidate.
        /// Returns null if no spot is found.
        /// </summary>
        public static MoveCheck? FindCohesionSpot(Soldier follower, Vector3 from, Vector3 leaderPosition,
            Func<Vector3, MoveCheck> check)
        {
            Vector3 flatLeader = new Vector3(leaderPosition.x, leaderPosition.y, leaderPosition.z);
            Vector3 dir = from - flatLeader;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.back;
            dir.Normalize();

            // Ideal: stay put if already inside, otherwise step to just inside the radius on our side.
            float margin = follower.Radius + 0.1f;
            float idealDist = Mathf.Min(Measure.Horizontal(from, flatLeader), CohesionRange - margin);
            float baseAngle = Mathf.Atan2(dir.z, dir.x);

            // Search outward from the ideal spot: decreasing distance from the leader, widening angle.
            for (float dist = idealDist; dist >= follower.Radius * 2f; dist -= 0.5f)
            {
                for (int step = 0; step <= 18; step++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        if (step == 0 && sign == 1) continue;
                        float a = baseAngle + sign * step * (Mathf.PI / 18f);
                        Vector3 p = flatLeader + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dist;
                        var result = check(p);
                        if (result.Ok) return result;
                    }
                }
            }
            return null;
        }
    }
}
