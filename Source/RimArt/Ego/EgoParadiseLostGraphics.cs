using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using T = RimArt.EgoParadiseLostTiming;

namespace RimArt
{
    /// <summary>
    /// The palette, the layers and the drawing pieces the Paradise Lost pictures share: the port of the parts of
    /// ego-paradise-lost-v2.js that every picture uses, and of the lab helpers it borrows that have no port of
    /// the same shape yet (chain-sickle.js's tube with a width per point and an offset band, six-paths-impact.js's
    /// two-line band). Lines are GokuGraphics.Line (goku.js's line), level rings PaperBombGraphics.RingAt
    /// (goku.js's ringAt), straight streaks, sprites and the disc VfxDraw's. Strips are written relative to the
    /// ground point given to VfxDraw.Begin, so each picture's Draw calls Begin first.
    ///
    /// A point with height is a ground point plus h lab cells up, drawn h x Lift cells north of the pawn's
    /// ground line (<see cref="Scr"/>) with its shadow along the sun (<see cref="Shd"/>). The ground line is the
    /// cell centre + Ground, the real-size stand-in's (lib/pawn.js), so these are a real pawn's numbers and
    /// nothing goes through PawnFit.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class EgoParadiseLostGraphics
    {
        // WhiteNight's red for the thorns and the star (v2: maroon), white for the staff and the wings, gold for the halo.
        internal static readonly Color Thorn = new Color(0.50f, 0.04f, 0.07f), ThornLit = new Color(0.82f, 0.18f, 0.18f),
            ThornDark = new Color(0.22f, 0.01f, 0.03f), RedInk = new Color(0.07f, 0.02f, 0.03f);
        internal static readonly Color Gore = new Color(0.42f, 0.03f, 0.05f), Clot = new Color(0.36f, 0.02f, 0.04f);
        internal static readonly Color Gold = new Color(0.96f, 0.76f, 0.26f), GoldLit = new Color(1f, 0.93f, 0.62f);
        internal static readonly Color Shaft = new Color(0.94f, 0.93f, 0.90f), ShaftLine = new Color(0.36f, 0.35f, 0.38f),
            Snake = new Color(0.80f, 0.80f, 0.78f), Eye = new Color(0.05f, 0.05f, 0.05f);
        internal static readonly Color Apple = new Color(0.80f, 0.07f, 0.09f), AppleLit = new Color(1f, 0.48f, 0.42f), Stem = new Color(0.28f, 0.40f, 0.20f);
        internal static readonly Color Feather = new Color(0.97f, 0.96f, 0.95f), FeatherLine = new Color(0.48f, 0.48f, 0.54f),
            FeatherShade = new Color(0.78f, 0.78f, 0.84f), Smear = new Color(0.66f, 0.04f, 0.06f);
        internal static readonly Color WingBone = new Color(0.90f, 0.89f, 0.88f), WingGrey = new Color(0.62f, 0.62f, 0.66f),
            Star = new Color(0.86f, 0.07f, 0.09f), StarGlow = new Color(1f, 0.22f, 0.16f);
        internal static readonly Color White = new Color(1f, 1f, 1f), CrossRed = new Color(1f, 0.30f, 0.24f), Pink = new Color(1f, 0.78f, 0.86f);

        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();
        internal static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();

        /// <summary>Most points one tube or line takes: a feather's spine has 9, the snake 57.</summary>
        private const int MostPoints = 64;
        /// <summary>The points and half-widths of the tube being built; fill them, then call <see cref="Tube"/>.</summary>
        internal static readonly Vector2[] Pts = new Vector2[MostPoints];
        internal static readonly float[] W = new float[MostPoints];

        /// <summary>A ground point <paramref name="h"/> lab cells up, as drawn: on the ground line, h x Lift north.</summary>
        internal static Vector2 Scr(Vector2 ground, float h) => new Vector2(ground.x, ground.y + PawnBody.Ground + h * T.Lift);

        /// <summary>The shadow of a ground point <paramref name="h"/> lab cells up: on the ground line, along the sun.</summary>
        internal static Vector2 Shd(Vector2 ground, float h, Vector2 sun) => new Vector2(ground.x + sun.x * h, ground.y + PawnBody.Ground + sun.y * h);

        /// <summary>The direction 90 degrees to the left of <paramref name="d"/>.</summary>
        internal static Vector2 Left(Vector2 d) => new Vector2(-d.y, d.x);

        /// <summary>Degrees of a direction, 0 east, 90 north.</summary>
        internal static float DegOf(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        internal static void Disc(Vector2 at, float altitude, float rx, float rz, float angle, Color colour, Material material = null) =>
            DrawMesh(disc, at, altitude, rx, rz, angle, colour, material ?? solid);

        /// <summary><see cref="VfxDraw.Tube"/> through the first <paramref name="count"/> of <see cref="Pts"/>, <see cref="W"/>[i] + <paramref name="add"/> cells to each side.</summary>
        internal static void Tube(int count, float add, Color colour, float altitude, float lo = -1f, float hi = 1f) =>
            VfxDraw.Tube(Pts, W, count, colour, altitude, lo, hi, add);

        /// <summary>
        /// The quad between the line a0-a1 and the line b0-b1 (six-paths-impact.js's band with two points a side):
        /// a0 faces b0 and a1 faces b1. b0 = b1 makes a spike from the base a0-a1.
        /// </summary>
        internal static void Band(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, Color colour, float altitude)
        {
            if (colour.a <= 0.001f) return;
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = a0; a[1] = a1;
            b[0] = b0; b[1] = b1;
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A spike lying on the floor from a base <paramref name="halfWidth"/> to each side of <paramref name="at"/> to <paramref name="tip"/>.</summary>
        internal static void Spike(Vector2 at, Vector2 side, float halfWidth, Vector2 tip, Color colour, float altitude) =>
            Band(at + side * halfWidth, at - side * halfWidth, tip, tip, colour, altitude);

        /// <summary>A straight line from a to b through two points, goku.js's line with its taper.</summary>
        internal static void Line2(Vector2 a, Vector2 b, float width, Color colour, Material material, float altitude, GokuGraphics.Taper taper)
        {
            Vector2[] pts = GokuGraphics.Points(2);
            pts[0] = a;
            pts[1] = b;
            GokuGraphics.Line(pts, width, colour, material, altitude, taper);
        }
    }
}
