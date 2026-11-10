using System.Linq;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>
    /// Machine Gun Team: 1 Sergeant (Pistol), 3 Troopers (SMG and Pistol), 1 Machine Gunner (Machine Gun); no Frag Grenade.
    /// </summary>
    public class MachineGunTeamTests
    {
        private const string Data = "Assets/_Project/Data";
        private Unit spawned;

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn();
        }

        [TearDown]
        public void TearDown()
        {
            if (spawned != null) Object.DestroyImmediate(spawned.gameObject);
            Turns.ResetCounter();
        }

        private static UnitTemplate Template() => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/MachineGunTeam.asset");

        private static string[] WeaponNames(SoldierTemplate s) => s.weapons.Select(w => w.displayName).ToArray();

        [Test]
        public void TheTeamIsASergeantThreeSmgTroopersAndAMachineGunner()
        {
            var team = Template();
            Assert.That(team.displayName, Is.EqualTo("Machine Gun Team"));
            Assert.That(team.Size, Is.EqualTo(5));

            Assert.That(team.leader.displayName, Is.EqualTo("Sergeant"));
            Assert.That(WeaponNames(team.leader), Is.EqualTo(new[] { "Pistol" }));

            var troopers = team.members.Single(m => m.soldier.displayName == "Trooper");
            Assert.That(troopers.count, Is.EqualTo(3));
            Assert.That(WeaponNames(troopers.soldier), Is.EqualTo(new[] { "SMG", "Pistol" }));
            Assert.That(troopers.soldier.PrimaryWeapon.displayName, Is.EqualTo("SMG"));

            var gunner = team.members.Single(m => m.soldier.displayName == "Machine Gunner");
            Assert.That(gunner.count, Is.EqualTo(1));
            Assert.That(WeaponNames(gunner.soldier), Is.EqualTo(new[] { "Machine Gun" }));
        }

        [Test]
        public void TheyHaveTheInfantrySoldiersStatsAndNoGrenade()
        {
            foreach (var s in Template().Roster().Distinct())
            {
                Assert.That((s.health, s.armorLevel, s.accuracy, s.movementRange), Is.EqualTo((5, 1, 75, 10f)), s.displayName);
                Assert.That(s.HasKeyword("Biological"), Is.True);
                Assert.That(s.abilities.Any(a => a != null && a.displayName == "Frag Grenade"), Is.False, $"{s.displayName}: no grenade");
                Assert.That(s.prefab, Is.Not.Null, $"{s.displayName} has a model");
            }
        }

        [Test]
        public void ASpawnedTeamFightsWithItsOwnWeapons()
        {
            spawned = UnitFactory.Spawn(Template(), Side.Player, Vector3.zero, Quaternion.identity);
            Assert.That(spawned.Soldiers.Count, Is.EqualTo(5));
            Assert.That(spawned.Leader.name, Is.EqualTo("Sergeant"));
            Assert.That(spawned.Leader.WeaponFor(null).Name, Is.EqualTo("Pistol"));
            Assert.That(spawned.Soldiers.Count(s => s.WeaponFor(null).Name == "SMG"), Is.EqualTo(3));
            var gunner = spawned.Soldiers.Single(s => s.name.StartsWith("Machine Gunner"));   // "Machine Gunner_01"
            Assert.That(gunner.WeaponFor(null).Name, Is.EqualTo("Machine Gun"));
            Assert.That(Rules.ActiveAbilities.UnitAbilities(spawned), Is.Empty, "no abilities to use");

            // Deployed: once the gunner has moved, the machine gun can't be used (and they carry nothing else).
            gunner.GainKeyword(KeywordLibrary.Get(KeywordLibrary.Moved), Turns.NextTurn);
            Assert.That(gunner.WeaponFor(null), Is.Null);
        }

        [Test]
        public void ItIsntInTheDemoArmiesYet()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/UrbanDemo.unity");
            string text = System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(scene));
            Assert.That(text.Contains(AssetDatabase.AssetPathToGUID($"{Data}/Units/MachineGunTeam.asset")), Is.False);
        }
    }
}
