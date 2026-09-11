using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Tracks who is currently ringing.
    ///
    /// Same self-healing shape as <see cref="RecursionRegistry"/>: the hediff comp reports
    /// itself every tick and drops itself on removal, so nothing here depends on a callback
    /// firing.
    ///
    /// CarrierCount exists for the same reason the other registries have their counts. The
    /// resonance is read from a postfix on Thing.TakeDamage, which runs for everything that is
    /// ever hurt on every map; it has to cost one static integer read in the overwhelmingly
    /// common case where no active weapon has granted Resonance.
    /// </summary>
    public static class ResonanceRegistry
    {
        private static readonly List<HediffComp_Resonance> carriers = new List<HediffComp_Resonance>();

        public static int CarrierCount => carriers.Count;

        public static void Report(HediffComp_Resonance comp)
        {
            if (comp != null && !carriers.Contains(comp)) carriers.Add(comp);
        }

        public static void Drop(HediffComp_Resonance comp)
        {
            carriers.Remove(comp);
        }

        /// <summary>
        /// The resonance this pawn is carrying, or null. Only reached once the carrier count is
        /// non-zero; the list is at most a handful of entries, so a scan beats a dictionary.
        /// </summary>
        public static HediffComp_Resonance CarrierOf(Pawn pawn)
        {
            if (pawn == null) return null;

            for (int i = 0; i < carriers.Count; i++)
            {
                if (carriers[i].Pawn == pawn) return carriers[i];
            }
            return null;
        }
    }
}
