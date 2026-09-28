using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// Vergil seen for a moment where he is not: the afterimages at the far end of each Judgement Cut End chord
    /// and along a Yamato Dash (decided 2026-09-21: his real body, not a drawn silhouette). His own renderer is
    /// run a second time at the afterimage's spot and facing, every part on the additive glow shader with a
    /// blue-white tint, so every part of him is right without building anything. No shadow, no wounds and no
    /// katana: it is light, not a body.
    ///
    /// The render sequence is MimicRender's: EnsureInitialized, ParallelPreDraw and Draw at the new spot (as of
    /// 1.6 a pawn is drawn from results its render tree computed for one place, so all three are needed), then
    /// EnsureInitialized and ParallelPreDraw back at his real place so anything else drawing him this frame gets
    /// his own results. Called on the main thread from the casts' Draw, outside the game's parallel pre-draw.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class VergilGhost
    {
        /// <summary>The afterimage's light: his own textures on the glow shader, times this.</summary>
        private static readonly Color Colour = new Color(0.45f, 0.72f, 1f);

        private static readonly float Altitude = Overhead - 0.045f;

        /// <summary>The pawn whose afterimage is being drawn right now, or null. The pose patches read it.</summary>
        public static Pawn Drawing { get; private set; }

        /// <summary>This pass's tint, alpha included.</summary>
        public static Color Tint { get; private set; }

        /// <summary>How many afterimages have been drawn since the game started, for the tests.</summary>
        public static int Drawn { get; private set; }

        /// <summary>
        /// One afterimage of <paramref name="pawn"/> standing at <paramref name="ground"/> (its feet), facing
        /// <paramref name="facing"/>, at <paramref name="alpha"/> 0 to 1.
        /// </summary>
        public static void Draw(Pawn pawn, Vector2 ground, Rot4 facing, float alpha)
        {
            if (alpha <= 0.01f || pawn == null || !pawn.Spawned || Drawing != null) return;
            PawnRenderer renderer = pawn.Drawer?.renderer;
            if (renderer == null) return;

            // Above the pawns and above Judgement Cut End's dark area (Overhead - 0.05), as the sketch draws its
            // afterimage over the dark; under the cuts (Overhead + 0.03). A pawn's own layers add up to 0.037.
            var at = new Vector3(ground.x, Altitude, ground.y + VergilKit.FeetBelowDrawPos);
            // A soft light round it, under it, so it reads as light rather than a pale body.
            Sprite(new Vector2(ground.x, ground.y + 0.35f), 1.1f, 1.5f, Fade(VergilGraphics.Ice, 0.3f * alpha), glow, at.y - 0.002f);

            Drawing = pawn;
            Tint = new Color(Colour.r, Colour.g, Colour.b, Mathf.Clamp01(alpha));
            try
            {
                At(renderer, at, facing, true);
                Drawn++;
            }
            finally
            {
                // Cleared before the push-back: those results are his own and must not come out tinted.
                Drawing = null;
            }
            At(renderer, pawn.DrawPos, null, false);
        }

        private static void At(PawnRenderer renderer, Vector3 position, Rot4? rotation, bool draw)
        {
            // The render tree picks materials only when its parameters change, and they may not: while he is gone his
            // own pass is already flagged invisible, so an afterimage facing his way would reuse his see-through
            // materials instead of the glow. Both passes ask for a fresh pick.
            if (renderer.renderTree?.rootNode != null) renderer.renderTree.rootNode.requestRecache = true;
            renderer.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, position, rotation, true);
            renderer.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, position, rotation, true);
            if (draw) renderer.DynamicDrawPhaseAt(DrawPhase.Draw, position, rotation, true);
        }

        /// <summary>The facing that best shows a pawn moving along <paramref name="direction"/> (map x, z).</summary>
        public static Rot4 Facing(Vector2 direction) =>
            direction.sqrMagnitude < 1e-6f ? Rot4.South : Rot4.FromAngleFlat(90f - Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }
}
