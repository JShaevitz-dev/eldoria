using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Eldoria.Core;
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
    /// <summary>The Urban Demo map: size, 180° symmetry, deployment, and that every area can be reached.</summary>
    public class UrbanDemoPlayTests
    {
        private const float Tolerance = 0.05f;
        private IMoveMeasurer measurer;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("UrbanDemo");
            yield return null;
            yield return null;
            measurer = BattlefieldNavigation.Measurer;
            Assert.That(measurer, Is.Not.Null);
        }

        private static Vector3 Twin(Vector3 p) => new Vector3(-p.x, p.y, -p.z);

        [Test]
        public void TheMapIs80By60Metres()
        {
            Assert.That(BattlefieldScale.BoardWidth, Is.EqualTo(80f));
            Assert.That(BattlefieldScale.BoardDepth, Is.EqualTo(60f));
            Assert.That(MovementRules.IsInsideBattlefield(new Vector3(38f, 0f, 28f), 0.35f), Is.True, "outside the old 60 x 44 room");
            Assert.That(MovementRules.IsInsideBattlefield(new Vector3(41f, 0f, 0f), 0.35f), Is.False);
        }

        [Test]
        public void EveryObjectHasATwinTurned180DegreesAboutTheCentre()
        {
            var objects = Object.FindObjectsByType<TerrainObject>(FindObjectsSortMode.None);
            Assert.That(objects.Length, Is.GreaterThan(30));
            foreach (var o in objects)
            {
                var c = o.GetComponent<Collider>().bounds;
                bool found = objects.Any(t => t.DisplayName == o.DisplayName
                    && Vector3.Distance(t.GetComponent<Collider>().bounds.center, Twin(c.center)) < Tolerance
                    && Vector3.Distance(t.GetComponent<Collider>().bounds.size, c.size) < Tolerance);
                Assert.That(found, Is.True, $"{o.DisplayName} at {c.center} has no twin at {Twin(c.center)}");
            }
        }

        [Test]
        public void EveryWallAndStructureHasATwin()
        {
            var structures = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.gameObject.layer == TerrainLayers.Structure).Select(c => c.bounds).ToList();
            Assert.That(structures.Count, Is.GreaterThan(50));
            foreach (var b in structures)
                Assert.That(structures.Any(t => Vector3.Distance(t.center, Twin(b.center)) < Tolerance && Vector3.Distance(t.size, b.size) < Tolerance),
                    Is.True, $"structure at {b.center} has no twin");
        }

        [Test]
        public void EachSideDeploysInsideItsOwnZone()
        {
            var zones = Object.FindObjectsByType<DeploymentZone>(FindObjectsSortMode.None);
            Assert.That(zones.Select(z => z.Side), Is.EquivalentTo(new[] { Side.Player, Side.AI }));
            var units = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
            foreach (var side in new[] { Side.Player, Side.AI })
            {
                var army = units.Where(u => u.Side == side).ToList();
                Assert.That(army.Count, Is.EqualTo(5));
                Assert.That(army.Count(u => u.Template.displayName == "Infantry Squad"), Is.EqualTo(3), "three infantry squads");
                Assert.That(army.Count(u => u.Template.displayName == "Field Commander"), Is.EqualTo(1));
                Assert.That(army.Count(u => u.Template.displayName == "Battle Priest"), Is.EqualTo(1));
            }
            foreach (var u in units)
            {
                var zone = zones.Single(z => z.Side == u.Side);
                foreach (var s in u.Soldiers)
                    Assert.That(zone.Contains(s.Position), Is.True, $"{u.DisplayName}/{s.name} at {s.Position}");
            }
            Assert.That(Object.FindFirstObjectByType<MovementController>().PlayerUnits.Count, Is.EqualTo(5));
        }

        private Vector3 Stand(Vector3 p)
        {
            Assert.That(measurer.TryGetStandPoint(p, 0.35f, 1.8f, out var stand, out var why), Is.True, $"{p}: {why}");
            return stand;
        }

        [Test]
        public void EveryAreaCanBeReachedFromBothDeploymentZones()
        {
            var places = new List<Vector3>
            {
                new Vector3(0f, 0f, -4f),   // plaza, by the fountain
                new Vector3(-30f, 0f, 0f),  // west side of the centre road
                new Vector3(36f, 0f, -26f), // corner of the deployment zone behind the container
                new Vector3(22f, 1.2f, -14f), // on top of the loading dock (up its slope)
            };
            // Inside every room of every building (and their twins).
            var rooms = new Dictionary<string, Vector3[]>
            {
                ["Apartment Block"] = new[] { new Vector3(-3.5f, 0f, 1f), new Vector3(3.5f, 0f, -1f) },
                ["Corner Shop"] = new[] { new Vector3(-1f, 0f, -1.5f) },
                ["Garage"] = new[] { new Vector3(-2.5f, 0f, 0f) },
                ["Ruined House"] = new[] { new Vector3(1.5f, 0f, -1f) },
            };
            foreach (var st in Object.FindObjectsByType<StructureTerrain>(FindObjectsSortMode.None))
                if (rooms.TryGetValue(st.name, out var local))
                    places.AddRange(local.Select(l => st.transform.TransformPoint(l)));
            Assert.That(places.Count, Is.EqualTo(4 + 2 * 5));

            foreach (var start in new[] { new Vector3(0f, 0f, -26f), new Vector3(0f, 0f, 26f) })
            {
                var from = Stand(start + new Vector3(3f, 0f, 0f));
                foreach (var p in places.Concat(places.Select(Twin)))
                {
                    var to = Stand(p);
                    var path = measurer.Measure(from, to);
                    Assert.That(path.Reachable, Is.True, $"from {from} to {p}: {path.Reason}");
                }
            }
        }

        [Test]
        public void WindowsCanBeSeenAndClimbedThroughAndGiveHalfCover()
        {
            var sills = Object.FindObjectsByType<TerrainObject>(FindObjectsSortMode.None).Where(o => o.DisplayName == "Window Sill").ToList();
            Assert.That(sills.Count, Is.GreaterThanOrEqualTo(20));
            foreach (var sill in sills)
            {
                Assert.That(sill.CanMantle, Is.True, "soldiers can climb through");
                Assert.That(sill.HasKeyword("Half-cover"), Is.True);
                // The sill is thin along its local x axis: look straight through the window at standing eye height.
                Vector3 c = sill.GetComponent<Collider>().bounds.center;
                Vector3 across = sill.transform.right;
                Vector3 a = new Vector3(c.x, 1.6f, c.z) + across * 1.5f, b = new Vector3(c.x, 1.6f, c.z) - across * 1.5f;
                Assert.That(Physics.Linecast(a, b, TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore), Is.False,
                    $"window at {c} should not block sight");
            }

            // Soldiers climb straight through a window: from 1 m inside to 1 m outside is a 2 m move, at no extra cost.
            int checkedWindows = 0;
            foreach (var sill in sills)
            {
                Vector3 sc = sill.GetComponent<Collider>().bounds.center;
                Vector3 p = new Vector3(sc.x, 0f, sc.z), across = sill.transform.right;
                // Skip windows with furniture right behind them.
                if (!measurer.TryGetStandPoint(p + across, 0.35f, 1.8f, out var inside, out _)) continue;
                if (!measurer.TryGetStandPoint(p - across, 0.35f, 1.8f, out var outside, out _)) continue;
                var route = measurer.Measure(inside, outside);
                Assert.That(route.Reachable, Is.True, $"window at {sc}: {route.Reason}");
                Assert.That(route.Length, Is.LessThan(2.5f), $"route through the window at {sc} was {route.Length:0.0} m");
                checkedWindows++;
            }
            Assert.That(checkedWindows, Is.GreaterThan(10));
        }
    
        // ---- Controls and feedback ----

        [Test]
        public void EscLeavesASelectedUnitThatHasntMovedYet()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var squad = controller.PlayerUnits.First(u => u.DisplayName == "Infantry Squad 1");
            Assert.That(controller.SelectUnit(squad), Is.True);

            Assert.That(controller.CancelSelection(), Is.True);
            Assert.That(controller.ActiveUnit, Is.Null);
            Assert.That(controller.HasActed(squad), Is.False, "it didn't act, so it can be picked again");
            Assert.That(controller.SelectUnit(squad), Is.True);

            // Once the leader has moved, Esc no longer leaves the unit.
            Assert.That(controller.OrderLeaderMove(squad.Leader.Position + new Vector3(0f, 0f, 4f)), Is.True);
            Assert.That(controller.CancelSelection(), Is.False);
            Assert.That(controller.ActiveUnit, Is.EqualTo(squad));
        }

        [Test]
        public void TheBellRingsWhenThePlayersTurnStarts()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            Assert.That(CombatAudio.BellsPlayed, Is.GreaterThan(0), "it rang for turn 1");
            int before = CombatAudio.BellsPlayed;
            controller.EndTurn(instant: true);   // the AI plays, then the player's turn 2 starts
            Assert.That(Turns.Current, Is.EqualTo(2));
            Assert.That(CombatAudio.BellsPlayed, Is.EqualTo(before + 1), "once, at the start of the player's turn");
        }

        [Test]
        public void EverySoldiersLabelShowsTheirHealth()
        {
            var trooper = Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None).First(s => s.name == "Trooper_03");
            Assert.That(SoldierLabels.LabelFor(trooper), Is.EqualTo("Trooper_03  5/5"));
            trooper.TakeDamage(2);
            Assert.That(SoldierLabels.LabelFor(trooper), Is.EqualTo("Trooper_03  3/5"));
            Assert.That(SoldierLabels.ShortLabelFor(trooper), Is.EqualTo("T3 3/5"), "the compact form when zoomed out");
            Assert.That(SoldierLabels.ShortName("Sergeant"), Is.EqualTo("Sgt"));
            Assert.That(SoldierLabels.ShortName("Field Commander"), Is.EqualTo("Cmdr"));
            Assert.That(SoldierLabels.ShortName("Battle Priest"), Is.EqualTo("Priest"));
        }

        // ---- The AI ----

        /// <summary>Optional: a straight-down view of the whole map, written to the path after <paramref name="argName"/>.</summary>
        private static void TopDownSnapshot(string argName)
        {
            string outPath = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == argName) outPath = args[i + 1];
            if (outPath == null) return;
            var cam = Camera.main;
            var (pos, rot, ortho, size) = (cam.transform.position, cam.transform.rotation, cam.orthographic, cam.orthographicSize);
            cam.orthographic = true;
            cam.orthographicSize = 32f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 80f, 0f), Quaternion.Euler(90f, 0f, 0f));
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
            cam.orthographic = ortho;
            cam.orthographicSize = size;
            cam.transform.SetPositionAndRotation(pos, rot);
        }

        private static Dictionary<Soldier, Vector3> PositionsOf(Side side) =>
            Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None).Where(s => s.Unit != null && s.Unit.Side == side)
                .ToDictionary(s => s, s => s.Position);

        [Test]
        public void EndingTheTurnLetsTheAIMoveThenStartsTheNextTurn()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var before = PositionsOf(Side.AI);
            Assert.That(Turns.Current, Is.EqualTo(1));

            controller.EndTurn(instant: true);

            Assert.That(controller.IsAITurn, Is.False, "the AI's turn is over");
            Assert.That(Turns.Current, Is.EqualTo(2), "and the next turn has started");
            Assert.That(controller.ActiveUnit, Is.Null, "with no unit selected");
            int moved = before.Count(kv => Vector3.Distance(kv.Key.Position, kv.Value) > 0.5f);
            Assert.That(moved, Is.GreaterThan(10), "the AI's units advanced");
            foreach (var kv in before)
                Assert.That(MovementRules.IsInsideBattlefield(kv.Key.Position, 0f), Is.True);
        }

        [UnityTest]
        public IEnumerator TheAITurnCanBeWatchedAndSkipped()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            controller.EndTurn();
            Assert.That(controller.IsAITurn, Is.True, "the AI plays out its turn over time");
            Assert.That(controller.SelectUnit(controller.PlayerUnits[0]), Is.False, "the player can only watch");
            yield return new WaitForSeconds(1f);

            controller.GetComponent<AITurnController>().Skip();
            float waited = 0f;
            while (controller.IsAITurn && waited < 20f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.That(controller.IsAITurn, Is.False, "Skip finishes the AI's turn");
            Assert.That(Turns.Current, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AgainstAPlayerWhoNeverActsTheAIAdvancesAndAttacks()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var mine = Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None).Where(s => s.Unit.Side == Side.Player).ToList();
            int startHealth = mine.Sum(s => s.Health);
            for (int turn = 0; turn < 12 && !Victory.IsOver; turn++)
            {
                controller.EndTurn(instant: true);
                yield return null;
                if (turn == 1) TopDownSnapshot("-snapshotAI");
            }
            int healthNow = mine.Sum(s => s.IsAlive ? s.Health : 0);
            Debug.Log($"[Eldoria] After {Turns.Current - 1} AI turns: player health {healthNow}/{startHealth}, " +
                      $"{mine.Count(s => !s.IsAlive)} down, outcome {Victory.Outcome}.");
            Assert.That(healthNow, Is.LessThan(startHealth), "the AI has done damage");
        }

        [Test]
        public void WipingOutTheEnemyArmyWinsTheBattleAndStopsOrders()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var units = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
            var squad = controller.PlayerUnits.First(u => u.DisplayName == "Infantry Squad 2");
            Assert.That(controller.SelectUnit(squad), Is.True);
            Assert.That(controller.BattleOver, Is.False);

            // Every enemy soldier but one falls; the battle isn't over until the last one does.
            var enemies = units.Where(u => u.Side == Side.AI).SelectMany(u => u.LivingSoldiers).ToList();
            Assert.That(enemies.Count, Is.EqualTo(32), "3 squads of 10, a commander and a priest");
            foreach (var e in enemies.Skip(1)) e.Kill();
            Assert.That(controller.BattleOver, Is.False);
            enemies[0].Kill();

            Assert.That(controller.BattleOver, Is.True);
            Assert.That(Victory.Outcome, Is.EqualTo(BattleOutcome.PlayerWins));
            Assert.That(controller.SelectUnit(controller.PlayerUnits.First(u => u != squad)), Is.False, "no more orders");
            int turn = Turns.Current;
            controller.NextTurn();
            Assert.That(Turns.Current, Is.EqualTo(turn), "the turn doesn't advance");
        }

        [UnityTest]
        public IEnumerator ARedMarkerShowsOverSoldiersInCloseRange()
        {
            var units = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
            var mine = units.First(u => u.DisplayName == "Infantry Squad 2").Leader;
            var theirs = units.First(u => u.DisplayName == "Infantry Squad 2 (Enemy)").Soldiers.Last();
            CloseRangeMarkers.RefreshNow();
            Assert.That(CloseRangeMarkers.IsShowing(mine), Is.False);

            // An enemy walks up to within 1.5 m.
            var theirStart = theirs.transform.position;
            theirs.transform.position = mine.Position + new Vector3(1.5f, 0f, 0f);
            CloseRangeMarkers.RefreshNow();
            Assert.That(CloseRangeMarkers.IsShowing(mine), Is.True);
            Assert.That(CloseRangeMarkers.IsShowing(theirs), Is.True, "both are in close range");
            Assert.That(CloseRangeMarkers.IsShowing(units.First(u => u.DisplayName == "Infantry Squad 2").Soldiers.Last()), Is.False,
                "squadmates farther away are not");

            yield return null;
            string outPath = null;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-snapshotCloseRange") outPath = args[i + 1];
            if (outPath != null)
            {
                var cam = Camera.main;
                var look = mine.Position + new Vector3(0.75f, 1.2f, 0f);
                cam.transform.position = look + new Vector3(-1.5f, 3f, -6f);
                cam.transform.LookAt(look);
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

            // They go back where they came from: the markers go.
            theirs.transform.position = theirStart;
            CloseRangeMarkers.RefreshNow();
            Assert.That(CloseRangeMarkers.IsShowing(mine), Is.False);
            Assert.That(CloseRangeMarkers.IsShowing(theirs), Is.False);
        }

        [Test]
        public void AnInfantrySquadThrowsOneFragGrenadeAsItsActionOncePerBattle()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            var squad = controller.PlayerUnits.First(u => u.DisplayName == "Infantry Squad 2");
            Assert.That(controller.SelectUnit(squad), Is.True);

            // Ten soldiers have the ability; the unit gets one Frag Grenade button.
            var abilities = ActiveAbilities.UnitAbilities(squad);
            Assert.That(abilities.Count(a => a.ability.displayName == "Frag Grenade"), Is.EqualTo(1));
            var (user, frag) = abilities.Single(a => a.ability.displayName == "Frag Grenade");
            Assert.That(user, Is.EqualTo(squad.Leader));

            controller.BeginAbility(user, frag);
            var spot = squad.Leader.Position + new Vector3(0f, 0f, 8f);
            var r = controller.UseAbility(frag, user, null, null, spot);
            Assert.That(r.Used, Is.True, r.Reason);
            Assert.That(controller.HasTakenAction, Is.True, "throwing is the unit's action");
            Assert.That(controller.OrderShoot(Object.FindObjectsByType<Unit>(FindObjectsSortMode.None).First(u => u.Side == Side.AI)), Is.Null,
                "no shooting as well");

            controller.NextTurn();
            Assert.That(controller.SelectUnit(squad), Is.True);
            controller.BeginAbility(user, frag);
            Assert.That(controller.UseAbility(frag, user, null, null, spot).Used, Is.False, "once per battle");
            Assert.That(controller.HasTakenAction, Is.False, "the refused grenade cost nothing");
        }

        [Test]
        public void ASquadLeaderIsReplacedFromTheirOwnSquadEvenWithAnotherSquadNearby()
        {
            var squads = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None)
                .Where(u => u.Side == Side.Player && u.Template.displayName == "Infantry Squad").ToList();
            var a = squads.First();
            var b = squads.First(u => u != a);
            // Move squad B's soldiers right up against squad A's sergeant, closer than any of A's own troopers.
            var sgt = a.Leader;
            foreach (var (s, i) in b.Soldiers.Select((s, i) => (s, i)))
                s.transform.position = sgt.Position + Quaternion.Euler(0f, i * 36f, 0f) * Vector3.forward * 0.8f;

            sgt.Kill();

            Assert.That(a.Leader, Is.Not.Null);
            Assert.That(a.Leader.Unit, Is.EqualTo(a), "command passed within the squad");
            Assert.That(a.Soldiers, Does.Contain(a.Leader));
            Assert.That(b.Soldiers.Contains(a.Leader), Is.False, "not a soldier of the nearby squad");
        }
    }
}
