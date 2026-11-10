using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoria.Tests
{
    public class FullCoverAndUniqueKeywordTests
    {
        private readonly List<Object> created = new List<Object>();
        private Keyword halfCover, inHalfCover, fullCover, inFullCover, biological;
        private SoldierTemplate soldierT;
        private System.Func<Vector3, Vector3, Component, bool> originalCoverCheck;
        private System.Func<Soldier, Soldier, bool> originalSight;

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
            originalCoverCheck = CoverCheck.IsBetween;
            originalSight = LineOfSight.CanSee;
            Turns.ResetCounter();

            inHalfCover = Kw("In half-cover");
            inHalfCover.interactions.Add(new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always,
                effect = KeywordEffect.AccuracyPenalty, amount = 25, onlyIfSourceIsBetween = true
            });
            halfCover = Kw("Half-cover");
            halfCover.grantsNearby = inHalfCover;

            inFullCover = Kw("In full cover");
            inFullCover.interactions.Add(new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always,
                effect = KeywordEffect.CannotBeTargeted, onlyIfSourceIsBetween = true
            });
            fullCover = Kw("Full-cover");
            fullCover.grantsNearby = inFullCover;
            fullCover.grantsWhenBetween = true;

            biological = Kw("Biological");
            soldierT = Make<SoldierTemplate>();
            soldierT.accuracy = 70;
            soldierT.keywords.Add(biological);
            soldierT.weapons.Add(Make<WeaponTemplate>());
        }

        [TearDown]
        public void TearDown()
        {
            CoverCheck.IsBetween = originalCoverCheck;
            LineOfSight.CanSee = originalSight;
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private TerrainObject Cover(string name, Vector3 pos, params Keyword[] keywords)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(go);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(2f, 1f, 0.4f);
            var obj = go.AddComponent<TerrainObject>();
            obj.Configure(name, true, true, false, keywords);
            Physics.SyncTransforms();
            return obj;
        }

        private Soldier Spawn(Vector3 pos)
        {
            var go = new GameObject("Soldier");
            created.Add(go);
            go.transform.position = pos;
            var s = go.AddComponent<Soldier>();
            s.Configure(soldierT, false);
            return s;
        }

        private static int Count(Soldier s, string keyword) => s.Keywords.Count(k => k.Is(keyword));

        // ---- Each keyword only once ----

        [Test]
        public void GainingAKeywordTwiceGivesOneCopy()
        {
            var s = Spawn(Vector3.zero);
            var stunned = Kw("Stunned");
            s.GainKeyword(stunned);
            s.GainKeyword(stunned);
            Assert.That(Count(s, "Stunned"), Is.EqualTo(1));
            Assert.That(s.GainedKeywords.Count, Is.EqualTo(1));
        }

        [Test]
        public void GainingAKeywordTheSoldierAlreadyHasDoesNothing()
        {
            var s = Spawn(Vector3.zero);
            s.GainKeyword(biological);
            Assert.That(Count(s, "Biological"), Is.EqualTo(1));
            Assert.That(s.GainedKeywords, Is.Empty);
        }

        [Test]
        public void KeywordsWithTheSameNameCountAsTheSameKeyword()
        {
            var s = Spawn(Vector3.zero);
            s.GainKeyword(Kw("biological"));
            Assert.That(Count(s, "Biological"), Is.EqualTo(1));
        }

        [Test]
        public void BeingNearTwoCoverObjectsGivesTheKeywordOnceAndThePenaltyOnce()
        {
            var a = Cover("Barricade A", new Vector3(-1.5f, 0.5f, 0f), halfCover);
            var b = Cover("Barricade B", new Vector3(1.5f, 0.5f, 0f), halfCover);
            // Target right behind barricade B, shooter straight out in front of it (B is between them).
            var target = Spawn(new Vector3(1.5f, 0f, -1f));
            var shooter = Spawn(new Vector3(1.5f, 0f, 20f));
            NearbyKeywords.RefreshAll();

            Assert.That(Count(target, "In half-cover"), Is.EqualTo(1));
            Assert.That(target.SourcesOf(inHalfCover).Count(), Is.EqualTo(2), "both barricades are remembered as sources");

            var r = Attack.Resolve(shooter, shooter.Weapons[0], target, false, new System.Random(1), applyDamage: false);
            Assert.That(r.Accuracy.Final, Is.EqualTo(70 - 25), "−25 once, not −50");
        }

        [Test]
        public void TemplatesDropRepeatedKeywords()
        {
            var list = new List<Keyword> { biological, Kw("Robotic"), biological, null, Kw("BIOLOGICAL") };
            Keyword.RemoveDuplicates(list);
            Assert.That(list.Count(k => k != null && k.Is("Biological")), Is.EqualTo(1));
            Assert.That(list.Count(k => k != null && k.Is("Robotic")), Is.EqualTo(1));
            Assert.That(list.Contains(null), Is.True, "empty Inspector slots are left alone");
        }

        // ---- Full cover ----

        [Test]
        public void ASoldierInFullCoverCannotBeTargeted()
        {
            Cover("Container", new Vector3(0f, 0.5f, 0f), fullCover);
            var target = Spawn(new Vector3(0f, 0f, -1f));
            var shooter = Spawn(new Vector3(0f, 0f, 20f));
            NearbyKeywords.RefreshAll();

            Assert.That(target.HasKeyword("In full cover"), Is.False, "not a standing status: it depends on who's shooting");
            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[0], target), Is.False);

            var r = Attack.Resolve(shooter, shooter.Weapons[0], target, false, new System.Random(1));
            Assert.That(r.NotTargetable, Is.True);
            Assert.That(r.NotTargetableReason, Does.Contain("full cover"));
            Assert.That(r.Hit, Is.False);
            Assert.That(r.DamageDealt, Is.EqualTo(0));
        }

        [Test]
        public void FullCoverOnlyProtectsWhenTheCoverIsBetween()
        {
            Cover("Container", new Vector3(0f, 0.5f, 0f), fullCover);
            var target = Spawn(new Vector3(0f, 0f, -1f));
            var flanker = Spawn(new Vector3(0f, 0f, -20f));
            NearbyKeywords.RefreshAll();
            CoverCheck.IsBetween = (a, b, src) => false;

            Assert.That(Attack.CanTarget(flanker, flanker.Weapons[0], target), Is.True);
        }

        [Test]
        public void FullCoverWorksAtAnyDistanceFromTheTarget()
        {
            Cover("Container", new Vector3(0f, 0.5f, 0f), fullCover);
            var target = Spawn(new Vector3(0f, 0f, -15f));
            var shooter = Spawn(new Vector3(0f, 0f, 20f));
            LineOfSight.CanSee = (a, t) => true;   // the rule, not the geometry
            NearbyKeywords.RefreshAll();
            var r = Attack.Resolve(shooter, shooter.Weapons[0], target, false, new System.Random(1), applyDamage: false);
            Assert.That(r.NotTargetableReason, Does.Contain("full cover"));
        }

        [Test]
        public void FullCoverWorksAtAnyDistanceFromTheShooter()
        {
            Cover("Container", new Vector3(0f, 0.5f, 0f), fullCover);
            var shooter = Spawn(new Vector3(0f, 0f, 1f));      // right behind the cover...
            var target = Spawn(new Vector3(0f, 0f, -25f));     // ...the target far beyond it
            LineOfSight.CanSee = (a, t) => true;
            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[0], target), Is.False);
        }

        [Test]
        public void ASoldierWithNoFullCoverBetweenCanBeTargeted()
        {
            Cover("Container", new Vector3(10f, 0.5f, 0f), fullCover);
            var target = Spawn(new Vector3(0f, 0f, -15f));
            var shooter = Spawn(new Vector3(0f, 0f, 20f));
            LineOfSight.CanSee = (a, t) => true;
            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[0], target), Is.True);
        }
    }
}
