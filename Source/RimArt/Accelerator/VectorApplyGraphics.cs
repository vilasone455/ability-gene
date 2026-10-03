using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.AcceleratorGraphics;
using V = RimArt.VectorApply;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Draws the manipulation's Apply moment, the port of the lab's accelerator-vector-apply.js: at
    /// each changed round a white stroke with a black edge coming in along the old heading and bent
    /// onto the new one, with a white jagged star on the corner; a white trail with a black edge
    /// behind the round, 2 x force cells long; a strain ring out of his head, larger with the strain
    /// spent; the reach ring flashing once. White and black only: the group colours stay in the
    /// paused panel.
    ///
    /// Not drawn: the rounds themselves (the game draws the real bullets), the paused panel's lines
    /// (VectorEditDrawer), the pawns and rifles. The lines are flat and the rings level, so there is
    /// one drawing for every volley direction. The strain ring's height goes through PawnFit; the
    /// round points are drawn where they are given. Every routine takes times and keeps no state.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class VectorApplyGraphics
    {
        private static readonly Vector2[] stroke = new Vector2[3], trail = new Vector2[3];

        /// <summary>Draws <paramref name="shot"/> <paramref name="seconds"/> after Apply; nothing before 0, or when his cell is out of bounds or fogged.</summary>
        internal static void Draw(in VectorApplyShot shot, float seconds, Map map)
        {
            if (seconds < 0f || !Shown(shot.Feet, map)) return;
            Begin(shot.Feet);

            // --- the reach ring and the strain ring ----------------------------------------------------------------------
            if (seconds < V.FlashLife)
            {
                float u = seconds / V.FlashLife, reach = shot.Reach * (1f + V.ReachSwell * Smooth(u));
                PaperBombGraphics.RingAt(shot.Feet, reach, Fade(Edge, 0.35f * (1f - u)), Floor + 0.022f);
                PaperBombGraphics.RingAt(shot.Feet, reach - 0.06f, Fade(White, 0.45f * (1f - u)), Floor + 0.023f);
                var head = new Vector2(shot.Feet.x, shot.Feet.y + PawnFit.Y(V.HeadUp));
                float size = V.StrainBase + V.StrainPer * shot.Strain;
                PaperBombGraphics.RingAt(head, 0.15f + Smooth(u) * size, Fade(Edge, 0.85f * (1f - u)), Overhead + 0.19f);
                PaperBombGraphics.RingAt(head, 0.12f + Smooth(u) * size, Fade(White, 0.9f * (1f - u)), Overhead + 0.191f);
            }
            if (shot.Rounds == null) return;

            for (int n = 0; n < shot.Rounds.Length; n++)
            {
                VectorApplyRound r = shot.Rounds[n];
                if (seconds <= V.FlashLife) Corner(r, seconds);
                Trail(r, seconds);
            }
        }

        /// <summary>The bend at the catch point: the stroke in along the old heading and out along the new, and the star.</summary>
        private static void Corner(in VectorApplyRound r, float age)
        {
            float u = age / V.FlashLife, grow = Smooth(age / V.Grow);
            Vector2 c = r.Caught;
            stroke[0] = c - r.OldHeading * V.StrokeIn;
            stroke[1] = c;
            stroke[2] = c + r.NewHeading * (V.StrokeOut * grow + 0.01f);
            GokuGraphics.Line(stroke, 0.12f, Fade(Edge, 0.85f * (1f - u)), null, Overhead + 0.15f, Taper.Both);
            GokuGraphics.Line(stroke, 0.06f, Fade(White, 0.95f * (1f - u)), whiteGlow, Overhead + 0.151f, Taper.Both);

            // The star: 7 white spikes of uneven length and width at uneven angles, out in 0.05 s, gone by StarLife.
            float st = Mathf.Clamp01(age / V.StarLife);
            if (st >= 1f) return;
            float white = 1f - st * st, edge = 0.7f * (1f - st) * (1f - st), core = 0.6f * (1f - st) + 0.15f;
            Sprite(c, core, core, Fade(White, 0.9f * white), glow, Overhead + 0.16f);
            int seed = r.Seed;
            for (int i = 0; i < V.Spikes; i++)
            {
                float q = Rand(i + seed * 13), ang = (i * 51f + (q - 0.5f) * 40f + seed * 23f) * Mathf.Deg2Rad;
                float len = (0.15f + 0.65f * Mathf.Pow(Rand(i + seed * 7 + 50), 1.5f)) * Smooth(Mathf.Min(1f, st * 3f));
                float w = 0.04f + 0.05f * Rand(i + seed * 5 + 90);
                var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 a = c + way * 0.05f, b = c + way * len;
                Streak(a, b, w + 0.035f, Fade(Edge, edge), solid, Overhead + 0.161f, 3);
                Streak(a, b, w, Fade(White, white), whiteGlow, Overhead + 0.162f, 3);
            }
        }

        /// <summary>The trail behind the round, 2 x force cells, running in to its last point once it is gone.</summary>
        private static void Trail(in VectorApplyRound r, float seconds)
        {
            V.Trail(r, seconds, out float head, out float tail);
            if (tail >= head) return;
            float behind = head - tail;
            trail[0] = r.Live;
            trail[1] = r.Live - r.NewHeading * (behind / 2f);
            trail[2] = r.Live - r.NewHeading * behind;
            GokuGraphics.Line(trail, 0.11f, Fade(Edge, 0.75f), null, Overhead + 0.09f);
            GokuGraphics.Line(trail, 0.055f, Fade(White, 0.95f), whiteGlow, Overhead + 0.091f);
        }
    }
}
