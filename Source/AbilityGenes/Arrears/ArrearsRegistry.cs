using System.Collections.Generic;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Tracks who is currently not being told what has happened to them.
    ///
    /// Same self-healing shape as the other registries here: the hediff comp reports itself
    /// every tick and drops itself on removal, so nothing depends on a callback firing.
    ///
    /// HolderCount exists so the Thing.TakeDamage prefix costs one static integer read in the
    /// overwhelmingly common case where nobody is holding a debt.
    /// </summary>
    public static class ArrearsRegistry
    {
        private static readonly List<HediffComp_Arrears> holders = new List<HediffComp_Arrears>();

        public static int HolderCount => holders.Count;

        public static void Report(HediffComp_Arrears comp)
        {
            if (comp != null && !holders.Contains(comp)) holders.Add(comp);
        }

        public static void Drop(HediffComp_Arrears comp)
        {
            holders.Remove(comp);
        }

        /// <summary>
        /// The ledger this pawn is running, or null. Only reached once the holder count is
        /// non-zero; the list is at most a handful of entries, so a scan beats a dictionary.
        /// </summary>
        public static HediffComp_Arrears HolderFor(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null) return null;

            for (int i = 0; i < holders.Count; i++)
            {
                if (holders[i].Pawn == pawn) return holders[i];
            }
            return null;
        }
    }
}
