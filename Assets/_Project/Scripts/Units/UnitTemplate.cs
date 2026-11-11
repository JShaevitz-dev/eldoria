using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>A unit's role on the battlefield (e.g. for building army lists).</summary>
    public enum UnitRole
    {
        Infantry,
        Commander,
        HeavySupport,
        Specialist,
    }

    public static class UnitRoles
    {
        /// <summary>The role as it's written in the rules: "Heavy Support", not "HeavySupport".</summary>
        /// <summary>Order for listing units: Commanders first, then Infantry, Heavy Support and Specialists.</summary>
        public static int ListOrder(this UnitRole role) => role switch
        {
            UnitRole.Commander => 0,
            UnitRole.Infantry => 1,
            UnitRole.HeavySupport => 2,
            _ => 3,
        };

        public static string DisplayName(this UnitRole role) => role switch
        {
            UnitRole.HeavySupport => "Heavy Support",
            _ => role.ToString(),
        };
    }

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

        [Tooltip("The unit's role: Infantry, Commander, Heavy Support or Specialist.")]
        public UnitRole role = UnitRole.Specialist;

        [Tooltip("How the unit plays and what it does in an army (shown in the army builder).")]
        [TextArea(3, 8)] public string description;

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
