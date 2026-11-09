using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Eldoira.Core;
using Eldoira.Gameplay;
using Eldoira.Rules;
using Eldoira.Terrain;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Eldoira.Tests
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
                Assert.That(army.Count(u => u.Template.displayName == "Battlefield Priest"), Is.EqualTo(1));
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
