using System.Linq;
using NUnit.Framework;
using UnityEngine;
using L = Eldoria.Terrain.CountrysideLayout;

namespace Eldoria.Tests
{
    /// <summary>The shape of the English Countryside map.</summary>
    public class CountrysideLayoutTests
    {
        [Test]
        public void TheRiverIsWaterExceptAtTheFord()
        {
            foreach (float x in new[] { -45f, -5f, 14f, 40f })
                Assert.That(L.IsUnderWater(x, L.RiverZ(x)), Is.True, $"the river at x = {x}");
            Assert.That(L.IsUnderWater(L.FordX, L.RiverZ(L.FordX)), Is.False, "the ford is shallow enough to cross dry-shod");
            Assert.That(L.GroundHeight(L.FordX, L.RiverZ(L.FordX)), Is.LessThan(0f), "but still in the river's channel");
        }

        [Test]
        public void TheRiverCrossesTheWholeMap()
        {
            for (float x = -L.Half; x <= L.Half; x += 5f)
                Assert.That(Mathf.Abs(L.RiverZ(x)), Is.LessThan(L.Half - L.DeployDepth - 15f), $"well clear of the deployment zones at x = {x}");
        }

        [Test]
        public void LowGroundAwayFromTheRiverIsDry()
        {
            for (float x = -L.Half; x <= L.Half; x += 2.5f)
            for (float z = -L.Half; z <= L.Half; z += 2.5f)
                if (L.DistanceToRiver(x, z) > L.RiverHalfWidth + 0.5f)
                    Assert.That(L.IsUnderWater(x, z), Is.False, $"({x}, {z})");
        }

        [Test]
        public void TheDeploymentStripsAreFlat()
        {
            for (float x = -L.Half; x <= L.Half; x += 2f)
            foreach (float z in new[] { -49f, -45f, -40.5f, 40.5f, 45f, 49f })
                Assert.That(L.GroundHeight(x, z), Is.EqualTo(0f).Within(0.001f), $"({x}, {z})");
        }

        [Test]
        public void TheFarmBuildingsStandOnFlatPads()
        {
            foreach (var pad in L.Pads)
            {
                float h = L.GroundHeight(pad.Centre.x, pad.Centre.y);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8f;
                    var p = pad.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * pad.Radius * 0.95f;
                    Assert.That(L.GroundHeight(p.x, p.y), Is.EqualTo(h).Within(0.01f), pad.Name);
                }
            }
        }

        [Test]
        public void TheHillsRiseGradually()
        {
            Assert.That(L.GroundHeight(L.KnollCentre.x, L.KnollCentre.y), Is.GreaterThan(4f), "the knoll");
            var mid = (L.RidgeStart + L.RidgeEnd) / 2f;
            Assert.That(L.GroundHeight(mid.x, mid.y), Is.GreaterThan(2.5f), "the ridge");
            // No cliffs: the slope never gets steeper than about 35° anywhere.
            for (float x = -L.Half; x < L.Half; x += 1f)
            for (float z = -L.Half; z < L.Half; z += 1f)
            {
                float dx = Mathf.Abs(L.GroundHeight(x + 0.5f, z) - L.GroundHeight(x, z)) / 0.5f;
                float dz = Mathf.Abs(L.GroundHeight(x, z + 0.5f) - L.GroundHeight(x, z)) / 0.5f;
                Assert.That(Mathf.Max(dx, dz), Is.LessThan(0.7f), $"({x}, {z})");
            }
        }

        [Test]
        public void TheMapIsNotSymmetrical()
        {
            Assert.That(Mathf.Abs(L.GroundHeight(L.KnollCentre.x, L.KnollCentre.y) - L.GroundHeight(-L.KnollCentre.x, -L.KnollCentre.y)), Is.GreaterThan(2f));
            Assert.That(L.Pads.Select(p => p.Centre), Has.None.EqualTo(-L.Pads[0].Centre));
        }
    }
}
