using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// Builds a <see cref="Unit"/> on the battlefield from a <see cref="UnitTemplate"/>: one soldier per roster slot,
    /// each from its soldier template's prefab and carrying that template's weapons.
    /// Soldiers deploy in ranks of up to five, facing forward, with the leader at the centre of the front rank.
    /// </summary>
    public static class UnitFactory
    {
        public const int RankWidth = 5;

        /// <param name="instantiate">How to create soldier objects (prefab, parent). The editor passes one that keeps prefab links.</param>
        public static Unit Spawn(UnitTemplate template, Side side, Vector3 position, Quaternion facing,
            Transform parent = null, string unitName = null, Func<GameObject, Transform, GameObject> instantiate = null)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            instantiate ??= (prefab, p) => UnityEngine.Object.Instantiate(prefab, p);

            var unitGo = new GameObject(unitName ?? template.displayName);
            unitGo.transform.SetParent(parent, false);
            unitGo.transform.SetPositionAndRotation(position, facing);
            var unit = unitGo.AddComponent<Unit>();
            unit.Configure(template, side, unitName);

            var roster = template.Roster().ToList();
            var slots = FormationSlots(roster.Count, template.deploymentSpacing);
            var counts = new Dictionary<SoldierTemplate, int>();

            for (int i = 0; i < roster.Count; i++)
            {
                var soldierTemplate = roster[i];
                bool isLeader = i == 0 && template.leader != null;

                GameObject go = soldierTemplate.prefab != null
                    ? instantiate(soldierTemplate.prefab, unitGo.transform)
                    : Placeholder(unitGo.transform);
                counts.TryGetValue(soldierTemplate, out int n);
                counts[soldierTemplate] = ++n;
                go.name = isLeader ? soldierTemplate.displayName : $"{soldierTemplate.displayName}_{n:00}";
                go.transform.localPosition = slots[i];
                go.transform.localRotation = Quaternion.identity;

                var soldier = go.GetComponent<Soldier>();
                if (soldier == null) soldier = go.AddComponent<Soldier>();
                soldier.Configure(soldierTemplate, isLeader);
            }

            unit.RefreshSoldiers();
            return unit;
        }

        /// <summary>
        /// Local positions for a unit of <paramref name="count"/> soldiers: ranks of up to five, spaced
        /// <paramref name="spacing"/> apart, extending backwards. Slot 0 is the centre of the front rank.
        /// </summary>
        public static List<Vector3> FormationSlots(int count, float spacing)
        {
            var slots = new List<Vector3>();
            int ranks = Mathf.CeilToInt(count / (float)RankWidth);
            for (int r = 0; r < ranks; r++)
            {
                int inRank = Mathf.Min(RankWidth, count - r * RankWidth);
                // Fill from the centre outwards: 0, -1, +1, -2, +2 ...
                for (int k = 0; k < inRank; k++)
                {
                    int offset = (k + 1) / 2 * (k % 2 == 1 ? -1 : 1);
                    float x = offset * spacing + (inRank % 2 == 0 ? spacing / 2f : 0f);
                    slots.Add(new Vector3(x, 0f, -r * spacing));
                }
            }
            return slots;
        }

        private static GameObject Placeholder(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
