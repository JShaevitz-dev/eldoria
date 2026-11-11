using System.Collections.Generic;
using System.Linq;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// Puts the chosen armies (<see cref="ArmySelection"/>) on the battlefield, replacing the armies placed in the scene.
    /// Each army deploys in its deployment zone, one unit per slot in list order, left to right as its side looks
    /// across the map; the AI's slots are the player's turned 180°. Units take their faction's colour (see PrimaryColor).
    /// </summary>
    public static class ArmyDeployment
    {
        /// <summary>The player's six deployment slots (unit leader positions) on the Urban Demo map, west to east.</summary>
        public static readonly Vector3[] PlayerSlots =
        {
            new Vector3(-32.5f, 0f, -25f), new Vector3(-19.5f, 0f, -25f), new Vector3(-6.5f, 0f, -25f),
            new Vector3(6.5f, 0f, -25f), new Vector3(19.5f, 0f, -25f), new Vector3(32.5f, 0f, -25f),
        };

        public static Vector3 SlotFor(Side side, int index) => side == Side.Player ? PlayerSlots[index] : -PlayerSlots[index];

        /// <summary>The AI's colour when it has no faction (e.g. tests): the old enemy red. The player keeps the model's own blue.</summary>
        public static readonly Color NoFactionAIColor = new Color(0.62f, 0.16f, 0.14f);

        /// <summary>Deploys the selected armies, if any were chosen. Returns true if it did.</summary>
        public static bool DeploySelected()
        {
            if (!ArmySelection.HasArmies) return false;
            Deploy(ArmySelection.PlayerArmy, ArmySelection.AIArmy, ArmySelection.PlayerFaction, ArmySelection.AIFaction);
            return true;
        }

        /// <summary>Removes every unit on the battlefield and deploys these two armies.</summary>
        public static List<Unit> Deploy(IReadOnlyList<UnitTemplate> player, IReadOnlyList<UnitTemplate> ai,
            Faction playerFaction = null, Faction aiFaction = null)
        {
            // Out with the armies placed in the scene (deactivated at once so nothing finds them this frame).
            foreach (var old in Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                old.gameObject.SetActive(false);
                Object.Destroy(old.gameObject);
            }
            var oldRoot = GameObject.Find("Armies");
            if (oldRoot != null) { oldRoot.SetActive(false); Object.Destroy(oldRoot); }

            var root = new GameObject("Armies").transform;
            var deployed = new List<Unit>();
            deployed.AddRange(DeployArmy(player, Side.Player, root, playerFaction));
            deployed.AddRange(DeployArmy(ai, Side.AI, root, aiFaction));
            return deployed;   // nearby keywords (cover, auras) are worked out when the first turn starts
        }

        private static IEnumerable<Unit> DeployArmy(IReadOnlyList<UnitTemplate> army, Side side, Transform root, Faction faction)
        {
            var units = army.Where(u => u != null).Take(ArmyRules.MaxUnits).ToList();
            var names = UnitNames(units, side);
            var facing = Quaternion.Euler(0f, side == Side.Player ? 0f : 180f, 0f);
            for (int i = 0; i < units.Count; i++)
                yield return Spawn(units[i], side, SlotFor(side, i), facing, names[i], root, faction);
        }

        /// <summary>
        /// Names for the units of an army: a unit taken more than once is numbered ("Infantry Squad 1", "Infantry Squad 2");
        /// the AI's units are marked "(Enemy)".
        /// </summary>
        public static List<string> UnitNames(IReadOnlyList<UnitTemplate> units, Side side)
        {
            string suffix = side == Side.AI ? " (Enemy)" : "";
            var counts = units.GroupBy(u => u).ToDictionary(g => g.Key, g => g.Count());
            var seen = new Dictionary<UnitTemplate, int>();
            var names = new List<string>();
            foreach (var u in units)
            {
                seen.TryGetValue(u, out int n);
                seen[u] = ++n;
                names.Add((counts[u] > 1 ? $"{u.displayName} {n}" : u.displayName) + suffix);
            }
            return names;
        }

        /// <summary>Spawns one unit: on the ground, in its faction's colour, with a marker over its leader.</summary>
        public static Unit Spawn(UnitTemplate template, Side side, Vector3 position, Quaternion facing, string name, Transform parent,
            Faction faction = null)
        {
            var unit = UnitFactory.Spawn(template, side, position, facing, parent, name);
            var catalog = UnitCatalog.Load();

            if (catalog != null && (faction != null || side == Side.AI))
                PrimaryColor.Apply(unit.gameObject, catalog.primaryColorMaterial, faction != null ? faction.color : NoFactionAIColor);

            // Stand on the ground (or whatever structure is underfoot).
            foreach (var soldier in unit.Soldiers)
            {
                var p = soldier.transform.position;
                if (Physics.Raycast(new Vector3(p.x, 50f, p.z), Vector3.down, out var hit, 100f, TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore))
                    soldier.transform.position = new Vector3(p.x, hit.point.y, p.z);
            }

            if (unit.Soldiers.Count > 1)
                unit.gameObject.AddComponent<LeaderMarker>().SetMaterial(catalog != null ? catalog.leaderMarker : null);
            return unit;
        }
    }
}
