using Verse;

namespace RimArt
{
    /// <summary>
    /// One pawn's worth of "the mouse is over the vector manipulation gizmo right now".
    ///
    /// Hover is only knowable from OnGUI, and the reach ring has to be drawn from the map's
    /// Update pass - the same context the targeter draws a verb's range ring from, and the same
    /// one <see cref="VectorEditDrawer"/> already uses for the editor's own ring. So the gizmo
    /// reports here every frame the mouse is on it, and the map component takes the report and
    /// clears it.
    ///
    /// Update runs before OnGUI in a frame, so the ring trails the mouse by one frame and
    /// disappears one frame after the cursor leaves. At sixteen milliseconds that is not a lag
    /// anyone can see, and it buys a draw call that is definitely in a draw context.
    /// </summary>
    public static class VectorHoverRing
    {
        private static Pawn hovered;

        public static void Report(Pawn pawn)
        {
            hovered = pawn;
        }

        /// <summary>Takes the pending report, if any, and clears it.</summary>
        public static Pawn Consume()
        {
            Pawn pawn = hovered;
            hovered = null;
            return pawn;
        }
    }
}
