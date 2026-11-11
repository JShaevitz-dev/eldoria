using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEngine;

namespace Eldoria.Rules
{
    /// <summary>What an area attack (a grenade, an explosion) did.</summary>
    public class AreaAttackResult
    {
        public Vector3 Point;
        public float Radius;
        /// <summary>Everyone caught in the area, and the damage each took (0 if armor or keywords stopped it).</summary>
        public readonly List<(Soldier soldier, int damage)> Hits = new List<(Soldier soldier, int damage)>();
        /// <summary>Soldiers within the radius whom terrain shielded from the blast.</summary>
        public readonly List<Soldier> Shielded = new List<Soldier>();
        public readonly List<string> Log = new List<string>();
    }

    /// <summary>
    /// Area attacks: every living soldier within the radius of a point (horizontal) - friend or foe - is attacked with a
    /// weapon, unless object or structure terrain lies between them and the point (<see cref="BlastCheck"/>). No hit roll;
    /// for each soldier, as for shooting: keywords (e.g. "no effect"), damage (Base Damage − penalties + bonuses), then
    /// penetration vs armor (a weapon with no penetration value always does 100%). Soldiers who take damage fire their
    /// "when damaged" triggers; anyone killed is removed when it's over. Used by Frag Grenade and by explosions on death.
    /// </summary>
    public static class AreaAttacks
    {
        private const float Epsilon = 0.01f;

        /// <param name="attacker">Who the attack comes from (for keyword checks; may already be dead, e.g. an explosion on death).</param>
        /// <param name="exclude">A soldier left out (e.g. the one who exploded).</param>
        public static AreaAttackResult Resolve(Soldier attacker, WeaponTemplate weaponTemplate, Vector3 point, float radius,
            Soldier exclude = null)
        {
            var result = new AreaAttackResult { Point = point, Radius = radius };
            if (weaponTemplate == null) return result;
            var weapon = new Weapon(weaponTemplate, attacker);   // the attacker's faction changes apply (e.g. to a grenade)

            var inRadius = Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(s => s != exclude && s.IsAlive && Measure.ToPoint(s, point) <= radius + Epsilon)
                .OrderBy(s => Measure.ToPoint(s, point))
                .ToList();
            var caught = new List<Soldier>();
            foreach (var s in inRadius)
            {
                if (BlastCheck.Reaches(point, s)) caught.Add(s);
                else
                {
                    result.Shielded.Add(s);
                    result.Log.Add($"  {s.name} is shielded from the blast by terrain.");
                }
            }

            Casualties.BeginVolley();
            try
            {
                foreach (var s in caught)
                {
                    if (!s.IsAlive) continue;   // killed by an earlier blast in a chain reaction
                    var keywords = KeywordRules.Evaluate(attacker, weapon, s);
                    if (keywords.NoEffect)
                    {
                        result.Hits.Add((s, 0));
                        result.Log.Add($"  {s.name}: no effect ({string.Join("; ", keywords.Applied)}).");
                        continue;
                    }
                    int finalDamage = Attack.FinalDamage(weapon.BaseDamage, keywords);
                    int armor = Attack.ArmorAgainst(s, keywords);
                    var pen = ArmorPenetration.Resolve(finalDamage, weapon.PenetrationValue, armor);
                    result.Hits.Add((s, pen.Damage));
                    if (pen.Damage > 0)
                    {
                        result.Log.Add($"  {s.name} takes {pen.Damage} ({finalDamage} vs armor {armor}).");
                        s.TakeDamage(pen.Damage);   // a death here can set off that soldier's own death triggers
                        if (!s.IsAlive) result.Log.Add($"  {s.name} is down.");
                        result.Log.AddRange(KeywordTriggers.Fire(TriggerEvent.Damaged, s, attacker));
                    }
                    else result.Log.Add($"  {s.name}: armor stops it.");
                }
            }
            finally
            {
                var removed = Casualties.EndVolley();
                if (removed.Count > 0)
                    result.Log.Add("Removed from the battlefield: " + string.Join(", ", removed.Where(r => r != null).Select(r => r.name)) + ".");
            }
            return result;
        }
    }
}
