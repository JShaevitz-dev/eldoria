using System.Collections.Generic;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoria.Tests
{
    public class LeaderSuccessionTests
    {
        private readonly List<Object> created = new List<Object>();
        private SoldierTemplate profile;
        private Unit squad;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<SoldierTemplate>();
            created.Add(profile);
            var go = new GameObject("Unit");
            created.Add(go);
            squad = go.AddComponent<Unit>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        private Soldier Spawn(string name, bool sergeant, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(squad.transform);
            go.transform.position = pos;
            var m = go.AddComponent<Soldier>();
            m.Configure(profile, sergeant);
            return m;
        }

        [Test]
        public void SergeantStartsAsLeader()
        {
            Spawn("A", false, Vector3.zero);
            var sgt = Spawn("Sgt", true, Vector3.right);
            squad.RefreshSoldiers();
            Assert.That(squad.Leader, Is.EqualTo(sgt));
            Assert.That(sgt.IsLeader, Is.True);
        }

        [Test]
        public void ClosestSoldierInheritsCommand()
        {
            var sgt = Spawn("Sgt", true, Vector3.zero);
            Spawn("Far", false, new Vector3(4, 0, 0));
            var near = Spawn("Near", false, new Vector3(0, 0, -1.5f));
            Spawn("Mid", false, new Vector3(-2, 0, 0));
            squad.RefreshSoldiers();

            sgt.Kill();

            Assert.That(squad.Leader, Is.EqualTo(near));
            Assert.That(sgt.IsAlive, Is.False);
        }

        [Test]
        public void TiesAreBrokenAtRandomAmongTheClosest()
        {
            var tiedNames = new HashSet<string>();
            for (int seed = 0; seed < 40; seed++)
            {
                var sgt = Spawn("Sgt", true, Vector3.zero);
                Spawn("Left", false, new Vector3(-2, 0, 0));
                Spawn("Right", false, new Vector3(2, 0, 0));
                Spawn("Far", false, new Vector3(0, 0, -3));
                squad.RefreshSoldiers();

                var result = LeaderSuccession.ChooseSuccessor(sgt.Position,
                    squad.LivingSoldiers.Where(m => m != sgt), new System.Random(seed));
                Assert.That(result.TiedCandidates, Is.EqualTo(2));
                Assert.That(new[] { "Left", "Right" }, Does.Contain(result.NewLeader.name));
                tiedNames.Add(result.NewLeader.name);

                foreach (Transform child in squad.transform.Cast<Transform>().ToList())
                    Object.DestroyImmediate(child.gameObject);
            }
            Assert.That(tiedNames, Is.EquivalentTo(new[] { "Left", "Right" }), "both tied soldiers should win sometimes");
        }

        [Test]
        public void KillingANonLeaderDoesNotChangeCommand()
        {
            var sgt = Spawn("Sgt", true, Vector3.zero);
            var t = Spawn("T", false, Vector3.right);
            squad.RefreshSoldiers();
            t.Kill();
            Assert.That(squad.Leader, Is.EqualTo(sgt));
        }

        [Test]
        public void CommandKeepsPassingAsLeadersFall()
        {
            var sgt = Spawn("Sgt", true, Vector3.zero);
            var a = Spawn("A", false, new Vector3(1, 0, 0));
            var b = Spawn("B", false, new Vector3(2.5f, 0, 0));
            squad.RefreshSoldiers();

            sgt.Kill();
            Assert.That(squad.Leader, Is.EqualTo(a));
            a.Kill();
            Assert.That(squad.Leader, Is.EqualTo(b));
            b.Kill();
            Assert.That(squad.Leader, Is.Null);
            Assert.That(squad.IsDestroyed, Is.True);
        }

        private Unit OtherUnit(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent);
            else created.Add(go);
            return go.AddComponent<Unit>();
        }

        private Soldier SpawnIn(Unit unit, string name, bool sergeant, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(unit.transform);
            go.transform.position = pos;
            var m = go.AddComponent<Soldier>();
            m.Configure(profile, sergeant);
            return m;
        }

        [Test]
        public void CommandNeverPassesToASoldierOfAnotherUnit()
        {
            // A neighbouring squad's soldier stands right next to the sergeant; the sergeant's own trooper is farther.
            var sgt = Spawn("Sgt", true, Vector3.zero);
            var own = Spawn("Own", false, new Vector3(0, 0, -4f));
            var neighbours = OtherUnit("Neighbours");
            var neighbourSgt = SpawnIn(neighbours, "Neighbour Sgt", true, new Vector3(5, 0, 0));
            var stranger = SpawnIn(neighbours, "Stranger", false, new Vector3(0.6f, 0, 0));
            squad.RefreshSoldiers();
            neighbours.RefreshSoldiers();

            sgt.Kill();

            Assert.That(squad.Leader, Is.EqualTo(own));
            Assert.That(neighbours.Leader, Is.EqualTo(neighbourSgt), "the other unit's command is untouched");
            Assert.That(stranger.IsLeader, Is.False);
        }

        [Test]
        public void AUnitPlacedInsideAnotherKeepsItsOwnSoldiers()
        {
            var sgt = Spawn("Sgt", true, Vector3.zero);
            var own = Spawn("Own", false, new Vector3(0, 0, -4f));
            var inner = OtherUnit("Inner", squad.transform);           // nested in the hierarchy
            var innerSgt = SpawnIn(inner, "Inner Sgt", true, new Vector3(6, 0, 0));
            var innerTrooper = SpawnIn(inner, "Inner Trooper", false, new Vector3(0.6f, 0, 0));
            squad.RefreshSoldiers();
            inner.RefreshSoldiers();

            Assert.That(squad.Soldiers, Is.EquivalentTo(new[] { sgt, own }));
            Assert.That(inner.Soldiers, Is.EquivalentTo(new[] { innerSgt, innerTrooper }));
            Assert.That(innerTrooper.Unit, Is.EqualTo(inner));

            sgt.Kill();
            Assert.That(squad.Leader, Is.EqualTo(own), "not the closer soldier of the nested unit");
            Assert.That(inner.Leader, Is.EqualTo(innerSgt));
        }

        [Test]
        public void CohesionIsMeasuredFromTheNewLeader()
        {
            var sgt = Spawn("Sgt", true, Vector3.zero);
            var heir = Spawn("Heir", false, new Vector3(0, 0, -1f));
            var other = Spawn("Other", false, new Vector3(0, 0, -3f));
            squad.RefreshSoldiers();
            sgt.Kill();

            var act = new UnitMoveActivation(squad, squad.LivingSoldiers);
            Assert.That(act.Leader, Is.EqualTo(heir));
            Assert.That(act.TryMoveLeader(new Vector3(0, 0, 9f), out _), Is.True);
            Assert.That(act.TryMoveFollower(other, new Vector3(0, 0, 4.5f), out _), Is.True);
            Assert.That(act.Positions.ContainsKey(sgt), Is.False, "dead soldiers take no part in the move");
        }
    }
}
