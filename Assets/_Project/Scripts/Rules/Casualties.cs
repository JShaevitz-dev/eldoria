using System.Collections.Generic;
using Eldoira.Units;

namespace Eldoira.Rules
{
    /// <summary>
    /// When the dead leave the battlefield. A soldier killed during a volley (one attacking unit's round of shooting)
    /// stays until that volley ends, so soldiers who already picked them still shoot at them; when the volley ends
    /// they are removed and can't be targeted again (so the next attacking unit can't pick them).
    /// A soldier killed outside a volley is removed at once.
    /// </summary>
    public static class Casualties
    {
        private static int openVolleys;
        private static readonly List<Soldier> pending = new List<Soldier>();

        /// <summary>A volley is being resolved right now.</summary>
        public static bool VolleyInProgress => openVolleys > 0;

        /// <summary>Killed during the current volley and not yet removed (can still be shot until it ends).</summary>
        public static bool IsPendingRemoval(Soldier soldier) => soldier != null && pending.Contains(soldier);

        public static void BeginVolley() => openVolleys++;

        /// <summary>Ends a volley: everyone killed during it is removed from the battlefield. Returns them.</summary>
        public static List<Soldier> EndVolley()
        {
            if (openVolleys > 0) openVolleys--;
            if (openVolleys > 0) return new List<Soldier>();
            var removed = new List<Soldier>(pending);
            pending.Clear();
            foreach (var s in removed)
                if (s != null) s.RemoveFromPlay();
            return removed;
        }

        /// <summary>Called by <see cref="Soldier.Kill"/>.</summary>
        public static void OnKilled(Soldier soldier)
        {
            if (soldier == null) return;
            if (VolleyInProgress) { if (!pending.Contains(soldier)) pending.Add(soldier); }
            else soldier.RemoveFromPlay();
        }

        /// <summary>Forgets any open volley (new game, tests).</summary>
        public static void Reset()
        {
            openVolleys = 0;
            pending.Clear();
        }
    }
}
