using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Life energy, which Instant Transmission locks onto and Spirit Bomb gathers. Only flesh has it,
    /// so mechanoids have none.
    /// </summary>
    public static class GokuLifeEnergy
    {
        public static bool Has(Pawn pawn) => pawn != null && !pawn.Dead && pawn.RaceProps.IsFlesh;

        /// <summary>Overall health (0-1) x body size capped at 1: a healthy human or cow 1, a human at 40 % health 0.4, a rat 0.2.</summary>
        public static float Of(Pawn pawn) =>
            Has(pawn) ? pawn.health.summaryHealth.SummaryHealthPercent * Mathf.Min(1f, pawn.BodySize) : 0f;

        /// <summary>The Rest bar (0-1); 1 for a pawn with no Rest need (a gene can remove it).</summary>
        public static float Rest(Pawn pawn) => pawn?.needs?.rest?.CurLevel ?? 1f;

        /// <summary>What a pawn can give a Spirit Bomb per second, before the ability's rate: life energy x Rest.</summary>
        public static float Ki(Pawn pawn) => Of(pawn) * Rest(pawn);
    }
}
