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
    /// The thorns round one pawn hit, the port of thorns() in ego-paradise-lost-v2.js (Lobotomy's normal hit):
    /// maroon branched thorns rise out of the floor in a circle round the pawn in 0.08 s, the tallest 1.5 pawn
    /// heights, stand 0.6 s and sink in 0.3 s. Each thorn is an inked tube with a dark side and a lit stripe,
    /// leaning outward with a kink, 2 or 3 branches rising outward on alternate sides, and a shadow along the sun.
    /// A red flash on the body for 0.15 s; 10 blood drops thrown out from the chest that land as spatter; a
    /// dark burst on the floor and a hole under each thorn. The marks and the spatter fade over 2.5 s once the
    /// thorns start to sink. A ring's hit is the small version: 6 thorns 0.55 pawn heights, 0.3 s, 4 drops.
    ///
    /// The thorns south of the pawn draw over it and the ones north of it under it, so the pawn stands among
    /// them. Everything is upright or flat on the floor, so there is no per-facing method.
    ///
    /// Not drawn (the sketch's stand-ins and lab aids): the pawn, its flinch, the shrinking slow ring at its feet
    /// and its damage bar.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoParadiseLostThornGraphics
    {
        private const float Tau = 6.2831855f;

        /// <summary>
        /// The thorns of one hit round the pawn at <paramref name="pawn"/> (its DrawPos, the cell centre for a
        /// standing pawn), <paramref name="age"/> s after the hit. <paramref name="seed"/> picks the thorns' shapes;
        /// <paramref name="ring"/> draws a ring's small thorns instead of the room hit's.
        /// </summary>
        public static void Draw(Vector2 pawn, float age, int seed, bool ring, Map map)
        {
            float life = ring ? T.RingThornLife : T.ThornLife;
            if (age < 0f || age >= T.HitGone(life) || !Shown(pawn, map)) return;
            Begin(pawn);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Thorns(pawn, age, seed, ring ? T.RingThornScale : T.ThornScale, life, ring ? T.RingThorns : T.Thorns, sun, strength);
        }

        // The thorns with every number given: tallest `scale` pawn heights, standing `life` s, `count` of them.
        private static void Thorns(Vector2 g, float age, int seed, float scale, float life, int count, Vector2 sun, float strength)
        {
            var foot = new Vector2(g.x, g.y + PawnBody.Ground);
            float mark = T.Mark(age, life);
            if (mark > 0f) Sprite(foot, 1.0f * scale, 0.6f * scale, Fade(ThornDark, 0.45f * mark), soft, Floor + 0.02f);
            if (age < T.BodyFlash)
                Sprite(new Vector2(g.x, g.y + 0.05f), 1.1f * scale, 1.2f * scale, Fade(StarGlow, 0.6f * (1f - age / T.BodyFlash)), glow, Overhead + 0.09f);
            Blood(g, age, seed, scale, mark);

            float k = T.Rise(age, life);
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count + 0.07f * Rand(seed + i)) * Tau;
                var outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 n = Left(outward), b = g + outward * (0.14f + 0.2f * Rand(seed + 10 + i));
                if (mark > 0f) Disc(new Vector2(b.x, b.y + PawnBody.Ground), Floor + 0.021f + i * 0.0002f, 0.04f, 0.03f, 0f, Fade(RedInk, 0.6f * mark));
                if (k <= 0f) continue;
                OneThorn(g, b, outward, n, i, seed, scale, k, sun, strength);
            }
        }

        // The blood spray (v2, Lobotomy's corridor frame): drops thrown out from the chest on a parabola,
        // landing as spatter stretched along their flight that fades with the floor marks.
        private static void Blood(Vector2 g, float age, int seed, float scale, float mark)
        {
            int drops = scale >= 1f ? T.Drops : T.RingDrops;
            for (int j = 0; j < drops; j++)
            {
                int r = seed * 5 + j;
                float a = Rand(r + 700) * Tau, v = (0.9f + 0.9f * Rand(r + 710)) * scale;
                var dd = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float up = 1f + Rand(r + 720), land = (up + Mathf.Sqrt(up * up + 2f * T.Gravity * T.ChestH)) / T.Gravity, tt = Mathf.Min(age, land);
                Vector2 q = g + dd * (v * tt);
                float h = Mathf.Max(0f, T.ChestH + up * tt - 0.5f * T.Gravity * tt * tt), size = 0.035f + 0.03f * Rand(r + 730);
                if (age < land) Disc(new Vector2(q.x, q.y + PawnBody.Ground + h * T.Lift), Overhead + 0.085f, size, size, 0f, Gore);
                else if (mark > 0f) Disc(new Vector2(q.x, q.y + PawnBody.Ground), Floor + 0.022f + j * 0.0002f, size * 1.8f, size * 1.1f, -DegOf(dd), Fade(Gore, 0.85f * mark));
            }
        }

        /// <summary>
        /// Thorn <paramref name="i"/> from the floor at <paramref name="b"/>, <paramref name="k"/> of the way up: three
        /// points (the floor, a kink at 55 %, the tip) leaning outward, then its branches.
        /// </summary>
        private static void OneThorn(Vector2 g, Vector2 b, Vector2 outward, Vector2 n, int i, int seed, float scale, float k, Vector2 sun, float strength)
        {
            float H = T.PawnH * scale * (i % 3 == 0 ? 1f : 0.55f + 0.3f * Rand(seed + 20 + i)) * k;
            float lean = (0.1f + 0.16f * Rand(seed + 30 + i)) * H, kink = (Rand(seed + 40 + i) - 0.5f) * 0.3f * H;
            Vector2 p1 = b + outward * (lean * 0.45f) + n * kink, p2 = b + outward * lean;
            float h1 = 0.55f * H, w = (0.035f + 0.03f * Rand(seed + 50 + i)) * Mathf.Max(0.6f, scale / 1.5f);
            // Behind the pawn (north of it) under it, in front over it.
            float L = (b.y > g.y ? PawnLayer - 0.015f : Overhead + 0.03f) + i * 0.0008f;

            Vector2[] shadow = GokuGraphics.Points(3);
            shadow[0] = Shd(b, 0f, sun);
            shadow[1] = Shd(p1, h1, sun);
            shadow[2] = Shd(p2, H, sun);
            GokuGraphics.Line(shadow, w * 1.6f, Fade(RedInk, strength * 0.4f), solid, ShadowLayer, Taper.End);

            Pts[0] = Scr(b, 0f);
            Pts[1] = Scr(p1, h1);
            Pts[2] = Scr(p2, H);
            for (int j = 0; j < 3; j++) W[j] = w * Mathf.Pow(1f - j / 2f, 0.9f) + 0.003f;
            Tube(3, 0.01f, RedInk, L);
            Tube(3, 0f, Fade(Thorn, 0.94f), L + 0.0001f);
            Tube(3, 0f, Fade(ThornDark, 0.7f), L + 0.0002f, -1f, -0.25f);
            Tube(3, 0f, Fade(ThornLit, 0.8f), L + 0.0003f, 0.2f, 0.75f);

            // Two or three branches up the thorn, on alternate sides, rising outward.
            int branches = 2 + (Rand(seed + 60 + i) > 0.5f ? 1 : 0);
            for (int c = 0; c < branches; c++)
            {
                float u = 0.28f + 0.2f * c + 0.08f * Rand(seed + 70 + i * 3 + c), sg = (c + i) % 2 == 1 ? 1f : -1f;
                Vector2 b0 = u < 0.55f ? Vector2.Lerp(b, p1, u / 0.55f) : Vector2.Lerp(p1, p2, (u - 0.55f) / 0.45f);
                float h0 = u * H, BL = (0.18f + 0.12f * Rand(seed + 80 + i * 3 + c)) * H;
                Vector2 b1 = b0 + outward * (0.35f * BL) + n * (sg * 0.5f * BL);
                Pts[0] = Scr(b0, h0);
                Pts[1] = Scr(b1, h0 + 0.6f * BL);
                W[0] = w * 0.6f * (1f - u * 0.5f) + 0.003f;
                W[1] = 0.003f;
                float lay = L + c * 0.00005f;
                Tube(2, 0.009f, RedInk, lay + 0.0004f);
                Tube(2, 0f, Fade(Thorn, 0.94f), lay + 0.0005f);
                Tube(2, 0f, Fade(ThornLit, 0.75f), lay + 0.0006f, 0.2f, 0.8f);
            }
        }
    }
}
