using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The decoy's picture, which is the source pawn's own renderer run a second time at the
    /// decoy's position.
    ///
    /// Nothing is generated and nothing is copied. A generated pawn would mean building a body,
    /// a head, hair, colours, a xenotype and a wardrobe that matched - and then destroying all of
    /// it again on exit so nothing drops. Driving the renderer gets every one of those exactly
    /// right, including the weapon in the hand, and leaves nothing to clean up.
    ///
    /// The three phases are not optional, and the reason is the same one recorded in
    /// <see cref="ArcRun"/>: as of 1.6 a pawn is drawn from results the render tree computed
    /// earlier in the frame for wherever that pawn actually is, so asking for a draw at a
    /// position of your own draws nothing at all. EnsureInitialized and ParallelPreDraw at the
    /// decoy's position re-point those results first.
    ///
    /// They are then pushed back to the source's real position, because the results are the
    /// source's and anything else drawing that pawn this frame would otherwise inherit the
    /// decoy's.
    /// </summary>
    internal static class MimicRender
    {
        /// <summary>
        /// The pawn currently being drawn as somebody else's projection, or null.
        ///
        /// <see cref="Patch_PawnRenderer_MimicTint"/> and
        /// <see cref="Patch_PawnRenderer_MimicUncached"/> read this, and it is what tells those
        /// two patches that this particular pass over this particular pawn is the copy rather
        /// than the person. Ordinary static state is enough: the game finishes every parallel
        /// pre-draw job inside Map.MapUpdate's DrawDynamicThings, and MapComponentUpdate - where
        /// the call below happens - runs afterwards on the main thread, so nothing else is
        /// rendering a pawn while this is set.
        /// </summary>
        public static Pawn DrawingAsDecoy { get; private set; }

        /// <summary>The projection's colour, assembled from the four numbers in MimicDefaults.</summary>
        public static Color Tint => new Color(MimicDefaults.TintRed, MimicDefaults.TintGreen,
            MimicDefaults.TintBlue, MimicDefaults.TintAlpha);

        public static void Draw(MimicDecoy decoy)
        {
            Pawn source = decoy?.Source;
            if (source == null || !source.Spawned) return;

            PawnRenderer renderer = source.Drawer?.renderer;
            if (renderer == null) return;

            Vector3 position = decoy.DrawPos;
            position.y = AltitudeLayer.Pawn.AltitudeFor();

            DrawingAsDecoy = source;
            try
            {
                At(renderer, position, decoy.Facing, true);
            }
            finally
            {
                // Cleared before the push-back, and that order is the whole point: the second
                // pass re-points this pawn's cached render results at where the pawn actually is,
                // and those results must be the ordinary untinted ones or the person themselves
                // comes out blue for the rest of the frame.
                DrawingAsDecoy = null;
            }

            At(renderer, source.DrawPos, null, false);
        }

        /// <summary>
        /// The rotation override is what stops the decoy from turning as its source turns, and
        /// neverAimWeapon is what stops it from raising a gun it cannot fire. Both existing
        /// callers of this renderer in the mod pass null and true; this one is the reason the
        /// first parameter exists.
        /// </summary>
        private static void At(PawnRenderer renderer, Vector3 position, Rot4? rotation, bool draw)
        {
            renderer.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, position, rotation, true);
            renderer.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, position, rotation, true);
            if (draw) renderer.DynamicDrawPhaseAt(DrawPhase.Draw, position, rotation, true);
        }

        /// <summary>
        /// The ending, and the only thing a decoy ever leaves: a flash the colour of the
        /// projection, gone in under a second. Drawn on the way out for all three endings, so a
        /// decoy shot to pieces and one that simply ran out look the same to anybody watching -
        /// which is correct, because from outside they are the same event.
        /// </summary>
        public static void Collapse(Vector3 position, Map map)
        {
            if (map == null || position.ToIntVec3().Fogged(map)) return;

            FleckDef flash = DefDatabase<FleckDef>.GetNamedSilentFail("PlainFlash");
            if (flash == null) return;

            FleckCreationData data = FleckMaker.GetDataStatic(position, map, flash, 1.4f);
            data.instanceColor = new Color(0.29f, 0.70f, 0.84f);
            map.flecks.CreateFleck(data);
        }
    }
}
