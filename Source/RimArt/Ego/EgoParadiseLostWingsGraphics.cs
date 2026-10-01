using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoParadiseLostGraphics;
using T = RimArt.EgoParadiseLostTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// The corroded wielder's wings, the port of wings() and feather() in ego-paradise-lost-v2.js (v2.1, built as
    /// the Ruina Realization sprite draws them): three wings a side, each a curved pale arm with 6 notches, 5 short
    /// covert feathers along it and 6 long flight feathers from its outer half, overlapping like shingles; 66
    /// feathers in all. A feather is a broad curved blade a fifth as wide as long, widest at 40 %, with a grey
    /// outline, a shaded half, a grey centre line, two grey vane strokes and 2 or 3 blood blotches stretched along
    /// it, darker toward the tip; about half have a dark red tip. They open from folded down along the back
    /// (open 0) to spread (open 1), the lower wing first, and sway 3 degrees.
    ///
    /// A per-facing method, because the span collapses onto the height axis facing east or west: facing south or
    /// north both sides spread east and west, behind the pawn facing south and over it facing north; facing east
    /// or west both sweep back and 20 degrees up, the near side over the pawn and the far side behind it at 82 %,
    /// shifted 0.06 back and 0.1 up.
    ///
    /// Once a wing is fully open its shape holds and the sway only turns it about its root, so it is baked once per
    /// facing (<see cref="VfxDraw.BeginBake"/>, about 100 meshes a wing, the same for every pawn) and drawn turned
    /// each frame. Only the unfold and fold rebuild the strips every frame. Without the bake the six wings rebuilt
    /// about 590 strips (15,000 vertices) every frame while corroded.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoParadiseLostWingsGraphics
    {
        // The fully open wings, per facing (Rot4.AsInt) and wing (side x 3 + arm), and the root each was baked at.
        private static readonly List<VfxBakedDraw>[,] baked = new List<VfxBakedDraw>[4, 6];
        private static readonly Vector2[,] bakedRoot = new Vector2[4, 6];

        /// <summary>
        /// The wings of a pawn at <paramref name="pos"/> (its DrawPos) facing <paramref name="facing"/>,
        /// <paramref name="open"/> 0 (folded, not drawn) to 1; <paramref name="seconds"/> drives the sway.
        /// Called by EgoParadiseLostCorrodedGraphics after VfxDraw.Begin.
        /// </summary>
        internal static void Draw(Vector2 pos, Rot4 facing, float open, float seconds, Vector2 sun, float strength)
        {
            if (open <= 0f) return;
            bool sideView = facing.IsHorizontal;
            float back = facing == Rot4.East ? -1f : 1f, R = T.WingSpan * T.WingReach;
            var root = new Vector2(pos.x, pos.y + 0.14f);
            Sprite(new Vector2(pos.x + sun.x * 1.6f, pos.y + PawnBody.Ground + sun.y * 1.6f), (sideView ? 1.8f : 3.2f) * T.WingSpan * open, 1.1f * open,
                Fade(RedInk, strength * 0.25f * open), soft, ShadowLayer);
            if (sideView)
            {
                Wing(root + new Vector2(0.06f * back, 0.1f), back, 0.82f, PawnLayer - 0.03f, 0, open, seconds, R, facing);
                Wing(root, back, 1f, PawnLayer + 0.03f, 1, open, seconds, R, facing);
                return;
            }
            float layer = facing == Rot4.South ? PawnLayer - 0.03f : PawnLayer + 0.03f;
            Wing(root, -1f, 1f, layer, 0, open, seconds, R, facing);
            Wing(root, 1f, 1f, layer, 1, open, seconds, R, facing);
        }

        /// <summary>
        /// One side's three wings from <paramref name="r0"/>, spreading east (<paramref name="sx"/> 1) or west (-1),
        /// <paramref name="size"/> of full size. <paramref name="wi"/> is the side's index, for the sway and the seeds.
        /// </summary>
        private static void Wing(Vector2 r0, float sx, float size, float layer, int wi, float open, float seconds, float R, Rot4 facing)
        {
            for (int ai = 2; ai >= 0; ai--)                     // the lower wing first, the upper one on top
            {
                float e = Smooth(open * 1.4f - ai * 0.15f), sway = 3f * Mathf.Sin(seconds * 2.4f + ai + wi) * e;
                if (e < 1f || EgoParadiseLostCorrodedGraphics.Rebuild)
                {
                    Arm(r0, sx, size, layer, wi, ai, e, sway, R, facing.IsHorizontal);
                    continue;
                }
                int f = facing.AsInt, w = wi * 3 + ai;
                if (baked[f, w] == null)
                {
                    var into = new List<VfxBakedDraw>();
                    BeginBake(into);
                    try { Arm(r0, sx, size, layer, wi, ai, 1f, 0f, R, facing.IsHorizontal); }
                    finally { EndBake(); }
                    baked[f, w] = into;
                    bakedRoot[f, w] = r0;
                }
                // The sway adds to th, which turns a wing spreading east (sx 1) counter-clockwise on screen and one
                // spreading west clockwise; DrawMesh turns clockwise, hence -sx.
                DrawBaked(baked[f, w], bakedRoot[f, w], -sx * sway, r0 - bakedRoot[f, w]);
            }
        }

        /// <summary>
        /// Wing <paramref name="ai"/> of one side (0 the upper), <paramref name="e"/> of the way open, turned
        /// <paramref name="sway"/> degrees from its resting angle.
        /// </summary>
        private static void Arm(Vector2 r0, float sx, float size, float layer, int wi, int ai, float e, float sway, float R, bool sideView)
        {
            float k = size * (sideView ? 0.85f : 1f) * Mathf.Lerp(0.45f, 1f, e);
            float th = Mathf.Lerp(-100f, T.ArmAngle[ai] + (sideView ? 20f : 0f), e) + sway;
            float rad = th * Mathf.Deg2Rad;
            // A screen angle for a wing spreading east, mirrored for the west side.
            var d = new Vector2(Mathf.Cos(rad) * sx, Mathf.Sin(rad));
            var up = new Vector2(-Mathf.Sin(rad) * sx, Mathf.Cos(rad));
            float AL = R * T.ArmLength[ai] * k, L0 = layer + (2 - ai) * 0.006f + wi * 0.0001f;

            // Flight feathers from the outer half, outer first so the inner ones lie over them.
            for (int j = T.Flights - 1; j >= 0; j--)
            {
                float f = j / (float)(T.Flights - 1), t = 0.45f + 0.55f * f, rel = Mathf.Lerp(70f, 10f, f) * e;
                DrawFeather(ArmAt(r0, d, up, AL, t), AngleOf(th - rel, sx), R * 0.62f * k * (0.8f + 0.25f * f), 0.2f,
                    ai * 97 + j * 7 + wi * 300, L0 + (T.Flights - j) * 0.0002f, sx);
            }
            // Coverts along the arm, over the flight feathers.
            for (int j = T.Coverts - 1; j >= 0; j--)
            {
                float f = j / (float)(T.Coverts - 1), t = 0.12f + 0.7f * f, rel = Mathf.Lerp(62f, 30f, f) * e;
                DrawFeather(ArmAt(r0, d, up, AL, t), AngleOf(th - rel, sx), R * 0.28f * k * (0.85f + 0.3f * f), 0.25f,
                    ai * 53 + j * 11 + wi * 500, L0 + 0.002f + (T.Coverts - j) * 0.0002f, sx);
            }
            // The arm on top: a pale ridge with notches.
            for (int i = 0; i < 9; i++)
            {
                Pts[i] = ArmAt(r0, d, up, AL, i / 8f);
                W[i] = 0.045f * (1f - 0.5f * i / 8f);
            }
            Tube(9, 0.012f, FeatherLine, L0 + 0.0035f);
            Tube(9, 0f, WingBone, L0 + 0.0036f);
            for (int n = 0; n < 6; n++)
            {
                float t = 0.15f + 0.13f * n, w = 0.04f * (1f - 0.5f * t);
                Vector2 c = ArmAt(r0, d, up, AL, t);
                Line2(c + up * w, c - up * w, 0.012f, FeatherLine, solid, L0 + 0.0037f + n * 0.00001f, Taper.None);
            }
        }

        /// <summary>A point <paramref name="t"/> of the way along an arm, bowed 12 % of its length toward its upper side.</summary>
        private static Vector2 ArmAt(Vector2 r0, Vector2 d, Vector2 up, float length, float t) =>
            r0 + d * (length * t) + up * (0.12f * length * Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)));

        /// <summary>The screen direction, in degrees, of the angle <paramref name="deg"/> for a wing spreading east, mirrored when <paramref name="sx"/> is -1.</summary>
        private static float AngleOf(float deg, float sx)
        {
            float r = deg * Mathf.Deg2Rad;
            return DegOf(new Vector2(Mathf.Cos(r) * sx, Mathf.Sin(r)));
        }

        /// <summary>
        /// One feather from <paramref name="b"/> along <paramref name="ang"/> degrees, <paramref name="length"/> cells
        /// long and <paramref name="broad"/> of that wide, curving toward the side <paramref name="mir"/> (1 for a wing
        /// spreading east, -1 west), so the curve and the shaded half mirror with the wing.
        /// </summary>
        private static void DrawFeather(Vector2 b, float ang, float length, float broad, int seed, float layer, float mir)
        {
            float r = ang * Mathf.Deg2Rad;
            var fd = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            var fn = new Vector2(-fd.y * mir, fd.x * mir);
            float bend = 0.08f * length, half = length * broad / 2f;
            for (int i = 0; i < 9; i++)
            {
                float u = i / 8f;
                Pts[i] = Spine(b, fd, fn, length, bend, u);
                W[i] = Width(half, u);
            }
            Tube(9, 0.01f, FeatherLine, layer);
            Tube(9, 0f, Feather, layer + 0.00002f);
            Tube(9, 0f, Fade(FeatherShade, 0.55f), layer + 0.00004f, mir > 0f ? -1f : 0.15f, mir > 0f ? -0.15f : 1f);
            Vector2[] rachis = GokuGraphics.Points(6);
            for (int i = 0; i < 6; i++) rachis[i] = Spine(b, fd, fn, length, bend, 0.05f + 0.85f * i / 5f);
            GokuGraphics.Line(rachis, 0.014f, Fade(FeatherLine, 0.75f), solid, layer + 0.00006f, Taper.End);
            for (int h = 0; h < 2; h++)
            {
                float u = 0.35f + 0.25f * h;
                Line2(Spine(b, fd, fn, length, bend, u), Spine(b, fd, fn, length, bend, u + 0.1f) - fn * (Width(half, u + 0.1f) * 0.85f),
                    0.01f, Fade(FeatherLine, 0.5f), solid, layer + 0.00007f + h * 0.000002f, Taper.None);
            }
            // Blood: smears stretched along the feather, each two overlapping ovals so the edge is uneven; on about
            // half the feathers the tip is dipped dark red.
            int blots = 2 + (Rand(seed + 3) > 0.5f ? 1 : 0);
            float rot = -DegOf(fd);
            for (int k = 0; k < blots; k++)
            {
                float u = 1f - 0.7f * Mathf.Pow(Rand(seed + 10 + k), 1.5f);
                Vector2 c = Spine(b, fd, fn, length, bend, u) + fn * ((Rand(seed + 20 + k) - 0.5f) * 0.7f * Width(half, u));
                float rx = half * (0.9f + 1.0f * Rand(seed + 30 + k)) * (0.6f + 0.6f * u), rz = Mathf.Min(Width(half, u) * 0.7f, rx * 0.35f);
                Vector2 c2 = c + fd * (rx * (0.35f + 0.3f * Rand(seed + 40 + k))) + fn * ((Rand(seed + 50 + k) - 0.5f) * rz);
                Disc(c, layer + 0.00008f + k * 0.00001f, rx, rz, rot, Fade(u > 0.7f ? Clot : Smear, 0.78f));
                Disc(c2, layer + 0.000085f + k * 0.00001f, rx * 0.55f, rz * 0.75f, rot + 8f * (Rand(seed + 60 + k) - 0.5f), Fade(u > 0.6f ? Clot : Smear, 0.7f));
            }
            if (Rand(seed + 7) > 0.5f)
            {
                for (int i = 0; i < 4; i++)
                {
                    float u = 0.8f + 0.2f * i / 3f;
                    Pts[i] = Spine(b, fd, fn, length, bend, u);
                    W[i] = Width(half, u) * 0.95f;
                }
                Tube(4, 0f, Fade(Clot, 0.7f), layer + 0.00009f);
            }
        }

        private static Vector2 Spine(Vector2 b, Vector2 fd, Vector2 fn, float length, float bend, float u) =>
            b + fd * (length * u) + fn * (bend * u * u);

        /// <summary>A feather's half-width at <paramref name="u"/> along it: rising to <paramref name="half"/> at 40 %, then tapering to a point.</summary>
        private static float Width(float half, float u) =>
            half * (u < 0.4f ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI / 2f * u / 0.4f)), 0.8f) : Mathf.Pow(Mathf.Max(0f, (1f - u) / 0.6f), 0.85f)) + 0.003f;
    }
}
