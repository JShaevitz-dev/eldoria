using System;
using System.Collections.Generic;
using System.Linq;
using Eldoira.Units;

namespace Eldoira.Rules
{
    /// <summary>
    /// Resolves keyword triggers when something happens to a soldier during shooting. Data-driven triggers
    /// (on keyword assets) run first, then any coded abilities subscribed to <see cref="Fired"/>.
    /// </summary>
    public static class KeywordTriggers
    {
        /// <summary>
        /// For abilities written in code: (event, soldier it happened to, the other soldier involved).
        /// Runs immediately, interrupting shooting.
        /// </summary>
        public static event Action<TriggerEvent, Soldier, Soldier> Fired;

        /// <summary>Fires every trigger on <paramref name="self"/>'s keywords for this event. Returns a description of each.</summary>
        public static List<string> Fire(TriggerEvent when, Soldier self, Soldier other)
        {
            var log = new List<string>();
            if (self == null) return log;

            // Snapshot: triggers may change the soldier's keywords.
            foreach (var owner in self.Keywords.ToList())
            {
                if (owner == null) continue;
                foreach (var t in owner.triggers)
                {
                    if (t == null || t.when != when || t.keyword == null) continue;
                    var subject = t.subject == TriggerSubject.Self ? self : other;
                    if (subject == null) continue;

                    if (t.action == TriggerAction.GainKeyword)
                    {
                        subject.GainKeyword(t.keyword, t.untilNextTurn ? Turns.NextTurn : 0);
                        log.Add($"{self.name}'s {owner.displayName}: {subject.name} gains {t.keyword.displayName}");
                    }
                    else if (subject.LoseKeyword(t.keyword))
                    {
                        log.Add($"{self.name}'s {owner.displayName}: {subject.name} loses {t.keyword.displayName}");
                    }
                }
            }

            Fired?.Invoke(when, self, other);
            return log;
        }
    }
}
