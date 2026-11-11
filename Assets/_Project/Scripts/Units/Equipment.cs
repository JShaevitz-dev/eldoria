using System.Collections.Generic;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// A piece of equipment a soldier can be given in the army builder (if their soldier type offers it). It can give
    /// keywords, abilities and extra weapons, and change stats (added to the soldier's own; negative values subtract).
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoria/Equipment", fileName = "NewEquipment", order = 3)]
    public class Equipment : ScriptableObject
    {
        public string displayName = "Equipment";
        [TextArea] public string description;

        [Header("Gives")]
        public List<Keyword> keywords = new List<Keyword>();
        public List<Ability> abilities = new List<Ability>();
        [Tooltip("Weapons carried in addition to the soldier's own (not their primary).")]
        public List<WeaponTemplate> weapons = new List<WeaponTemplate>();

        [Header("Stat changes")]
        public int health;
        public int armorLevel;
        public int accuracy;
        [Tooltip("Meters added to the normal move (sprint is double the result).")]
        public float movement;
    }
}
