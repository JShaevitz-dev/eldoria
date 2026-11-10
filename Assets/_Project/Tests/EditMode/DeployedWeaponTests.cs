using System.Collections.Generic;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>Deployed: the weapon can't be used in a volley if the soldier holding it has moved this turn.</summary>
    public class DeployedWeaponTests
    {
        private readonly List<Object> created = new List<Object>();
        private WeaponTemplate mgT, pistolT;
        private SoldierTemplate gunnerT, gunnerWithPistolT, targetT;

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
            var deployed = Make<Keyword>();
            deployed.displayName = "Deployed";
            var pistolKw = Make<Keyword>();
            pistolKw.displayName = "Pistol";

            mgT = Make<WeaponTemplate>();
            mgT.displayName = "Machine Gun";
            mgT.baseDamage = 6; mgT.penetration = 1; mgT.range = 20f;
            mgT.keywords.Add(deployed);
            pistolT = Make<WeaponTemplate>();
            pistolT.displayName = "Pistol";
            pistolT.baseDamage = 2; pistolT.range = 10f;
            pistolT.keywords.Add(pistolKw);

            gunnerT = Soldier(mgT);
            gunnerWithPistolT = Soldier(mgT, pistolT);
            gunnerWithPistolT.primaryWeapon = mgT;
            targetT = Soldier(pistolT);
        }

        private SoldierTemplate Soldier(params WeaponTemplate[] weapons)
        {
            var t = Make<SoldierTemplate>();
            t.health = 20;
            t.accuracy = 100;
            t.movementRange = 10f;
            t.weapons.AddRange(weapons);
            return t;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Unit MakeUnit(string name, Side side, SoldierTemplate t, params Vector3[] positions)
        {
            var go = new GameObject(name);
            created.Add(go);
            var unit = go.AddComponent<Unit>();
            unit.Configure(null, side, name);
            for (int i = 0; i < positions.Length; i++)
            {
                var s = new GameObject($"{name}_{i}");
                s.transform.SetParent(go.transform);
                s.transform.position = positions[i];
                s.AddComponent<Soldier>().Configure(t, i == 0);
            }
            unit.RefreshSoldiers();
            return unit;
        }

        [Test]
        public void AGunnerWhoHasntMovedFiresTheMachineGun()
        {
            var gunners = MakeUnit("Gunners", Side.Player, gunnerT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 15));
            new UnitMoveActivation(gunners, gunners.Soldiers).HoldLeader();   // holding position isn't moving
            Assert.That(gunners.Soldiers[0].WeaponFor(null).Name, Is.EqualTo("Machine Gun"));
            var r = UnitShooting.Resolve(gunners, them, false, new System.Random(1));
            Assert.That(r.Shots[0].Attack, Is.Not.Null);
            Assert.That(r.Shots[0].Attack.DamageDealt, Is.GreaterThan(0));
        }

        [Test]
        public void AGunnerWhoMovedCantUseTheMachineGunInThatVolley()
        {
            var gunners = MakeUnit("Gunners", Side.Player, gunnerT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 15));
            Assert.That(new UnitMoveActivation(gunners, gunners.Soldiers).TryMoveLeader(new Vector3(0, 0, 2), out _), Is.True);
            Assert.That(gunners.Soldiers[0].HasKeyword("Moved"), Is.True);

            Assert.That(gunners.Soldiers[0].WeaponFor(null), Is.Null);
            var r = UnitShooting.Resolve(gunners, them, false, new System.Random(1));
            Assert.That(r.Shots[0].Attack, Is.Null, "no shot");
            Assert.That(r.Shots[0].Note, Does.Contain("Deployed"));
            Assert.That(UnitShooting.SoldiersAbleToShoot(gunners, them), Is.EqualTo(0), "so a shoot order would be refused");
        }

        [Test]
        public void AGunnerWhoMovedUsesAnotherWeaponInstead()
        {
            var gunner = MakeUnit("Gunners", Side.Player, gunnerWithPistolT, Vector3.zero).Soldiers[0];
            gunner.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Turns.NextTurn);
            Assert.That(gunner.WeaponFor(null).Name, Is.EqualTo("Pistol"));
        }

        [Test]
        public void ItCanBeUsedAgainNextTurnIfTheGunnerStaysPut()
        {
            var gunner = MakeUnit("Gunners", Side.Player, gunnerT, Vector3.zero).Soldiers[0];
            gunner.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Turns.NextTurn);
            Assert.That(gunner.WeaponFor(null), Is.Null);
            Turns.StartNextTurn();
            Assert.That(gunner.WeaponFor(null).Name, Is.EqualTo("Machine Gun"));
        }

        [Test]
        public void TheMachineGunIsAsDesigned()
        {
            var mg = AssetDatabase.LoadAssetAtPath<WeaponTemplate>("Assets/_Project/Data/Weapons/MachineGun.asset");
            Assert.That(mg.displayName, Is.EqualTo("Machine Gun"));
            Assert.That((mg.baseDamage, mg.PenetrationValue, mg.accuracyBonus, mg.range), Is.EqualTo((6, (int?)1, 0, 20f)));
            Assert.That(mg.HasKeyword("Deployed"), Is.True);
            var deployed = AssetDatabase.LoadAssetAtPath<Keyword>("Assets/_Project/Data/Keywords/Deployed.asset");
            Assert.That((deployed.appliesTo, deployed.category), Is.EqualTo((KeywordTarget.Weapon, KeywordCategory.Trait)));
        }
    }
}
