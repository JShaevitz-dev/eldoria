using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Rules
{
    /// <summary>
    /// Close range: a Biological soldier is "in close range" if a Biological enemy soldier is less than 2 m away
    /// (horizontally). Mechanical soldiers can never be in close range. A soldier in close range can't use any weapon unless it has the keyword "Pistol" or "Melee" - that goes for
    /// shooting and for abilities that use a weapon (e.g. Frag Grenade). Magic and other non-weapon abilities still work,
    /// and passive abilities are unaffected.
    /// </summary>
    public static class CloseRange
    {
        /// <summary>Enemies closer than this (meters) put a soldier in close range.</summary>
        public const float Distance = 2f;

        /// <summary>Weapons with this keyword can be used in close range.</summary>
        public const string PistolKeyword = "Pistol";

        /// <summary>Melee weapons (e.g. the Iron Claw) can also be used in close range.</summary>
        public const string MeleeKeyword = "Melee";

        /// <summary>Can this weapon be used in close range? Pistols and melee weapons can.</summary>
        public static bool UsableInCloseRange(System.Func<string, bool> weaponHas) => weaponHas(PistolKeyword) || weaponHas(MeleeKeyword);

        /// <summary>Status keyword shown on soldiers in close range.</summary>
        public const string StatusKeyword = "In close range";

        private const string Biological = "Biological";

        /// <summary>Living Biological enemy soldiers putting <paramref name="soldier"/> in close range.</summary>
        /// <param name="others">Soldiers to check (default: every soldier on the battlefield).</param>
        /// <param name="positionOf">Where each soldier is (default: where they stand now).</param>
        public static List<Soldier> EnemiesInCloseRange(Soldier soldier, IEnumerable<Soldier> others = null,
            System.Func<Soldier, Vector3> positionOf = null)
        {
            var result = new List<Soldier>();
            if (soldier == null || !soldier.IsAlive || !soldier.HasKeyword(Biological) || soldier.Unit == null) return result;
            if (soldier.HasKeyword(MovementRules.MechanicalKeyword)) return result;   // Mechanical: never in close range
            positionOf ??= s => s.Position;
            others ??= Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Vector3 at = positionOf(soldier);
            foreach (var e in others)
            {
                if (e == null || e == soldier || !e.IsAlive || e.Unit == null || e.Unit.Side == soldier.Unit.Side) continue;
                if (!e.HasKeyword(Biological)) continue;
                if (Measure.Horizontal(positionOf(e), at) < Distance) result.Add(e);
            }
            return result;
        }

        public static bool IsInCloseRange(Soldier soldier) => EnemiesInCloseRange(soldier).Count > 0;

        /// <summary>Can the soldier use this weapon right now? Always, unless in close range and it isn't a Pistol or Melee weapon.</summary>
        public static bool CanUseWeapon(Soldier soldier, WeaponTemplate weapon) =>
            weapon != null && (UsableInCloseRange(weapon.HasKeyword) || !IsInCloseRange(soldier));

        public static bool CanUseWeapon(Soldier soldier, Weapon weapon) =>
            weapon != null && (UsableInCloseRange(weapon.HasKeyword) || !IsInCloseRange(soldier));
    }
}
