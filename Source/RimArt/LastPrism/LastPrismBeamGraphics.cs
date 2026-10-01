using System;
using UnityEngine;
using Verse;
using static RimArt.LastPrismGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.LastPrismTiming;

namespace RimArt
{
    /// <summary>
    /// The Last Prism's beams, for <see cref="LastPrismGraphics.Draw"/>. The fan: six beams from the tip, each three
    /// nested soft additive strips (its colour, a paler middle, a thin white core) with a rounded start, a ripple
    /// running out along it and a 1.5-cell taper at full range, a glow where it leaves the tip, two flow lines, light
    /// on the floor under it, and where a wall stops it a glow and sparks thrown back. The joined beam: rainbow and
    /// white light on the floor, a pale glow, six colour bands side by side (red on one edge to violet on the
    /// other, each wobbling on its own), a pale sheath and a white core, ten flow lines, twelve sparkles, the pulse
    /// that runs down it at the join, a glow on the wall that stops it, and a flare at the tip. The join itself: a
    /// white flash and two rings opening at the tip.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class LastPrismBeamGraphics
    {
        private static readonly Color Crystal = new Color(0.74f, 0.93f, 1f);
        /// <summary>This frame's fan beams, from <see cref="Spread"/>: angle (radians), where each leaves the tip on the ground, cells to a wall or the range, stopped by a wall, colour.</summary>
        internal static readonly float[] Angle = new float[T.Beams], Length = new float[T.Beams];
        internal static readonly Vector2[] Start = new Vector2[T.Beams];
        internal static readonly bool[] Blocked = new bool[T.Beams];
        internal static readonly Color[] Colour = new Color[T.Beams];

        /// <summary>Works out this frame's six fan beams. Their colours turn round the wheel once every 8.3 s.</summary>
        internal static void Spread(in LastPrismShot shot, in LastPrismFrame f)
        {
            for (int i = 0; i < T.Beams; i++)
            {
                T.FanBeam(i, f.S, shot.ChannelAt, shot.Join, shot.Fan, out double turn, out double shift);
                Angle[i] = f.Theta + (float)turn;
                Start[i] = f.TipG + f.Side * (float)shift;
                Length[i] = Reach(shot, Start[i], Angle[i]);
                Blocked[i] = Length[i] < shot.Range - 1e-6f;
                Colour[i] = Hue(i / (float)T.Beams + f.S * 0.12f);
            }
        }

        /// <summary>Fan beam <paramref name="i"/> passes within <paramref name="reach"/> of the ground point <paramref name="at"/> (after <see cref="Spread"/>).</summary>
        internal static bool Crosses(int i, Vector2 at, float reach) =>
            T.OnLine(at.x, at.y, Start[i].x, Start[i].y, Angle[i], Length[i], reach);

        /// <summary>The glow round the tip while the beam shows: it grows from 0.5 to 1.4 cells as the beams narrow.</summary>
        internal static void TipGlow(in LastPrismFrame f)
        {
            if (f.Live <= 0f) return;
            float size = 0.5f + 0.9f * f.U;
            Sprite(f.TipS, size, size, Fade(Hue(f.S * 0.5f), (0.35f + 0.4f * f.U) * f.Live), glow, f.GlowLayer);
            Sprite(f.TipS, size * 0.45f, size * 0.45f, Fade(White, (0.4f + 0.5f * f.U) * f.Live), glow, f.GlowLayer + 0.001f);
        }

        internal static void DrawFan(in LastPrismFrame f)
        {
            float s = f.S, al = f.Opacity * f.Live, wf = Mathf.Lerp(0.45f, 1f, f.U) * f.Shrink;
            for (int i = 0; i < T.Beams; i++)
            {
                float len = Length[i], angle = Angle[i], end = Blocked[i] ? 0.15f : 1.5f;
                Color c = Colour[i];
                var way = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 a = Start[i] + new Vector2(0f, f.Lift);
                Sprite(Start[i] + way * (len / 2f), len, 0.6f * wf, Fade(c, 0.08f * al), glow, Floor + 0.01f + i * 0.0005f, -angle * Mathf.Rad2Deg);
                Ray(a, angle, len, T.FanHalf * wf, Fade(c, 0.24f * al), f.BeamLayer + 0.01f, end, i, s);
                Ray(a, angle, len, T.FanHalf * 0.5f * wf, Fade(Pale(c), 0.45f * al), f.BeamLayer + 0.011f, end, i, s);
                Ray(a, angle, len, T.FanHalf * 0.16f * wf, Fade(White, 0.75f * al), f.BeamLayer + 0.012f, end, i, s);
                Sprite(a, 0.32f, 0.32f, Fade(c, 0.5f * al), glow, f.BeamLayer + 0.013f);
                for (int k = 0; k < 2; k++)
                {
                    float d = (s * 16f + Rand(i * 7 + k) * len) % len;
                    if (d > 0.5f && d < len - 1.2f) Streak(a + way * d, a + way * (d + 0.9f), 0.05f * wf, Fade(White, 0.55f * al), whiteGlow, f.BeamLayer + 0.013f, 3);
                }
                if (!Blocked[i]) continue;
                // Where a wall stops it: a glow, and sparks thrown back off the wall.
                Vector2 e = a + way * len;
                Sprite(e, 0.7f, 0.7f, Fade(c, 0.6f * al), glow, Overhead + 0.014f);
                Sprite(e, 0.22f, 0.22f, Fade(White, 0.85f * al), glow, Overhead + 0.015f);
                for (int q = 0; q < 3; q++)
                {
                    float v = (s * 5f + Rand(i * 5 + q)) % 1f, back = angle + Mathf.PI + (Rand(i * 5 + q + 50) - 0.5f) * 2.2f, r0 = 0.1f + v * 0.6f;
                    Streak(Around(e, back, r0), Around(e, back, r0 + 0.25f), 0.04f, Fade(Pale(c), (1f - v) * al), whiteGlow, Overhead + 0.016f, 3);
                }
            }
        }

        internal static void DrawJoined(in LastPrismShot shot, in LastPrismFrame f)
        {
            float s = f.S, inF = Mathf.Clamp01((s - f.JoinAt) / T.JoinIn) * f.Live, W = shot.Width * f.Shrink, L = f.Lane, end = f.LaneBlocked ? 0.2f : 1.8f;
            float theta = f.Theta, degrees = theta * Mathf.Rad2Deg;
            Color tint = Hue(s * 0.25f, 0.55f);
            Vector2 a = f.TipS, dir = f.Dir, side = f.Side, mid = f.TipG + dir * (L / 2f);
            Vector2 Pt(float d, float across = 0f) => a + dir * d + side * across;
            Sprite(mid, L, W * 2.4f, Fade(tint, 0.12f * inF), glow, Floor + 0.012f, -degrees);
            Sprite(mid, L, W * 1.1f, Fade(White, 0.06f * inF), glow, Floor + 0.013f, -degrees);
            Ray(a, theta, L, W / 2f * 1.15f, Fade(Pale(tint), 0.1f * inF), f.BeamLayer + 0.02f, end, 0, s);
            // The six beams, joined: six bands side by side, red on one edge to violet on the other, each wobbling on its
            // own. They overlap into white in the middle and keep their colour at the edges. (Terraria's joined beam is a
            // white core with two drifting colour fringes; the bands were kept by choice, 2026-10-01.)
            for (int q = 0; q < T.Beams; q++)
                Ray(a, theta, L, W * 0.1f, Fade(Hue(q / (float)T.Beams + 0.04f * Mathf.Sin(s * 0.7f), 0.95f), 0.3f * inF), f.BeamLayer + 0.021f + q * 0.0003f, end, q, s,
                    true, (q - 2.5f) * W * 0.14f);
            Ray(a, theta, L, W / 2f * 0.32f, Fade(Crystal, 0.35f * inF), f.BeamLayer + 0.024f, end, 0, s);
            Ray(a, theta, L, W / 2f * 0.12f, Fade(White, 0.9f * inF), f.BeamLayer + 0.0245f, end, 0, s);
            if (L > 0f)
            {
                for (int k = 0; k < 10; k++)
                {
                    float d = (s * 30f + Rand(k + 11) * L) % L, across = (Rand(k + 20) - 0.5f) * W * 0.6f;
                    if (d > 0.6f && d < L - 1.6f) Streak(Pt(d, across), Pt(d + 1.4f, across), 0.06f, Fade(White, 0.5f * inF), whiteGlow, f.BeamLayer + 0.027f, 3);
                }
                for (int g = 0; g < 12; g++)
                {
                    float v = (s * 1.3f + Rand(g + 40)) % 1f, d = 0.8f + Rand(g + 41) * (L - 1.6f), sideways = (Rand(g + 42) > 0.5f ? 1f : -1f) * (W * 0.3f + v * W * 0.6f);
                    GokuGraphics.Glint(Pt(d, sideways), 0.06f + 0.1f * (1f - v), 0.85f * ChainSickleGraphics.Bump(v) * inF, Hue(Rand(g) + s * 0.2f, 0.6f), 45f * v);
                }
            }
            float pulse = (s - f.JoinAt) / T.PulseTime;
            if (pulse < 1f) Sprite(Pt(L * pulse), W * 2.5f, W * 2.5f, Fade(White, 0.8f * (1f - pulse)), glow, f.BeamLayer + 0.028f);
            if (f.LaneBlocked)
            {
                Vector2 e = Pt(L);
                Sprite(e, W * 1.8f, W * 1.8f, Fade(tint, 0.6f * inF), glow, Overhead + 0.03f);
                Sprite(e, W * 0.7f, W * 0.7f, Fade(White, 0.85f * inF), glow, Overhead + 0.031f);
            }
            float flare = 1f + 0.08f * Mathf.Sin(s * 47f);
            Sprite(f.TipS, W * 1.8f * flare, W * 1.8f * flare, Fade(tint, 0.45f * inF), glow, f.GlowLayer + 0.002f);
            Sprite(f.TipS, W * 0.8f, W * 0.8f, Fade(White, 0.85f * inF), glow, f.GlowLayer + 0.003f);
            GokuGraphics.Glint(f.TipS, 0.5f, 0.6f * inF, White, s * 200f);
        }

        /// <summary>The join: a white flash 3.2 cells across and a white and a coloured ring opening at the tip over <see cref="LastPrismTiming.JoinFlash"/> s.</summary>
        internal static void JoinFlash(in LastPrismFrame f)
        {
            float v = (f.S - f.JoinAt) / T.JoinFlash, left = 1f - v;
            Sprite(f.TipS, 3.2f, 3.2f, Fade(White, 0.7f * left * left), glow, Overhead + 0.2f);
            PaperBombGraphics.RingAt(f.TipS, 0.25f + 1.8f * Smooth(v), Fade(White, 0.8f * left), Overhead + 0.21f, false, whiteGlow);
            PaperBombGraphics.RingAt(f.TipS, 0.15f + 1.2f * Smooth(v), Fade(Hue(f.S * 0.25f), 0.7f * left), Overhead + 0.211f, false, whiteGlow);
        }

        /// <summary>
        /// One soft beam layer from the drawn point <paramref name="a"/> along <paramref name="angle"/>, <paramref name="half"/>
        /// cells to either side: rounded over its first 0.2 cells, tapered over the last <paramref name="softEnd"/>, with a
        /// 7 % ripple running out along it. A band is shifted <paramref name="across"/> to the beam's left and wobbles 0.025
        /// cells about that. A point every 0.4 cells, counted in double as the sketch does.
        /// </summary>
        private static void Ray(Vector2 a, float angle, float len, float half, Color colour, float altitude, float softEnd, int i, float s,
            bool band = false, float across = 0f)
        {
            if (colour.a <= 0.002f || len <= 0.05f) return;
            float dx = Mathf.Cos(angle), dz = Mathf.Sin(angle);
            int n = Math.Max(2, (int)Math.Ceiling(len / 0.4)), count = Math.Min(n, MostPoints - 1);
            Sides(count + 1, out Vector2[] left, out Vector2[] right);
            for (int k = 0; k <= count; k++)
            {
                float d = len * k / count;
                float w = half * Mathf.Sqrt(Mathf.Clamp01(d / 0.2f) * Mathf.Clamp01((len - d) / softEnd)) * (1f + 0.07f * Mathf.Sin(d * 1.7f - s * 26f + i * 1.3f)) + 0.003f;
                float shift = band ? across + 0.025f * Mathf.Sin(d * 0.8f - s * 10f + i * 1.7f) : 0f;
                float x = a.x + dx * d - dz * shift, z = a.y + dz * d + dx * shift;
                left[k] = new Vector2(x - dz * w, z + dx * w);
                right[k] = new Vector2(x + dz * w, z - dx * w);
            }
            Strip(left, right, colour, whiteGlow, altitude);
        }
    }
}
