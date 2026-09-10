using System.Collections.Generic;
using Verse;

namespace RimArt
{
    public static class AnchorUtility
    {
        /// <summary>
        /// The carrier's mark store, or null. Keyed on the gene class rather than on a DefOf so
        /// the lookup keeps working for any GeneDef that declares this geneClass.
        /// </summary>
        public static Gene_Anchors GeneOf(Pawn pawn)
        {
            if (pawn == null || pawn.genes == null) return null;

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                Gene_Anchors anchors = genes[i] as Gene_Anchors;
                if (anchors != null && anchors.Active) return anchors;
            }
            return null;
        }
    }
}
