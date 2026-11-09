using System.Collections.Generic;
using System.Linq;
using Eldoira.Gameplay;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoira.Tests
{
    public class MovementRulesTests
    {
        private readonly List<Object> created = new List<Object>();
        private Unit squad;
        private Soldier sergeant;
        private List<Soldier> troopers;

        [SetUp]
        public void SetUp()
        {
            var profile = ScriptableObject.CreateInstance<SoldierTemplate>(); // move 10, sprint 20, radius 0.35
            created.Add(profile);

            var squadGo = new GameObject("Unit");
            created.Add(squadGo);
            squad = squadGo.AddComponent<Unit>();

            // Sergeant at the origin, nine troopers in a line 2 m behind.
            sergeant = Spawn(squadGo.transform, profile, true, Vector3.zero);
            troopers = Enumerable.Range(0, 9)
                .Select(i => Spawn(squadGo.transform, profile, false, new Vector3((i - 4) * 1.5f, 0f, -2f)))
                .ToList();
            squad.RefreshSoldiers();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        private Soldier Spawn(Transform parent, SoldierTemplate profile, bool leader, Vector3 pos)
        {
            var go = new GameObject(leader ? "Sergeant" : "Trooper");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var m = go.AddComponent<Soldier>();
            m.Configure(profile, leader);
            return m;
        }

        private UnitMoveActivation NewActivation() => new UnitMoveActivation(squad, squad.Soldiers);

        [Test]
        public void SergeantMoveUpTo10mIsNormal()
        {
            var a = NewActivation();
            Assert.That(a.CheckLeaderMove(new Vector3(0, 0, 10f)).Kind, Is.EqualTo(MoveKind.Normal));
            Assert.That(a.CheckLeaderMove(new Vector3(6, 0, 6)).Kind, Is.EqualTo(MoveKind.Normal));
        }

        [Test]
        public void SergeantMoveBetween10And20mIsSprint()
        {
            var a = NewActivation();
            Assert.That(a.CheckLeaderMove(new Vector3(0, 0, 10.5f)).Kind, Is.EqualTo(MoveKind.Sprint));
            Assert.That(a.CheckLeaderMove(new Vector3(0, 0, 20f)).Kind, Is.EqualTo(MoveKind.Sprint));
        }

        [Test]
        public void SergeantCannotMoveBeyond20m()
        {
            var a = NewActivation();
            Assert.That(a.TryMoveLeader(new Vector3(0, 0, 20.5f), out _), Is.False);
            Assert.That(a.Phase, Is.EqualTo(MovePhase.MoveLeader));
        }

        [Test]
        public void SergeantCanOnlyMoveOnce()
        {
            var a = NewActivation();
            Assert.That(a.TryMoveLeader(new Vector3(0, 0, 5f), out _), Is.True);
            Assert.That(a.TryMoveLeader(new Vector3(0, 0, 6f), out _), Is.False);
        }

        [Test]
        public void SoldiersCannotMoveBeforeSergeant()
        {
            var a = NewActivation();
            Assert.That(a.TryMoveFollower(troopers[0], new Vector3(1, 0, 1), out _), Is.False);
        }

        [Test]
        public void SoldiersMoveAnyDistanceIfTheyEndWithin5mOfSergeant()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 20f), out _); // sprint
            // Trooper travels ~22 m but ends 4 m from the sergeant: legal.
            Assert.That(a.TryMoveFollower(troopers[0], new Vector3(0, 0, 16f), out var check), Is.True, check.Reason);
        }

        [Test]
        public void SoldiersMustEndWithin5mOfSergeant()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 10f), out _);
            Assert.That(a.TryMoveFollower(troopers[0], new Vector3(0, 0, 4.9f), out _), Is.False);
            Assert.That(a.TryMoveFollower(troopers[0], new Vector3(0, 0, 5f), out _), Is.True);
        }

        [Test]
        public void SoldiersGivenAMoveCountAsMovedEvenForZeroMeters()
        {
            var a = NewActivation();
            Assert.That(a.HasMoved(sergeant), Is.False);
            a.TryMoveLeader(new Vector3(0, 0, 2f), out _); // trooper 4 at (0, -2) stays within 5 m
            Assert.That(a.HasMoved(sergeant), Is.True);

            // A trooper ordered to stay exactly where they are (a 0 m move) still counts as having moved.
            var here = a.PositionOf(troopers[4]);
            Assert.That(a.TryMoveFollower(troopers[4], here, out var check), Is.True, check.Reason);
            Assert.That(check.Distance, Is.EqualTo(0f));
            Assert.That(a.HasMoved(troopers[4]), Is.True);
            Assert.That(a.HasMoved(troopers[0]), Is.False, "not given a move");

            a.Reset();
            Assert.That(a.HasMoved(sergeant), Is.False);
        }

        [Test]
        public void HoldingIsNotMoving()
        {
            var a = NewActivation();
            a.HoldLeader();
            Assert.That(a.HasMoved(sergeant), Is.False);
        }

        [Test]
        public void CohesionIgnoresElevation()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 10f), out _);
            // 4 m away across the ground but 3 m up (e.g. on a container): straight-line 5 m, flat 4 m → in cohesion.
            Assert.That(a.TryMoveFollower(troopers[0], new Vector3(4f, 3f, 10f), out var check), Is.True, check.Reason);
            // 5.5 m away across the ground at the same height: out of cohesion.
            Assert.That(a.TryMoveFollower(troopers[1], new Vector3(-5.5f, 0f, 10f), out _), Is.False);
        }

        [Test]
        public void SoldiersCannotEndOnTopOfEachOther()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 10f), out _);
            Assert.That(a.TryMoveFollower(troopers[0], new Vector3(2, 0, 10f), out _), Is.True);
            Assert.That(a.TryMoveFollower(troopers[1], new Vector3(2.3f, 0, 10f), out _), Is.False);
            Assert.That(a.TryMoveFollower(troopers[1], new Vector3(0.3f, 0, 10f), out _), Is.False, "overlaps the sergeant");
        }

        [Test]
        public void CannotMoveOffTheBattlefield()
        {
            sergeant.transform.position = new Vector3(0, 0, 18f);
            var a = NewActivation();
            Assert.That(a.TryMoveLeader(new Vector3(0, 0, 25f), out var check), Is.False);
            Assert.That(check.Reason, Does.Contain("battlefield"));
        }

        [Test]
        public void MoveCannotEndWhileSoldiersAreOutsideTheRadius()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 10f), out _);
            Assert.That(a.OutOfCohesion().Count, Is.EqualTo(9));
            Assert.That(a.Complete(), Is.False);
        }

        [Test]
        public void GatherBringsEveryoneWithinRadiusWithoutOverlap()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 20f), out _);
            a.GatherIntoCohesion();

            Assert.That(a.OutOfCohesion(), Is.Empty);
            var placed = squad.Soldiers.Select(m => a.PositionOf(m)).ToList();
            for (int i = 0; i < placed.Count; i++)
            for (int j = i + 1; j < placed.Count; j++)
                Assert.That(Measure.Horizontal(placed[i], placed[j]), Is.GreaterThanOrEqualTo(0.69f));
            Assert.That(a.Complete(), Is.True);
        }

        [Test]
        public void HoldingLetsTheSquadRepositionAroundTheSergeant()
        {
            var a = NewActivation();
            Assert.That(a.HoldLeader(), Is.True);
            Assert.That(a.LeaderMove, Is.EqualTo(MoveKind.Hold));
            Assert.That(a.TryMoveFollower(troopers[0], new Vector3(-3, 0, 3), out _), Is.True);
        }

        [Test]
        public void ResetPutsEveryoneBack()
        {
            var a = NewActivation();
            Vector3 start = a.PositionOf(troopers[0]);
            a.TryMoveLeader(new Vector3(0, 0, 10f), out _);
            a.TryMoveFollower(troopers[0], new Vector3(0, 0, 8f), out _);
            a.Reset();
            Assert.That(a.Phase, Is.EqualTo(MovePhase.MoveLeader));
            Assert.That(a.LeaderPosition, Is.EqualTo(Vector3.zero));
            Assert.That(a.PositionOf(troopers[0]), Is.EqualTo(start));
        }
    }
}
