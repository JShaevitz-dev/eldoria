using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoira.Units
{
    public enum KeywordTarget
    {
        Weapon,
        Soldier,
        Any,
        TerrainObject,
        /// <summary>Classifies an ability, e.g. Spell.</summary>
        Ability
    }

    /// <summary>What kind of keyword this is.</summary>
    public enum KeywordCategory
    {
        None,
        /// <summary>A lasting property of what the soldier/weapon is, e.g. Biological, Officer, Solo.</summary>
        Trait,
        /// <summary>A temporary state gained during play, e.g. Moved, In half-cover, Led from the front.</summary>
        Status
    }

    /// <summary>Who an interaction checks for a keyword.</summary>
    public enum KeywordParty
    {
        /// <summary>The soldier making the attack.</summary>
        Attacker,
        /// <summary>The weapon being used.</summary>
        Weapon,
        /// <summary>The soldier being attacked.</summary>
        Target
    }

    public enum KeywordCondition
    {
        Has,
        Lacks,
        /// <summary>Applies whenever the keyword is involved (no other keyword is checked).</summary>
        Always
    }

    /// <summary>When a soldier's keyword interaction is checked (weapon keywords always apply when attacking).</summary>
    public enum KeywordRole
    {
        WhenAttacking,
        WhenDefending
    }

    public enum KeywordEffect
    {
        /// <summary>The attack has no effect at all: no damage and no bonus effects.</summary>
        NoEffect,
        /// <summary>Adds to the final damage calculation.</summary>
        DamageBonus,
        /// <summary>Subtracts from the final damage calculation.</summary>
        DamagePenalty,
        /// <summary>Adds to accuracy.</summary>
        AccuracyBonus,
        /// <summary>Subtracts from accuracy.</summary>
        AccuracyPenalty,
        /// <summary>Accuracy is set to "always hits".</summary>
        AlwaysHits,
        /// <summary>Accuracy is set to "always misses".</summary>
        AlwaysMisses,
        /// <summary>The target can't be chosen for a shooting attack at all (e.g. In full cover).</summary>
        CannotBeTargeted,
        /// <summary>The attack ignores line of sight, including whether cover is between attacker and target (e.g. Indirect fire).</summary>
        IgnoresLineOfSight,
        /// <summary>The attack ignores accuracy penalties and bonuses: it uses the soldier's Accuracy stat (e.g. Indirect fire).</summary>
        IgnoresAccuracyModifiers,
        /// <summary>Adds to the target's Armor Level for this attack (e.g. Divine Sanctuary: +1).</summary>
        ArmorBonus,
        /// <summary>Subtracts from the target's Armor Level for this attack (not below 0).</summary>
        ArmorPenalty
    }

    /// <summary>
    /// One way a keyword interacts with another, e.g. Toxic: "if the Target lacks Biological → No effect".
    /// </summary>
    [Serializable]
    public class KeywordInteraction
    {
        [Tooltip("For soldier keywords: whether this applies when the soldier attacks or is attacked.")]
        public KeywordRole role = KeywordRole.WhenAttacking;
        [Tooltip("Whose keywords to check.")]
        public KeywordParty party = KeywordParty.Target;
        public KeywordCondition condition = KeywordCondition.Has;
        public Keyword keyword;
        public KeywordEffect effect = KeywordEffect.DamageBonus;
        [Tooltip("Damage or accuracy points (ignored for No Effect / Always Hits / Always Misses).")]
        [Min(0)] public int amount;
        [Tooltip("For keywords granted by nearby terrain (e.g. In half-cover): only applies if that terrain object is " +
                 "between the attacker and the target. (The between check is added with line of sight; until then it always passes.)")]
        public bool onlyIfSourceIsBetween;

        public string Describe(Keyword owner)
        {
            string who = party.ToString().ToLowerInvariant();
            string kw = keyword != null ? keyword.displayName : "?";
            string between = onlyIfSourceIsBetween ? " (if its source is between attacker and target)" : "";
            string what = effect switch
            {
                KeywordEffect.NoEffect => "no effect",
                KeywordEffect.DamageBonus => $"+{amount} damage",
                KeywordEffect.DamagePenalty => $"−{amount} damage",
                KeywordEffect.AccuracyBonus => $"+{amount}% accuracy",
                KeywordEffect.AccuracyPenalty => $"−{amount}% accuracy",
                KeywordEffect.AlwaysHits => "always hits",
                KeywordEffect.CannotBeTargeted => "can't be targeted",
                KeywordEffect.IgnoresLineOfSight => "ignores line of sight",
                KeywordEffect.IgnoresAccuracyModifiers => "ignores accuracy modifiers",
                KeywordEffect.ArmorBonus => $"+{amount} armor",
                KeywordEffect.ArmorPenalty => $"−{amount} armor",
                _ => "always misses"
            };
            if (condition == KeywordCondition.Always) return $"{owner.displayName}: {what}{between}";
            return $"{owner.displayName}: {who} {(condition == KeywordCondition.Has ? "has" : "lacks")} {kw} → {what}{between}";
        }
    }

    /// <summary>When a keyword's trigger fires during shooting.</summary>
    public enum TriggerEvent
    {
        /// <summary>The soldier has just been picked as the target of an attack (shooting step 6).</summary>
        Targeted,
        /// <summary>The soldier has just taken damage (shooting step 13).</summary>
        Damaged
    }

    public enum TriggerAction
    {
        GainKeyword,
        LoseKeyword
    }

    /// <summary>Who a trigger's action applies to.</summary>
    public enum TriggerSubject
    {
        /// <summary>The soldier whose keyword triggered.</summary>
        Self,
        /// <summary>The soldier attacking them.</summary>
        Attacker
    }

    /// <summary>
    /// Something a keyword does by itself when an event happens to its soldier, e.g.
    /// "when Targeted → Self gains Alert until the next turn". Triggers interrupt shooting: they are resolved
    /// at once, then shooting carries on (so a keyword gained when targeted affects that same attack).
    /// </summary>
    [Serializable]
    public class KeywordTrigger
    {
        public TriggerEvent when = TriggerEvent.Targeted;
        public TriggerAction action = TriggerAction.GainKeyword;
        public TriggerSubject subject = TriggerSubject.Self;
        public Keyword keyword;
        [Tooltip("For Gain Keyword: the keyword wears off at the start of the next turn (otherwise it lasts until removed).")]
        public bool untilNextTurn = true;
    }

    /// <summary>
    /// A named property given to weapons (e.g. Toxic, Explosive), soldiers (e.g. Biological, Robotic)
    /// or terrain objects (e.g. Half-cover).
    /// Keywords interact with each other: each keyword lists its interactions, checked whenever it is
    /// involved in an attack (see Rules.KeywordRules). Each keyword is its own asset so new ones need no code.
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoira/Keyword", fileName = "NewKeyword", order = 3)]
    public class Keyword : ScriptableObject
    {
        public string displayName = "Keyword";
        [Tooltip("Trait (what something is) or Status (a temporary state).")]
        public KeywordCategory category = KeywordCategory.None;
        [TextArea] public string description;
        [Tooltip("What this keyword can be given to.")]
        public KeywordTarget appliesTo = KeywordTarget.Any;
        [Tooltip("How this keyword interacts with other keywords in an attack.")]
        public List<KeywordInteraction> interactions = new List<KeywordInteraction>();

        [Tooltip("Things this keyword does by itself when its soldier is targeted or damaged.")]
        public List<KeywordTrigger> triggers = new List<KeywordTrigger>();

        [Header("Granted nearby")]
        [Tooltip("On terrain objects: soldiers within Grant Radius of the object gain this keyword (e.g. Half-cover → In half-cover).")]
        public Keyword grantsNearby;
        [Tooltip("Meters, measured horizontally from the nearest part of the object.")]
        [Min(0f)] public float grantRadius = 5f;

        public bool CanApplyTo(KeywordTarget target) => appliesTo == KeywordTarget.Any || appliesTo == target;

        public bool Is(string name) => string.Equals(displayName, name, StringComparison.OrdinalIgnoreCase);

        /// <summary>Same keyword: the same asset, or one with the same name.</summary>
        public bool SameAs(Keyword other) => other != null && (other == this || Is(other.displayName));

        /// <summary>Adds the keyword unless the list already has it (each keyword is only held once).</summary>
        public static bool AddUnique(List<Keyword> list, Keyword keyword)
        {
            if (keyword == null || list.Any(k => k != null && k.SameAs(keyword))) return false;
            list.Add(keyword);
            return true;
        }

        /// <summary>Null-safe: true if both are the same keyword.</summary>
        public static bool Same(Keyword a, Keyword b) => a != null && a.SameAs(b);

        /// <summary>Removes repeated keywords from a list, keeping the first (used when editing templates).
        /// Empty slots are kept so the Inspector can still add new entries.</summary>
        public static void RemoveDuplicates(List<Keyword> list)
        {
            if (list == null) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null) continue;
                for (int j = 0; j < i; j++)
                {
                    if (Same(list[j], list[i]))
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>True if the list contains a keyword with this name (case-insensitive).</summary>
        public static bool Contains(IEnumerable<Keyword> keywords, string name) =>
            keywords != null && keywords.Any(k => k != null && k.Is(name));
    }
}
