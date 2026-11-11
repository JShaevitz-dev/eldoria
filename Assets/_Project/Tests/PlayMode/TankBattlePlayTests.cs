using System.Collections;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Eldoria.Tests
{
    /// <summary>Medium Tanks deploy and fight on both maps (they're much bigger than a soldier).</summary>
    public class TankBattlePlayTests
    {
        private static UnitTemplate Unit(string name) => UnitCatalog.Load().Available.First(u => u.displayName == name);

        [TearDown]
        public void ForgetArmies() => ArmySelection.Clear();

        [UnityTest]
        public IEnumerator TanksDeployAndTheBattlePlaysOn([Values("UrbanDemo", "Countryside")] string map)
        {
            var army = new[] { Unit("Field Commander"), Unit("Medium Tank"), Unit("Infantry Squad"), Unit("Medium Tank") };
            ArmySelection.Set(army, army);
            SceneManager.LoadScene(map);
            yield return null;
            yield return null;

            var tanks = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Template.displayName == "Medium Tank").ToList();
            Assert.That(tanks.Count, Is.EqualTo(4));
            foreach (var t in tanks)
            {
                var s = t.Leader;
                Assert.That(BattlefieldNavigation.Measurer.TryGetStandPoint(s.Position, s.Radius, s.Height, out _, out var why), Is.True,
                    $"{t.DisplayName} has room to stand: {why}");
            }

            var controller = Object.FindFirstObjectByType<MovementController>();
            for (int turn = 0; turn < 4 && !Victory.IsOver; turn++)
            {
                controller.EndTurn(instant: true);
                yield return null;
            }
            Assert.That(Turns.Current, Is.GreaterThanOrEqualTo(4), "the AI moved and fought with its tanks");
            Assert.That(tanks.Where(t => t.Side == Side.AI && t.Leader != null).All(t => !t.Leader.HasKeyword(CloseRange.StatusKeyword)), Is.True);
        }
    }
}
