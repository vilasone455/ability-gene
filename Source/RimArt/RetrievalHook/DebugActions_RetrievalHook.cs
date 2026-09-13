using LudeonTK;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the retrieval hook belt.</summary>
    public static class DebugActions_RetrievalHook
    {
        private const string Category = "RimArts";

        /// <summary>Finishes the reel-in on the clicked pawn's belt, skipping the 10 seconds of work.</summary>
        [DebugAction(Category, "Retrieval hook: reload belt", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Reload(Pawn pawn)
        {
            CompRetrievalHookBelt belt = CompRetrievalHookBelt.WornBy(pawn);
            if (belt == null)
            {
                Messages.Message("not wearing a retrieval hook belt", MessageTypeDefOf.RejectInput, false);
                return;
            }
            belt.AddReloadWork(RetrievalHookDefaults.ReloadTicks);
        }

        /// <summary>Unloads the clicked pawn's belt, for testing the reel-in without firing.</summary>
        [DebugAction(Category, "Retrieval hook: unload belt", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Unload(Pawn pawn)
        {
            CompRetrievalHookBelt.WornBy(pawn)?.Unload();
        }
    }
}
