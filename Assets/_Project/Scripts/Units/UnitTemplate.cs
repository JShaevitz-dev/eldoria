using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// Template for a class of unit (e.g. "Infantry Squad"): which soldier templates it is made of, and how many.
    /// The leader leads the unit when it is deployed (see leader succession in the rules).
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoria/Unit Template", fileName = "NewUnit", order = 0)]
    public class UnitTemplate : ScriptableObject
    {
        [Serializable]
        public class Member
        {
            public SoldierTemplate soldier;
            [Min(1)] public int count = 1;
        }

        public string displayName = "Unit";

        [Tooltip("The soldier who leads the unit when deployed (e.g. a Sergeant).")]
        public SoldierTemplate leader;

        [Tooltip("The rest of the unit.")]
        public List<Member> members = new List<Member>();

        [Tooltip("Meters between soldiers in the formation they deploy in.")]
        [Min(0.5f)] public float deploymentSpacing = 2f;

        /// <summary>Total soldiers including the leader.</summary>
        public int Size => (leader != null ? 1 : 0) + members.Where(m => m.soldier != null).Sum(m => m.count);

        /// <summary>Every soldier in deployment order: the leader first, then the members.</summary>
        public IEnumerable<SoldierTemplate> Roster()
        {
            if (leader != null) yield return leader;
            foreach (var m in members)
            {
                if (m.soldier == null) continue;
                for (int i = 0; i < m.count; i++) yield return m.soldier;
            }
        }
    }
}
