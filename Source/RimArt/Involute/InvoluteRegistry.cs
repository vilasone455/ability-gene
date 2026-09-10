using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Tracks which carriers currently have the hole connected.
    ///
    /// Same self-healing shape and the same reason as <see cref="ReflectionRegistry"/>: the
    /// lookup is read from inside the damage worker, which runs for pawns on every map and off
    /// it, so no one MapComponent owns the pruning.
    ///
    /// ActiveCount exists so the ApplyDamageToPart prefix costs one static read in the
    /// overwhelmingly common case where nobody is vented.
    /// </summary>
    public static class InvoluteRegistry
    {
        /// <summary>
        /// How long an entry survives without being re-reported. A vented carrier reports once
        /// per pawn tick; this only has to cover a pawn that stops ticking before its hediff
        /// comp gets the chance to drop itself.
        /// </summary>
        private const int StaleTicks = 10;

        private static readonly Dictionary<Pawn, int> lastReport = new Dictionary<Pawn, int>();

        public static int ActiveCount => lastReport.Count;

        public static void Report(Pawn pawn)
        {
            if (pawn == null) return;
            lastReport[pawn] = Find.TickManager.TicksGame;
        }

        public static void Drop(Pawn pawn)
        {
            if (pawn == null) return;
            lastReport.Remove(pawn);
        }

        public static bool IsVented(Pawn pawn)
        {
            if (pawn == null) return false;

            int tick;
            if (!lastReport.TryGetValue(pawn, out tick)) return false;

            if (Find.TickManager.TicksGame - tick > StaleTicks)
            {
                lastReport.Remove(pawn);
                return false;
            }
            return true;
        }

        /// <summary>The gene of a pawn whose hole is currently connected, or null.</summary>
        public static Gene_Involute VentedGeneOf(Pawn pawn)
        {
            if (!IsVented(pawn)) return null;
            return InvoluteUtility.GeneOf(pawn);
        }
    }
}
