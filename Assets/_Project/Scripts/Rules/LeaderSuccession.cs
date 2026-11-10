using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Rules
{
    public readonly struct SuccessionResult
    {
        public readonly Soldier NewLeader;
        /// <summary>How many soldiers were tied for closest (1 = no tie; >1 = decided by a random roll).</summary>
        public readonly int TiedCandidates;

        public SuccessionResult(Soldier newLeader, int tiedCandidates)
        {
            NewLeader = newLeader;
            TiedCandidates = tiedCandidates;
        }

        public bool WasTie => TiedCandidates > 1;
    }

    /// <summary>
    /// When a squad leader is killed, the living squad member physically closest to where
    /// the leader fell takes command. Ties (within <see cref="TieTolerance"/>) are settled at random.
    /// </summary>
    public static class LeaderSuccession
    {
        /// <summary>Distances closer than this (meters) count as a tie.</summary>
        public const float TieTolerance = 0.01f;

        public static SuccessionResult ChooseSuccessor(Vector3 fallenPosition, IEnumerable<Soldier> candidates, System.Random rng)
        {
            var ranked = candidates
                .Where(m => m != null && m.IsAlive)
                .Select(m => (model: m, dist: Measure.Horizontal(m.Position, fallenPosition)))
                .OrderBy(x => x.dist)
                .ToList();

            if (ranked.Count == 0) return new SuccessionResult(null, 0);

            float best = ranked[0].dist;
            var tied = ranked.Where(x => x.dist - best <= TieTolerance).Select(x => x.model).ToList();
            var chosen = tied.Count == 1 ? tied[0] : tied[rng.Next(tied.Count)];
            return new SuccessionResult(chosen, tied.Count);
        }
    }
}
