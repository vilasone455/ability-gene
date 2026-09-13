using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Draws a dragged pawn between the two cells it is travelling through. Its real position
    /// moves a whole cell at a time; only the drawing is smoothed. The altitude from the original
    /// getter is kept so the pawn still sorts correctly against things around it.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    static class Patch_RetrievalHook_DrawPos
    {
        static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (!MapComponent_RetrievalHooks.AnyDragged) return;
            if (MapComponent_RetrievalHooks.TryGetDragPosition(___pawn, out Vector3 position))
                __result = position.WithY(__result.y);
        }
    }

    /// <summary>
    /// A pawn on the end of the tether takes no jobs, so a downed pawn cannot crawl off or be
    /// put back to bed halfway through. Blocking the start rather than ending jobs every tick
    /// also keeps the think tree from being evaluated each tick for nothing.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    static class Patch_RetrievalHook_StartJob
    {
        static bool Prefix(Pawn ___pawn)
        {
            return !MapComponent_RetrievalHooks.IsDragged(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "TryFindAndStartJob")]
    static class Patch_RetrievalHook_TryFindAndStartJob
    {
        static bool Prefix(Pawn ___pawn)
        {
            return !MapComponent_RetrievalHooks.IsDragged(___pawn);
        }
    }
}
