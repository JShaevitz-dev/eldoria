using System.Collections.Generic;
using Eldoira.Units;
using UnityEngine;

namespace Eldoira.Rules
{
    /// <summary>
    /// Squad coherency: soldiers in a squad must stay close together.
    /// Each soldier must be within <see cref="Range"/> meters horizontally (and
    /// <see cref="VerticalRange"/> meters vertically) of at least one other soldier in its squad,
    /// or at least two others if the squad has <see cref="LargeSquadSize"/> or more soldiers.
    /// Values are placeholders to tune.
    /// </summary>
    public static class Coherency
    {
        public const float Range = 4f;
        public const float VerticalRange = 3f;
        public const int LargeSquadSize = 6;

        public static int RequiredLinks(int squadSize)
        {
            if (squadSize <= 1) return 0;
            return squadSize >= LargeSquadSize ? 2 : 1;
        }

        public static bool AreLinked(UnitModel a, UnitModel b)
        {
            return Measure.Between(a, b) <= Range
                   && Mathf.Abs(a.Position.y - b.Position.y) <= VerticalRange;
        }

        /// <summary>Returns true if every soldier is in coherency; lists offenders in <paramref name="broken"/>.</summary>
        public static bool Check(IReadOnlyList<UnitModel> models, out List<UnitModel> broken)
        {
            broken = new List<UnitModel>();
            int required = RequiredLinks(models.Count);
            if (required == 0) return true;

            for (int i = 0; i < models.Count; i++)
            {
                if (models[i] == null) continue;
                int links = 0;
                for (int j = 0; j < models.Count && links < required; j++)
                {
                    if (i == j || models[j] == null) continue;
                    if (AreLinked(models[i], models[j])) links++;
                }
                if (links < required) broken.Add(models[i]);
            }
            return broken.Count == 0;
        }
    }
}
