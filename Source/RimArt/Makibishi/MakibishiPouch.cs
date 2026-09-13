using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Finds the makibishi pouch's charge store on a pawn. Vanilla <see cref="CompApparelReloadable"/>,
    /// the same as <see cref="KunaiBelt"/>: it saves the count, starts a new pouch full, and supplies
    /// the Reload option and the automatic reload job.
    /// </summary>
    public static class MakibishiPouch
    {
        /// <summary>The reloadable comp of the makibishi pouch this pawn is wearing, or null.</summary>
        public static CompApparelReloadable WornBy(Pawn pawn)
        {
            List<Apparel> worn = pawn?.apparel?.WornApparel;
            if (worn == null) return null;
            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i].def != MakibishiDefOf.AG_MakibishiPouch) continue;
                CompApparelReloadable comp = worn[i].TryGetComp<CompApparelReloadable>();
                if (comp != null) return comp;
            }
            return null;
        }
    }
}
