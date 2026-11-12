using System;
using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Network
{
    /// <summary>One message between the two games: a type and a JSON payload.</summary>
    [Serializable]
    public class NetEnvelope
    {
        public string type;
        public string json;

        public static string Pack(string type, object payload) =>
            JsonUtility.ToJson(new NetEnvelope { type = type, json = payload != null ? JsonUtility.ToJson(payload) : "" });

        public static NetEnvelope Unpack(string line)
        {
            try { return JsonUtility.FromJson<NetEnvelope>(line); }
            catch { return null; }
        }

        public T Payload<T>() => JsonUtility.FromJson<T>(json);
    }

    public static class MessageTypes
    {
        public const string Hello = "hello";
        public const string Map = "map";
        public const string Army = "army";
        public const string Start = "start";
        public const string Order = "order";
        public const string Bye = "bye";
    }

    [Serializable]
    public class HelloMsg
    {
        public int version;
        public string build;
    }

    [Serializable]
    public class MapMsg
    {
        public string scene;
    }

    /// <summary>One soldier type's army builder choices, by asset name.</summary>
    [Serializable]
    public class LoadoutEntry
    {
        public int slot;
        public string soldier;
        public List<string> swapFrom = new List<string>();
        public List<string> swapTo = new List<string>();
        public List<string> equipment = new List<string>();
    }

    /// <summary>A player's army list: faction, units and loadouts, by asset name (both games have the same assets).</summary>
    [Serializable]
    public class ArmyMsg
    {
        public string faction;
        public List<string> units = new List<string>();
        public List<LoadoutEntry> loadouts = new List<LoadoutEntry>();

        public static ArmyMsg From(Faction faction, IReadOnlyList<UnitTemplate> army, IReadOnlyList<UnitLoadout> loadouts)
        {
            var msg = new ArmyMsg { faction = faction != null ? faction.name : "" };
            for (int i = 0; i < army.Count; i++)
            {
                var unit = army[i];
                msg.units.Add(unit.name);
                var loadout = loadouts != null && i < loadouts.Count ? loadouts[i] : null;
                if (loadout == null) continue;
                foreach (var soldier in unit.Roster().Distinct())
                {
                    var l = loadout.Get(soldier);
                    if (l == null) continue;
                    var entry = new LoadoutEntry { slot = i, soldier = soldier.name };
                    foreach (var kv in l.Swaps) { entry.swapFrom.Add(kv.Key.name); entry.swapTo.Add(kv.Value.name); }
                    entry.equipment.AddRange(l.Equipment.Where(e => e != null).Select(e => e.name));
                    msg.loadouts.Add(entry);
                }
            }
            return msg;
        }

        /// <summary>Rebuilds the army from the catalog. False (with a reason) if anything isn't recognised.</summary>
        public bool TryResolve(UnitCatalog catalog, out Faction faction, out List<UnitTemplate> army, out List<UnitLoadout> loadouts, out string error)
        {
            faction = catalog.Factions.FirstOrDefault(f => f.name == this.faction);
            army = new List<UnitTemplate>();
            loadouts = new List<UnitLoadout>();
            error = null;
            if (faction == null) { error = $"Unknown faction '{this.faction}'"; return false; }
            foreach (var name in units)
            {
                var unit = catalog.Available.FirstOrDefault(u => u.name == name);
                if (unit == null) { error = $"Unknown unit '{name}'"; return false; }
                army.Add(unit);
                loadouts.Add(new UnitLoadout());
            }
            foreach (var e in this.loadouts)
            {
                if (e.slot < 0 || e.slot >= army.Count) { error = "Bad loadout slot"; return false; }
                var soldier = army[e.slot].Roster().FirstOrDefault(s => s.name == e.soldier);
                if (soldier == null) { error = $"Unknown soldier '{e.soldier}'"; return false; }
                var l = loadouts[e.slot].For(soldier);
                for (int i = 0; i < e.swapFrom.Count && i < e.swapTo.Count; i++)
                {
                    var option = soldier.weaponOptions.FirstOrDefault(o => o != null && o.weapon != null && o.weapon.name == e.swapFrom[i]);
                    var to = option?.alternatives.FirstOrDefault(w => w != null && w.name == e.swapTo[i]);
                    if (to == null) { error = $"Unknown weapon swap for {soldier.name}"; return false; }
                    l.Swaps[option.weapon] = to;
                }
                foreach (var name in e.equipment)
                {
                    var item = soldier.equipmentOptions.FirstOrDefault(x => x != null && x.name == name);
                    if (item == null) { error = $"Unknown equipment '{name}'"; return false; }
                    l.Equipment.Add(item);
                }
            }
            return true;
        }
    }

    /// <summary>The host starts the battle: map, the shared dice seed, which player deploys south and goes first, and both armies.</summary>
    [Serializable]
    public class StartMsg
    {
        public string scene;
        public int seed;
        public bool hostIsSouth;
        public ArmyMsg hostArmy;
        public ArmyMsg clientArmy;
    }

    public static class OrderKinds
    {
        public const string Select = "select";
        public const string Cancel = "cancel";
        public const string LeaderMove = "leaderMove";
        public const string Hold = "hold";
        public const string FollowerMove = "followerMove";
        public const string Gather = "gather";
        public const string EndMove = "endMove";
        public const string Reset = "reset";
        public const string Shoot = "shoot";
        public const string Ability = "ability";
        public const string EndTurn = "endTurn";
    }

    /// <summary>
    /// One order given by the player whose turn it is, replayed by the other game. Units are named by their network id,
    /// soldiers by their index within their unit; points are sent as exact float bits. <see cref="checksum"/> is the state after
    /// the order, so the other game can tell if it got a different result.
    /// </summary>
    [Serializable]
    public class OrderMsg
    {
        public string kind;
        public int seq;
        public int unit = -1;
        /// <summary>The soldier's index in their unit (-1: none).</summary>
        public int soldier = -1;
        public int targetUnit = -1;
        public int targetSoldier = -1;
        public string ability = "";
        public bool hasPoint;
        public int px, py, pz;
        public long checksum;

        public Vector3 Point
        {
            get => new Vector3(BitConverter.Int32BitsToSingle(px), BitConverter.Int32BitsToSingle(py), BitConverter.Int32BitsToSingle(pz));
            set
            {
                hasPoint = true;
                px = BitConverter.SingleToInt32Bits(value.x);
                py = BitConverter.SingleToInt32Bits(value.y);
                pz = BitConverter.SingleToInt32Bits(value.z);
            }
        }
    }
}
