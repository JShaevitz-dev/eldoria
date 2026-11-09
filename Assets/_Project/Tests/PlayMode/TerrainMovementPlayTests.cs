using System.Collections;
using System.Linq;
using Eldoira.Gameplay;
using Eldoira.Rules;
using Eldoira.Terrain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Eldoira.Tests
{
    /// <summary>
    /// Movement measuring over the CubeRoom test terrain:
    /// hill (-14, 4) r7.5 h3; ditch along z=-4 from x=3..21, 1.2 m deep; building at (14, 9) with a
    /// south doorway; low walls at z=-10 (x -2..2 and -8..-4); crates near (-2.3, -2.2);
    /// shipping container x 0..6, z 1.8..4.2 (2.6 m tall); rock pillar at (-9, -13);
    /// 2 m platform x -1..5, z 12..16 with a slope on its south side (x 1..3, z 8.5..12);
    /// Half-cover barricade at (6, -13), 2.5 m wide.
    /// </summary>
    public class TerrainMovementPlayTests
    {
        private IMoveMeasurer measurer;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("CubeRoom");
            yield return null;
            yield return null;
            measurer = BattlefieldNavigation.Measurer;
            Assert.That(measurer, Is.Not.Null, "scene should build its navigation on load");
        }

        private Vector3 Stand(float x, float z)
        {
            Assert.That(measurer.TryGetStandPoint(new Vector3(x, 0f, z), 0.35f, 1.8f, out var p, out var why), Is.True, why);
            return p;
        }

        private static float Flat(Vector3 a, Vector3 b) => Measure.Horizontal(a, b);

        [Test]
        public void SoldiersNearTheBarricadeStartInHalfCover()
        {
            // Barricade at (6, -13), x 4.75..7.25. Trooper_04 stands at (4, -16): ~2.9 m away. Sergeant at (0, -16): ~5.5 m.
            var unit = Object.FindObjectsByType<Units.Unit>(FindObjectsSortMode.None).First(u => u.Side == Units.Side.Player && u.DisplayName == "Infantry Squad");
            var t4 = unit.Soldiers.First(s => s.name == "Trooper_04");
            Assert.That(t4.HasKeyword("In half-cover"), Is.True);
            Assert.That(unit.Leader.HasKeyword("In half-cover"), Is.False);
        }

        [Test]
        public void TheFieldCommanderLeadsTheSquadAndCanBeCommanded()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var units = Object.FindObjectsByType<Units.Unit>(FindObjectsSortMode.None);
            var squad = units.First(u => u.Side == Units.Side.Player && u.DisplayName == "Infantry Squad");
            var commander = units.First(u => u.DisplayName == "Field Commander");
            var enemy = units.First(u => u.Side == Units.Side.AI);

            Assert.That(commander.Soldiers.Count, Is.EqualTo(1));
            Assert.That(squad.LivingSoldiers.All(s => s.HasKeyword("Led from the front")), Is.True, "whole squad within 15 m");
            Assert.That(enemy.LivingSoldiers.Any(s => s.HasKeyword("Led from the front")), Is.False);

            // Step 1: switch to the Field Commander and move them.
            Assert.That(controller.ActiveUnit, Is.Null, "the turn starts with no unit selected");
            // Tab picks a player unit (the first in the scene hierarchy); then switch to the commander.
            Assert.That(controller.SelectNextUnit(), Is.True);
            Assert.That(controller.ActiveUnit, Is.Not.Null);
            Assert.That(controller.SelectUnit(commander) || controller.ActiveUnit == commander, Is.True);
            Assert.That(controller.ActiveUnit, Is.EqualTo(commander));
            Assert.That(controller.OrderLeaderMove(commander.Leader.Position + new Vector3(-6f, 0f, 0f)), Is.True);
            Assert.That(controller.Activation.Phase, Is.EqualTo(MovePhase.MoveFollowers), "a unit of one still finishes its move");
            Assert.That(controller.OrderEndMove(), Is.True);

            // Back to the squad: the commander has finished; the squad hasn't acted.
            Assert.That(controller.SelectUnit(squad), Is.True);
            Assert.That(controller.HasActed(commander), Is.True);
            Assert.That(controller.SelectUnit(commander), Is.False, "already acted this turn");
        }

        [Test]
        public void TheBattlefieldPriestBlessesTheSquad()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var units = Object.FindObjectsByType<Units.Unit>(FindObjectsSortMode.None);
            var squad = units.First(u => u.Side == Units.Side.Player && u.DisplayName == "Infantry Squad");
            var priestUnit = units.First(u => u.DisplayName == "Battlefield Priest");
            var priest = priestUnit.Leader;
            var sanctuary = priest.Abilities.First(a => a.displayName == "Divine Sanctuary");
            var healing = priest.Abilities.First(a => a.displayName == "Divine Healing");

            Assert.That(controller.SelectUnit(priestUnit), Is.True);
            var r = controller.UseAbility(sanctuary, priest, squad.Leader);
            Assert.That(r.Used, Is.True, r.Reason);
            Assert.That(squad.LivingSoldiers.All(s => s.HasKeyword("Divine Sanctuary")), Is.True);

            // Using one ability is the unit's action for the turn: no second ability, no shooting, no moving.
            Assert.That(controller.HasTakenAction, Is.True);
            Assert.That(controller.HasActed(priestUnit), Is.True);
            Assert.That(controller.Activation.Phase, Is.EqualTo(MovePhase.Complete));
            var wounded = squad.LivingSoldiers.Last();
            wounded.TakeDamage(3);
            Assert.That(controller.UseAbility(healing, priest, wounded).Used, Is.False, "one action per turn");
            var enemy = units.First(u => u.Side == Units.Side.AI);
            Assert.That(controller.OrderShoot(enemy), Is.Null, "can't also shoot");

            // Next turn: the priest moves, then heals (an ability after moving).
            controller.NextTurn();
            Assert.That(controller.SelectUnit(priestUnit), Is.True);
            Assert.That(controller.OrderLeaderMove(priest.Position + new Vector3(0f, 0f, 2f)), Is.True);
            Assert.That(controller.OrderEndMove(), Is.True);
            Assert.That(controller.UseAbility(healing, priest, wounded).Used, Is.True);
            Assert.That(wounded.Health, Is.EqualTo(wounded.MaxHealth));
        }

        [Test]
        public void HealingAnInvalidTargetPlaysAnErrorAndLetsThePlayerPickAgain()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var units = Object.FindObjectsByType<Units.Unit>(FindObjectsSortMode.None);
            var squad = units.First(u => u.Side == Units.Side.Player && u.DisplayName == "Infantry Squad");
            var priestUnit = units.First(u => u.DisplayName == "Battlefield Priest");
            var priest = priestUnit.Leader;
            var healing = priest.Abilities.First(a => a.displayName == "Divine Healing");
            var healthy = squad.Leader;
            var wounded = squad.LivingSoldiers.Last();
            wounded.TakeDamage(3);

            Assert.That(controller.SelectUnit(priestUnit), Is.True);
            controller.BeginAbility(priest, healing);   // the HUD button

            int errorsBefore = CombatAudio.ErrorSoundsPlayed;
            int castsBefore = CombatAudio.CastSoundsPlayed;
            var refused = controller.UseAbility(healing, priest, healthy);
            Assert.That(CombatAudio.CastSoundsPlayed, Is.EqualTo(castsBefore), "no hallelujah for a refused target");
            Assert.That(refused.Used, Is.False);
            Assert.That(refused.Reason, Does.Contain("full health"));
            Assert.That(CombatAudio.ErrorSoundsPlayed, Is.EqualTo(errorsBefore + 1), "error sound");
            Assert.That(controller.HasTakenAction, Is.False, "nothing happened");
            Assert.That(controller.TargetingAbility, Is.EqualTo(healing), "still choosing a target");
            Assert.That(healthy.Health, Is.EqualTo(healthy.MaxHealth));

            // Pick a different target: it works.
            Assert.That(controller.UseAbility(healing, priest, wounded).Used, Is.True);
            Assert.That(wounded.Health, Is.EqualTo(wounded.MaxHealth));
            Assert.That(controller.TargetingAbility, Is.Null);
            Assert.That(CombatAudio.ErrorSoundsPlayed, Is.EqualTo(errorsBefore + 1), "no error for a valid target");
            Assert.That(CombatAudio.CastSoundsPlayed, Is.EqualTo(castsBefore + 1), "the hallelujah plays once");
            Assert.That(CombatAudio.LastCastSound.name, Is.EqualTo("Hallelujah"));
        }

        [UnityTest]
        public IEnumerator DivineSanctuaryMakesTheBlessedUnitSparkleUntilItWearsOff()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var units = Object.FindObjectsByType<Units.Unit>(FindObjectsSortMode.None);
            var squad = units.First(u => u.Side == Units.Side.Player && u.DisplayName == "Infantry Squad");
            var priestUnit = units.First(u => u.DisplayName == "Battlefield Priest");
            var enemy = units.First(u => u.Side == Units.Side.AI);
            var priest = priestUnit.Leader;
            var sanctuary = priest.Abilities.First(a => a.displayName == "Divine Sanctuary");

            KeywordSparkles.RefreshNow();
            Assert.That(squad.LivingSoldiers.Any(KeywordSparkles.IsSparkling), Is.False, "no sparkles before the spell");

            Assert.That(controller.SelectUnit(priestUnit), Is.True);
            Assert.That(controller.UseAbility(sanctuary, priest, squad.Leader).Used, Is.True);
            KeywordSparkles.RefreshNow();
            Assert.That(squad.LivingSoldiers.All(KeywordSparkles.IsSparkling), Is.True, "every soldier in the blessed unit sparkles");
            Assert.That(KeywordSparkles.IsSparkling(priest), Is.False, "the priest's own unit wasn't the target");
            Assert.That(enemy.LivingSoldiers.Any(KeywordSparkles.IsSparkling), Is.False);
            Assert.That(KeywordSparkles.SparkleColorFor(squad.Leader).Value.r, Is.GreaterThan(0.9f), "gold");

            yield return new WaitForSeconds(1.5f);
            Snapshot("-snapshotSparkles", squad.Leader.Position + new Vector3(0f, 0.9f, -1.5f), new Vector3(2.5f, 4.5f, 8f));

            // The spell lasts until the start of the next turn: then the sparkles stop.
            controller.NextTurn();
            KeywordSparkles.RefreshNow();
            Assert.That(squad.LivingSoldiers.Any(s => s.HasKeyword("Divine Sanctuary")), Is.False);
            Assert.That(squad.LivingSoldiers.Any(KeywordSparkles.IsSparkling), Is.False);
        }

        [Test]
        public void ShootingUsesTheUnitsActionSoNoAbilityAfterwards()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var units = Object.FindObjectsByType<Units.Unit>(FindObjectsSortMode.None);
            var priestUnit = units.First(u => u.DisplayName == "Battlefield Priest");
            var priest = priestUnit.Leader;
            var enemy = units.First(u => u.Side == Units.Side.AI);

            Assert.That(controller.SelectUnit(priestUnit), Is.True);
            Assert.That(controller.OrderShoot(enemy), Is.Not.Null);
            var healing = priest.Abilities.First(a => a.displayName == "Divine Healing");
            Assert.That(controller.UseAbility(healing, priest, priest).Used, Is.False);
        }

        [Test]
        public void TheSquadShootsAtTheEnemySquad()
        {
            var controller = MovementControllerPlayTests.SelectInfantrySquad(Object.FindFirstObjectByType<MovementController>());
            var enemy = Object.FindObjectsByType<Units.Unit>(FindObjectsSortMode.None).First(u => u.Side == Units.Side.AI);

            int soundsBefore = CombatAudio.VolleySoundsPlayed;
            var r = controller.OrderShoot(enemy);
            Assert.That(r, Is.Not.Null);
            Assert.That(CombatAudio.VolleySoundsPlayed, Is.EqualTo(soundsBefore + 1), "the gunshot plays once when the volley ends");
            Assert.That(Resources.Load<AudioClip>(CombatAudio.VolleySoundPath), Is.Not.Null);
            Assert.That(r.HasLineOfSight, Is.True);
            Assert.That(r.Shots.Count, Is.EqualTo(10), "every soldier picks a target");
            Assert.That(r.ObjectsInLineOfSight.Select(o => o.DisplayName), Does.Contain("Shipping Container"));
            Assert.That(r.ShotsTaken, Is.GreaterThan(0));
            Assert.That(controller.HasTakenAction, Is.True);
            Assert.That(controller.Activation.Phase, Is.EqualTo(MovePhase.Complete), "shooting ends the unit's turn");
            Assert.That(controller.OrderShoot(enemy), Is.Null, "can't shoot twice in a turn");

            // Enemies right behind the container (from the squad's side) are in full cover and aren't picked.
            foreach (var shot in r.Shots.Where(s => s.Target != null))
                Assert.That(shot.Target.HasKeyword("In full cover") && ShotGeometry.Crosses(shot.Shooter.Position, shot.Target.Position,
                    Object.FindObjectsByType<TerrainObject>(FindObjectsSortMode.None).First(o => o.DisplayName == "Shipping Container")), Is.False);
        }

        [Test]
        public void FlatGroundIsMeasuredAsAStraightLine()
        {
            var a = Stand(6f, -16f);
            var b = Stand(6f, -8f);
            var path = measurer.Measure(a, b);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Length, Is.EqualTo(8f).Within(0.05f));
        }

        [Test]
        public void ClimbingAHillCostsTheDistanceUpTheSlope()
        {
            var bottom = Stand(-14f, -6f);
            var top = Stand(-14f, 4f);
            Assert.That(top.y, Is.EqualTo(3f).Within(0.1f));

            var path = measurer.Measure(bottom, top);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Length, Is.GreaterThan(Flat(bottom, top) + 0.3f), "slope adds distance");
            Assert.That(path.Length, Is.LessThan(Flat(bottom, top) + 1.5f));
        }

        [Test]
        public void CrossingADitchCostsTheDistanceDownAndUp()
        {
            var a = Stand(12f, -9f);
            var b = Stand(12f, 1f);
            var path = measurer.Measure(a, b);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Points.Min(p => p.y), Is.LessThan(-1f), "route goes through the ditch");
            Assert.That(path.Length, Is.GreaterThan(10.2f));
        }

        [Test]
        public void MantleableObjectsAreMeasuredStraightThrough()
        {
            var a = Stand(0f, -13f);
            var b = Stand(0f, -7f);
            var path = measurer.Measure(a, b);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Length, Is.EqualTo(6f).Within(0.1f), "low wall adds no distance");
        }

        [Test]
        public void ObjectsThatCannotBeMantledBlockMovement()
        {
            var a = Stand(3f, -0.5f);
            var b = Stand(3f, 6.5f);
            var path = measurer.Measure(a, b);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Length, Is.GreaterThan(Flat(a, b) + 1f), "route goes around the container");
        }

        [Test]
        public void SoldiersCannotStandOnObjectsThatCannotBeClimbed()
        {
            Assert.That(measurer.TryGetStandPoint(new Vector3(0f, 1f, -10f), 0.35f, 1.8f, out _, out var why), Is.False);
            Assert.That(why, Does.Contain("Low Wall"));
            Assert.That(measurer.TryGetStandPoint(new Vector3(-9f, 3f, -13f), 0.35f, 1.8f, out _, out why), Is.False);
            Assert.That(why, Does.Contain("Rock Pillar"));
        }

        [Test]
        public void SoldiersCanClimbOntoClimbableObjects()
        {
            Assert.That(measurer.TryGetStandPoint(new Vector3(3f, 0f, 3f), 0.35f, 1.8f, out var top, out var why), Is.True, why);
            Assert.That(top.y, Is.EqualTo(2.6f).Within(0.05f));

            var from = Stand(3f, -1f);
            var path = measurer.Measure(from, top);
            Assert.That(path.Reachable, Is.True, path.Reason);
            Assert.That(path.Climbs, Is.EqualTo(1));
            // ~2.45 m to the container's side, then a flat 10 m to climb it.
            Assert.That(path.Length, Is.EqualTo(2.45f + MovementRules.ClimbCost).Within(0.3f));

            // And back down again: climbing down is free, so only the walk from the container's side counts.
            var down = measurer.Measure(top, from);
            Assert.That(down.Reachable, Is.True);
            Assert.That(down.Climbs, Is.EqualTo(0));
            Assert.That(down.Length, Is.EqualTo(2.45f).Within(0.3f));
        }

        [Test]
        public void BuildingWallsBlockButTheDoorwayLetsSoldiersIn()
        {
            var inside = Stand(14f, 9f);
            var southOfDoor = Stand(14f, 3f);
            var behind = Stand(14f, 14f);

            var throughDoor = measurer.Measure(southOfDoor, inside);
            Assert.That(throughDoor.Length, Is.EqualTo(6f).Within(0.2f));

            var aroundBack = measurer.Measure(behind, inside);
            Assert.That(aroundBack.Reachable, Is.True);
            Assert.That(aroundBack.Length, Is.GreaterThan(5f + 8f), "must walk around to the doorway");

            Assert.That(measurer.TryGetStandPoint(new Vector3(14f, 3f, 12f), 0.35f, 1.8f, out _, out _), Is.False, "not on top of a wall");
        }

        [Test]
        public void ClimbingCostsTheSameWhateverTheHeight()
        {
            // Single crate (1.2 m) and the container (2.6 m): both cost the flat climb on top of walking there.
            Assert.That(measurer.TryGetStandPoint(new Vector3(-1.2f, 0f, -2.7f), 0.35f, 1.8f, out var crate, out var why), Is.True, why);
            Assert.That(crate.y, Is.EqualTo(1.2f).Within(0.05f));
            var nearCrate = Stand(-1.2f, -5f);
            var climbCrate = measurer.Measure(nearCrate, crate);
            Assert.That(climbCrate.Climbs, Is.EqualTo(1));
            Assert.That(climbCrate.Length, Is.GreaterThan(MovementRules.ClimbCost));
            Assert.That(climbCrate.Length, Is.LessThan(MovementRules.ClimbCost + 3f));
        }

        [Test]
        public void ClimbingDownCostsNothing()
        {
            // From the top of the stacked crates (2.4 m up) to the ground just beside them.
            Assert.That(measurer.TryGetStandPoint(new Vector3(-2.3f, 0f, -2.15f), 0.35f, 1.8f, out var top, out var why), Is.True, why);
            Assert.That(top.y, Is.EqualTo(2.4f).Within(0.05f));
            var below = Stand(-2.3f, -3.6f);

            var path = measurer.Measure(top, below);
            Assert.That(path.Reachable, Is.True, path.Reason);
            Assert.That(path.Climbs, Is.EqualTo(0));
            Assert.That(path.Length, Is.LessThan(1f), "the 2.4 m drop is free; only any walk on the ground counts");
        }

        [Test]
        public void SlopesAreMeasuredAlongTheIncline()
        {
            // Platform (2, 14) is 2 m high; a slope climbs 2 m over 3.5 m from z=8.5 to z=12 at x=2.
            var start = Stand(2f, 6f);
            var top = Stand(2f, 14f);
            Assert.That(top.y, Is.EqualTo(2f).Within(0.05f));

            var path = measurer.Measure(start, top);
            Assert.That(path.Reachable, Is.True, path.Reason);
            float ramp = Mathf.Sqrt(3.5f * 3.5f + 2f * 2f);           // 4.03 m up the slope
            float expected = 2.5f + ramp + 2f;                         // ground + slope + platform top
            Assert.That(path.Length, Is.EqualTo(expected).Within(0.2f));
            Assert.That(path.Climbs, Is.EqualTo(0), "walking up a slope is not climbing");
        }

        [Test]
        public void PlatformSidesBlockSoUseTheSlope()
        {
            var behind = Stand(2f, 17.5f);
            var top = Stand(2f, 14f);
            var path = measurer.Measure(behind, top);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Length, Is.GreaterThan(10f), "must walk round to the slope");
        }

        [UnityTest]
        public IEnumerator SquadLeaderMoveUsesTerrainDistance()
        {
            var controller = MovementControllerPlayTests.SelectInfantrySquad(Object.FindFirstObjectByType<MovementController>());
            var a = controller.Activation;

            // Up onto the stacked crates: ~14 m of walking + 10 m to climb is beyond a 20 m sprint.
            var crateTop = new Vector3(-2.3f, 0f, -2.15f);
            var check = a.CheckLeaderMove(crateTop);
            Assert.That(check.Ok, Is.False);
            Assert.That(check.Reason, Does.Contain("Out of range"));
            Assert.That(check.Path.Climbs, Is.EqualTo(1));
            Assert.That(check.Distance, Is.GreaterThan(20f));

            controller.PreviewAt(crateTop);
            Snapshot("-snapshotTerrainClimb", new Vector3(0f, 0f, -6f), new Vector3(0f, 34f, -30f));

            // Into the building: route goes round to the doorway and is out of range.
            controller.PreviewAt(new Vector3(15f, 0f, 10f));
            var far = a.CheckLeaderMove(new Vector3(15f, 0f, 10f));
            Assert.That(far.Ok, Is.False);
            Assert.That(far.Path, Is.Not.Null);
            Snapshot("-snapshotTerrainRoute", new Vector3(6f, 0f, -2f), new Vector3(0f, 40f, -30f));

            // Over a low wall (measured straight through it) to beside the crates: a sprint.
            var besideCrates = new Vector3(-0.5f, 0f, -5f);
            var over = a.CheckLeaderMove(besideCrates);
            Assert.That(over.Ok, Is.True, over.Reason);
            Assert.That(over.Kind, Is.EqualTo(MoveKind.Sprint));
            Assert.That(controller.OrderLeaderMove(besideCrates), Is.True);
            controller.OrderGather();
            Assert.That(a.OutOfCohesion(), Is.Empty);
            yield return new WaitForSeconds(6f);
            Snapshot("-snapshotTerrainGathered", new Vector3(-1f, 0f, -4f), new Vector3(6f, 14f, -14f));
        }

        private static void Snapshot(string argName, Vector3 lookAt, Vector3 offset)
        {
            string outPath = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == argName) outPath = args[i + 1];
            if (outPath == null) return;

            var cam = Camera.main;
            cam.transform.position = lookAt + offset;
            cam.transform.LookAt(lookAt);
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
