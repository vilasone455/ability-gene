using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Fits a picture laid out on the VFX lab's stand-in pawn to a real pawn in game. The stand-in most
    /// sketches draw is 0.89 cells tall, from 0.14 below the point it is drawn at to 0.75 above; a real
    /// humanlike is about 1.15 tall, from 0.53 below its DrawPos to 0.63 above (measured in game on
    /// 2026-09-29, docs/pawn-height-handoff.md). A height y above a pawn's DrawPos in the sketch goes to
    /// <see cref="Scale"/> y - <see cref="Drop"/> on a real pawn: head centre +0.58 to +0.42, chest
    /// +0.30 to +0.06, feet -0.14 to -0.51. Body-shaped things (coils, silhouettes, shells) are drawn
    /// <see cref="Scale"/> times bigger; hit flashes and sparks keep their size.
    ///
    /// A kit's game code draws its pictures between <see cref="Begin"/> and <see cref="End"/>. The
    /// picture code puts heights on a pawn through <see cref="Y"/> (on-screen cells) or <see cref="H"/>
    /// (lab heights, drawn x Lift) and sizes of body-shaped things through <see cref="Body"/>; floor
    /// things at the pawn's cell stay where they are. Outside Begin and End every one of them gives the
    /// sketch's number back, so the lab's recorder and the in-game previews, which never call Begin,
    /// still draw the stand-in's picture and the recordings still match the sketches.
    /// </summary>
    public static class PawnFit
    {
        public const float Scale = 1.3f, Drop = 0.33f;

        /// <summary>True between <see cref="Begin"/> and <see cref="End"/>: the picture is drawn on real pawns.</summary>
        public static bool On { get; private set; }

        public static void Begin() => On = true;

        public static void End() => On = false;

        /// <summary>A height on a pawn, in on-screen cells above its DrawPos: Scale y - Drop in game, y in the lab.</summary>
        public static float Y(float y) => On ? FitY(y) : y;

        /// <summary>A height on a pawn in lab units (drawn x Lift above the DrawPos), fitted the same way as <see cref="Y"/>.</summary>
        public static float H(float h) => On ? FitH(h) : h;

        /// <summary><see cref="Y"/> fitted whether or not a kit is drawing, for game code that sets up a picture ahead of drawing it.</summary>
        public static float FitY(float y) => Scale * y - Drop;

        /// <summary><see cref="H"/> fitted whether or not a kit is drawing.</summary>
        public static float FitH(float h) => Scale * h - Drop / SixPathsHeight.Lift;

        /// <summary>The size factor for body-shaped things: Scale in game, 1 in the lab.</summary>
        public static float Body => On ? Scale : 1f;

        /// <summary>A point on a pawn given as its DrawPos plus an on-screen offset, fitted.</summary>
        public static Vector2 At(Vector2 drawPos, float dx, float dy) =>
            new Vector2(drawPos.x + dx * Body, drawPos.y + Y(dy));
    }
}
