namespace Eldoria.Core
{
    /// <summary>
    /// Shared source of randomness for rules (tie-breaks now; dice rolls later).
    /// Re-seed for reproducible games and tests.
    /// </summary>
    public static class GameRandom
    {
        public static System.Random Shared { get; private set; } = new System.Random();

        public static void Seed(int seed) => Shared = new System.Random(seed);
    }
}
