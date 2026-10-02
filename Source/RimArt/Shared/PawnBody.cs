namespace RimArt
{
    /// <summary>
    /// Points on a standing humanlike pawn of the average body, in on-screen cells north of its DrawPos: the
    /// numbers of the lab's real-size stand-in (Tools/VfxLab/web/sketches/lib/pawn.js, measured in game on
    /// 2026-09-29, docs/pawn-height-handoff.md). A sketch drawn on that stand-in is ported with these numbers
    /// as they are. <see cref="PawnFit"/> is for the older 0.89-tall stand-in; applying it on top would move
    /// these points a second time (the chest from +0.05 to -0.27).
    ///
    /// <see cref="Ground"/> is the ground contact, <see cref="Feet"/> + 0.12, where lib/pawn.js puts the shadow.
    /// Fat and hulk bodies differ by up to 0.06 (lib/pawn.js Bodies); no picture uses them yet.
    /// </summary>
    public static class PawnBody
    {
        public const float HeadTop = 0.63f, Head = 0.41f, Neck = 0.20f, Chest = 0.05f, Feet = -0.54f, Ground = -0.42f;

        /// <summary>
        /// Draw heights against a real pawn's own height, its DrawPos.y. Core gives every pawn a seeded offset of up to
        /// +/-0.0366 on that height (Pawn_DrawTracker.SeededYOffset) and draws each part (body, head, hair, apparel, the
        /// held weapon) Clamp(layer, -10, 100) x 0.000366 above it (PawnRenderUtility.AltitudeForLayer): from -0.0037 to
        /// +0.0366. A picture over the pawn starts <see cref="Over"/> above its DrawPos.y; one behind it starts at
        /// <see cref="Under"/> or lower and keeps its own stack of layers under -0.005. Placed from
        /// AltitudeLayer.Pawn.AltitudeFor() instead, a picture at +0.03 to +0.07 is hidden under some pawns' heads and
        /// one at -0.03 drawn over others' bodies. The previews have no real pawn and pass AltitudeLayer.Pawn.
        /// </summary>
        public const float Over = 0.038f, Under = -0.02f;
    }
}
