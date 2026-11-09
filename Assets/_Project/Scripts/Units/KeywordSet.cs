using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoira.Units
{
    /// <summary>A keyword gained during the game, with when it wears off.</summary>
    [Serializable]
    public class GainedKeyword
    {
        public Keyword keyword;
        [Tooltip("Removed at the start of this turn number. 0 = lasts until removed.")]
        public int expiresAtTurn;
    }

    /// <summary>A keyword granted by something nearby (e.g. In half-cover from a barricade), and its source.</summary>
    [Serializable]
    public class NearbyKeyword
    {
        public Keyword keyword;
        public Component source;
    }

    /// <summary>
    /// The keywords a soldier or weapon has right now: its inherent keywords (from its template),
    /// minus any it has lost, plus any it has gained during the game.
    /// A keyword is only ever held once: keywords are matched by name, and gaining one already held does
    /// not add a second copy (it can only extend how long it lasts).
    /// </summary>
    [Serializable]
    public class KeywordSet
    {
        [SerializeField] private List<GainedKeyword> gained = new List<GainedKeyword>();
        [SerializeField] private List<Keyword> lost = new List<Keyword>();

        public IReadOnlyList<GainedKeyword> Gained => gained;
        public IReadOnlyList<Keyword> Lost => lost;

        /// <summary>Current keywords given the owner's inherent ones.</summary>
        public List<Keyword> Current(IEnumerable<Keyword> inherent)
        {
            var result = new List<Keyword>();
            if (inherent != null)
                foreach (var k in inherent)
                    if (k != null && !lost.Any(l => Keyword.Same(l, k))) Keyword.AddUnique(result, k);
            foreach (var g in gained)
                Keyword.AddUnique(result, g.keyword);
            return result;
        }

        /// <param name="inherent">The owner's inherent keywords: gaining one of those (that wasn't lost) changes nothing.</param>
        /// <param name="expiresAtTurn">Expiry point (see Rules.Turns.NextTurnOf) at whose start the keyword is removed; 0 = until removed.</param>
        public void Gain(Keyword keyword, IEnumerable<Keyword> inherent, int expiresAtTurn = 0)
        {
            if (keyword == null) return;

            // Regaining a lost inherent keyword just restores it.
            if (lost.RemoveAll(l => Keyword.Same(l, keyword)) > 0) return;
            if (inherent != null && inherent.Any(k => Keyword.Same(k, keyword))) return;

            var existing = gained.FirstOrDefault(g => Keyword.Same(g.keyword, keyword));
            if (existing == null)
            {
                gained.Add(new GainedKeyword { keyword = keyword, expiresAtTurn = expiresAtTurn });
                return;
            }
            // Keep the longer-lasting of the two.
            if (existing.expiresAtTurn != 0 && (expiresAtTurn == 0 || expiresAtTurn > existing.expiresAtTurn))
                existing.expiresAtTurn = expiresAtTurn;
        }

        /// <summary>Removes a keyword, whether gained or inherent. Returns false if the owner didn't have it.</summary>
        public bool Lose(Keyword keyword, IEnumerable<Keyword> inherent)
        {
            if (keyword == null) return false;
            bool had = gained.RemoveAll(g => Keyword.Same(g.keyword, keyword)) > 0;
            var own = inherent?.FirstOrDefault(k => Keyword.Same(k, keyword));
            if (own != null && !lost.Any(l => Keyword.Same(l, keyword)))
            {
                lost.Add(own);
                had = true;
            }
            return had;
        }

        /// <summary>Removes gained keywords that wear off at the start of <paramref name="turn"/>. Returns them.</summary>
        public List<Keyword> ExpireAtTurnStart(int turn)
        {
            var expired = gained.Where(g => g.expiresAtTurn != 0 && g.expiresAtTurn <= turn).Select(g => g.keyword).ToList();
            gained.RemoveAll(g => g.expiresAtTurn != 0 && g.expiresAtTurn <= turn);
            return expired;
        }

        public void Clear()
        {
            gained.Clear();
            lost.Clear();
        }
    }
}
