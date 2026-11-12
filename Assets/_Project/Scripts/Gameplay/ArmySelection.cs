using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The armies chosen for the next battle. <see cref="Side.Player"/> is the south side, which deploys at the bottom of
    /// the map and moves first; <see cref="Side.AI"/> is the north side. In single player the human is south and the AI
    /// north; in multiplayer each game's player commands <see cref="LocalSide"/>.
    /// Kept between scenes, so restarting a battle replays the same armies. When nothing is chosen (e.g. the battle
    /// scene opened directly in the editor), the battle uses the armies placed in the scene.
    /// </summary>
    public static class ArmySelection
    {
        public static IReadOnlyList<UnitTemplate> PlayerArmy { get; private set; }
        public static IReadOnlyList<UnitTemplate> AIArmy { get; private set; }
        public static Faction PlayerFaction { get; private set; }
        public static Faction AIFaction { get; private set; }
        /// <summary>Each of the south side's units' weapon swaps and equipment (same order as <see cref="PlayerArmy"/>; may be null).</summary>
        public static IReadOnlyList<UnitLoadout> PlayerLoadouts { get; private set; }
        /// <summary>The same for the north side (multiplayer; the AI doesn't customize).</summary>
        public static IReadOnlyList<UnitLoadout> AILoadouts { get; private set; }

        /// <summary>The side this game's player commands: south in single player; either in multiplayer.</summary>
        public static Side LocalSide { get; set; } = Side.Player;

        public static bool HasArmies => PlayerArmy != null && PlayerArmy.Count > 0 && AIArmy != null && AIArmy.Count > 0;

        public static void Set(IEnumerable<UnitTemplate> player, IEnumerable<UnitTemplate> ai, Faction playerFaction = null, Faction aiFaction = null,
            IEnumerable<UnitLoadout> playerLoadouts = null, IEnumerable<UnitLoadout> aiLoadouts = null)
        {
            PlayerLoadouts = playerLoadouts?.Select(l => l?.Clone()).ToList();
            AILoadouts = aiLoadouts?.Select(l => l?.Clone()).ToList();
            PlayerArmy = player.Where(u => u != null).ToList();
            AIArmy = ai.Where(u => u != null).ToList();
            PlayerFaction = playerFaction;
            AIFaction = aiFaction;
        }

        public static void Clear()
        {
            PlayerArmy = null;
            AIArmy = null;
            PlayerFaction = null;
            AIFaction = null;
            PlayerLoadouts = null;
            AILoadouts = null;
            LocalSide = Side.Player;
        }
    }
}
