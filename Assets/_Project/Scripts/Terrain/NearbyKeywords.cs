using System;
using System.Collections.Generic;
using System.Linq;
using Eldoira.Rules;
using Eldoira.Units;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Eldoira.Terrain
{
    /// <summary>
    /// Keywords granted by being near something:
    ///  - Terrain objects: if an object has a keyword with "Grants Nearby" set (e.g. Half-cover → In half-cover),
    ///    soldiers within its Grant Radius of the nearest part of the object have the granted keyword.
    ///  - Soldier auras: if a living soldier has a passive aura ability (e.g. Lead from the front → Led from the front),
    ///    the soldiers it affects (allies/enemies, optionally itself) within its radius have the granted keyword.
    /// All distances are horizontal. Refreshed whenever a soldier moves, a move is undone, and at the start of each turn.
    /// </summary>
    public static class NearbyKeywords
    {
        private const float Epsilon = 0.01f;

        /// <summary>Works out a soldier's nearby-granted keywords as if everyone stood where <paramref name="positionOf"/> says.</summary>
        public static void Refresh(Soldier soldier, Func<Soldier, Vector3> positionOf, IEnumerable<TerrainObject> objects,
            IEnumerable<Soldier> auraSources)
        {
            Vector3 at = positionOf(soldier);
            var granted = new List<NearbyKeyword>();

            foreach (var obj in objects)
            {
                if (obj == null) continue;
                foreach (var k in obj.Keywords)
                {
                    if (k == null || k.grantsNearby == null) continue;
                    if (obj.HorizontalDistanceTo(at) <= k.grantRadius + Epsilon)
                        granted.Add(new NearbyKeyword { keyword = k.grantsNearby, source = obj });
                }
            }

            foreach (var source in auraSources)
            {
                if (source == null || !source.IsAlive) continue;
                foreach (var ability in source.Abilities)
                {
                    if (ability == null || !ability.IsAura || !Affects(ability, source, soldier)) continue;
                    if (Measure.Horizontal(positionOf(source), at) <= ability.radius + Epsilon)
                        granted.Add(new NearbyKeyword { keyword = ability.grantsNearby, source = source });
                }
            }

            soldier.SetNearbyKeywords(granted);
        }

        private static bool Affects(Ability ability, Soldier source, Soldier soldier)
        {
            if (soldier == source) return ability.includeSelf;
            bool sameSide = source.Unit != null && soldier.Unit != null && source.Unit.Side == soldier.Unit.Side;
            return ability.affects == AuraAffects.Everyone
                   || (ability.affects == AuraAffects.Allies && sameSide)
                   || (ability.affects == AuraAffects.Enemies && !sameSide);
        }

        public static void Refresh(Soldier soldier, Vector3 at)
        {
            Refresh(soldier, s => s == soldier ? at : s.Position, AllObjects(), AllSoldiers().Where(s => s.Abilities.Count > 0));
        }

        /// <summary>
        /// Refreshes every soldier. <paramref name="positionOf"/> gives where each soldier is (or will be once their
        /// planned move finishes); by default their current position.
        /// </summary>
        public static void RefreshAll(Func<Soldier, Vector3> positionOf = null)
        {
            positionOf ??= s => s.Position;
            var objects = AllObjects();
            var soldiers = AllSoldiers();
            var sources = soldiers.Where(s => s.Abilities.Count > 0).ToList();
            foreach (var s in soldiers)
                Refresh(s, positionOf, objects, sources);
        }

        private static TerrainObject[] AllObjects() =>
            Object.FindObjectsByType<TerrainObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        private static Soldier[] AllSoldiers() =>
            Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }
}
