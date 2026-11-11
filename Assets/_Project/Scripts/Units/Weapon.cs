using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eldoria.Units
{
    /// <summary>
    /// A weapon carried by a soldier during play. Stats come from its template plus the changes made by the faction of
    /// the soldier holding it, and its name is that faction's name for it (see <see cref="WeaponStats"/>); keywords are
    /// the template's plus any gained (minus any lost) during the game.
    /// </summary>
    [Serializable]
    public class Weapon
    {
        [SerializeField] private WeaponTemplate template;
        [SerializeField] private KeywordSet keywordState = new KeywordSet();

        /// <summary>The soldier holding (or using) the weapon: their unit's faction changes its stats and name.</summary>
        [NonSerialized] private Soldier owner;

        public Weapon(WeaponTemplate template, Soldier owner = null)
        {
            this.template = template;
            this.owner = owner;
        }

        /// <summary>The turn this weapon last fired in a volley (for Manual Load).</summary>
        [NonSerialized] public int FiredOnTurn = int.MinValue;

        public WeaponTemplate Template => template;
        public Soldier Owner { get => owner; internal set => owner = value; }
        /// <summary>The faction whose changes apply: the holder's unit's faction (none if it has no faction).</summary>
        public Faction Faction => owner != null && owner.Unit != null ? owner.Unit.Faction : null;
        /// <summary>The weapon's stats as its holder's faction has them.</summary>
        public WeaponStats Stats => WeaponStats.For(template, Faction);

        /// <summary>The name shown for it: the holder's faction's name for this weapon, if it has one.</summary>
        public string Name => Stats.Name;
        public int BaseDamage => Stats.BaseDamage;
        /// <summary>How many times the soldier shoots with it in a volley (at least 1).</summary>
        public int Shots => Stats.Shots;
        public int Penetration => Stats.PenetrationValue ?? 0;
        /// <summary>Penetration, or null if the weapon has no penetration value: its attacks always do 100% damage, regardless of armor.</summary>
        public int? PenetrationValue => Stats.PenetrationValue;
        public int AccuracyBonus => Stats.AccuracyBonus;
        /// <summary>Range in meters (horizontal); 0 = no limit.</summary>
        public float Range => Stats.Range;
        public bool HasRangeLimit => Range > 0f;

        private IEnumerable<Keyword> Inherent => template != null ? template.keywords : null;

        /// <summary>Keywords the weapon has right now.</summary>
        public IReadOnlyList<Keyword> Keywords => keywordState.Current(Inherent);
        public bool HasKeyword(string name) => Keyword.Contains(Keywords, name);

        /// <param name="expiresAtTurn">Expiry point (see Rules.Turns.NextTurnOf) at whose start the keyword is removed; 0 = until removed.</param>
        public void GainKeyword(Keyword keyword, int expiresAtTurn = 0) => keywordState.Gain(keyword, Inherent, expiresAtTurn);
        public bool LoseKeyword(Keyword keyword) => keywordState.Lose(keyword, Inherent);
        public void OnTurnStarted(int turn) => keywordState.ExpireAtTurnStart(turn);
    }
}
