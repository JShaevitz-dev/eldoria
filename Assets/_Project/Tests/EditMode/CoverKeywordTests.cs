using System.Collections.Generic;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoria.Tests
{
    public class CoverKeywordTests
    {
        private readonly List<Object> created = new List<Object>();
        private Keyword halfCover, inHalfCover;
        private TerrainObject barricade;
        private SoldierTemplate soldierT;
        private WeaponTemplate rifleT;
        private System.Func<Vector3, Vector3, Component, bool> originalCoverCheck;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        [SetUp]
        public void SetUp()
        {
            originalCoverCheck = CoverCheck.IsBetween;
            Turns.ResetCounter();

            inHalfCover = Make<Keyword>();
            inHalfCover.displayName = "In half-cover";
            inHalfCover.interactions.Add(new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always,
                effect = KeywordEffect.AccuracyPenalty, amount = 25, onlyIfSourceIsBetween = true
            });
            halfCover = Make<Keyword>();
            halfCover.displayName = "Half-cover";
            halfCover.grantsNearby = inHalfCover;
            halfCover.grantRadius = 5f;

            // Barricade 2 m wide (x −1..1) at z = 0.
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(go);
            go.transform.position = new Vector3(0f, 0.5f, 0f);
            go.transform.localScale = new Vector3(2f, 1f, 0.4f);
            barricade = go.AddComponent<TerrainObject>();
            barricade.Configure("Barricade", true, true, false, new[] { halfCover });
            Physics.SyncTransforms();

            rifleT = Make<WeaponTemplate>();
            soldierT = Make<SoldierTemplate>();
            soldierT.accuracy = 70;
            soldierT.weapons.Add(rifleT);
        }

        [TearDown]
        public void TearDown()
        {
            CoverCheck.IsBetween = originalCoverCheck;
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Soldier Spawn(Vector3 pos, Transform parent = null)
        {
            var go = new GameObject("Soldier");
            if (parent != null) go.transform.SetParent(parent);
            else created.Add(go);
            go.transform.position = pos;
            var s = go.AddComponent<Soldier>();
            s.Configure(soldierT, false);
            return s;
        }

        [Test]
        public void TerrainObjectsHaveKeywords()
        {
            Assert.That(barricade.HasKeyword("Half-cover"), Is.True);
        }

        [Test]
        public void SoldiersWithin5mOfHalfCoverGainInHalfCover()
        {
            var near = Spawn(new Vector3(0f, 0f, -4.5f));  // 4.3 m from the barricade's face
            var far = Spawn(new Vector3(0f, 0f, -6f));     // 5.8 m
            var diagonal = Spawn(new Vector3(4f, 0f, -2f)); // 3 m right of its end, 1.8 m back: 3.5 m
            NearbyKeywords.RefreshAll();

            Assert.That(near.HasKeyword("In half-cover"), Is.True);
            Assert.That(far.HasKeyword("In half-cover"), Is.False);
            Assert.That(diagonal.HasKeyword("In half-cover"), Is.True, "measured from the nearest part of the object");
            Assert.That(near.SourcesOf(inHalfCover).Single(), Is.EqualTo(barricade));
        }

        [Test]
        public void DistanceIgnoresHeight()
        {
            var up = Spawn(new Vector3(0f, 6f, -4f));
            NearbyKeywords.RefreshAll();
            Assert.That(up.HasKeyword("In half-cover"), Is.True);
        }

        [Test]
        public void AttacksOnASoldierInHalfCoverTakeMinus25Accuracy()
        {
            var target = Spawn(new Vector3(0f, 0f, -1f));
            var shooter = Spawn(new Vector3(0f, 0f, 20f));
            NearbyKeywords.RefreshAll();

            var r = Attack.Resolve(shooter, shooter.Weapons[0], target, false, new System.Random(1), applyDamage: false);
            Assert.That(r.Accuracy.Final, Is.EqualTo(70 - 25));
        }

        [Test]
        public void NoPenaltyIfTheCoverIsNotBetween()
        {
            var target = Spawn(new Vector3(0f, 0f, -1f));
            var shooter = Spawn(new Vector3(0f, 0f, -20f));
            NearbyKeywords.RefreshAll();
            CoverCheck.IsBetween = (a, b, src) => false; // stand-in until line of sight is built

            var r = Attack.Resolve(shooter, shooter.Weapons[0], target, false, new System.Random(1), applyDamage: false);
            Assert.That(r.Accuracy.Final, Is.EqualTo(70));
            Assert.That(target.HasKeyword("In half-cover"), Is.True, "still has the keyword; it just doesn't apply");
        }

        [Test]
        public void MovingIntoAndOutOfCoverUpdatesTheKeyword()
        {
            var unitGo = new GameObject("Unit");
            created.Add(unitGo);
            var unit = unitGo.AddComponent<Unit>();
            var leader = Spawn(new Vector3(0f, 0f, -12f), unitGo.transform);
            leader.Configure(soldierT, true);
            unit.RefreshSoldiers();
            Turns.StartNextTurn();
            Assert.That(leader.HasKeyword("In half-cover"), Is.False);

            var a = new UnitMoveActivation(unit, unit.Soldiers);
            Assert.That(a.TryMoveLeader(new Vector3(0f, 0f, -3f), out var check), Is.True, check.Reason);
            Assert.That(leader.HasKeyword("In half-cover"), Is.True, "gained on arriving within 5 m");

            a.Reset();
            Assert.That(leader.HasKeyword("In half-cover"), Is.False, "undoing the move takes it away");
        }

        [Test]
        public void ObjectsCanGainAndLoseKeywords()
        {
            Assert.That(barricade.LoseKeyword(halfCover), Is.True);
            var s = Spawn(new Vector3(0f, 0f, -1f));
            NearbyKeywords.RefreshAll();
            Assert.That(s.HasKeyword("In half-cover"), Is.False, "a destroyed/removed half-cover grants nothing");

            barricade.GainKeyword(halfCover);
            NearbyKeywords.RefreshAll();
            Assert.That(s.HasKeyword("In half-cover"), Is.True);
        }
    }
}
