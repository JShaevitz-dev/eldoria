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

        [UnitySetUp]
        public IEnumerator DeployTwoFullArmies()
        {
            ArmySelection.Set(
                new[] { Unit("Field Commander"), Unit("Battlefield Priest"), Unit("Sniper"), Unit("Powered Armor Squad"), Unit("Machine Gun Team"), Unit("Infantry Squad") },
                new[] { Unit("Field Commander"), Unit("Infantry Squad"), Unit("Infantry Squad"), Unit("Machine Gun Team"), Unit("Machine Gun Team"), Unit("Sniper") });
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
                { "Field Commander", "Battlefield Priest", "Sniper", "Powered Armor Squad", "Machine Gun Team", "Infantry Squad" }));
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
        public void TheAIsSoldiersWearTheEnemyColours()
        {
            var catalog = UnitCatalog.Load();
            foreach (var unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {
                var mats = unit.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).ToList();
                if (unit.Side == Side.AI) Assert.That(mats.Contains(catalog.playerArmor), Is.False, unit.DisplayName);
                if (unit.Side == Side.AI && unit.Template.displayName == "Infantry Squad") Assert.That(mats.Contains(catalog.enemyArmor), Is.True);
                if (unit.Side == Side.Player) Assert.That(mats.Contains(catalog.enemyArmor), Is.False, unit.DisplayName);
            }
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
