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
using L = Eldoria.Terrain.CountrysideLayout;

namespace Eldoria.Tests
{
    /// <summary>The English Countryside map: the river (wading costs double; bridge and ford are dry), hills that block sight, farm props.</summary>
    public class CountrysidePlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadMap()
        {
            ArmySelection.Clear();
            SceneManager.LoadScene(Battlefields.EnglishCountryside.Scene);
            yield return null;
            yield return null;
        }

        private static IMoveMeasurer Measurer => BattlefieldNavigation.Measurer;

        private static Vector3 Stand(float x, float z)
        {
            Assert.That(Measurer.TryGetStandPoint(new Vector3(x, 0f, z), 0.35f, 1.8f, out var p, out var why), Is.True, $"({x}, {z}): {why}");
            return p;
        }

        /// <summary>A route straight across the river at x (from 9 m south of its centre to 9 m north).</summary>
        private static MovePath Across(float x)
        {
            float rz = L.RiverZ(x);
            var path = Measurer.Measure(Stand(x, rz - 9f), Stand(x, rz + 9f));
            Assert.That(path.Reachable, Is.True, path.Reason);
            return path;
        }

        private static float Walked(MovePath path)
        {
            float d = 0f;
            for (int i = 1; i < path.Points.Count; i++) d += Vector3.Distance(path.Points[i - 1], path.Points[i]);
            return d;
        }

        [Test]
        public void TheMapIs100By100()
        {
            Assert.That((Core.BattlefieldScale.BoardWidth, Core.BattlefieldScale.BoardDepth), Is.EqualTo((100f, 100f)));
            var zones = Object.FindObjectsByType<DeploymentZone>(FindObjectsSortMode.None);
            Assert.That(zones.Single(z => z.Side == Side.Player).transform.position.z, Is.EqualTo(-45f));
            Assert.That(zones.Single(z => z.Side == Side.AI).transform.position.z, Is.EqualTo(45f));
        }

        [Test]
        public void WadingTheRiverCostsDouble()
        {
            var path = Across(-5f);
            Assert.That(path.WadingMeters, Is.InRange(4f, 9f), "a few meters of water");
            Assert.That(path.Length, Is.EqualTo(Walked(path) + path.WadingMeters * (MovementRules.WadingCostMultiplier - 1f)).Within(0.05f),
                "every meter in the water counts twice");
            Assert.That(path.Length, Is.GreaterThan(Walked(path) + 4f));
        }

        [Test]
        public void TheBridgeAndTheFordAreDry()
        {
            var bridge = Across(L.BridgeX);
            Assert.That(bridge.WadingMeters, Is.LessThan(0.1f), "over the bridge");
            Assert.That(bridge.Length, Is.EqualTo(Walked(bridge)).Within(0.05f));
            var ford = Across(L.FordX);
            Assert.That(ford.WadingMeters, Is.LessThan(0.1f), "through the shallow ford");
        }

        [Test]
        public void RoutesUseTheBridgeWhenItsCheaper()
        {
            // Just beside the bridge: stepping over to it is cheaper than wading.
            var path = Across(L.BridgeX + L.BridgeWidth / 2f + 1.5f);
            Assert.That(path.WadingMeters, Is.LessThan(0.1f), "the route goes over the bridge");
            Assert.That(path.Points.Any(p => Mathf.Abs(p.x - L.BridgeX) < L.BridgeWidth / 2f), Is.True);
        }

        [Test]
        public void SoldiersCanStandInTheRiver()
        {
            var p = Stand(-5f, L.RiverZ(-5f));
            Assert.That(p.y, Is.LessThan(L.WaterLevel), "on the riverbed");
            Assert.That(TerrainMeasurer.InWater(p), Is.True);
            Assert.That(TerrainMeasurer.InWater(Stand(-20f, -36f)), Is.False, "fields are dry");
        }

        [UnityTest]
        public IEnumerator TheKnollBlocksLineOfSight()
        {
            var sniper = UnitCatalog.Load().Available.First(u => u.displayName == "Sniper");
            Unit Place(float x, float z) => UnitFactory.Spawn(sniper, Side.Player, new Vector3(x, L.GroundHeight(x, z), z), Quaternion.identity);
            var west = Place(L.KnollCentre.x - L.KnollRadius, L.KnollCentre.y);
            var east = Place(L.KnollCentre.x + L.KnollRadius, L.KnollCentre.y);
            var a = Place(-20f, -44f);
            var b = Place(20f, -44f);
            yield return null;
            try
            {
                Assert.That(LineOfSight.CanSee(west.Leader, east.Leader), Is.False, "the knoll's crest is in the way");
                Assert.That(LineOfSight.CanSee(a.Leader, b.Leader), Is.True, "open, flat ground");
            }
            finally
            {
                foreach (var u in new[] { west, east, a, b }) Object.Destroy(u.gameObject);
            }
        }

        [Test]
        public void FarmPropsGiveHalfCover()
        {
            var objects = Object.FindObjectsByType<TerrainObject>(FindObjectsSortMode.None);
            TerrainObject One(string name)
            {
                var found = objects.Where(o => o.DisplayName == name).ToList();
                Assert.That(found, Is.Not.Empty, name);
                foreach (var o in found) Assert.That(o.HasKeyword("Half-cover"), Is.True, $"{name} gives half-cover");
                return found[0];
            }
            Assert.That((One("Hay Bale").CanMantle, One("Hay Bale").CanClimbOnto), Is.EqualTo((true, true)));
            Assert.That(One("Haystack").CanMantle, Is.False);
            Assert.That((One("Cart").CanMantle, One("Cart").CanClimbOnto), Is.EqualTo((false, true)));
            Assert.That((One("Fence").CanMantle, One("Fence").CanClimbOnto), Is.EqualTo((true, false)));
            Assert.That(One("Well").CanMantle, Is.False);
            Assert.That(One("Bridge Parapet").CanMantle, Is.False);
        }

        [Test]
        public void TheFarmBuildingsAreThere()
        {
            var buildings = Object.FindObjectsByType<StructureTerrain>(FindObjectsSortMode.None)
                .Where(s => s.Kind == StructureKind.Building).Select(s => s.name).ToList();
            Assert.That(buildings, Does.Contain("Farmhouse").And.Contain("Barn").And.Contain("Stone Shed"));
            Assert.That(Object.FindObjectsByType<StructureTerrain>(FindObjectsSortMode.None).Count(s => s.Kind == StructureKind.Water), Is.EqualTo(1));
        }

        [Test]
        public void TheArmiesStartInTheirZonesOnStandableGround()
        {
            var zones = Object.FindObjectsByType<DeploymentZone>(FindObjectsSortMode.None);
            var units = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
            Assert.That(units.Length, Is.EqualTo(10), "the demo army on each side");
            foreach (var unit in units)
            foreach (var s in unit.Soldiers)
            {
                Assert.That(zones.Single(z => z.Side == unit.Side).Contains(s.Position), Is.True, s.name);
                Assert.That(Measurer.TryGetStandPoint(s.Position, s.Radius, s.Height, out _, out var why), Is.True, $"{s.name}: {why}");
            }
        }

        [UnityTest]
        public IEnumerator TheAIPlaysOnTheCountryside()
        {
            var controller = Object.FindFirstObjectByType<MovementController>();
            for (int turn = 0; turn < 3 && !Victory.IsOver; turn++)
            {
                controller.EndTurn(instant: true);
                yield return null;
            }
            Assert.That(Turns.Current, Is.GreaterThanOrEqualTo(3));
        }
    }
}
