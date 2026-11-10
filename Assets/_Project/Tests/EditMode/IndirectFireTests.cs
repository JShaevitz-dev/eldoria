using System.Collections.Generic;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoria.Tests
{
    public class IndirectFireTests
    {
        private readonly List<Object> created = new List<Object>();
        private Keyword indirectFire, halfCover, inHalfCover, fullCover, inFullCover, alwaysMissesKw;
        private WeaponTemplate mortarT, rifleT;
        private SoldierTemplate soldierT;
        private System.Func<Vector3, Vector3, Component, bool> originalCover;
        private System.Func<Soldier, Soldier, bool> originalSight;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        private Keyword Kw(string name, params KeywordInteraction[] interactions)
        {
            var k = Make<Keyword>();
            k.displayName = name;
            k.interactions = new List<KeywordInteraction>(interactions);
            return k;
        }

        [SetUp]
        public void SetUp()
        {
            originalCover = CoverCheck.IsBetween;
            originalSight = LineOfSight.CanSee;
            Turns.ResetCounter();

            indirectFire = Kw("Indirect fire",
                new KeywordInteraction { condition = KeywordCondition.Always, effect = KeywordEffect.IgnoresLineOfSight },
                new KeywordInteraction { condition = KeywordCondition.Always, effect = KeywordEffect.IgnoresAccuracyModifiers });
            inHalfCover = Kw("In half-cover", new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always,
                effect = KeywordEffect.AccuracyPenalty, amount = 25, onlyIfSourceIsBetween = true
            });
            halfCover = Kw("Half-cover");
            halfCover.grantsNearby = inHalfCover;
            inFullCover = Kw("In full cover", new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always,
                effect = KeywordEffect.CannotBeTargeted, onlyIfSourceIsBetween = true
            });
            fullCover = Kw("Full-cover");
            fullCover.grantsNearby = inFullCover;

            mortarT = Make<WeaponTemplate>();
            mortarT.displayName = "Mortar";
            mortarT.baseDamage = 5;
            mortarT.accuracyBonus = 15;
            mortarT.keywords.Add(indirectFire);
            rifleT = Make<WeaponTemplate>();
            rifleT.displayName = "Rifle";
            rifleT.accuracyBonus = 15;

            soldierT = Make<SoldierTemplate>();
            soldierT.accuracy = 60;
            soldierT.weapons = new List<WeaponTemplate> { mortarT, rifleT };
        }

        [TearDown]
        public void TearDown()
        {
            CoverCheck.IsBetween = originalCover;
            LineOfSight.CanSee = originalSight;
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
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

        private void Cover(Vector3 pos, Keyword keyword)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(go);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(2f, 1f, 0.4f);
            go.AddComponent<TerrainObject>().Configure("Cover", true, true, false, new[] { keyword });
            Physics.SyncTransforms();
        }

        [Test]
        public void IndirectFireCanTargetASoldierInFullCover()
        {
            Cover(new Vector3(0f, 0.5f, 0f), fullCover);
            var target = Spawn(new Vector3(0f, 0f, -1f));
            var shooter = Spawn(new Vector3(0f, 0f, 20f));
            NearbyKeywords.RefreshAll();

            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[1], target), Is.False, "a rifle can't");
            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[0], target), Is.True, "a mortar can");
            var r = Attack.Resolve(shooter, shooter.Weapons[0], target, false, new System.Random(3), applyDamage: false);
            Assert.That(r.NotTargetable, Is.False);
        }

        [Test]
        public void IndirectFireIgnoresLineOfSight()
        {
            var target = Spawn(new Vector3(0f, 0f, -1f));
            var shooter = Spawn(new Vector3(0f, 0f, 20f));
            LineOfSight.CanSee = (a, t) => false; // e.g. behind a building

            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[1], target), Is.False);
            Assert.That(Attack.Resolve(shooter, shooter.Weapons[1], target, false, new System.Random(1)).NotTargetableReason,
                Does.Contain("line of sight"));
            Assert.That(Attack.CanTarget(shooter, shooter.Weapons[0], target), Is.True);
        }

        [Test]
        public void IndirectFireUsesTheAccuracyStatWithNoModifiers()
        {
            Cover(new Vector3(0f, 0.5f, 0f), halfCover);
            var target = Spawn(new Vector3(0f, 0f, -1f));
            var shooter = Spawn(new Vector3(0f, 0f, 20f));
            NearbyKeywords.RefreshAll();

            // Rifle: 60 − 25 (sprinted) − 25 (half-cover) + 15 (weapon) = 25.
            var rifle = Attack.Resolve(shooter, shooter.Weapons[1], target, attackerSprinted: true, new System.Random(1), applyDamage: false);
            Assert.That(rifle.Accuracy.Final, Is.EqualTo(25));

            // Mortar: ignores the sprint penalty, the cover penalty and its own +15 bonus → 60.
            var mortar = Attack.Resolve(shooter, shooter.Weapons[0], target, attackerSprinted: true, new System.Random(1), applyDamage: false);
            Assert.That(mortar.Accuracy.Final, Is.EqualTo(60));
            Assert.That(mortar.Accuracy.Penalties, Is.Empty);
            Assert.That(mortar.Accuracy.Bonuses, Is.Empty);
        }

        [Test]
        public void AccuracyOverridesStillApplyToIndirectFire()
        {
            var shooter = Spawn(new Vector3(0f, 0f, 20f));
            var blindedKw = Kw("Blinded", new KeywordInteraction
            {
                role = KeywordRole.WhenAttacking, condition = KeywordCondition.Always, effect = KeywordEffect.AlwaysMisses
            });
            shooter.GainKeyword(blindedKw);
            var target = Spawn(new Vector3(0f, 0f, -1f));

            var r = Attack.Resolve(shooter, shooter.Weapons[0], target, false, new System.Random(1), applyDamage: false);
            Assert.That(r.Accuracy.HitChance, Is.EqualTo(0));
        }
    }
}
