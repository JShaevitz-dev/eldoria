using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Gameplay
{
    public enum MovePhase
    {
        /// <summary>Waiting for the squad leader to move (or hold).</summary>
        MoveLeader,
        /// <summary>Unit leader has moved; soldiers may be repositioned around them.</summary>
        MoveFollowers,
        /// <summary>The squad's move is finished.</summary>
        Complete
    }

    /// <summary>
    /// One squad's movement for a turn. Tracks where every soldier is planned to end up,
    /// enforces <see cref="MovementRules"/> and the order (squad leader first, then the rest).
    /// The leader is whoever holds the role when the activation is created.
    /// Contains no visuals or input, so it can be driven by the player, the AI or tests.
    /// Positions here are the committed destinations; visuals may still be walking there.
    /// </summary>
    public class UnitMoveActivation
    {
        public Unit Unit { get; }
        public Soldier Leader { get; }
        public MovePhase Phase { get; private set; } = MovePhase.MoveLeader;
        public MoveKind LeaderMove { get; private set; } = MoveKind.None;
        public Vector3 LeaderStart { get; }
        public Vector3 LeaderPosition => positions[Leader];

        private readonly Dictionary<Soldier, Vector3> startPositions = new Dictionary<Soldier, Vector3>();
        private readonly Dictionary<Soldier, Vector3> positions = new Dictionary<Soldier, Vector3>();
        private readonly Dictionary<Soldier, MovePath> paths = new Dictionary<Soldier, MovePath>();
        private readonly List<Soldier> givenMovedKeyword = new List<Soldier>();

        /// <summary>Measures routes over the battlefield (terrain-aware in the game, straight lines in tests).</summary>
        public IMoveMeasurer Measurer { get; }

        /// <summary>Planned end positions of every soldier on the battlefield (this squad and others).</summary>
        public IReadOnlyDictionary<Soldier, Vector3> Positions => positions;

        /// <param name="allSoldiers">Every soldier on the battlefield, so moves can't end on top of anyone.</param>
        /// <param name="measurer">Route measuring; defaults to straight lines over flat ground.</param>
        public UnitMoveActivation(Unit squad, IEnumerable<Soldier> allSoldiers, IMoveMeasurer measurer = null)
        {
            Unit = squad;
            Measurer = measurer ?? StraightLineMeasurer.Instance;
            Leader = squad.Leader;
            foreach (var m in allSoldiers.Concat(squad.LivingSoldiers).Distinct())
            {
                if (m == null || !m.IsAlive) continue;
                positions[m] = m.Position;
                startPositions[m] = m.Position;
            }
            LeaderStart = Leader.Position;
        }

        public Vector3 StartOf(Soldier m) => startPositions[m];
        public Vector3 PositionOf(Soldier m) => positions[m];

        /// <summary>The route of the soldier's most recent move this activation (null if they haven't moved).</summary>
        public MovePath PathOf(Soldier m) => paths.TryGetValue(m, out var p) ? p : null;

        /// <summary>Where a soldier will be once planned moves finish (their current position if not in this activation).</summary>
        public Vector3 PlannedPosition(Soldier m) => m != null && positions.TryGetValue(m, out var p) ? p : m.Position;

        /// <summary>
        /// True if the soldier was given a move this activation, even one costing 0 m (e.g. climbing down off an object).
        /// A leader who holds position, and soldiers left where they stood, have not moved.
        /// </summary>
        public bool HasMoved(Soldier m) => paths.ContainsKey(m);

        /// <summary>A soldier who moves gains the "Moved" keyword until the start of the next turn.</summary>
        private void MarkMoved(Soldier m)
        {
            // Keywords from nearby terrain and auras (e.g. In half-cover, Led from the front) follow the new positions.
            Terrain.NearbyKeywords.RefreshAll(PlannedPosition);
            if (m.HasKeyword(KeywordLibrary.Moved)) return;
            m.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Turns.NextTurnOf(m));
            givenMovedKeyword.Add(m);
        }

        // ---- Unit leader ----

        public MoveCheck CheckLeaderMove(Vector3 destination)
        {
            if (Phase != MovePhase.MoveLeader) return MoveCheck.Fail(0f, "The squad leader has already moved");
            return MovementRules.CheckLeaderMove(Leader, LeaderStart, destination, positions, Measurer);
        }

        public bool TryMoveLeader(Vector3 destination, out MoveCheck check)
        {
            check = CheckLeaderMove(destination);
            if (!check.Ok) return false;
            positions[Leader] = check.Destination;
            paths[Leader] = check.Path;
            MarkMoved(Leader);
            LeaderMove = check.Kind;
            Unit.SetSprintedThisTurn(check.Kind == MoveKind.Sprint);
            Phase = MovePhase.MoveFollowers;
            return true;
        }

        /// <summary>The squad leader stays put; the squad may still reposition around them.</summary>
        public bool HoldLeader()
        {
            if (Phase != MovePhase.MoveLeader) return false;
            LeaderMove = MoveKind.Hold;
            Phase = MovePhase.MoveFollowers;
            return true;
        }

        // ---- Rest of the squad ----

        public bool IsFollower(Soldier m) => m != null && m.IsAlive && m != Leader && m.Unit == Unit && positions.ContainsKey(m);

        public MoveCheck CheckFollowerMove(Soldier follower, Vector3 destination)
        {
            if (Phase != MovePhase.MoveFollowers) return MoveCheck.Fail(0f, "Move the squad leader first");
            if (!IsFollower(follower)) return MoveCheck.Fail(0f, "Not a soldier in this squad");
            return MovementRules.CheckFollowerMove(follower, positions[follower], destination, LeaderPosition, positions, Measurer);
        }

        public bool TryMoveFollower(Soldier follower, Vector3 destination, out MoveCheck check)
        {
            check = CheckFollowerMove(follower, destination);
            if (!check.Ok) return false;
            positions[follower] = check.Destination;
            paths[follower] = check.Path;
            MarkMoved(follower);
            return true;
        }

        /// <summary>Soldiers whose planned position is outside cohesion range.</summary>
        public List<Soldier> OutOfCohesion()
        {
            if (Phase == MovePhase.MoveLeader) return new List<Soldier>();
            return Unit.LivingSoldiers.Where(m => IsFollower(m)
                                           && !MovementRules.IsWithinCohesion(LeaderPosition, positions[m])).ToList();
        }

        /// <summary>Moves every soldier out of cohesion to the nearest legal spot. Returns the soldiers that were moved.</summary>
        public List<Soldier> GatherIntoCohesion()
        {
            var moved = new List<Soldier>();
            foreach (var m in OutOfCohesion())
            {
                var soldier = m;
                var spot = MovementRules.FindCohesionSpot(soldier, positions[soldier], LeaderPosition,
                    p => MovementRules.CheckFollowerMove(soldier, positions[soldier], p, LeaderPosition, positions, Measurer));
                if (spot.HasValue)
                {
                    positions[soldier] = spot.Value.Destination;
                    paths[soldier] = spot.Value.Path;
                    MarkMoved(soldier);
                    moved.Add(soldier);
                }
            }
            return moved;
        }

        // ---- Finish / undo ----

        public bool CanComplete => Phase == MovePhase.MoveFollowers && OutOfCohesion().Count == 0;

        /// <summary>Ends the activation now (e.g. the unit has shot, which ends its turn). Soldiers stay where they are.</summary>
        public void EndActivation()
        {
            if (Phase == MovePhase.MoveLeader && LeaderMove == MoveKind.None) LeaderMove = MoveKind.Hold;
            Phase = MovePhase.Complete;
        }

        public bool Complete()
        {
            if (!CanComplete) return false;
            Phase = MovePhase.Complete;
            return true;
        }

        /// <summary>Puts every soldier in the squad back where they started and restarts the activation.</summary>
        public void Reset()
        {
            foreach (var m in Unit.LivingSoldiers)
            {
                if (!startPositions.ContainsKey(m)) continue;
                positions[m] = startPositions[m];
            }
            Terrain.NearbyKeywords.RefreshAll(PlannedPosition);
            paths.Clear();
            foreach (var m in givenMovedKeyword) m.LoseKeyword(KeywordLibrary.Get(KeywordLibrary.Moved));
            givenMovedKeyword.Clear();
            Unit.SetSprintedThisTurn(false);
            LeaderMove = MoveKind.None;
            Phase = MovePhase.MoveLeader;
        }
    }
}
