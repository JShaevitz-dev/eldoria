using System.Collections;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Eldoria.Tests
{
    /// <summary>The optional turn timer: off and 10 minutes by default; when on, the player's turn ends when time runs out.</summary>
    public class TurnTimerPlayTests
    {
        private bool savedEnabled;
        private float savedMinutes;
        private MovementController controller;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            savedEnabled = TurnTimerSettings.Enabled;
            savedMinutes = TurnTimerSettings.Minutes;
            SceneManager.LoadScene("UrbanDemo");
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<MovementController>();
        }

        [TearDown]
        public void RestoreSettings()
        {
            TurnTimerSettings.Enabled = savedEnabled;
            TurnTimerSettings.Minutes = savedMinutes;
        }

        private IEnumerator FinishAITurn()
        {
            controller.GetComponent<AITurnController>().Skip();
            float waited = 0f;
            while (controller.IsAITurn && waited < 20f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.That(controller.IsAITurn, Is.False);
        }

        [Test]
        public void TheTimerIsOffAnd10MinutesByDefault()
        {
            TurnTimerSettings.ResetToDefaults();
            Assert.That(TurnTimerSettings.Enabled, Is.False);
            Assert.That(TurnTimerSettings.Minutes, Is.EqualTo(10f));
            controller.ResetTurnTimer();
            Assert.That(controller.TurnTimeLeft, Is.Null, "no countdown when it's off");
        }

        [UnityTest]
        public IEnumerator WhenTimeRunsOutThePlayersTurnEnds()
        {
            TurnTimerSettings.Enabled = true;
            TurnTimerSettings.Minutes = 1f / 60f;   // 1 second, for the test
            controller.ResetTurnTimer();
            Assert.That(controller.TurnTimeLeft, Is.EqualTo(1f).Within(0.01f));

            yield return new WaitForSeconds(1.5f);
            Assert.That(controller.IsAITurn, Is.True, "time's up: the AI's turn has begun");

            TurnTimerSettings.Minutes = 10f;         // so it doesn't run out again straight away
            yield return FinishAITurn();
            Assert.That(Turns.Current, Is.EqualTo(2));
            Assert.That(controller.TurnTimeLeft, Is.EqualTo(600f).Within(1f), "a fresh 10 minutes for the next turn");
        }

        [UnityTest]
        public IEnumerator TheTimerPausesDuringTheAIsTurn()
        {
            TurnTimerSettings.Enabled = true;
            TurnTimerSettings.Minutes = 5f;
            controller.ResetTurnTimer();
            controller.EndTurn();
            Assert.That(controller.IsAITurn, Is.True);
            float before = controller.TurnTimeLeft.Value;
            yield return new WaitForSeconds(0.5f);
            Assert.That(controller.TurnTimeLeft.Value, Is.EqualTo(before), "not counting while the AI plays");
            yield return FinishAITurn();
        }

        [UnityTest]
        public IEnumerator TimeUpInTheMiddleOfAMoveFinishesTheMoveThenEndsTheTurn()
        {
            TurnTimerSettings.Enabled = true;
            TurnTimerSettings.Minutes = 10f;
            controller.ResetTurnTimer();
            var squad = controller.PlayerUnits.First(u => u.DisplayName == "Infantry Squad 2");
            Assert.That(controller.SelectUnit(squad), Is.True);
            Assert.That(controller.OrderLeaderMove(squad.Leader.Position + new Vector3(0f, 0f, 9f)), Is.True);
            Assert.That(controller.Activation.Phase, Is.EqualTo(MovePhase.MoveFollowers), "the rest of the squad hasn't moved yet");

            controller.TurnTimeUp();
            Assert.That(controller.IsAITurn, Is.True);
            Assert.That(controller.HasActed(squad) || controller.ActiveUnit == null, Is.True);
            yield return FinishAITurn();
            Assert.That(squad.OutOfCohesion(), Is.Empty, "the squad ended its move in cohesion");
        }

        [Test]
        public void TheMenuStepsThroughTheTimerLengths()
        {
            TurnTimerSettings.Minutes = 10f;
            MainMenu.StepMinutes(+1);
            Assert.That(TurnTimerSettings.Minutes, Is.EqualTo(15f));
            MainMenu.StepMinutes(-1);
            MainMenu.StepMinutes(-1);
            Assert.That(TurnTimerSettings.Minutes, Is.EqualTo(5f));
            for (int i = 0; i < 10; i++) MainMenu.StepMinutes(-1);
            Assert.That(TurnTimerSettings.Minutes, Is.EqualTo(1f), "stops at the shortest");
        }
    }
}
