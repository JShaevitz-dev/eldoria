using System.Collections.Generic;
using System.Linq;
using Eldoira.Gameplay;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoira.Tests
{
    public class GainedKeywordTests
    {
        private readonly List<Object> created = new List<Object>();
        private Keyword biological, stunned;
        private SoldierTemplate template;
        private Unit unit;
        private Soldier sergeant;
        private List<Soldier> troopers;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn(); // turn 1

            biological = Make<Keyword>();
            biological.displayName = "Biological";
            stunned = Make<Keyword>();
            stunned.displayName = "Stunned";

            template = Make<SoldierTemplate>();
            template.keywords.Add(biological);

            var go = new GameObject("Unit");
            created.Add(go);
            unit = go.AddComponent<Unit>();
            sergeant = Spawn(go.transform, true, Vector3.zero);
            troopers = Enumerable.Range(0, 4).Select(i => Spawn(go.transform, false, new Vector3((i - 1.5f) * 1.5f, 0f, -2f))).ToList();
            unit.RefreshSoldiers();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Soldier Spawn(Transform parent, bool sgt, Vector3 pos)
        {
            var go = new GameObject(sgt ? "Sergeant" : "Trooper");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var s = go.AddComponent<Soldier>();
            s.Configure(template, sgt);
            return s;
        }

        private UnitMoveActivation NewActivation() => new UnitMoveActivation(unit, unit.Soldiers);

        [Test]
        public void SoldiersCanGainAndLoseKeywords()
        {
            var s = troopers[0];
            Assert.That(s.HasKeyword("Stunned"), Is.False);
            s.GainKeyword(stunned);
            Assert.That(s.HasKeyword("Stunned"), Is.True);
            Assert.That(s.HasKeyword("Biological"), Is.True, "inherent keywords are kept");
            Assert.That(s.LoseKeyword(stunned), Is.True);
            Assert.That(s.HasKeyword("Stunned"), Is.False);
            Assert.That(s.LoseKeyword(stunned), Is.False, "didn't have it any more");
        }

        [Test]
        public void SoldiersCanLoseAndRegainInherentKeywords()
        {
            var s = troopers[0];
            Assert.That(s.LoseKeyword(biological), Is.True);
            Assert.That(s.HasKeyword("Biological"), Is.False);
            Assert.That(template.keywords, Does.Contain(biological), "the template is unchanged");
            Assert.That(troopers[1].HasKeyword("Biological"), Is.True, "other soldiers are unaffected");
            s.GainKeyword(biological);
            Assert.That(s.HasKeyword("Biological"), Is.True);
        }

        [Test]
        public void TemporaryKeywordsWearOffAtTheStartOfTheirTurn()
        {
            var s = troopers[0];
            s.GainKeyword(stunned, Turns.NextTurn); // wears off at the start of turn 2
            var permanent = Make<Keyword>();
            permanent.displayName = "Veteran";
            s.GainKeyword(permanent);

            Turns.StartNextTurn(); // turn 2
            Assert.That(s.HasKeyword("Stunned"), Is.False);
            Assert.That(s.HasKeyword("Veteran"), Is.True);
        }

        [Test]
        public void WeaponsCanGainAndLoseKeywords()
        {
            var w = new Weapon(Make<WeaponTemplate>());
            var overheated = Make<Keyword>();
            overheated.displayName = "Overheated";
            w.GainKeyword(overheated, Turns.NextTurn);
            Assert.That(w.HasKeyword("Overheated"), Is.True);
            w.OnTurnStarted(Turns.NextTurn);
            Assert.That(w.HasKeyword("Overheated"), Is.False);
        }

        [Test]
        public void MovingGivesTheMovedKeyword()
        {
            var a = NewActivation();
            Assert.That(sergeant.HasKeyword(KeywordLibrary.Moved), Is.False);

            a.TryMoveLeader(new Vector3(0, 0, 3f), out _);
            Assert.That(sergeant.HasKeyword(KeywordLibrary.Moved), Is.True);

            a.TryMoveFollower(troopers[0], new Vector3(-1f, 0, 1f), out var check);
            Assert.That(check.Ok, Is.True, check.Reason);
            Assert.That(troopers[0].HasKeyword(KeywordLibrary.Moved), Is.True);
            Assert.That(troopers[1].HasKeyword(KeywordLibrary.Moved), Is.False, "not moved");
        }

        [Test]
        public void GatheringGivesTheMovedKeywordToThoseWhoMove()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 9f), out _);
            var moved = a.GatherIntoCohesion();
            Assert.That(moved, Is.Not.Empty);
            foreach (var m in moved) Assert.That(m.HasKeyword(KeywordLibrary.Moved), Is.True);
        }

        [Test]
        public void HoldingDoesNotGiveMoved()
        {
            var a = NewActivation();
            a.HoldLeader();
            Assert.That(sergeant.HasKeyword(KeywordLibrary.Moved), Is.False);
        }

        [Test]
        public void UndoingTheMoveRemovesMoved()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 3f), out _);
            a.Reset();
            Assert.That(sergeant.HasKeyword(KeywordLibrary.Moved), Is.False);
        }

        [Test]
        public void MovedIsRemovedAtTheStartOfTheNextTurn()
        {
            var a = NewActivation();
            a.TryMoveLeader(new Vector3(0, 0, 3f), out _);
            a.Complete();
            Assert.That(sergeant.HasKeyword(KeywordLibrary.Moved), Is.True, "still Moved for the rest of this turn");

            Turns.StartNextTurn();
            Assert.That(sergeant.HasKeyword(KeywordLibrary.Moved), Is.False);
        }

        [Test]
        public void GainedKeywordsTakePartInKeywordInteractions()
        {
            // e.g. a weapon that is better against targets that moved.
            var tracking = Make<Keyword>();
            tracking.displayName = "Tracking";
            tracking.interactions.Add(new KeywordInteraction
            {
                party = KeywordParty.Target, condition = KeywordCondition.Has,
                keyword = KeywordLibrary.Get(KeywordLibrary.Moved), effect = KeywordEffect.AccuracyBonus, amount = 10
            });

            var target = troopers[0];
            Assert.That(KeywordRules.Evaluate(null, new List<Keyword> { tracking }, target.Keywords).AccuracyModifiers, Is.Empty);
            target.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Turns.NextTurn);
            Assert.That(KeywordRules.Evaluate(null, new List<Keyword> { tracking }, target.Keywords).AccuracyModifiers.Count, Is.EqualTo(1));
        }
    }
}
