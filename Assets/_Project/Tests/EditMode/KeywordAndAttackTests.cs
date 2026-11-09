using System.Collections.Generic;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoira.Tests
{
    public class KeywordAndAttackTests
    {
        private readonly List<Object> created = new List<Object>();
        private Keyword biological, toxic;
        private WeaponTemplate rifle, toxinSprayer;
        private SoldierTemplate trooperT, tankT;

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

        private Soldier Spawn(SoldierTemplate t)
        {
            var go = new GameObject(t.displayName);
            created.Add(go);
            var s = go.AddComponent<Soldier>();
            s.Configure(t, false);
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            biological = Kw("Biological");
            toxic = Kw("Toxic", new KeywordInteraction
            {
                party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = biological, effect = KeywordEffect.NoEffect
            });

            rifle = Make<WeaponTemplate>();
            rifle.displayName = "Rifle";
            rifle.baseDamage = 4;
            rifle.penetration = 1;
            toxinSprayer = Make<WeaponTemplate>();
            toxinSprayer.displayName = "Toxin Sprayer";
            toxinSprayer.baseDamage = 3;
            toxinSprayer.penetration = 1;
            toxinSprayer.keywords.Add(toxic);

            trooperT = Make<SoldierTemplate>();
            trooperT.displayName = "Trooper";
            trooperT.health = 10;
            trooperT.armorLevel = 1;
            trooperT.accuracy = 100;
            trooperT.keywords.Add(biological);
            trooperT.weapons = new List<WeaponTemplate> { rifle, toxinSprayer };

            tankT = Make<SoldierTemplate>();
            tankT.displayName = "Tank";
            tankT.health = 10;
            tankT.armorLevel = 1;
            tankT.accuracy = 100;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void ToxicHasNoEffectOnATargetThatIsNotBiological()
        {
            var shooter = Spawn(trooperT);
            var tank = Spawn(tankT);
            var r = Attack.Resolve(shooter, shooter.Weapons[1], tank, false, new System.Random(1));
            Assert.That(r.NoEffect, Is.True);
            Assert.That(r.DamageDealt, Is.EqualTo(0));
            Assert.That(r.BonusEffectsApply, Is.False);
            Assert.That(tank.Health, Is.EqualTo(10));
        }

        [Test]
        public void ToxicDamagesABiologicalTarget()
        {
            var shooter = Spawn(trooperT);
            var target = Spawn(trooperT);
            // 100% accuracy → always hits, no crit; 3 damage, penetration = armor → 3.
            var r = Attack.Resolve(shooter, shooter.Weapons[1], target, false, new System.Random(1));
            Assert.That(r.NoEffect, Is.False);
            Assert.That(r.Hit, Is.True);
            Assert.That(r.DamageDealt, Is.EqualTo(3));
            Assert.That(target.Health, Is.EqualTo(7));
        }

        [Test]
        public void ARifleWithoutKeywordsAffectsAnyTarget()
        {
            var shooter = Spawn(trooperT);
            var tank = Spawn(tankT);
            var r = Attack.Resolve(shooter, shooter.Weapons[0], tank, false, new System.Random(1));
            Assert.That(r.NoEffect, Is.False);
            Assert.That(r.DamageDealt, Is.EqualTo(4));
        }

        [Test]
        public void FinalDamageIsBaseDamagePlusKeywordBonusesAndPenalties()
        {
            Assert.That(Attack.FinalDamage(4, new KeywordOutcome()), Is.EqualTo(4), "no keywords: just base damage");
            Assert.That(Attack.FinalDamage(4, new KeywordOutcome { DamageBonus = 2 }), Is.EqualTo(6));
            Assert.That(Attack.FinalDamage(4, new KeywordOutcome { DamagePenalty = 1 }), Is.EqualTo(3));
            Assert.That(Attack.FinalDamage(2, new KeywordOutcome { DamagePenalty = 5, DamageBonus = 1 }), Is.EqualTo(1),
                "penalties stop at 0 before bonuses");
        }

        [Test]
        public void KeywordsCanAddDamageAndAccuracy()
        {
            var robotic = Kw("Robotic");
            var emp = Kw("EMP",
                new KeywordInteraction { party = KeywordParty.Target, condition = KeywordCondition.Has, keyword = robotic, effect = KeywordEffect.DamageBonus, amount = 3 },
                new KeywordInteraction { party = KeywordParty.Target, condition = KeywordCondition.Has, keyword = robotic, effect = KeywordEffect.AccuracyBonus, amount = 10 });

            var o = KeywordRules.Evaluate(null, new List<Keyword> { emp }, new List<Keyword> { robotic });
            Assert.That(o.DamageBonus, Is.EqualTo(3));
            Assert.That(o.AccuracyModifiers.Count, Is.EqualTo(1));
            Assert.That(o.Applied.Count, Is.EqualTo(2));

            var none = KeywordRules.Evaluate(null, new List<Keyword> { emp }, new List<Keyword> { biological });
            Assert.That(none.DamageBonus, Is.EqualTo(0));
        }

        [Test]
        public void SoldierKeywordsApplyOnlyInTheirRole()
        {
            var sniperRifle = Kw("Long-barrelled");
            var sniper = Kw("Sniper", new KeywordInteraction
            {
                role = KeywordRole.WhenAttacking, party = KeywordParty.Weapon, condition = KeywordCondition.Has,
                keyword = sniperRifle, effect = KeywordEffect.AccuracyBonus, amount = 15
            });
            var weaponKw = new List<Keyword> { sniperRifle };

            var attacking = KeywordRules.Evaluate(new List<Keyword> { sniper }, weaponKw, null);
            Assert.That(attacking.AccuracyModifiers.Count, Is.EqualTo(1));

            // A sniper being shot at with a long-barrelled weapon gets nothing from their own keyword.
            var defending = KeywordRules.Evaluate(null, weaponKw, new List<Keyword> { sniper });
            Assert.That(defending.AccuracyModifiers.Count, Is.EqualTo(0));
        }

        [Test]
        public void DamageThatReducesHealthToZeroKills()
        {
            var shooter = Spawn(trooperT);
            tankT.health = 4;
            var tank = Spawn(tankT);
            Attack.Resolve(shooter, shooter.Weapons[0], tank, false, new System.Random(1));
            Assert.That(tank.Health, Is.EqualTo(0));
            Assert.That(tank.IsAlive, Is.False);
        }

        [Test]
        public void SprintingLowersTheAttackersAccuracy()
        {
            var shooter = Spawn(trooperT);
            var target = Spawn(tankT);
            var r = Attack.Resolve(shooter, shooter.Weapons[0], target, attackerSprinted: true, new System.Random(1), applyDamage: false);
            Assert.That(r.Accuracy.Final, Is.EqualTo(75));
        }
    }
}
