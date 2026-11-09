using System.Collections.Generic;
using UnityEngine;

namespace Eldoira.Units
{
    public enum AbilityKind
    {
        /// <summary>Always in effect; the player doesn't activate it.</summary>
        Passive,
        /// <summary>The player chooses to use it, picking a target.</summary>
        Active
    }

    /// <summary>Which soldiers an ability affects or may target, relative to the soldier who has the ability.</summary>
    public enum AuraAffects
    {
        Allies,
        Enemies,
        Everyone
    }

    /// <summary>What an active ability is aimed at.</summary>
    public enum AbilityTarget
    {
        /// <summary>One soldier.</summary>
        Soldier,
        /// <summary>A whole unit: every soldier in it is affected.</summary>
        Unit,
        /// <summary>A spot on the ground (e.g. Frag Grenade): an area attack centred there.</summary>
        Point
    }

    /// <summary>
    /// An ability a soldier has (from their template).
    ///  - Passive auras (e.g. "Lead from the front") grant a keyword to soldiers within a radius of the soldier who has
    ///    the ability; the keyword carries the effect.
    ///  - Active abilities (e.g. "Divine Healing") are used by the player on a target soldier or unit within range:
    ///    they can restore health and/or give a keyword (e.g. Divine Sanctuary: +1 armor).
    /// All distances are horizontal, ignoring height.
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoira/Ability", fileName = "NewAbility", order = 5)]
    public class Ability : ScriptableObject
    {
        public string displayName = "Ability";
        [TextArea] public string description;
        public AbilityKind kind = AbilityKind.Passive;

        [Tooltip("What kind of ability this is, e.g. Spell (a Spell can't be cast if the soldier's unit sprinted this turn).")]
        public List<Keyword> keywords = new List<Keyword>();

        [Tooltip("Passive: who the aura affects. Active: who can be targeted.")]
        public AuraAffects affects = AuraAffects.Allies;
        [Tooltip("Passive: whether the soldier with the ability is affected too. Active: whether they can target themself (or their own unit).")]
        public bool includeSelf = true;

        [Header("Aura (passive)")]
        [Tooltip("Keyword given to soldiers within Radius of the soldier with this ability.")]
        public Keyword grantsNearby;
        [Tooltip("Meters, measured horizontally from the soldier with this ability.")]
        [Min(0f)] public float radius = 15f;

        [Header("Active")]
        public AbilityTarget targets = AbilityTarget.Soldier;
        [Tooltip("Meters, horizontally, from the user to the target soldier (or the closest soldier of the target unit).")]
        [Min(0f)] public float range = 25f;
        [Tooltip("A target soldier must have this keyword (e.g. Biological for healing). Empty = no requirement.")]
        public Keyword targetMustHave;
        [Tooltip("Health restored to the target (not above their starting health).")]
        [Min(0)] public int heal;
        [Tooltip("Keyword given to the target soldier, or every soldier of the target unit.")]
        public Keyword grantsKeyword;
        [Tooltip("The granted keyword wears off at the start of the next turn (otherwise it lasts until removed).")]
        public bool grantUntilNextTurn = true;
        [Tooltip("How many times a soldier can use this ability each turn.")]
        [Min(1)] public int usesPerTurn = 1;
        [Tooltip("How many times a unit can use this ability in a whole battle, shared by all its soldiers. 0 = no limit. " +
                 "Only limits like this are shown to the player.")]
        [Min(0)] public int usesPerBattle;

        [Header("Area attack (target: Point)")]
        [Tooltip("Every soldier within this many meters (horizontally) of the chosen spot is attacked - friend or foe.")]
        [Min(0f)] public float areaRadius = 2.5f;
        [Tooltip("The weapon the area attack uses: its Base Damage, Penetration and keywords (e.g. Frag Grenade: 2, 0, Explosive). " +
                 "No hit roll: everyone in the area is hit. Cover doesn't protect; armor does.")]
        public WeaponTemplate areaWeapon;

        [Header("Presentation")]
        [Tooltip("Played once when the ability is used successfully (not when a target is refused).")]
        public AudioClip castSound;

        public bool IsAura => kind == AbilityKind.Passive && grantsNearby != null;
        public bool IsActive => kind == AbilityKind.Active;
        public bool IsAreaAttack => targets == AbilityTarget.Point && areaWeapon != null;

        public bool HasKeyword(string name) => Keyword.Contains(keywords, name);

        /// <summary>Spells can't be cast by a soldier whose unit sprinted this turn.</summary>
        public bool IsSpell => HasKeyword(KeywordLibrary.Spell);

        private void OnValidate() => Keyword.RemoveDuplicates(keywords);
    }
}
