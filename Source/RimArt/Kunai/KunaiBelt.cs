using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Finds the kunai belt's charge store on a pawn.
    ///
    /// The store is vanilla <see cref="CompApparelReloadable"/>, the comp the jump pack uses. It
    /// saves the count on the belt, starts a new belt full, and supplies the Reload float menu
    /// option and the automatic reload job (JobGiver_Reload, when the count reaches zero and
    /// kunai items are reachable). This mod only spends charges from it.
    /// </summary>
    public static class KunaiBelt
    {
        /// <summary>The reloadable comp of the kunai belt this pawn is wearing, or null.</summary>
        public static CompApparelReloadable WornBy(Pawn pawn)
        {
            List<Apparel> worn = pawn?.apparel?.WornApparel;
            if (worn == null) return null;
            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i].def != KunaiDefOf.AG_KunaiBelt) continue;
                CompApparelReloadable comp = worn[i].TryGetComp<CompApparelReloadable>();
                if (comp != null) return comp;
            }
            return null;
        }
    }
}
