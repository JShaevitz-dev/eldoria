using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Eldoria.Units
{
    /// <summary>
    /// Template for a class of weapon (e.g. "Assault Rifle"): its stats and keywords.
    /// Soldier templates list the weapon templates their soldiers carry.
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoria/Weapon Template", fileName = "NewWeapon", order = 2)]
    public class WeaponTemplate : ScriptableObject
    {
        public string displayName = "Weapon";

        [Header("Stats")]
        [Tooltip("Damage dealt by a hit.")]
        [FormerlySerializedAs("maxDamage")]
        [Min(0)] public int baseDamage = 3;

        [Tooltip("How many times a soldier shoots with this weapon in a volley. Each shot runs through the shooting steps, " +
                 "picking a target afresh.")]
        [Min(1)] public int shots = 1;

        [Tooltip("Off = the weapon has no penetration value (\"—\"): its attacks always do 100% damage, regardless of armor.")]
        public bool hasPenetration = true;

        [Tooltip("How well the weapon gets through armor (compared against the target's Armor Level). Ignored if Has Penetration is off.")]
        [Min(0)] public int penetration;

        /// <summary>The weapon's Penetration, or null if it has no penetration value (always 100% damage).</summary>
        public int? PenetrationValue => hasPenetration ? penetration : (int?)null;

        [Tooltip("Meters (horizontal): the farthest a target can be to be shot with this weapon. 0 = no limit.")]
        [Min(0f)] public float range;

        [Tooltip("Added to the soldier's Accuracy when attacking with this weapon (can be negative).")]
        [FormerlySerializedAs("aimModifier")]
        public int accuracyBonus;

        [Header("Keywords")]
        [Tooltip("Extra properties, e.g. Explosive or Incendiary.")]
        public List<Keyword> keywords = new List<Keyword>();

        public bool HasKeyword(string name) => Keyword.Contains(keywords, name);

        private void OnValidate() => Keyword.RemoveDuplicates(keywords);
    }
}
