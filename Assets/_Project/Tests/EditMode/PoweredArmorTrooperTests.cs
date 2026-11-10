using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>Trooper and Sergeant in Powered Armor (Health 10, Armor Level 2, Accuracy 75%, Movement 6 m; Biological), and the Powered Armor Squad.</summary>
    public class PoweredArmorTrooperTests
    {
        private static SoldierTemplate Template() =>
            AssetDatabase.LoadAssetAtPath<SoldierTemplate>("Assets/_Project/Data/Soldiers/PoweredArmorTrooper.asset");

        [Test]
        public void TheyHaveTheDesignedStats()
        {
            var t = Template();
            Assert.That(t.displayName, Is.EqualTo("Trooper in Powered Armor"));
            Assert.That((t.health, t.armorLevel, t.accuracy, t.movementRange), Is.EqualTo((10, 2, 75, 6f)));
            Assert.That(t.HasKeyword("Biological"), Is.True);
            Assert.That(t.PrimaryWeapon.displayName, Is.EqualTo("Assault Rifle"), "armed in the Powered Armor Squad");
            Assert.That(t.abilities, Is.Empty);
            Assert.That(t.prefab, Is.Not.Null);
        }

        [Test]
        public void TheSergeantInPoweredArmorHasTheSameStats()
        {
            var sgt = AssetDatabase.LoadAssetAtPath<SoldierTemplate>("Assets/_Project/Data/Soldiers/PoweredArmorSergeant.asset");
            var t = Template();
            Assert.That(sgt.displayName, Is.EqualTo("Sergeant in Powered Armor"));
            Assert.That((sgt.health, sgt.armorLevel, sgt.accuracy, sgt.movementRange), Is.EqualTo((t.health, t.armorLevel, t.accuracy, t.movementRange)));
            Assert.That((sgt.health, sgt.armorLevel, sgt.accuracy, sgt.movementRange), Is.EqualTo((10, 2, 75, 6f)));
            Assert.That(sgt.HasKeyword("Biological"), Is.True);
            Assert.That(sgt.PrimaryWeapon.displayName, Is.EqualTo("Assault Rifle"));
            Assert.That(sgt.prefab, Is.Not.Null);
            Assert.That(sgt.prefab, Is.Not.EqualTo(t.prefab), "its own model, with sergeant markings");
        }

        [Test]
        public void ThePoweredArmorSquadIsASergeantAndFourTroopersWithRifles()
        {
            var squad = AssetDatabase.LoadAssetAtPath<UnitTemplate>("Assets/_Project/Data/Units/PoweredArmorSquad.asset");
            Assert.That(squad.displayName, Is.EqualTo("Powered Armor Squad"));
            Assert.That(squad.Size, Is.EqualTo(5));
            Assert.That(squad.leader.displayName, Is.EqualTo("Sergeant in Powered Armor"));
            var troopers = squad.members.Single();
            Assert.That((troopers.soldier.displayName, troopers.count), Is.EqualTo(("Trooper in Powered Armor", 4)));
            foreach (var s in squad.Roster())
            {
                Assert.That(s.weapons.Select(w => w.displayName), Is.EqualTo(new[] { "Assault Rifle" }), s.displayName);
                Assert.That(s.abilities.Any(a => a != null && a.displayName == "Frag Grenade"), Is.False, "no grenade");
            }

            var unit = UnitFactory.Spawn(squad, Side.Player, Vector3.zero, Quaternion.identity);
            try
            {
                Assert.That(unit.Soldiers.Count, Is.EqualTo(5));
                Assert.That(unit.Leader.name, Is.EqualTo("Sergeant in Powered Armor"));
                Assert.That(unit.Soldiers.All(s => s.WeaponFor(null).Name == "Assault Rifle"), Is.True);
                Assert.That(unit.Leader.Move, Is.EqualTo(6f), "the squad moves at the leader's 6 m");
            }
            finally
            {
                Object.DestroyImmediate(unit.gameObject);
            }
        }

        [Test]
        public void TheyMove6MetresOrSprint12()
        {
            var go = new GameObject("Unit");
            try
            {
                var unit = go.AddComponent<Unit>();
                var s = new GameObject("PA");
                s.transform.SetParent(go.transform);
                var soldier = s.AddComponent<Soldier>();
                soldier.Configure(Template(), true);
                unit.RefreshSoldiers();
                Assert.That((soldier.Move, soldier.Sprint), Is.EqualTo((6f, 12f)));

                // Armor Level 2: an Assault Rifle (Penetration 1) is 1 below → 50% damage.
                Assert.That(ArmorPenetration.Resolve(4, 1, soldier.ArmorLevel).Damage, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
