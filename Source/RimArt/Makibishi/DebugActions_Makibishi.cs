using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for makibishi: spikes without the pouch or the throw, and a forced step.</summary>
    public static class DebugActions_Makibishi
    {
        private const string Category = "RimArts";

        /// <summary>Spikes the patch around the clicked cell, blamed on the selected pawn if there is one.</summary>
        [DebugAction(Category, "Makibishi: scatter here", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ScatterHere()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            Pawn thrower = Find.Selector.SelectedPawns.FirstOrDefault(p => p.Map == map);
            if (MakibishiPatch.Scatter(UI.MouseCell(), map, thrower) == 0)
                Messages.Message("no cell there can hold spikes", MessageTypeDefOf.RejectInput, false);
        }

        /// <summary>The clicked pawn steps on a spike, skipping the 35% roll.</summary>
        [DebugAction(Category, "Makibishi: step on one", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
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
