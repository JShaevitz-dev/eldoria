using System.Collections;
using Eldoria.Gameplay;
using Eldoria.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Eldoria.Tests
{
    /// <summary>The main menu: the first scene, Play starts a game, and the game can come back to it.</summary>
    public class MainMenuPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadMenu()
        {
            SceneManager.LoadScene(MainMenu.MenuScene);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayStartsAGameOnTheDemoMap()
        {
            Assert.That(Object.FindFirstObjectByType<MainMenu>(), Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));

            MainMenu.Play();
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("UrbanDemo"));
            var controller = Object.FindFirstObjectByType<MovementController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(Turns.Current, Is.EqualTo(1), "a fresh battle on turn 1");
            Assert.That(Victory.IsOver, Is.False);
        }

        [UnityTest]
        public IEnumerator TheGameCanReturnToTheMenu()
        {
            MainMenu.Play();
            yield return null;
            MainMenu.BackToMenu();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }
    }
}
