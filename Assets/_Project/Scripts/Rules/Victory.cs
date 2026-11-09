using System;
using System.Linq;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Rules
{
    public enum BattleOutcome
    {
        InProgress,
        PlayerWins,
        AIWins,
        /// <summary>Both sides lost their last soldiers at the same moment (e.g. one grenade).</summary>
        Draw
    }

    /// <summary>
    /// The win condition: a side with no soldiers left loses, and the other side wins. If both sides lose their last
    /// soldiers at the same moment, it's a draw. Checked whenever soldiers are removed from the battlefield (the end of
    /// a volley or blast, or a death outside combat). Only sides that have units on the battlefield take part.
    /// </summary>
    public static class Victory
    {
        public static BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;
        public static bool IsOver => Outcome != BattleOutcome.InProgress;

        /// <summary>Raised once, when the battle is decided.</summary>
        public static event Action<BattleOutcome> BattleEnded;

        /// <summary>Living soldiers the side has on the battlefield.</summary>
        public static int SoldiersLeft(Side side) =>
            AllUnits().Where(u => u.Side == side).Sum(u => u.LivingSoldiers.Count());

        /// <summary>Checks whether a side has been wiped out. Returns the outcome (unchanged once decided).</summary>
        public static BattleOutcome Check()
        {
            if (IsOver) return Outcome;
            var units = AllUnits();
            bool hasPlayer = units.Any(u => u.Side == Side.Player), hasAI = units.Any(u => u.Side == Side.AI);
            bool playerOut = hasPlayer && SoldiersLeft(Side.Player) == 0;
            bool aiOut = hasAI && SoldiersLeft(Side.AI) == 0;

            if (playerOut && aiOut) Outcome = BattleOutcome.Draw;
            else if (aiOut) Outcome = BattleOutcome.PlayerWins;
            else if (playerOut) Outcome = BattleOutcome.AIWins;
            else return Outcome;

            Debug.Log($"[Eldoira] Battle over: {Describe(Outcome)}");
            BattleEnded?.Invoke(Outcome);
            return Outcome;
        }

        public static string Describe(BattleOutcome outcome) => outcome switch
        {
            BattleOutcome.PlayerWins => "Victory! The enemy has no soldiers left.",
            BattleOutcome.AIWins => "Defeat. You have no soldiers left.",
            BattleOutcome.Draw => "Draw. Neither side has any soldiers left.",
            _ => "The battle goes on."
        };

        /// <summary>A new battle (new game, tests).</summary>
        public static void Reset() => Outcome = BattleOutcome.InProgress;

        private static Unit[] AllUnits() =>
            UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }
}
