using System;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Rules
{
    /// <summary>
    /// The game's turn counter. Each turn the player's side goes first, then the AI's side (see <see cref="StartSideTurn"/>).
    /// Starting a turn removes soldiers killed and not yet removed, removes keywords that wear off at that point
    /// (e.g. "Moved"), then raises <see cref="TurnStarted"/>.
    ///
    /// "Until the start of the next turn" means until the start of the <b>caster's own</b> next turn: something the player
    /// gains on their turn lasts through the AI's turn; something the AI gains on its turn lasts through the player's turn.
    /// Expiry points are counted in side turns ("phases"): <see cref="Phase"/> = 2 × turn, +1 during the AI's side turn.
    /// </summary>
    public static class Turns
    {
        /// <summary>The current turn number (0 before the first turn starts).</summary>
        public static int Current { get; private set; }

        /// <summary>The side taking its turn right now (the player first, every turn).</summary>
        public static Side ActiveSide { get; private set; } = Side.Player;

        /// <summary>Where we are, counted in side turns: 2 × turn, +1 during the AI's side turn.</summary>
        public static int Phase => PhaseOf(Current, ActiveSide);

        private static int PhaseOf(int turn, Side side) => turn * 2 + (side == Side.AI ? 1 : 0);

        /// <summary>Raised after a turn has started and expired keywords have been removed.</summary>
        public static event Action<int> TurnStarted;

        /// <summary>Starts the next turn (the player's side goes first).</summary>
        public static int StartNextTurn()
        {
            Current++;
            ActiveSide = Side.Player;
            BeginPhase(removeDead: true);
            TurnStarted?.Invoke(Current);
            return Current;
        }

        /// <summary>
        /// A side starts its part of the current turn (the AI's side, after the player ends theirs): keywords that last
        /// "until the start of that side's next turn" wear off now.
        /// </summary>
        public static void StartSideTurn(Side side)
        {
            ActiveSide = side;
            BeginPhase(removeDead: false);
        }

        private static void BeginPhase(bool removeDead)
        {
            int phase = Phase;
            foreach (var soldier in UnityEngine.Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // The dead are normally removed when the volley that killed them ends; this catches any left over.
                if (!soldier.IsAlive) { if (removeDead) soldier.RemoveFromPlay(); }
                else soldier.OnTurnStarted(phase);
            }
            foreach (var obj in UnityEngine.Object.FindObjectsByType<Terrain.TerrainObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                obj.OnTurnStarted(phase);
            Terrain.NearbyKeywords.RefreshAll();
        }

        /// <summary>Back to before the first turn (new game, tests).</summary>
        public static void ResetCounter()
        {
            Current = 0;
            ActiveSide = Side.Player;
            Casualties.Reset();
            Victory.Reset();
        }

        /// <summary>
        /// The expiry point for something that lasts "until the start of the next turn" of <paramref name="side"/>
        /// (the caster's side): the start of that side's next side turn.
        /// </summary>
        public static int NextTurnOf(Side side)
        {
            int thisTurn = PhaseOf(Current, side);
            return thisTurn > Phase ? thisTurn : thisTurn + 2;
        }

        /// <summary>The expiry point for "until the next turn" of the side taking its turn now.</summary>
        public static int NextTurn => NextTurnOf(ActiveSide);

        /// <summary>The expiry point for "until the start of <paramref name="soldier"/>'s side's next turn".</summary>
        public static int NextTurnOf(Soldier soldier) =>
            soldier != null && soldier.Unit != null ? NextTurnOf(soldier.Unit.Side) : NextTurn;
    }
}
