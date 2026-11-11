using System.Collections;
using System.Collections.Generic;
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
    /// <summary>The main menu: the first scene; Play opens the army builder; a valid army starts a game; back to the menu.</summary>
    public class MainMenuPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadMenu()
        {
            ArmySelection.Clear();
            MainMenu.ForgetLastArmy();
            SceneManager.LoadScene(MainMenu.MenuScene);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void ForgetArmies() => ArmySelection.Clear();

        [UnityTest]
        public IEnumerator PlayOpensTheArmyBuilderStartingWithTheDemoArmy()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.Builder, Is.Null);

            menu.OpenArmyBuilder();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"), "still on the menu, building");
            Assert.That(menu.Builder.Available.Count, Is.EqualTo(7), "every unit in the game");
            Assert.That(menu.Builder.Army.Select(u => u.displayName), Is.EqualTo(new[]
                { "Infantry Squad", "Field Commander", "Infantry Squad", "Battlefield Priest", "Infantry Squad" }));
        }

        [UnityTest]
        public IEnumerator AnInvalidArmyIsRefusedAndStaysOnTheMenu()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.OpenArmyBuilder();
            var builder = menu.Builder;
            builder.Clear();
            Assert.That(builder.Add(builder.Available.First(u => u.role == UnitRole.Infantry)), Is.Null);

            Assert.That(builder.Confirm(), Is.False);
            Assert.That(builder.Problems.Single(), Does.Contain("Commander"));
            Assert.That(MainMenu.StartBattle(builder.Army), Is.Not.Empty);
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(ArmySelection.HasArmies, Is.False);
        }

        [UnityTest]
        public IEnumerator AValidArmyIsTakenToTheBattlefieldAgainstAnAIArmy()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.OpenArmyBuilder();
            var builder = menu.Builder;
            builder.Clear();
            UnitTemplate Unit(string name) => builder.Available.First(u => u.displayName == name);
            foreach (var name in new[] { "Field Commander", "Sniper", "Machine Gun Team", "Machine Gun Team" })
                Assert.That(builder.Add(Unit(name)), Is.Null, name);
            Assert.That(builder.Add(Unit("Sniper")), Does.Contain("Already in the army"));
            Assert.That(builder.Confirm(), Is.True);
            var army = builder.Army.ToList();

            Assert.That(MainMenu.StartBattle(army), Is.Empty);
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("UrbanDemo"));
            var controller = Object.FindFirstObjectByType<MovementController>();
            Assert.That(controller.PlayerUnits.Select(u => u.DisplayName), Is.EqualTo(new[]
                { "Field Commander", "Sniper", "Machine Gun Team 1", "Machine Gun Team 2" }));
            Assert.That(ArmyRules.IsValid(ArmySelection.AIArmy), Is.True, "the AI's army follows the same rules");
            var aiUnits = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Side == Side.AI).ToList();
            Assert.That(aiUnits.Count, Is.EqualTo(ArmySelection.AIArmy.Count));
            Assert.That(Turns.Current, Is.EqualTo(1), "a fresh battle on turn 1");
            Assert.That(Victory.IsOver, Is.False);
        }

        [UnityTest]
        public IEnumerator TheGameCanReturnToTheMenu()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.OpenArmyBuilder();
            MainMenu.StartBattle(menu.Builder.Army.ToList());
            yield return null;
            MainMenu.BackToMenu();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }
    }
}
