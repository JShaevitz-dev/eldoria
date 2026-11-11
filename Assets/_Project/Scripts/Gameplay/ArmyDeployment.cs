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
    /// Each army deploys in its side's deployment zone on the map being played, one unit per slot in list order, left
    /// to right as its side looks across the map. Units take their faction's colour (see PrimaryColor).
    /// </summary>
    public static class ArmyDeployment
    {
        /// <summary>
        /// A deployment slot (unit leader position) in a zone: the six slots are spread evenly across the zone's width,
        /// left to right as the side looks across the map (the player looks north, the AI south), a little forward of
        /// the zone's middle so the unit's back ranks stay inside it.
        /// </summary>
        public static Vector3 Slot(Vector3 zoneCentre, Vector2 zoneSize, Side side, int index)
        {
            float forward = side == Side.Player ? 1f : -1f;   // towards the enemy
            float across = (index + 0.5f) * zoneSize.x / ArmyRules.MaxUnits - zoneSize.x / 2f;
            return zoneCentre + new Vector3(across * forward, 0f, forward * zoneSize.y * 0.125f);
        }

        /// <summary>The slot in the current map's deployment zone for that side (the Boxed in's zones if the map has none).</summary>
        public static Vector3 SlotFor(Side side, int index)
        {
            var zone = Object.FindObjectsByType<DeploymentZone>(FindObjectsSortMode.None).FirstOrDefault(z => z.Side == side);
            if (zone != null) return Slot(zone.transform.position, zone.Size, side, index);
            return Slot(new Vector3(0f, 0f, side == Side.Player ? -26f : 26f), new Vector2(80f, 8f), side, index);
        }

        /// <summary>The AI's colour when it has no faction (e.g. tests): the old enemy red. The player keeps the model's own blue.</summary>
        public static readonly Color NoFactionAIColor = new Color(0.62f, 0.16f, 0.14f);

        /// <summary>Deploys the selected armies, if any were chosen. Returns true if it did.</summary>
        public static bool DeploySelected()
        {
            if (!ArmySelection.HasArmies) return false;
            Deploy(ArmySelection.PlayerArmy, ArmySelection.AIArmy, ArmySelection.PlayerFaction, ArmySelection.AIFaction, ArmySelection.PlayerLoadouts);
            return true;
        }

        /// <summary>Removes every unit on the battlefield and deploys these two armies.</summary>
        public static List<Unit> Deploy(IReadOnlyList<UnitTemplate> player, IReadOnlyList<UnitTemplate> ai,
            Faction playerFaction = null, Faction aiFaction = null, IReadOnlyList<UnitLoadout> playerLoadouts = null)
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
            deployed.AddRange(DeployArmy(player, Side.Player, root, playerFaction, playerLoadouts));
            deployed.AddRange(DeployArmy(ai, Side.AI, root, aiFaction, null));
            return deployed;   // nearby keywords (cover, auras) are worked out when the first turn starts
        }

        private static IEnumerable<Unit> DeployArmy(IReadOnlyList<UnitTemplate> army, Side side, Transform root, Faction faction,
            IReadOnlyList<UnitLoadout> loadouts)
        {
            var units = army.Where(u => u != null).Take(ArmyRules.MaxUnits).ToList();
            var names = UnitNames(units, side);
            var facing = Quaternion.Euler(0f, side == Side.Player ? 0f : 180f, 0f);
            for (int i = 0; i < units.Count; i++)
                yield return Spawn(units[i], side, SlotFor(side, i), facing, names[i], root, faction,
                    loadouts != null && i < loadouts.Count ? loadouts[i] : null);
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

        /// <summary>Gives each soldier of the unit their type's army builder choices (weapon swaps, equipment), if any.</summary>
        public static void ApplyLoadout(Unit unit, UnitLoadout loadout)
        {
            if (unit == null || loadout == null) return;
            foreach (var s in unit.Soldiers)
            {
                var l = loadout.Get(s.Template);
                if (l != null) s.ApplyLoadout(l);
            }
        }

        /// <summary>Spawns one unit: on the ground, in its faction's colour, with a marker over its leader.</summary>
        public static Unit Spawn(UnitTemplate template, Side side, Vector3 position, Quaternion facing, string name, Transform parent,
            Faction faction = null, UnitLoadout loadout = null)
        {
            var unit = UnitFactory.Spawn(template, side, position, facing, parent, name);
            unit.SetFaction(faction);
            ApplyLoadout(unit, loadout);
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
