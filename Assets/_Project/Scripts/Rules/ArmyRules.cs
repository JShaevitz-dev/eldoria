using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;

namespace Eldoria.Rules
{
    /// <summary>
    /// Army list building (Docs/Rules.md, Army lists):
    ///   - the player picks a faction first; every unit must be one that faction has access to;
    ///   - an army has 6 slots: 1 to 6 units;
    ///   - exactly 1 Commander unit (at least one, and no more than one);
    ///   - no duplicates of a Commander or Specialist unit (Infantry and Heavy Support can be taken more than once).
    /// </summary>
    public static class ArmyRules
    {
        public const int MaxUnits = 6;

        /// <summary>Only one of each unit with this role per army.</summary>
        public static bool IsUnique(UnitRole role) => role == UnitRole.Commander || role == UnitRole.Specialist;

        /// <summary>Why the list isn't valid (empty if it is).</summary>
        /// <param name="faction">The army's faction: every unit must be one it has access to (null: not checked).</param>
        public static List<string> Problems(IReadOnlyList<UnitTemplate> army, Faction faction = null)
        {
            var problems = new List<string>();
            var units = (army ?? new List<UnitTemplate>()).Where(u => u != null).ToList();
            if (faction != null)
                foreach (var outsider in units.Where(u => !faction.Has(u)).Distinct())
                    problems.Add($"{outsider.displayName} isn't available to {faction.displayName}.");
            if (units.Count > MaxUnits) problems.Add($"Too many units: {units.Count} (an army has {MaxUnits} slots).");
            int commanders = units.Count(u => u.role == UnitRole.Commander);
            if (commanders == 0) problems.Add("An army needs at least 1 Commander unit.");
            if (commanders > 1) problems.Add($"Only 1 Commander unit is allowed in an army ({commanders} chosen).");
            foreach (var dupe in units.Where(u => IsUnique(u.role)).GroupBy(u => u).Where(g => g.Count() > 1))
                problems.Add($"Only one {dupe.Key.displayName} allowed ({dupe.Key.role.DisplayName()} units can't be duplicated).");
            return problems;
        }

        public static bool IsValid(IReadOnlyList<UnitTemplate> army, Faction faction = null) => Problems(army, faction).Count == 0;

        /// <summary>Null if <paramref name="unit"/> can be added to the list; otherwise why not.</summary>
        public static string WhyCannotAdd(IReadOnlyList<UnitTemplate> army, UnitTemplate unit, Faction faction = null)
        {
            if (unit == null) return "No unit";
            if (faction != null && !faction.Has(unit)) return $"Not available to {faction.displayName}";
            if (army.Count(u => u != null) >= MaxUnits) return $"All {MaxUnits} slots are full";
            if (IsUnique(unit.role) && army.Contains(unit)) return $"Already in the army ({unit.role.DisplayName()} units are unique)";
            if (unit.role == UnitRole.Commander && army.Any(u => u != null && u.role == UnitRole.Commander))
                return "The army already has its Commander (only 1 allowed)";
            return null;
        }

        /// <summary>
        /// A random valid army from the available units, filling every slot it can: 1 Commander, then random units (never a
        /// second Commander, nor a second copy of a Specialist).
        /// </summary>
        public static List<UnitTemplate> RandomArmy(IEnumerable<UnitTemplate> available, System.Random rng)
        {
            var pool = available.Where(u => u != null).Distinct().ToList();
            var army = new List<UnitTemplate>();
            var commanders = pool.Where(u => u.role == UnitRole.Commander).OrderBy(_ => rng.Next()).ToList();
            if (commanders.Count == 0) return army;   // no valid army possible
            army.Add(commanders[0]);

            while (army.Count < MaxUnits)
            {
                var options = pool.Where(u => u.role != UnitRole.Commander && WhyCannotAdd(army, u) == null).ToList();
                if (options.Count == 0) break;
                army.Add(options[rng.Next(options.Count)]);
            }
            return army;
        }

        /// <summary>What the unit is made of, e.g. "Sergeant + 9× Trooper".</summary>
        public static string Composition(UnitTemplate unit)
        {
            var parts = new List<string>();
            if (unit.leader != null) parts.Add(unit.leader.displayName);
            foreach (var m in unit.members.Where(m => m.soldier != null))
                parts.Add(m.count > 1 ? $"{m.count}× {m.soldier.displayName}" : m.soldier.displayName);
            return string.Join(" + ", parts);
        }
    }
}
