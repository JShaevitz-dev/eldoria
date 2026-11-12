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
    /// <summary>End turn (the button or N) asks first if any of the player's units can still move.</summary>
    public class EndTurnConfirmPlayTests
    {
        private MovementController controller;

        private static UnitTemplate Unit(string name) => UnitCatalog.Load().Available.First(u => u.displayName == name);

        [UnitySetUp]
        public IEnumerator LoadBattle()
        {
            var army = new[] { Unit("Field Commander"), Unit("Infantry Squad") };
            ArmySelection.Set(army, army);
            SceneManager.LoadScene("Countryside");
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<MovementController>();
        }

        [TearDown]
        public void ForgetArmies() => ArmySelection.Clear();

        private void MoveAndFinish(Unit unit)
        {
            Assert.That(controller.SelectUnit(unit), Is.True, unit.DisplayName);
            Assert.That(controller.OrderHold(), Is.True);
            controller.OrderGather();
            Assert.That(controller.OrderEndMove(), Is.True);
        }

        [UnityTest]
        public IEnumerator EndingTheTurnWithUnitsThatCanStillMoveAsksFirst()
        {
            int turn = Turns.Current;
            var units = controller.PlayerUnits.ToList();
            Assert.That(controller.UnitsThatCanStillMove(), Is.EquivalentTo(units));

            controller.RequestEndTurn();
            Assert.That(controller.ConfirmingEndTurn, Is.True, "asked to confirm");
            Assert.That(controller.IsAITurn, Is.False, "the turn hasn't ended");
            Assert.That(Turns.Current, Is.EqualTo(turn));

            controller.CancelEndTurn();
            Assert.That(controller.ConfirmingEndTurn, Is.False, "kept playing");

            // One unit moves: the other still can, so it still asks.
            MoveAndFinish(units[0]);
            Assert.That(controller.UnitsThatCanStillMove(), Is.EqualTo(new[] { units[1] }), "a unit that has finished its move can't move again");
            controller.RequestEndTurn();
            Assert.That(controller.ConfirmingEndTurn, Is.True);

            // Confirming ends the turn.
            controller.EndTurn();
            Assert.That(controller.ConfirmingEndTurn, Is.False);
            Assert.That(controller.IsAITurn, Is.True, "the AI's turn started");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WhenEveryUnitHasMovedTheTurnEndsWithoutAsking()
        {
            foreach (var unit in controller.PlayerUnits.ToList()) MoveAndFinish(unit);
            Assert.That(controller.UnitsThatCanStillMove(), Is.Empty);
            controller.RequestEndTurn();
            Assert.That(controller.ConfirmingEndTurn, Is.False, "nothing to ask");
            Assert.That(controller.IsAITurn, Is.True, "the turn ended");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedUnitsDontCount()
        {
            var units = controller.PlayerUnits.ToList();
            MoveAndFinish(units[0]);
            foreach (var s in units[1].Soldiers.ToList()) s.Kill();
            yield return null;
            if (Victory.IsOver) Assert.Inconclusive("the battle ended");
            Assert.That(controller.UnitsThatCanStillMove(), Is.Empty);
            controller.RequestEndTurn();
            Assert.That(controller.ConfirmingEndTurn, Is.False);
        }
    }
}
