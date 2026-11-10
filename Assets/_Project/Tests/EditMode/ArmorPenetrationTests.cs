using System.Collections.Generic;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoria.Tests
{
    public class ArmorPenetrationTests
    {
        [TestCase(2, 2)]
        [TestCase(0, 0)]
        [TestCase(5, 5)]
        public void EqualPenetrationAndArmorDealsFullDamage(int pen, int armor)
        {
            var o = ArmorPenetration.Resolve(8, pen, armor);
            Assert.That(o.Result, Is.EqualTo(PenetrationResult.Normal));
            Assert.That(o.Damage, Is.EqualTo(8));
            Assert.That(o.BonusEffectsApply, Is.True);
        }

        [TestCase(1, 2)]
        [TestCase(0, 1)]
        public void OneBelowArmorDealsHalfDamage(int pen, int armor)
        {
            var o = ArmorPenetration.Resolve(8, pen, armor);
            Assert.That(o.Result, Is.EqualTo(PenetrationResult.Reduced));
            Assert.That(o.Damage, Is.EqualTo(4));
            Assert.That(o.BonusEffectsApply, Is.True);
        }

        [TestCase(0, 2)]
        [TestCase(1, 3)]
        [TestCase(0, 9)]
        public void TwoOrMoreBelowArmorDealsNothingAndStopsBonusEffects(int pen, int armor)
        {
            var o = ArmorPenetration.Resolve(8, pen, armor);
            Assert.That(o.Result, Is.EqualTo(PenetrationResult.Blocked));
            Assert.That(o.Damage, Is.EqualTo(0));
            Assert.That(o.BonusEffectsApply, Is.False);
        }

        [TestCase(3, 2)]
        [TestCase(9, 0)]
        public void AboveArmorByAnyAmountDealsOneAndAHalfTimesDamage(int pen, int armor)
        {
            var o = ArmorPenetration.Resolve(8, pen, armor);
            Assert.That(o.Result, Is.EqualTo(PenetrationResult.Overpenetrating));
            Assert.That(o.Damage, Is.EqualTo(12));
            Assert.That(o.BonusEffectsApply, Is.True);
        }

        [Test]
        public void FractionsRoundDown()
        {
            Assert.That(ArmorPenetration.Resolve(5, 0, 1).Damage, Is.EqualTo(2), "50% of 5 = 2.5 → 2");
            Assert.That(ArmorPenetration.Resolve(5, 2, 1).Damage, Is.EqualTo(7), "150% of 5 = 7.5 → 7");
        }

        [Test]
        public void UsesTheWeaponsPenetrationAndTheTargetsArmor()
        {
            var created = new List<Object>();
            var rifleT = ScriptableObject.CreateInstance<WeaponTemplate>();
            rifleT.penetration = 1;
            var heavyT = ScriptableObject.CreateInstance<SoldierTemplate>();
            heavyT.armorLevel = 3;
            created.Add(rifleT);
            created.Add(heavyT);
            var go = new GameObject("Target");
            created.Add(go);
            var target = go.AddComponent<Soldier>();
            target.Configure(heavyT, false);

            var o = ArmorPenetration.Resolve(6, new Weapon(rifleT), target);
            Assert.That(o.Result, Is.EqualTo(PenetrationResult.Blocked));
            Assert.That(o.Damage, Is.EqualTo(0));

            foreach (var c in created) Object.DestroyImmediate(c);
        }
    }
}
