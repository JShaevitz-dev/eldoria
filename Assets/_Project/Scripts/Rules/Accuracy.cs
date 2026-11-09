using System.Collections.Generic;
using System.Linq;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Rules
{
    public enum AccuracyModifierType
    {
        /// <summary>Subtracted from accuracy (applied before bonuses).</summary>
        Penalty,
        /// <summary>Added to accuracy (applied after penalties).</summary>
        Bonus,
        /// <summary>Replaces the accuracy value outright; nothing else can modify it.</summary>
        Override
    }

    /// <summary>One thing changing a soldier's accuracy for an attack, e.g. "Sprinted −25%".</summary>
    public readonly struct AccuracyModifier
    {
        public readonly string Source;
        public readonly AccuracyModifierType Type;
        /// <summary>Percentage points. Penalties and bonuses are given as positive numbers.</summary>
        public readonly int Value;

        public AccuracyModifier(string source, AccuracyModifierType type, int value)
        {
            Source = source;
            Type = type;
            Value = type == AccuracyModifierType.Override ? value : Mathf.Abs(value);
        }

        public static AccuracyModifier Penalty(string source, int points) => new AccuracyModifier(source, AccuracyModifierType.Penalty, points);
        public static AccuracyModifier Bonus(string source, int points) => new AccuracyModifier(source, AccuracyModifierType.Bonus, points);
        public static AccuracyModifier Override(string source, int accuracy) => new AccuracyModifier(source, AccuracyModifierType.Override, accuracy);

        /// <summary>A signed adjustment: positive becomes a bonus, negative a penalty (e.g. a weapon's Accuracy Bonus).</summary>
        public static AccuracyModifier Signed(string source, int points) =>
            points >= 0 ? Bonus(source, points) : Penalty(source, -points);

        public override string ToString() => Type switch
        {
            AccuracyModifierType.Penalty => $"{Source} −{Value}%",
            AccuracyModifierType.Bonus => $"{Source} +{Value}%",
            _ => $"{Source} = {Value}%"
        };
    }

    /// <summary>The worked-out accuracy for an attack, with each step for display.</summary>
    public class AccuracyResult
    {
        public int BaseAccuracy;
        public int AfterPenalties;
        public int Final;
        /// <summary>The override that set the final value, if any.</summary>
        public AccuracyModifier? AppliedOverride;
        public readonly List<AccuracyModifier> Penalties = new List<AccuracyModifier>();
        public readonly List<AccuracyModifier> Bonuses = new List<AccuracyModifier>();

        public bool WasOverridden => AppliedOverride.HasValue;

        /// <summary>Chance the hit roll succeeds: accuracy up to 100%.</summary>
        public int HitChance => Mathf.Clamp(Final, 0, 100);

        /// <summary>Chance the crit roll succeeds: accuracy above 100% (e.g. 105% → 5%), up to 100%.</summary>
        public int CritChance => Mathf.Clamp(Final - 100, 0, 100);
    }

    /// <summary>The outcome of an attack's hit roll and crit roll.</summary>
    public readonly struct AttackRoll
    {
        public readonly bool Hit;
        public readonly bool Crit;
        /// <summary>d100 results (1–100); a roll succeeds when it is at or below the chance.</summary>
        public readonly int HitRoll;
        public readonly int CritRoll;

        public AttackRoll(bool hit, bool crit, int hitRoll, int critRoll)
        {
            Hit = hit;
            Crit = crit;
            HitRoll = hitRoll;
            CritRoll = critRoll;
        }
    }

    /// <summary>
    /// Hit chance is worked out by adding and subtracting percentage points, in this order:
    ///   1. Start with the soldier's Accuracy stat.
    ///   2. Subtract every penalty.
    ///   3. Add every bonus.
    ///   If any modifier explicitly overrides accuracy, its value is used instead and nothing else modifies it.
    ///   If several overrides apply, the lowest wins (so "always misses" beats "always hits").
    /// Example: Accuracy 100%, sprinted (−25%) → 75%, weapon bonus (+20%) → 95%.
    /// Accuracy can't go below 0% but can go above 100%: the excess is the chance of a critical hit
    /// (105% → 100% to hit, then a 5% crit roll). Above 200% nothing more happens.
    /// </summary>
    public static class Accuracy
    {
        /// <summary>Penalty, in percentage points, for attacking in a turn the soldier's unit sprinted.</summary>
        public const int SprintPenalty = 25;

        public const int Min = 0;

        /// <summary>Override value for "always hits" (100% to hit; no extra crit chance).</summary>
        public const int AlwaysHits = 100;
        /// <summary>Override value for "always misses".</summary>
        public const int AlwaysMisses = 0;

        public static AccuracyResult Calculate(int accuracyStat, IEnumerable<AccuracyModifier> modifiers)
        {
            var list = modifiers?.ToList() ?? new List<AccuracyModifier>();
            var result = new AccuracyResult { BaseAccuracy = accuracyStat };
            result.Penalties.AddRange(list.Where(m => m.Type == AccuracyModifierType.Penalty));
            result.Bonuses.AddRange(list.Where(m => m.Type == AccuracyModifierType.Bonus));

            var overrides = list.Where(m => m.Type == AccuracyModifierType.Override).ToList();
            if (overrides.Count > 0)
            {
                // If more than one override applies, the lowest is used ("always misses" beats "always hits").
                var chosen = overrides.OrderBy(m => m.Value).First();
                result.AppliedOverride = chosen;
                result.AfterPenalties = chosen.Value;
                result.Final = Mathf.Max(Min, chosen.Value);
                return result;
            }

            int value = accuracyStat - result.Penalties.Sum(m => m.Value);
            value = Mathf.Max(Min, value);
            result.AfterPenalties = value;
            value += result.Bonuses.Sum(m => m.Value);
            result.Final = value;
            return result;
        }

        /// <summary>Rolls to hit, and if the attack hits, rolls for a crit.</summary>
        public static AttackRoll Roll(AccuracyResult accuracy, System.Random rng)
        {
            int hitRoll = rng.Next(1, 101);
            bool hit = hitRoll <= accuracy.HitChance;
            int critRoll = 0;
            bool crit = false;
            if (hit)
            {
                critRoll = rng.Next(1, 101);
                crit = critRoll <= accuracy.CritChance;
            }
            return new AttackRoll(hit, crit, hitRoll, critRoll);
        }

        /// <summary>
        /// The standard modifiers for a soldier attacking with a weapon: the sprint penalty if their unit sprinted
        /// this turn, and the weapon's Accuracy Bonus. Pass any others (cover, range, keywords...) in <paramref name="extra"/>.
        /// </summary>
        /// <param name="ignoreModifiers">E.g. Indirect fire: no penalties or bonuses apply (overrides still do).</param>
        public static AccuracyResult ForAttack(Soldier shooter, Weapon weapon, bool sprinted, IEnumerable<AccuracyModifier> extra = null,
            bool ignoreModifiers = false)
        {
            if (ignoreModifiers)
                return Calculate(shooter.Accuracy, extra?.Where(m => m.Type == AccuracyModifierType.Override));

            var mods = new List<AccuracyModifier>();
            if (sprinted) mods.Add(AccuracyModifier.Penalty("Sprinted", SprintPenalty));
            if (weapon != null && weapon.AccuracyBonus != 0) mods.Add(AccuracyModifier.Signed(weapon.Name, weapon.AccuracyBonus));
            if (extra != null) mods.AddRange(extra);
            return Calculate(shooter.Accuracy, mods);
        }
    }
}
