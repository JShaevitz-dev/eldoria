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
    /// <summary>Drives the real CubeRoom scene through a full squad move.</summary>
    public class MovementControllerPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("CubeRoom");
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SquadMovesSergeantFirstThenGathers()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.ActiveUnit, Is.Null, "each turn starts with no unit selected");
            Assert.That(controller.OrderLeaderMove(Vector3.zero), Is.False, "no orders until a unit is picked");
            SelectInfantrySquad(controller);
            var a = controller.Activation;
            var sergeant = a.Leader;
            Vector3 start = sergeant.Position;

            Assert.That(controller.OrderLeaderMove(start + new Vector3(3f, 0f, 12f)), Is.True);
            Assert.That(a.LeaderMove, Is.EqualTo(MoveKind.Sprint));
            Assert.That(controller.OrderEndMove(), Is.False, "soldiers out of cohesion should block ending the move");

            // Place one soldier by hand, then gather the rest.
            Assert.That(controller.Selected, Is.Not.Null);
            Assert.That(controller.OrderSelectedMove(a.LeaderPosition + new Vector3(-2f, 0f, -2f)), Is.True);
            controller.OrderGather();
            Assert.That(a.OutOfCohesion(), Is.Empty);

            yield return new WaitForSeconds(5f); // let everyone walk there
            Assert.That(Vector3.Distance(sergeant.Position, a.LeaderPosition), Is.LessThan(0.01f));

            Snapshot(controller, "-snapshotFollowers");
            Assert.That(controller.OrderEndMove(), Is.True);

            // Next turn: preview a sprint target to show the range rings.
            controller.NextTurn();
            Assert.That(controller.ActiveUnit, Is.Null, "the next turn starts with no unit selected");
            SelectInfantrySquad(controller);
            controller.PreviewAt(sergeant.Position + new Vector3(-6f, 0f, 13f));
            Snapshot(controller, "-snapshotLeader");
        }

        [UnityTest]
        public IEnumerator KilledLeaderIsReplacedByClosestSoldier()
        {
            var controller = SelectInfantrySquad(Object.FindFirstObjectByType<MovementController>());
            var squad = controller.Activation.Unit;
            var sergeant = squad.Leader;
            Assert.That(sergeant.IsSergeant, Is.True);

            SuccessionResult? result = null;
            squad.LeaderChanged += (_, r) => result = r;

            Assert.That(controller.DebugKillLeader(), Is.True);
            yield return null;

            // In the starting formation three troopers stand exactly 2 m from the sergeant: a random roll picks one.
            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.TiedCandidates, Is.EqualTo(3));
            Assert.That(new[] { "Trooper_01", "Trooper_02", "Trooper_05" }, Does.Contain(squad.Leader.name));
            Assert.That(controller.Activation.Leader, Is.EqualTo(squad.Leader), "the new leader leads this turn's move");

            // The new leader moves; the 5 m ring is now theirs.
            var heir = squad.Leader;
            Assert.That(controller.OrderLeaderMove(heir.Position + new Vector3(0f, 0f, 5f)), Is.True);
            controller.OrderGather();
            Assert.That(controller.Activation.OutOfCohesion(), Is.Empty);
            yield return new WaitForSeconds(4f);
            Snapshot(controller, "-snapshotSuccession");
            Assert.That(controller.OrderEndMove(), Is.True);
        }

        [UnityTest]
        public IEnumerator SelectedUnitListsItsSoldiersKeywords()
        {
            var controller = SelectInfantrySquad(Object.FindFirstObjectByType<MovementController>());
            var summary = controller.ActiveUnit.KeywordSummary();
            Assert.That(summary.Single(e => e.keyword.displayName == "Biological").soldiers.Count, Is.EqualTo(10));
            Assert.That(summary.Single(e => e.keyword.displayName == "Led from the front").soldiers.Count, Is.EqualTo(10), "the Field Commander's aura");
            Assert.That(summary.Any(e => e.keyword.displayName == "In half-cover"), Is.True, "troopers by the barricade");

            var lines = MovementController.UnitKeywordLines(controller.ActiveUnit);
            Debug.Log("[Eldoria] HUD unit keywords:\n" + string.Join("\n", lines));
            Assert.That(lines, Does.Contain("Biological — all 10"));
            Assert.That(lines.Any(l => l.StartsWith("In half-cover (status) — ") && l.Contains(" of 10: ")), Is.True);
            yield return null;
        }

        /// <summary>Step 1: picks the player's Infantry Squad (no unit is selected when a turn starts).</summary>
        internal static MovementController SelectInfantrySquad(MovementController controller)
        {
            var squad = controller.PlayerUnits.First(u => u.DisplayName == "Infantry Squad");
            Assert.That(controller.SelectUnit(squad), Is.True);
            Assert.That(controller.ActiveUnit, Is.EqualTo(squad));
            return controller;
        }

        /// <summary>Optional: renders the main camera to the path given after <paramref name="argName"/>.</summary>
        private static void Snapshot(MovementController controller, string argName)
        {
            string outPath = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == argName) outPath = args[i + 1];
            if (outPath == null) return;

            var cam = Camera.main;
            var target = controller.Activation.LeaderPosition;
            cam.transform.position = target + new Vector3(0f, 26f, -20f);
            cam.transform.LookAt(target);

            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
        }
    }
}
