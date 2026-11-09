using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eldoira.Units
{
    /// <summary>
    /// A weapon carried by a soldier during play. Stats come from its template; keywords are the template's
    /// plus any gained (minus any lost) during the game.
    /// </summary>
    [Serializable]
    public class Weapon
    {
        [SerializeField] private WeaponTemplate template;
        [SerializeField] private KeywordSet keywordState = new KeywordSet();

        public Weapon(WeaponTemplate template) => this.template = template;

        public WeaponTemplate Template => template;
        public string Name => template != null ? template.displayName : "(none)";
        public int BaseDamage => template != null ? template.baseDamage : 0;
        public int Penetration => template != null ? template.penetration : 0;
        public int AccuracyBonus => template != null ? template.accuracyBonus : 0;

        private IEnumerable<Keyword> Inherent => template != null ? template.keywords : null;

        /// <summary>Keywords the weapon has right now.</summary>
        public IReadOnlyList<Keyword> Keywords => keywordState.Current(Inherent);
        public bool HasKeyword(string name) => Keyword.Contains(Keywords, name);

        /// <param name="expiresAtTurn">Turn number at whose start the keyword is removed; 0 = until removed.</param>
        public void GainKeyword(Keyword keyword, int expiresAtTurn = 0) => keywordState.Gain(keyword, Inherent, expiresAtTurn);
        public bool LoseKeyword(Keyword keyword) => keywordState.Lose(keyword, Inherent);
        public void OnTurnStarted(int turn) => keywordState.ExpireAtTurnStart(turn);
    }
}
