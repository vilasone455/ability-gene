using System.Collections.Generic;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Tracks which pawns currently have their vectors reversed.
    ///
    /// Same self-healing shape as <see cref="TimeAlterRegistry"/>, but keyed on the last tick
    /// each pawn reported rather than pruned by a MapComponent: reflection is read from inside
    /// Thing.TakeDamage, which runs for things on every map and off-map too, so there is no one
    /// map component that owns the pruning.
    ///
    /// ActiveCount exists so the TakeDamage and CanReserve prefixes cost one static read in the
    /// overwhelmingly common case where nobody is reflecting.
    /// </summary>
    public static class ReflectionRegistry
    {
        /// <summary>
        /// How long an entry survives without being re-reported. A reflecting pawn reports once
        /// per pawn tick; this only has to cover a pawn that stops ticking (caravan, world pawn)
        /// before its hediff comp gets the chance to drop itself.
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

        /// <summary>Hot path, only reached when at least one pawn is reflecting.</summary>
        public static bool IsReflecting(Thing thing)
        {
            Pawn pawn = thing as Pawn;
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
    }
}
