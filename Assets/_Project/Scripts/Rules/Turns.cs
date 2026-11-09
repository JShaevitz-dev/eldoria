using System;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Rules
{
    /// <summary>
    /// The game's turn counter. Starting a turn removes soldiers killed during the previous turn, removes keywords
    /// that wear off at the start of that turn (e.g. "Moved"), then raises <see cref="TurnStarted"/>.
    /// </summary>
    public static class Turns
    {
        /// <summary>The current turn number (0 before the first turn starts).</summary>
        public static int Current { get; private set; }

        /// <summary>Raised after a turn has started and expired keywords have been removed.</summary>
        public static event Action<int> TurnStarted;

        /// <summary>Starts the next turn.</summary>
        public static int StartNextTurn()
        {
            Current++;
            foreach (var soldier in UnityEngine.Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // The dead are normally removed when the volley that killed them ends; this catches any left over.
                if (!soldier.IsAlive) soldier.RemoveFromPlay();
                else soldier.OnTurnStarted(Current);
            }
            foreach (var obj in UnityEngine.Object.FindObjectsByType<Terrain.TerrainObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                obj.OnTurnStarted(Current);
            Terrain.NearbyKeywords.RefreshAll();
            TurnStarted?.Invoke(Current);
            return Current;
        }

        /// <summary>Back to before the first turn (new game, tests).</summary>
        public static void ResetCounter()
        {
            Current = 0;
            Casualties.Reset();
        }

        /// <summary>The turn number at whose start something gained now should wear off ("until the next turn").</summary>
        public static int NextTurn => Current + 1;
    }
}
