using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Rules
{
    /// <summary>
    /// A unit's stats as text for the army builder, read straight from its templates (so they always match the game):
    /// one line per soldier type, one per weapon, one per ability.
    /// </summary>
    public static class UnitInfo
    {
        /// <summary>E.g. "9× Trooper — Health 5 · Armor 1 · Accuracy 75% · Move 10 m (sprint 20 m) · Assault Rifle".</summary>
        /// <param name="faction">The army's faction: weapons are shown under its names for them.</param>
        public static List<string> SoldierLines(UnitTemplate unit, Faction faction = null)
        {
            var lines = new List<string>();
            foreach (var group in unit.Roster().GroupBy(s => s))
            {
                var s = group.Key;
                int n = group.Count();
                string who = n > 1 ? $"{n}× {s.displayName}" : s.displayName;
                string weapons = string.Join(", ", WeaponsOf(s).Select(w => WeaponStats.For(w, faction).Name));
                lines.Add($"{who} — Health {s.health} · Armor {s.armorLevel} · Accuracy {s.accuracy}% · " +
                          $"Move {s.movementRange:0.#} m " + (s.HasKeyword(MovementRules.MechanicalKeyword) ? "(can't sprint)"
                              : $"(sprint {s.movementRange * MovementRules.SprintMultiplier:0.#} m)") + (weapons.Length > 0 ? $" · {weapons}" : ""));
            }
            return lines;
        }

        /// <summary>E.g. "Sniper Rifle — Damage 5 · Shots 1 · Penetration 1 · +50% accuracy · Range 50 m · Deployed".</summary>
        /// <param name="faction">The army's faction: its changes to the weapons' stats, and its names for them.</param>
        public static List<string> WeaponLines(UnitTemplate unit, Faction faction = null)
        {
            var lines = new List<string>();
            // Carried weapons, then weapons used through abilities (e.g. a Rocket Launcher).
            var abilityWeapons = unit.Roster().SelectMany(s => s.abilities).Where(a => a != null && a.attackWeapon != null).Select(a => a.attackWeapon);
            foreach (var w in unit.Roster().SelectMany(WeaponsOf).Concat(abilityWeapons).Distinct())
            {
                var stats = WeaponStats.For(w, faction);
                var parts = new List<string> { $"Damage {stats.BaseDamage}", $"Shots {stats.Shots}",
                    $"Penetration {(stats.PenetrationValue.HasValue ? stats.PenetrationValue.Value.ToString() : "X")}" };
                if (stats.AccuracyBonus != 0) parts.Add($"{(stats.AccuracyBonus > 0 ? "+" : "")}{stats.AccuracyBonus}% accuracy");
                parts.Add(stats.Range > 0f ? $"Range {stats.Range:0.#} m" : "Range unlimited");
                var keywords = w.keywords.Where(k => k != null).Select(k => k.displayName).ToList();
                if (keywords.Count > 0) parts.Add(string.Join(", ", keywords));
                lines.Add($"{stats.Name} — {string.Join(" · ", parts)}");
            }
            return lines;
        }

        /// <summary>E.g. "Frag Grenade (once per battle)".</summary>
        public static List<string> AbilityLines(UnitTemplate unit)
        {
            var lines = new List<string>();
            foreach (var a in unit.Roster().SelectMany(s => s.abilities).Where(a => a != null).Distinct())
            {
                var tags = new List<string> { a.IsActive ? "active" : "passive" };
                if (a.IsSpell) tags.Add("spell");
                if (a.usesPerBattle > 0) tags.Add(a.usesPerBattle == 1 ? "once per battle" : $"{a.usesPerBattle}× per battle");
                lines.Add($"{a.displayName} ({string.Join(", ", tags)})");
            }
            return lines;
        }

        /// <summary>Keywords every soldier of the unit has, e.g. "Biological, Officer, Solo".</summary>
        public static string SharedKeywords(UnitTemplate unit)
        {
            var roster = unit.Roster().ToList();
            if (roster.Count == 0) return "";
            var shared = roster[0].keywords.Where(k => k != null).Select(k => k.displayName)
                .Where(name => roster.All(s => s.HasKeyword(name))).Distinct();
            return string.Join(", ", shared);
        }

        private static IEnumerable<WeaponTemplate> WeaponsOf(SoldierTemplate s)
        {
            var primary = s.PrimaryWeapon;
            if (primary != null) yield return primary;
            foreach (var w in s.weapons)
                if (w != null && w != primary) yield return w;
        }
    }
}
