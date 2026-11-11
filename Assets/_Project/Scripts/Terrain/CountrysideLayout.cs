using UnityEngine;

namespace Eldoria.Terrain
{
    /// <summary>
    /// The shape of the English Countryside map (100 × 100 m, centred on the origin, not symmetrical): gently rolling
    /// fields, a knoll in the north-west, a long ridge in the south-west, a low rise in the south field, a river
    /// meandering west to east across the middle with a stone bridge and a shallow ford, and flat pads for the farm
    /// buildings. Used by the map builder (Editor) and by the tests.
    /// </summary>
    public static class CountrysideLayout
    {
        public const float Size = 100f;
        public const float Half = Size / 2f;
        public const float DeployDepth = 10f;

        // ---- River ----

        /// <summary>The water surface. Riverbed below it is water (wading); ground above it is dry.</summary>
        public const float WaterLevel = -0.45f;
        /// <summary>Half the width of the river valley (water plus banks).</summary>
        public const float RiverHalfWidth = 5f;
        public const float RiverDepth = 1.1f;

        /// <summary>The ford: the river is shallow enough to walk across dry-shod around this x.</summary>
        public const float FordX = -30f;
        public const float FordHalfLength = 3.5f;
        public const float FordDepth = 0.35f;

        /// <summary>The stone bridge crosses the river at this x.</summary>
        public const float BridgeX = 14f;
        public const float BridgeWidth = 4f;

        /// <summary>The river's centre line (z) at a given x: it meanders as it crosses the map west to east.</summary>
        public static float RiverZ(float x) => 4f + 7f * Mathf.Sin(0.055f * x + 0.6f) + 2f * Mathf.Sin(0.13f * x);

        /// <summary>The river's direction (horizontal unit vector, pointing east) at x.</summary>
        public static Vector3 RiverDirection(float x)
        {
            float dz = 7f * 0.055f * Mathf.Cos(0.055f * x + 0.6f) + 2f * 0.13f * Mathf.Cos(0.13f * x);
            return new Vector3(1f, 0f, dz).normalized;
        }

        /// <summary>Horizontal distance from a point to the river's centre line (good enough for a gentle meander).</summary>
        public static float DistanceToRiver(float x, float z)
        {
            var dir = RiverDirection(x);
            return Mathf.Abs((z - RiverZ(x)) * dir.x);
        }

        // ---- Hills ----

        public static readonly Vector2 KnollCentre = new Vector2(-34f, 20f);
        public const float KnollRadius = 16f, KnollHeight = 4.5f;

        public static readonly Vector2 RidgeStart = new Vector2(-44f, -24f), RidgeEnd = new Vector2(-14f, -14f);
        public const float RidgeHalfWidth = 11f, RidgeHeight = 3.5f;

        public static readonly Vector2 RiseCentre = new Vector2(12f, -26f);
        public const float RiseRadius = 9f, RiseHeight = 2f;

        // ---- Farm buildings (each stands on a flat pad) ----

        public struct Pad
        {
            public string Name;
            public Vector2 Centre;
            public float Radius;
        }

        public static readonly Pad[] Pads =
        {
            new Pad { Name = "Farmhouse", Centre = new Vector2(26f, 26f), Radius = 8f },
            new Pad { Name = "Barn", Centre = new Vector2(30f, -10f), Radius = 10f },
            new Pad { Name = "Stone Shed", Centre = new Vector2(-6f, -28f), Radius = 5f },
        };

        // ---- Height ----

        /// <summary>Ground height (m) at a point: hills and rolling fields, the river cut into them, flat pads, flat deployment strips.</summary>
        public static float GroundHeight(float x, float z)
        {
            float h = Unpadded(x, z);
            foreach (var pad in Pads)
            {
                float d = Vector2.Distance(new Vector2(x, z), pad.Centre);
                float padH = Unpadded(pad.Centre.x, pad.Centre.y);
                h = Mathf.Lerp(padH, h, Smooth(pad.Radius, pad.Radius + 5f, d));
            }
            return h;
        }

        /// <summary>The ground before the building pads are levelled.</summary>
        private static float Unpadded(float x, float z)
        {
            float h = 0.6f * Mathf.Sin(0.07f * x + 0.4f) * Mathf.Sin(0.05f * z + 1.1f) + 0.35f * Mathf.Sin(0.11f * x - 0.13f * z);
            h += Bump(new Vector2(x, z), KnollCentre, KnollRadius, KnollHeight);
            h += Bump(new Vector2(x, z), RiseCentre, RiseRadius, RiseHeight);
            h += RidgeHeightAt(new Vector2(x, z));

            // The deployment strips along the north and south edges are flat; so is the river's flood plain.
            h *= 1f - Smooth(Half - DeployDepth - 6f, Half - DeployDepth, Mathf.Abs(z));
            float river = DistanceToRiver(x, z);
            h *= Smooth(RiverHalfWidth, RiverHalfWidth + 15f, river);

            // The river cut into the plain: a cosine trough, shallow at the ford.
            if (river < RiverHalfWidth)
            {
                float ford = 1f - Smooth(FordHalfLength, FordHalfLength + 3f, Mathf.Abs(x - FordX));
                float depth = Mathf.Lerp(RiverDepth, FordDepth, ford);
                h -= depth * 0.5f * (1f + Mathf.Cos(Mathf.PI * river / RiverHalfWidth));
            }
            return h;
        }

        private static float Bump(Vector2 p, Vector2 centre, float radius, float height)
        {
            float d = Vector2.Distance(p, centre);
            return d < radius ? height * 0.5f * (1f + Mathf.Cos(Mathf.PI * d / radius)) : 0f;
        }

        private static float RidgeHeightAt(Vector2 p)
        {
            Vector2 ab = RidgeEnd - RidgeStart;
            float t = Mathf.Clamp01(Vector2.Dot(p - RidgeStart, ab) / ab.sqrMagnitude);
            float across = Vector2.Distance(p, RidgeStart + ab * t);
            float along = t * ab.magnitude;
            float taper = Mathf.Clamp01(Mathf.Min(along, ab.magnitude - along) / 6f);
            return across < RidgeHalfWidth ? RidgeHeight * taper * 0.5f * (1f + Mathf.Cos(Mathf.PI * across / RidgeHalfWidth)) : 0f;
        }

        /// <summary>0 below a, 1 above b, smooth in between.</summary>
        private static float Smooth(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

        /// <summary>True if the ground here is under water (wading): in the river's channel, below the water surface.</summary>
        public static bool IsUnderWater(float x, float z) => IsUnderWater(x, z, GroundHeight(x, z));

        /// <summary>As above, for a known ground height (low ground away from the river is dry, whatever its height).</summary>
        public static bool IsUnderWater(float x, float z, float height) =>
            DistanceToRiver(x, z) < RiverHalfWidth && height < WaterLevel;
    }
}
