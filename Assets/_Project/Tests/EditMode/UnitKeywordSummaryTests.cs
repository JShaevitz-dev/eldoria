using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>The keyword list shown for a selected unit: every keyword on its soldiers, and who has it.</summary>
    public class UnitKeywordSummaryTests
    {
        private readonly List<Object> created = new List<Object>();

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        private Keyword Kw(string name)
        {
            var k = Make<Keyword>();
            k.displayName = name;
            return k;
        }

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Unit MakeUnit(params SoldierTemplate[] templates)
        {
            var go = new GameObject("Unit");
            created.Add(go);
            var unit = go.AddComponent<Unit>();
            unit.Configure(null, Side.Player, "Unit");
            for (int i = 0; i < templates.Length; i++)
            {
                var s = new GameObject($"S{i}");
                s.transform.SetParent(go.transform);
                s.transform.position = new Vector3(i * 2, 0, 0);
                s.AddComponent<Soldier>().Configure(templates[i], i == 0);
            }
            unit.RefreshSoldiers();
            return unit;
        }

        [Test]
        public void ListsEveryKeywordWithTheSoldiersWhoHaveIt()
        {
            var biological = Kw("Biological");
            var officer = Kw("Officer");
            var moved = Kw("Moved");

            var sgtT = Make<SoldierTemplate>();
            sgtT.keywords = new List<Keyword> { biological, officer };
            var trooperT = Make<SoldierTemplate>();
            trooperT.keywords = new List<Keyword> { biological };

            var unit = MakeUnit(sgtT, trooperT, trooperT);
            unit.Soldiers[2].GainKeyword(moved, Turns.NextTurn); // gained during play

            var summary = unit.KeywordSummary();
            Assert.That(summary.Select(e => e.keyword.displayName), Is.EqualTo(new[] { "Biological", "Moved", "Officer" }),
                "most widely held first, then by name");
            Assert.That(summary[0].soldiers.Count, Is.EqualTo(3));
            Assert.That(summary.Single(e => e.keyword == moved).soldiers, Is.EqualTo(new[] { unit.Soldiers[2] }));
            Assert.That(summary.Single(e => e.keyword == officer).soldiers, Is.EqualTo(new[] { unit.Soldiers[0] }));
        }

        [Test]
        public void IncludesKeywordsGrantedByNearbyTerrainAndLeavesOutTheDead()
        {
            var inCover = Kw("In half-cover");
            var t = Make<SoldierTemplate>();
            var unit = MakeUnit(t, t);
            unit.Soldiers[0].SetNearbyKeywords(new[] { new NearbyKeyword { keyword = inCover } });
            Assert.That(unit.KeywordSummary().Single().keyword, Is.EqualTo(inCover));

            unit.Soldiers[0].Kill();
            Assert.That(unit.KeywordSummary(), Is.Empty);
        }

        [Test]
        public void TwoKeywordsWithTheSameNameAreListedOnce()
        {
            var t1 = Make<SoldierTemplate>();
            t1.keywords = new List<Keyword> { Kw("Biological") };
            var t2 = Make<SoldierTemplate>();
            t2.keywords = new List<Keyword> { Kw("Biological") };
            var summary = MakeUnit(t1, t2).KeywordSummary();
            Assert.That(summary.Count, Is.EqualTo(1));
            Assert.That(summary[0].soldiers.Count, Is.EqualTo(2));
        }
    }
}
