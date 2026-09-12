using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Colouring the copy without touching the person.
    ///
    /// The decoy has no materials of its own — it is the source pawn's renderer run a second time
    /// — so the only place to change how it looks is inside that renderer, for the duration of
    /// that one pass. Both patches below do nothing at all unless
    /// <see cref="MimicRender.DrawingAsDecoy"/> names the pawn being rendered, which is true for
    /// exactly one call a frame per live decoy.
    ///
    /// A postfix rather than anything cleverer because <c>tint</c> is already a field the game
    /// carries through the whole render tree and multiplies into every material
    /// (PawnRenderNodeWorker.PreDraw does <c>parms.tint * mat.color</c>). There was nothing to
    /// build; the hook was already there.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    static class Patch_PawnRenderer_MimicTint
    {
        static void Postfix(Pawn ___pawn, ref PawnDrawParms __result)
        {
            if (MimicRender.DrawingAsDecoy != ___pawn) return;

            __result.tint *= MimicRender.Tint;

            // The cloaking flag does two things and both are wanted. It swaps every material for
            // one on the Misc/Invisible shader, which is translucent and carries a distortion
            // texture — light with a ripple in it, which is what a projection should look like —
            // and it makes RenderPawnAt skip the shadow, because the game already treats a thing
            // drawn this way as not really being there.
            if (MimicDefaults.Shimmer) __result.flags |= PawnRenderFlags.Invisible;
        }
    }

    /// <summary>
    /// Keeping the copy out of the pawn texture atlas.
    ///
    /// Zoomed out past a certain point the game stops rendering humanlike pawns through the
    /// render tree and blits a cached frame from an atlas instead. That path takes its material
    /// from the atlas and passes PawnRenderFlags.None, so it honours neither the tint nor the
    /// shader above — the decoy would be correctly coloured while the camera was close and
    /// silently turn back into an ordinary-looking colonist as soon as the player zoomed out,
    /// which is the worst possible place for this to break.
    ///
    /// The game already has the parameter for this and simply never passes it from here, so the
    /// prefix sets it rather than inventing a mechanism.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    static class Patch_PawnRenderer_MimicUncached
    {
        static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (MimicRender.DrawingAsDecoy == ___pawn) disableCache = true;
        }
    }
}
