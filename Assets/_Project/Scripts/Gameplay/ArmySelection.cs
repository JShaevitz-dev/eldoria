using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;

namespace Eldoria.Gameplay
{
    /// <summary>
    /// The armies chosen for the next battle: the player's list from the army builder and the AI's list.
    /// Kept between scenes, so restarting a battle replays the same armies. When nothing is chosen (e.g. the battle
    /// scene opened directly in the editor), the battle uses the armies placed in the scene.
    /// </summary>
    public static class ArmySelection
    {
        public static IReadOnlyList<UnitTemplate> PlayerArmy { get; private set; }
        public static IReadOnlyList<UnitTemplate> AIArmy { get; private set; }
        public static Faction PlayerFaction { get; private set; }
        public static Faction AIFaction { get; private set; }

        public static bool HasArmies => PlayerArmy != null && PlayerArmy.Count > 0 && AIArmy != null && AIArmy.Count > 0;

        public static void Set(IEnumerable<UnitTemplate> player, IEnumerable<UnitTemplate> ai, Faction playerFaction = null, Faction aiFaction = null)
        {
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
        }
    }
}
