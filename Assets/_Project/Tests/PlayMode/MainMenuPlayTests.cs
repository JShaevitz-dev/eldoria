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
        public IEnumerator PlayOpensTheArmyBuilderOnTheFactionChoice()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.Builder, Is.Null);

            menu.OpenArmyBuilder();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"), "still on the menu, building");
            Assert.That(menu.Builder.Faction, Is.Null, "a faction is chosen first");
            Assert.That(menu.Builder.Factions.Select(f => f.displayName), Is.EqualTo(new[]
                { "United States of Liberteria", "United Kingdom of Aelfland", "The Draksa Reich" }));
            Assert.That(menu.Builder.Available, Is.Empty, "no units until a faction is chosen");
            Assert.That(menu.Builder.Add(UnitCatalog.Load().Available.First()), Does.Contain("faction"));
            Assert.That(menu.Builder.Confirm(), Is.False);

            menu.Builder.ChooseFaction(menu.Builder.Factions[1]);
            Assert.That(menu.Builder.Faction.displayName, Is.EqualTo("United Kingdom of Aelfland"));
            Assert.That(menu.Builder.Available.Count, Is.EqualTo(10), "every unit but Liberteria's Battle Mage and the Reich's Stahl Colossus");
            Assert.That(menu.Builder.Army.Select(u => u.displayName), Is.EqualTo(new[]
                { "Infantry Squad", "Field Commander", "Infantry Squad", "Battle Priest", "Infantry Squad" }));
        }

        [UnityTest]
        public IEnumerator AnInvalidArmyIsRefusedAndStaysOnTheMenu()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.OpenArmyBuilder();
            var builder = menu.Builder;
            builder.ChooseFaction(builder.Factions[0]);
            builder.Clear();
            Assert.That(builder.Add(builder.Available.First(u => u.role == UnitRole.Infantry)), Is.Null);

            Assert.That(builder.Confirm(), Is.False);
            Assert.That(builder.Problems.Single(), Does.Contain("Commander"));
            Assert.That(MainMenu.StartBattle(builder.Faction, builder.Army), Is.Not.Empty);
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
            builder.ChooseFaction(builder.Factions[2]);
            builder.Clear();
            UnitTemplate Unit(string name) => builder.Available.First(u => u.displayName == name);
            foreach (var name in new[] { "Field Commander", "Sniper", "Machine Gun Team", "Machine Gun Team" })
                Assert.That(builder.Add(Unit(name)), Is.Null, name);
            Assert.That(builder.Add(Unit("Sniper")), Does.Contain("Already in the army"));
            Assert.That(builder.Confirm(), Is.True);
            var army = builder.Army.ToList();

            Assert.That(MainMenu.StartBattle(builder.Faction, army), Is.Empty);
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("UrbanDemo"));
            var controller = Object.FindFirstObjectByType<MovementController>();
            Assert.That(controller.PlayerUnits.Select(u => u.DisplayName), Is.EqualTo(new[]
                { "Field Commander", "Sniper", "Machine Gun Team 1", "Machine Gun Team 2" }));
            Assert.That(ArmySelection.PlayerFaction.displayName, Is.EqualTo("The Draksa Reich"));
            Assert.That(ArmySelection.AIFaction, Is.Not.Null, "the AI picks a faction too");
            Assert.That(ArmyRules.IsValid(ArmySelection.AIArmy, ArmySelection.AIFaction), Is.True, "the AI's army follows the same rules");
            var aiUnits = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).Where(u => u.Side == Side.AI).ToList();
            Assert.That(aiUnits.Count, Is.EqualTo(ArmySelection.AIArmy.Count));
            Assert.That(Turns.Current, Is.EqualTo(1), "a fresh battle on turn 1");
            Assert.That(Victory.IsOver, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayChoosesAMapThenTheArmyFightsThere()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.OpenMapChoice();
            Assert.That((menu.ChoosingMap, menu.Builder), Is.EqualTo((true, (ArmyBuilderScreen)null)), "the map comes first");
            Assert.That(Battlefields.All.Select(m => m.Name), Is.EqualTo(new[] { "Urban Demo", "English Countryside" }));

            menu.ChooseMap(Battlefields.EnglishCountryside);
            Assert.That(menu.ChoosingMap, Is.False);
            Assert.That(menu.Builder, Is.Not.Null, "then the army builder");
            menu.Builder.ChooseFaction(menu.Builder.Factions[1]);
            Assert.That(MainMenu.StartBattle(menu.Builder.Faction, menu.Builder.Army.ToList()), Is.Empty);
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Countryside"));
            var zones = Object.FindObjectsByType<DeploymentZone>(FindObjectsSortMode.None);
            foreach (var unit in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
            foreach (var s in unit.Soldiers)
                Assert.That(zones.Single(z => z.Side == unit.Side).Contains(s.Position), Is.True, $"{unit.DisplayName}: {s.name}");
            Assert.That(Object.FindFirstObjectByType<MovementController>().PlayerUnits.Count, Is.EqualTo(ArmySelection.PlayerArmy.Count));
        }

        [UnityTest]
        public IEnumerator TheGameCanReturnToTheMenu()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.OpenArmyBuilder();
            menu.Builder.ChooseFaction(menu.Builder.Factions[0]);
            MainMenu.StartBattle(menu.Builder.Faction, menu.Builder.Army.ToList());
            yield return null;
            MainMenu.BackToMenu();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }
    }
}
