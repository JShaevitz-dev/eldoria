using System;
using System.Collections.Generic;
using System.Linq;
using Eldoria.Core;
using Eldoria.Units;

namespace Eldoria.Rules
{
    /// <summary>
    /// Resolves keyword triggers when something happens to a soldier: being targeted, taking damage, or being killed
    /// ("on death" - whatever killed them). The keywords checked are the soldier's own and those of the weapons they carry
    /// (e.g. a Flamethrower's Unstable). A trigger can have a chance to happen (e.g. 10%). Data-driven triggers (on
    /// keyword assets) run first, then any coded abilities subscribed to <see cref="Fired"/>. Triggers interrupt what's
    /// going on: they're resolved at once (an explosion on death happens before the next shot).
    /// </summary>
    public static class KeywordTriggers
    {
        /// <summary>
        /// For abilities written in code: (event, soldier it happened to, the other soldier involved).
        /// Runs immediately, interrupting shooting.
        /// </summary>
        public static event Action<TriggerEvent, Soldier, Soldier> Fired;

        /// <summary>An explosion went off (who exploded, and what it did), for effects and the log.</summary>
        public static event Action<Soldier, AreaAttackResult> Exploded;

        /// <summary>A line describing an "on death" trigger, which happens outside any caller's log.</summary>
        public static event Action<string> DeathTriggerLogged;

        /// <summary>Where trigger chances are rolled (tests can replace it).</summary>
        public static Random Rng = null;
        private static Random Dice => Rng ?? GameRandom.Shared;

        /// <summary>Fires every trigger on <paramref name="self"/>'s keywords (and their weapons') for this event. Returns a description of each.</summary>
        public static List<string> Fire(TriggerEvent when, Soldier self, Soldier other)
        {
            var log = new List<string>();
            if (self == null) return log;

            // Snapshot: triggers may change the soldier's keywords.
            var owners = self.Keywords.ToList();
            foreach (var w in self.Weapons)
                foreach (var k in w.Keywords) if (!owners.Contains(k)) owners.Add(k);

            foreach (var owner in owners)
            {
                if (owner == null) continue;
                foreach (var t in owner.triggers)
                {
                    if (t == null || t.when != when) continue;
                    if (t.chancePercent < 100)
                    {
                        int roll = Dice.Next(1, 101);
                        if (roll > t.chancePercent)
                        {
                            log.Add($"{self.name}'s {owner.displayName}: no ({roll} > {t.chancePercent}%)");
                            continue;
                        }
                        log.Add($"{self.name}'s {owner.displayName}: yes ({roll} ≤ {t.chancePercent}%)");
                    }

                    if (t.action == TriggerAction.Explode)
                    {
                        log.Add($"{self.name}'s {owner.displayName}: {self.name} explodes!");
                        var blast = AreaAttacks.Resolve(self, t.explosionWeapon, self.Position, t.explosionRadius, exclude: self);
                        log.AddRange(blast.Log);
                        Exploded?.Invoke(self, blast);
                        continue;
                    }

                    if (t.keyword == null) continue;
                    var subject = t.subject == TriggerSubject.Self ? self : other;
                    if (subject == null) continue;
                    if (t.action == TriggerAction.GainKeyword)
                    {
                        subject.GainKeyword(t.keyword, t.untilNextTurn ? Turns.NextTurnOf(self) : 0);   // until the next turn of the soldier whose keyword fired
                        log.Add($"{self.name}'s {owner.displayName}: {subject.name} gains {t.keyword.displayName}");
                    }
                    else if (subject.LoseKeyword(t.keyword))
                    {
                        log.Add($"{self.name}'s {owner.displayName}: {subject.name} loses {t.keyword.displayName}");
                    }
                }
            }

            Fired?.Invoke(when, self, other);
            if (when == TriggerEvent.Died)
                foreach (var line in log) DeathTriggerLogged?.Invoke(line);
            return log;
        }
    }
}
