namespace Eldoria.Core
{
    /// <summary>
    /// World scale conventions for the battlefield.
    /// 1 Unity unit == 1 meter. All rule distances (movement, ranges, coherency) are in meters.
    /// The board is centred on the origin; its size is set by the map being played
    /// (see Terrain.BattlefieldNavigation), and defaults to the 60 x 44 m test room.
    /// </summary>
    public static class BattlefieldScale
    {
        /// <summary>The test room's footprint, in meters.</summary>
        public const float DefaultWidth = 60f;
        public const float DefaultDepth = 44f;

        /// <summary>Size of the battlefield being played (east-west), in meters.</summary>
        public static float BoardWidth { get; private set; } = DefaultWidth;
        /// <summary>Size of the battlefield being played (north-south), in meters.</summary>
        public static float BoardDepth { get; private set; } = DefaultDepth;

        public static void SetBoard(float width, float depth)
        {
            BoardWidth = width;
            BoardDepth = depth;
        }

        public static void ResetBoard() => SetBoard(DefaultWidth, DefaultDepth);
    }
}
