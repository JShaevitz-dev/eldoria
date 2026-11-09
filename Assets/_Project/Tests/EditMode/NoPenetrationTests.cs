using System.Collections.Generic;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoira.Tests
{
    /// <summary>An attack with no penetration value always does 100% damage, regardless of armor.</summary>
    public class NoPenetrationTests
    {
        private readonly List<Object> created = new List<Object>();

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
            Turns.StartNextTurn();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Soldier MakeSoldier(SoldierTemplate t, Side side, Vector3 pos)
        {
            var go = new GameObject("Unit");
            created.Add(go);
            var unit = go.AddComponent<Unit>();
            unit.Configure(null, side, "Unit");
            var s = new GameObject("Soldier");
            s.transform.SetParent(go.transform);
            s.transform.position = pos;
            s.AddComponent<Soldier>().Configure(t, true);
            unit.RefreshSoldiers();
            return unit.Soldiers[0];
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(20)]
        public void NoPenetrationValueIsAlways100Percent(int armor)
        {
            var outcome = ArmorPenetration.Resolve(7, (int?)null, armor);
            Assert.That(outcome.Result, Is.EqualTo(PenetrationResult.IgnoresArmor));
            Assert.That(outcome.Damage, Is.EqualTo(7));
            Assert.That(outcome.BonusEffectsApply, Is.True);
        }

        [Test]
        public void ANumericPenetrationOf0IsStillComparedWithArmor()
        {
            Assert.That(ArmorPenetration.Resolve(4, 0, 1).Damage, Is.EqualTo(2), "0 is a value: 1 below armor → 50%");
            Assert.That(ArmorPenetration.Resolve(4, 0, 2).Damage, Is.EqualTo(0));
        }

        [Test]
        public void ShootingWithAWeaponThatHasNoPenetrationIgnoresArmorAndArmorKeywords()
        {
            var flamer = Make<WeaponTemplate>();
            flamer.baseDamage = 4;
            flamer.hasPenetration = false;
            flamer.penetration = 0; // ignored
            var shooterT = Make<SoldierTemplate>();
            shooterT.accuracy = 100;
            shooterT.weapons.Add(flamer);
            var tankT = Make<SoldierTemplate>();
            tankT.health = 20;
            tankT.armorLevel = 6;

            var plating = Make<Keyword>();
            plating.displayName = "Plating";
            plating.interactions.Add(new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always, effect = KeywordEffect.ArmorBonus, amount = 3
            });

            var shooter = MakeSoldier(shooterT, Side.Player, Vector3.zero);
            var tank = MakeSoldier(tankT, Side.AI, new Vector3(0, 0, 10));
            tank.GainKeyword(plating);

            var r = Attack.Resolve(shooter, shooter.Weapons[0], tank, false, new System.Random(1));
            Assert.That(r.Damage.Penetration.Result, Is.EqualTo(PenetrationResult.IgnoresArmor));
            Assert.That(r.DamageDealt, Is.EqualTo(4));
            Assert.That(tank.Health, Is.EqualTo(16));
        }

        [Test]
        public void AnAreaAttackWithNoPenetrationIgnoresArmor()
        {
            var blastT = Make<WeaponTemplate>();
            blastT.baseDamage = 2;
            blastT.hasPenetration = false;
            var ability = Make<Ability>();
            ability.kind = AbilityKind.Active;
            ability.targets = AbilityTarget.Point;
            ability.affects = AuraAffects.Everyone;
            ability.range = 10f;
            ability.areaRadius = 2.5f;
            ability.areaWeapon = blastT;
            var throwerT = Make<SoldierTemplate>();
            throwerT.abilities = new List<Ability> { ability };
            var armouredT = Make<SoldierTemplate>();
            armouredT.health = 5;
            armouredT.armorLevel = 4;

            var thrower = MakeSoldier(throwerT, Side.Player, Vector3.zero);
            var target = MakeSoldier(armouredT, Side.AI, new Vector3(0, 0, 8));
            Assert.That(ActiveAbilities.Use(thrower, ability, targetPoint: new Vector3(0, 0, 8)).Used, Is.True);
            Assert.That(target.Health, Is.EqualTo(3));
        }

        [Test]
        public void ExistingWeaponsKeepTheirPenetration()
        {
            foreach (var path in new[] { "AssaultRifle", "Pistol", "FragGrenade" })
            {
                var w = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"Assets/_Project/Data/Weapons/{path}.asset");
                Assert.That(w.hasPenetration, Is.True, path);
                Assert.That(w.PenetrationValue, Is.EqualTo(w.penetration), path);
            }
        }
    }
}
