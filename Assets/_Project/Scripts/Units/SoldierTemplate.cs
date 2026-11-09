using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Eldoira.Units
{
    /// <summary>
    /// Template for a class of soldier (e.g. "Trooper", "Sergeant"): their stats, keywords and the weapons they carry.
    /// Unit templates say which soldier templates a unit is made of.
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoira/Soldier Template", fileName = "NewSoldier", order = 1)]
    public class SoldierTemplate : ScriptableObject
    {
        public string displayName = "Soldier";

        [Tooltip("Model spawned for this soldier. Must have a Soldier component on its root.")]
        public GameObject prefab;

        [Header("Stats")]
        [Tooltip("Damage the soldier can take before dying.")]
        [FormerlySerializedAs("maxHealth")]
        [Min(1)] public int health = 4;

        [Tooltip("Protection against hits (compared against the weapon's Penetration).")]
        [FormerlySerializedAs("armor")]
        [Min(0)] public int armorLevel;

        [Tooltip("Base chance to hit, in percent.")]
        [FormerlySerializedAs("aim")]
        [Range(0, 100)] public int accuracy = 65;

        [Tooltip("Normal move, in meters. Sprinting doubles it. Only the unit leader's value is used to move the unit.")]
        [FormerlySerializedAs("move")]
        [Min(0f)] public float movementRange = 10f;

        [Header("Keywords")]
        [Tooltip("Extra properties, e.g. Sniper or Robotic.")]
        public List<Keyword> keywords = new List<Keyword>();

        [Header("Abilities")]
        [Tooltip("Abilities every soldier of this class has, e.g. Lead from the front.")]
        public List<Ability> abilities = new List<Ability>();

        [Header("Weapons")]
        [Tooltip("Weapons every soldier of this class carries.")]
        public List<WeaponTemplate> weapons = new List<WeaponTemplate>();

        [Tooltip("The weapon used to shoot under normal conditions. Must be one of the weapons above; if empty, the first is used.")]
        public WeaponTemplate primaryWeapon;

        /// <summary>The primary weapon, or the first weapon if none (or an invalid one) is set.</summary>
        public WeaponTemplate PrimaryWeapon =>
            primaryWeapon != null && weapons.Contains(primaryWeapon) ? primaryWeapon : weapons.Find(w => w != null);

        [Header("Body (not a stat: physical size on the battlefield)")]
        [Tooltip("Radius of the space the soldier occupies, in meters (spacing/collision).")]
        [Min(0.05f)] public float radius = 0.35f;
        [Tooltip("Standing height, in meters.")]
        [Min(0.1f)] public float height = 1.8f;

        public bool HasKeyword(string name) => Keyword.Contains(keywords, name);

        private void OnValidate()
        {
            Keyword.RemoveDuplicates(keywords);
            if (primaryWeapon != null && !weapons.Contains(primaryWeapon))
                Debug.LogWarning($"{name}: primary weapon {primaryWeapon.displayName} isn't in the weapons list; the first weapon will be used.", this);
        }
    }
}
