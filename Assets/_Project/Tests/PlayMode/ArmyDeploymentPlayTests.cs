using System.Collections;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Eldoria.Tests
{
    /// <summary>Chosen armies replace the scene's armies on the Urban Demo map, in their deployment zones.</summary>
    public class ArmyDeploymentPlayTests
    {
        private static UnitTemplate Unit(string name) => UnitCatalog.Load().Available.First(u => u.displayName == name);
        private static Faction Faction(string name) => UnitCatalog.Load().Factions.First(f => f.displayName == name);

        [UnitySetUp]
        public IEnumerator DeployTwoFullArmies()
        {
            ArmySelection.Set(
                new[] { Unit("Field Commander"), Unit("Battle Priest"), Unit("Sniper"), Unit("Powered Armor Squad"), Unit("Machine Gun Team"), Unit("Infantry Squad") },
                new[] { Unit("Field Commander"), Unit("Infantry Squad"), Unit("Infantry Squad"), Unit("Machine Gun Team"), Unit("Machine Gun Team"), Unit("Sniper") },
                Faction("United States of Liberteria"), Faction("The Draksa Reich"));
            SceneManager.LoadScene(MainMenu.GameScene);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void ForgetArmies() => ArmySelection.Clear();

        [Test]
        public void ThePlayersUnitsAreDeployedInListOrder()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            Assert.That(controller.PlayerUnits.Select(u => u.DisplayName), Is.EqualTo(new[]
                { "Field Commander", "Battle Priest", "Sniper", "Powered Armor Squad", "Machine Gun Team", "Infantry Squad" }));
            // Left to right across the deployment zone.
            var xs = controller.PlayerUnits.Select(u => u.Leader.Position.x).ToList();
            Assert.That(xs, Is.Ordered);
        }

        [Test]
        public void TheAIsUnitsAreNumberedAndMarkedAsTheEnemy()
        {
            var names = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Side == Side.AI)
                .Select(u => u.DisplayName).OrderBy(n => n).ToList();
            Assert.That(names, Is.EqualTo(new[]
            {
                "Field Commander (Enemy)", "Infantry Squad 1 (Enemy)", "Infantry Squad 2 (Enemy)",
                "Machine Gun Team 1 (Enemy)", "Machine Gun Team 2 (Enemy)", "Sniper (Enemy)",
            }));
        }

        [Test]
        public void OnlyTheChosenArmiesAreOnTheBattlefield()
        {
            Assert.That(Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Length, Is.EqualTo(12), "the scene's own armies are gone");
            Assert.That(Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None).Length,
                Is.EqualTo(ArmySelection.PlayerArmy.Concat(ArmySelection.AIArmy).Sum(u => u.Size)));
        }

        [Test]
        public void EverySoldierStartsInTheirSidesDeploymentZoneOnTheGround()
        {
            var zones = Object.FindObjectsByType<DeploymentZone>(FindObjectsSortMode.None);
            foreach (var unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                var zone = zones.Single(z => z.Side == unit.Side);
                foreach (var s in unit.Soldiers)
                {
                    Assert.That(zone.Contains(s.Position), Is.True, $"{unit.DisplayName}: {s.name} at {s.Position}");
                    Assert.That(Mathf.Abs(s.Position.y), Is.LessThan(0.6f), $"{s.name} stands on the ground");
                }
            }
        }

        [Test]
        public void EachUnitBelongsToItsArmysFactionAndItsWeaponsKnowIt()
        {
            foreach (var unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                var faction = unit.Side == Side.Player ? ArmySelection.PlayerFaction : ArmySelection.AIFaction;
                Assert.That(unit.Faction, Is.SameAs(faction), unit.DisplayName);
                foreach (var w in unit.Soldiers.SelectMany(s => s.Weapons))
                    Assert.That(w.Faction, Is.SameAs(faction), $"{unit.DisplayName}: {w.Name}");
            }
        }

        [Test]
        public void EachArmyWearsItsFactionsColour()
        {
            var catalog = UnitCatalog.Load();
            var primary = catalog.primaryColorMaterial;
            Color blue = primary.GetColor("_BaseColor");
            foreach (var unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                var faction = unit.Side == Side.Player ? ArmySelection.PlayerFaction : ArmySelection.AIFaction;
                var primaryParts = unit.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterial == primary).ToList();
                if (unit.Template.displayName != "Battle Priest")
                    Assert.That(primaryParts, Is.Not.Empty, $"{unit.DisplayName} has primary-coloured parts");
                foreach (var r in primaryParts)
                    Assert.That(Vector4.Distance(PrimaryColor.Of(r), faction.color), Is.LessThan(0.002f), $"{unit.DisplayName}: {r.name}");
            }
            Assert.That(Vector4.Distance(primary.GetColor("_BaseColor"), blue), Is.LessThan(0.002f), "the shared material itself is never changed");
            Assert.That(Vector4.Distance(blue, new Color(0.18f, 0.32f, 0.62f)), Is.LessThan(0.002f), "still the model's own blue");
        }

        [UnityTest]
        public IEnumerator TheBattlePlaysWithTheChosenArmies()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            for (int turn = 0; turn < 3 && !Victory.IsOver; turn++)
            {
                controller.EndTurn(instant: true);
                yield return null;
            }
            Assert.That(Turns.Current, Is.GreaterThanOrEqualTo(3), "the AI played its turns with its own army");
        }
    }
}
