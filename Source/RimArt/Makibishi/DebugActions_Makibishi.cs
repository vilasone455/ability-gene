using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for makibishi: spikes without the pouch or the throw, and a forced step.</summary>
    public static class DebugActions_Makibishi
    {
        /// <summary>Spikes the patch around the clicked cell, blamed on the selected pawn if there is one.</summary>
        [RimArtDebug("Makibishi", "scatter here")]
        private static void ScatterHere()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            Pawn thrower = Find.Selector.SelectedPawns.FirstOrDefault(p => p.Map == map);
            if (MakibishiPatch.Scatter(UI.MouseCell(), map, thrower) == 0)
                Messages.Message("no cell there can hold spikes", MessageTypeDefOf.RejectInput, false);
        }

        /// <summary>The clicked pawn steps on a spike, skipping the 35% roll.</summary>
        [RimArtDebug("Makibishi", "step on one", RimArtDebugKind.Pawn)]
        private static void StepOnOne(Pawn pawn)
        {
            if (!Makibishi.Affects(pawn))
            {
                Messages.Message("flying pawns and mechanoids are not affected", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Makibishi.Wound(pawn, null);
        }
    }
}
