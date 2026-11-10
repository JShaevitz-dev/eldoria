using System.Collections.Generic;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;
using static Eldoria.Rules.AccuracyModifier;

namespace Eldoria.Tests
{
    public class AccuracyTests
    {
        [Test]
        public void ExampleFromTheRules()
        {
            // 100% accuracy, sprinted (−25%) → 75%, weapon bonus (+20%) → 95%.
            var r = Accuracy.Calculate(100, new[] { Bonus("Weapon", 20), Penalty("Sprinted", 25) });
            Assert.That(r.AfterPenalties, Is.EqualTo(75));
            Assert.That(r.Final, Is.EqualTo(95));
        }

        [Test]
        public void NoModifiersUsesTheAccuracyStat()
        {
            Assert.That(Accuracy.Calculate(65, null).Final, Is.EqualTo(65));
        }

        [Test]
        public void PenaltiesComeBeforeBonuses()
        {
            // Penalties can't take accuracy below 0 before bonuses are added: 20 − 40 → 0, + 30 → 30.
            var r = Accuracy.Calculate(20, new[] { Bonus("Scope", 30), Penalty("Smoke", 40) });
            Assert.That(r.AfterPenalties, Is.EqualTo(0));
            Assert.That(r.Final, Is.EqualTo(30));

            // Bonuses can take accuracy over 100%: 90 − 10 → 80, + 50 → 130.
            Assert.That(Accuracy.Calculate(90, new[] { Bonus("Aim", 50), Penalty("Range", 10) }).Final, Is.EqualTo(130));
        }

        [TestCase(95, 95, 0)]
        [TestCase(100, 100, 0)]
        [TestCase(105, 100, 5)]
        [TestCase(160, 100, 60)]
        [TestCase(200, 100, 100)]
        [TestCase(250, 100, 100)]
        public void AccuracyOver100BecomesCritChance(int accuracy, int hit, int crit)
        {
            var r = Accuracy.Calculate(accuracy, null);
            Assert.That(r.Final, Is.EqualTo(accuracy));
            Assert.That(r.HitChance, Is.EqualTo(hit));
            Assert.That(r.CritChance, Is.EqualTo(crit));
        }

        [Test]
        public void AccuracyCannotGoBelowZero()
        {
            var r = Accuracy.Calculate(30, new[] { Penalty("Smoke", 80) });
            Assert.That(r.Final, Is.EqualTo(0));
            Assert.That(r.HitChance, Is.EqualTo(0));
            Assert.That(Accuracy.Calculate(30, new[] { Override("Odd effect", -20) }).Final, Is.EqualTo(0));
        }

        [Test]
        public void HitAndCritRollsUseTheirChances()
        {
            var rng = new System.Random(1234);
            int hits = 0, crits = 0;
            var r = Accuracy.Calculate(130, null); // 100% hit, 30% crit
            for (int i = 0; i < 4000; i++)
            {
                var roll = Accuracy.Roll(r, rng);
                if (roll.Hit) hits++;
                if (roll.Crit) crits++;
            }
            Assert.That(hits, Is.EqualTo(4000));
            Assert.That(crits / 4000f, Is.EqualTo(0.30f).Within(0.03f));

            var never = Accuracy.Calculate(0, null);
            for (int i = 0; i < 500; i++) Assert.That(Accuracy.Roll(never, rng).Hit, Is.False);

            var noCrit = Accuracy.Calculate(100, null);
            for (int i = 0; i < 500; i++) Assert.That(Accuracy.Roll(noCrit, rng).Crit, Is.False);
        }

        [Test]
        public void AMissNeverCrits()
        {
            var rng = new System.Random(99);
            var r = Accuracy.Calculate(50, null);
            for (int i = 0; i < 1000; i++)
            {
                var roll = Accuracy.Roll(r, rng);
                if (!roll.Hit) Assert.That(roll.Crit, Is.False);
            }
        }

        [Test]
        public void SeveralPenaltiesAndBonusesAddUp()
        {
            var r = Accuracy.Calculate(70, new[]
            {
                Penalty("Sprinted", 25), Penalty("Target in cover", 20), Bonus("Weapon", 5), Bonus("High ground", 10)
            });
            Assert.That(r.Final, Is.EqualTo(70 - 25 - 20 + 5 + 10));
        }

        [Test]
        public void AnOverrideReplacesAccuracyAndIgnoresEverythingElse()
        {
            var r = Accuracy.Calculate(70, new[] { Penalty("Sprinted", 25), Override("Guaranteed hit", 100), Bonus("Weapon", 20) });
            Assert.That(r.WasOverridden, Is.True);
            Assert.That(r.Final, Is.EqualTo(100));

            Assert.That(Accuracy.Calculate(70, new[] { Bonus("Weapon", 20), Override("Blinded", 10) }).Final, Is.EqualTo(10));
        }

        [Test]
        public void AlwaysMissesBeatsAlwaysHits()
        {
            var r = Accuracy.Calculate(70, new[]
            {
                Override("Always hits", Accuracy.AlwaysHits), Override("Always misses", Accuracy.AlwaysMisses), Bonus("Weapon", 20)
            });
            Assert.That(r.AppliedOverride.Value.Source, Is.EqualTo("Always misses"));
            Assert.That(r.HitChance, Is.EqualTo(0));
        }

        [Test]
        public void AlwaysHitsIsACertainHitWithNoCrit()
        {
            var r = Accuracy.Calculate(70, new[] { Override("Always hits", Accuracy.AlwaysHits), Bonus("Weapon", 50) });
            Assert.That(r.HitChance, Is.EqualTo(100));
            Assert.That(r.CritChance, Is.EqualTo(0), "an override can't be modified, so bonuses don't add crit chance");
        }

        [Test]
        public void ANegativeWeaponBonusCountsAsAPenalty()
        {
            var m = Signed("Heavy weapon", -10);
            Assert.That(m.Type, Is.EqualTo(AccuracyModifierType.Penalty));
            Assert.That(m.Value, Is.EqualTo(10));
        }

        [Test]
        public void AttackUsesSoldierAccuracySprintAndWeaponBonus()
        {
            var created = new List<Object>();
            var weaponT = ScriptableObject.CreateInstance<WeaponTemplate>();
            weaponT.displayName = "Rifle";
            weaponT.accuracyBonus = 20;
            var soldierT = ScriptableObject.CreateInstance<SoldierTemplate>();
            soldierT.accuracy = 100;
            soldierT.weapons.Add(weaponT);
            created.Add(weaponT);
            created.Add(soldierT);
            var go = new GameObject("Shooter");
            created.Add(go);
            var shooter = go.AddComponent<Soldier>();
            shooter.Configure(soldierT, false);

            Assert.That(Accuracy.ForAttack(shooter, shooter.Weapons[0], sprinted: true).Final, Is.EqualTo(95));
            var steady = Accuracy.ForAttack(shooter, shooter.Weapons[0], sprinted: false);
            Assert.That(steady.Final, Is.EqualTo(120));
            Assert.That(steady.HitChance, Is.EqualTo(100));
            Assert.That(steady.CritChance, Is.EqualTo(20));

            foreach (var c in created) Object.DestroyImmediate(c);
        }
    }
}
