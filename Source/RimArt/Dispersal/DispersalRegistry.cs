using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Tracks who is currently carrying a plexus with something left in it.
    ///
    /// Same self-healing shape as <see cref="ArrearsRegistry"/>: the gene reports itself every
    /// tick and drops itself on removal, so nothing depends on a callback firing.
    ///
    /// CarrierCount exists so the Thing.TakeDamage prefix costs one static integer read in the
    /// overwhelmingly common case where nobody on the map has this gene at all. That matters
    /// more here than anywhere else in the mod, because this prefix runs above every other one
    /// and therefore runs first on every damage instance in the game.
    /// </summary>
    public static class DispersalRegistry
    {
        private static readonly List<Gene_Dispersal> carriers = new List<Gene_Dispersal>();

        public static int CarrierCount => carriers.Count;

        public static void Report(Gene_Dispersal gene)
        {
            if (gene != null && !carriers.Contains(gene)) carriers.Add(gene);
        }

        public static void Drop(Gene_Dispersal gene)
        {
            carriers.Remove(gene);
        }

        /// <summary>
        /// The plexus this thing is carrying, or null. Only reached once the carrier count is
        /// non-zero; the list is at most a handful of entries, so a scan beats a dictionary.
        ///
        /// This prunes as it goes, and has to. The gene reports itself from TickInterval, which
        /// the gene tracker only calls on genes that are Active - so a plexus that is overridden
        /// by another gene, or whose carrier has died, stops reporting *and* stops getting the
        /// chance to drop itself. Dropping it here is the only place that catches both.
        /// </summary>
        public static Gene_Dispersal CarrierFor(Thing thing)
        {
            Pawn pawn = thing as Pawn;

            for (int i = carriers.Count - 1; i >= 0; i--)
            {
                Gene_Dispersal gene = carriers[i];
                if (gene.pawn == null || gene.pawn.Dead || !gene.Active)
                {
                    carriers.RemoveAt(i);
                    continue;
                }

                if (pawn != null && gene.pawn == pawn) return gene;
            }
            return null;
        }
    }
}
