using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Eldoira.Units
{
    /// <summary>
    /// Every keyword asset in the game, so rules can find the ones they hand out (e.g. "Moved") by name.
    /// Lives at Resources/KeywordLibrary. If a keyword isn't in the library, a stand-in with that name is made.
    /// </summary>
    [CreateAssetMenu(menuName = "Eldoira/Keyword Library", fileName = "KeywordLibrary", order = 4)]
    public class KeywordLibrary : ScriptableObject
    {
        public const string ResourcePath = "KeywordLibrary";

        /// <summary>Given to a soldier who moved this turn; removed at the start of the next turn.</summary>
        public const string Moved = "Moved";

        /// <summary>Ability keyword: a Spell can't be cast by a soldier whose unit sprinted this turn.</summary>
        public const string Spell = "Spell";

        public List<Keyword> keywords = new List<Keyword>();

        private static KeywordLibrary loaded;
        private static readonly Dictionary<string, Keyword> standIns = new Dictionary<string, Keyword>();

        /// <summary>Finds a keyword by name (case-insensitive).</summary>
        public static Keyword Get(string name)
        {
            if (loaded == null) loaded = Resources.Load<KeywordLibrary>(ResourcePath);
            var found = loaded != null ? loaded.keywords.FirstOrDefault(k => k != null && k.Is(name)) : null;
            if (found != null) return found;

            string key = name.ToLowerInvariant();
            if (!standIns.TryGetValue(key, out var standIn) || standIn == null)
            {
                standIn = CreateInstance<Keyword>();
                standIn.name = name;
                standIn.displayName = name;
                standIn.hideFlags = HideFlags.DontSave;
                standIns[key] = standIn;
            }
            return standIn;
        }
    }
}
