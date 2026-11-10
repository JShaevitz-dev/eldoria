using System;
using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Eldoria.Rules
{
    /// <summary>What the keywords involved in an attack add up to.</summary>
    public class KeywordOutcome
    {
        /// <summary>The attack has no effect at all (e.g. Toxic against a target that isn't Biological).</summary>
        public bool NoEffect;
        /// <summary>The target can't be targeted by this attack at all (e.g. In full cover behind the cover).</summary>
        public bool CannotBeTargeted;
        /// <summary>The attack ignores line of sight, and so any "only if its source is between" interactions (e.g. Indirect fire).</summary>
        public bool IgnoresLineOfSight;
        /// <summary>The attack uses the Accuracy stat with no penalties or bonuses (e.g. Indirect fire).</summary>
        public bool IgnoresAccuracyModifiers;
        /// <summary>Change to the target's Armor Level for this attack (e.g. +1 from Divine Sanctuary).</summary>
        public int ArmorModifier;
        public int DamageBonus;
        public int DamagePenalty;
        public readonly List<AccuracyModifier> AccuracyModifiers = new List<AccuracyModifier>();
        /// <summary>Which interactions applied, for display.</summary>
        public readonly List<string> Applied = new List<string>();
    }

    /// <summary>
    /// Whether a terrain object lies between an attacker and a target (e.g. a barricade giving In half-cover).
    /// Uses <see cref="Terrain.ShotGeometry"/>; replaceable (tests, special rules).
    /// </summary>
    public static class CoverCheck
    {
        public static Func<Vector3, Vector3, Component, bool> IsBetween = Terrain.ShotGeometry.IsBetween;
    }

    /// <summary>Whether the attacker can see the target. Uses <see cref="Terrain.ShotGeometry"/>; replaceable.</summary>
    public static class LineOfSight
    {
        public static Func<Soldier, Soldier, bool> CanSee = Terrain.ShotGeometry.CanSee;
    }

    /// <summary>
    /// Works out keyword interactions for an attack. Every keyword on the attacking soldier, their weapon and the
    /// target is checked; each of its interactions applies if its condition holds:
    ///   - weapon keywords and the attacker's "when attacking" interactions,
    ///   - the target's "when defending" interactions.
    /// </summary>
    public static class KeywordRules
    {
        public static KeywordOutcome Evaluate(Soldier attacker, Weapon weapon, Soldier target)
        {
            IReadOnlyList<Keyword> targetKeywords = target != null ? target.Keywords : null;
            var between = GrantedByObjectsBetween(attacker, target);
            if (between.Count > 0)
                targetKeywords = targetKeywords.Concat(between.Where(k => !Keyword.Contains(targetKeywords, k.displayName))).ToList();
            return Evaluate(attacker != null ? attacker.Keywords : null, weapon?.Keywords, targetKeywords, attacker, target);
        }

        /// <summary>
        /// Keywords the target has for this attack only, from terrain objects between the attacker and the target whose keyword
        /// grants "when between" (e.g. Full-cover gives In full cover), however far the object is from either of them.
        /// </summary>
        public static List<Keyword> GrantedByObjectsBetween(Soldier attacker, Soldier target)
        {
            var granted = new List<Keyword>();
            if (attacker == null || target == null) return granted;
            foreach (var obj in Object.FindObjectsByType<Terrain.TerrainObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            foreach (var k in obj.Keywords)
            {
                if (k == null || !k.grantsWhenBetween || k.grantsNearby == null) continue;
                if (Keyword.Contains(granted, k.grantsNearby.displayName)) continue;
                if (CoverCheck.IsBetween(attacker.Position, target.Position, obj)) granted.Add(k.grantsNearby);
            }
            return granted;
        }

        /// <param name="attacker">Optional: used to check "only if its source is between attacker and target".</param>
        /// <param name="target">Optional: as above.</param>
        public static KeywordOutcome Evaluate(IReadOnlyList<Keyword> attackerKeywords, IReadOnlyList<Keyword> weaponKeywords,
            IReadOnlyList<Keyword> targetKeywords, Soldier attacker = null, Soldier target = null)
        {
            var context = (attacker, target);
            var outcome = new KeywordOutcome();
            var parties = new Dictionary<KeywordParty, IReadOnlyList<Keyword>>
            {
                [KeywordParty.Attacker] = attackerKeywords ?? new List<Keyword>(),
                [KeywordParty.Weapon] = weaponKeywords ?? new List<Keyword>(),
                [KeywordParty.Target] = targetKeywords ?? new List<Keyword>(),
            };

            Apply(parties[KeywordParty.Weapon], null, null, parties, outcome, context);
            Apply(parties[KeywordParty.Attacker], KeywordRole.WhenAttacking, attacker, parties, outcome, context);
            Apply(parties[KeywordParty.Target], KeywordRole.WhenDefending, target, parties, outcome, context);
            return outcome;
        }

        /// <param name="role">Only interactions with this role apply; null (weapons) means all apply.</param>
        /// <param name="ownerSoldier">The soldier holding these keywords, if any (for keywords granted by terrain).</param>
        private static void Apply(IReadOnlyList<Keyword> owners, KeywordRole? role, Soldier ownerSoldier,
            Dictionary<KeywordParty, IReadOnlyList<Keyword>> parties, KeywordOutcome outcome, (Soldier attacker, Soldier target) context)
        {
            foreach (var owner in owners)
            {
                if (owner == null) continue;
                foreach (var i in owner.interactions)
                {
                    if (i == null) continue;
                    if (role.HasValue && i.role != role.Value) continue;

                    if (i.condition != KeywordCondition.Always)
                    {
                        if (i.keyword == null) continue;
                        bool has = Keyword.Contains(parties[i.party], i.keyword.displayName);
                        if (has != (i.condition == KeywordCondition.Has)) continue;
                    }
                    // Line-of-sight-based interactions (e.g. cover being between) don't apply to attacks that ignore line of sight.
                    if (i.onlyIfSourceIsBetween && (outcome.IgnoresLineOfSight || !SourceIsBetween(owner, ownerSoldier, context))) continue;

                    string source = owner.displayName;
                    switch (i.effect)
                    {
                        case KeywordEffect.NoEffect: outcome.NoEffect = true; break;
                        case KeywordEffect.CannotBeTargeted: outcome.CannotBeTargeted = true; break;
                        case KeywordEffect.IgnoresLineOfSight: outcome.IgnoresLineOfSight = true; break;
                        case KeywordEffect.IgnoresAccuracyModifiers: outcome.IgnoresAccuracyModifiers = true; break;
                        case KeywordEffect.ArmorBonus: outcome.ArmorModifier += i.amount; break;
                        case KeywordEffect.ArmorPenalty: outcome.ArmorModifier -= i.amount; break;
                        case KeywordEffect.DamageBonus: outcome.DamageBonus += i.amount; break;
                        case KeywordEffect.DamagePenalty: outcome.DamagePenalty += i.amount; break;
                        case KeywordEffect.AccuracyBonus: outcome.AccuracyModifiers.Add(AccuracyModifier.Bonus(source, i.amount)); break;
                        case KeywordEffect.AccuracyPenalty: outcome.AccuracyModifiers.Add(AccuracyModifier.Penalty(source, i.amount)); break;
                        case KeywordEffect.AlwaysHits: outcome.AccuracyModifiers.Add(AccuracyModifier.Override(source, Accuracy.AlwaysHits)); break;
                        case KeywordEffect.AlwaysMisses: outcome.AccuracyModifiers.Add(AccuracyModifier.Override(source, Accuracy.AlwaysMisses)); break;
                    }
                    outcome.Applied.Add(i.Describe(owner));
                }
            }
        }

        /// <summary>
        /// For a keyword granted by terrain: is any object granting it between the attacker and the target?
        /// Passes if the keyword wasn't granted by terrain, or if positions aren't known.
        /// </summary>
        private static bool SourceIsBetween(Keyword keyword, Soldier holder, (Soldier attacker, Soldier target) context)
        {
            if (holder == null || context.attacker == null || context.target == null) return true;
            var sources = holder.SourcesOf(keyword).ToList();
            if (sources.Count == 0) return true;
            return sources.Any(src => CoverCheck.IsBetween(context.attacker.Position, context.target.Position, src));
        }
    }
}
