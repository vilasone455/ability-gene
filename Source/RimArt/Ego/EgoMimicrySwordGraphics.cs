using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.EgoMimicryGraphics;
using T = RimArt.EgoMimicryTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>The sword in one frame: its pose, its size and what its flesh is doing.</summary>
    public struct EgoMimicrySword
    {
        /// <summary>The hand on the grip: x, z in the wielder's DrawPos frame, y cells up (see EgoMimicryTiming).</summary>
        public Vector3 Hand;
        /// <summary>Where the blade points, a 3D unit direction (x east, y up, z north).</summary>
        public Vector3 Blade;
        /// <summary>The blade's length in cells at size 1, and the size (2 at the grown swing's full swell).</summary>
        public float Length, Size;
        /// <summary>+1 shows the flesh on the left of the blade's screen direction, -1 mirrored, between them the blade turns edge-on.</summary>
        public float Flip;
        /// <summary>0 to 1: the grown swing's swell (the flesh bulges and wobbles, the spikes grow), the eyes opened wide, the corroded flesh over the grip.</summary>
        public float Swell, Open, Fuse;
        /// <summary>The heal: blood on the steel, the red glow sliding to <see cref="PulseAt"/> (share of the blade) and the green eye's glow, each 0 to 1.</summary>
        public float Blood, Pulse, PulseAt, EyePulse;
        public float Altitude;
        public Color Skin;
    }

    /// <summary>
    /// The Mimicry sword (Lobotomy's weapon sprite) laid flat and turned to where the blade points, as RimWorld
    /// draws equipment, mirrored when aiming west so the flesh stays on top: a dark steel edge curving up into
    /// the point, red muscle over the back half with fibres, thickest at the hilt where it wraps the blade in a
    /// bulb, three strands running out along the back, three bone spikes on the back and two under the flesh, a
    /// big green-iris eye near the hilt and a small blue-iris eye mid-blade, a black grip with a red ring and an
    /// end cap, black outlines, and the shadow cast along the sun. A blade pointing up or down is foreshortened
    /// by its screen length. The blade is about 4x the grip; its outline is the sketch's, in cells at size 1 from
    /// the guard: u along (0 to 1), v across (+ is the back, the flesh side).
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class EgoMimicrySwordGraphics
    {
        private static readonly float[,] EdgeV = { { 0f, -0.12f }, { 0.2f, -0.125f }, { 0.5f, -0.088f }, { 0.77f, -0.04f }, { 0.92f, 0.02f }, { 1f, 0.065f } };
        private static readonly float[,] BackV = { { 0f, 0.02f }, { 0.5f, 0.05f }, { 0.8f, 0.08f }, { 0.93f, 0.08f }, { 1f, 0.065f } };
        private static readonly float[,] TopV =
            { { 0f, 0.03f }, { 0.04f, 0.1f }, { 0.12f, 0.135f }, { 0.25f, 0.13f }, { 0.42f, 0.118f }, { 0.58f, 0.1f }, { 0.7f, 0.085f }, { 0.78f, 0.075f } };
        private static readonly float[,] BulbV = { { 0f, -0.05f }, { 0.04f, -0.14f }, { 0.1f, -0.145f }, { 0.2f, -0.095f } };
        /// <summary>The flesh covers the blade from the guard to 0.78 of its length.</summary>
        private const float FleshEnd = 0.78f;
        /// <summary>Bone spikes: where along (u), length in cells, lean toward the tip.</summary>
        private static readonly float[,] BackSpikes = { { 0.13f, 0.065f, 0.25f }, { 0.2f, 0.115f, 0.35f }, { 0.27f, 0.07f, 0.3f } };
        private static readonly float[,] BellySpikes = { { 0.2f, 0.045f, 0.2f }, { 0.36f, 0.04f, 0.25f } };
        /// <summary>Points along the steel and along the flesh.</summary>
        private const int SteelN = 18, FleshN = 14;

        // The frame's sword as locals for Pt and Bp: the hand on screen, the blade's screen direction and its
        // left, its foreshortening k, the wrist f, the size g, the blade length BL (cells), the swell and the clock.
        private static Vector2 hand, dir, nrm;
        private static float k, f, g, BL, swell, clock;

        /// <summary>A point <paramref name="a"/> cells along the sword from the hand and <paramref name="v"/> across (+ the back).</summary>
        private static Vector2 Pt(float a, float v) => hand + dir * (a * k) + nrm * (v * f);

        /// <summary>A point on the blade: share <paramref name="u"/> of its length from the guard, <paramref name="v"/> across at size 1.</summary>
        private static Vector2 Bp(float u, float v) => Pt(T.GuardA + u * BL, v * g);

        private static float Wob(float u) => swell * 0.016f * Mathf.Sin(u * 26f + clock * 24f);

        private static float Pl(float[,] pts, float u)
        {
            if (u <= pts[0, 0]) return pts[0, 1];
            for (int i = 1; i < pts.GetLength(0); i++)
                if (u <= pts[i, 0])
                {
                    float u0 = pts[i - 1, 0], v0 = pts[i - 1, 1], u1 = pts[i, 0], v1 = pts[i, 1];
                    return v0 + (v1 - v0) * (u - u0) / (u1 - u0);
                }
            return pts[pts.GetLength(0) - 1, 1];
        }

        private static float Edge(float u) => Pl(EdgeV, u);
        private static float Back(float u) => Pl(BackV, u);
        private static float Top(float u) => Pl(TopV, u);

        /// <summary>The flesh's underside: along the edge near the hilt (the bulb wraps the blade), along the back further out.</summary>
        private static float Low(float u) =>
            Mathf.Min(Mathf.Lerp(Edge(u) + 0.03f, Back(u) - 0.012f, VfxMath.Smooth((u - 0.3f) / 0.35f)), u < 0.2f ? Pl(BulbV, u) : 9f);

        private static float SteelU(int i) => i / (float)SteelN;
        private static float FleshU(int i) => i / (float)FleshN * FleshEnd;

        /// <summary>Draws the sword and returns the hand's point on screen (where the arm reaches).</summary>
        internal static Vector2 Draw(in EgoMimicrySword w, float s, Vector2 sun, float strength)
        {
            hand = Screen(w.Hand);
            g = w.Size;
            f = w.Flip;
            BL = w.Length * g;
            swell = w.Swell;
            clock = s;
            float dx = w.Blade.x, dz = w.Blade.z + w.Blade.y * T.Lift;
            k = Mathf.Sqrt(dx * dx + dz * dz);
            if (k < 0.05f) { dx = 0f; dz = 0.05f; k = 0.05f; }
            dir = new Vector2(dx / k, dz / k);
            nrm = new Vector2(-dir.y, dir.x);
            float rot = -T.DegOf(dir), L = w.Altitude;

            // Shadow: the blade's line from the hand to the tip, cast along the sun.
            {
                float reach = T.GuardA + BL;
                var tip = new Vector3(w.Hand.x + w.Blade.x * reach, Mathf.Max(0f, w.Hand.y + w.Blade.y * reach), w.Hand.z + w.Blade.z * reach);
                Vector2 a = Shadow(w.Hand, sun), b = Shadow(tip, sun), m = Vector2.LerpUnclamped(a, b, 0.35f), n = T.Side(T.Unit(b - a));
                float wm = 0.13f * g * Mathf.Max(0.25f, Mathf.Abs(f));
                Sides(3, out Vector2[] A, out Vector2[] B);
                A[0] = a + n * 0.03f; A[1] = m + n * wm; A[2] = b;
                B[0] = a - n * 0.03f; B[1] = m - n * wm; B[2] = b;
                Strip(A, B, Fade(Outline, strength * 0.55f), solid, ShadowLayer);
            }
            // Grip: a black rod, a lit line, the red ring at the guard, the end cap; the hand on it.
            Bar(T.PommelA, T.GuardA, -0.024f, 0.024f, GripC, L - 0.004f);
            Bar(T.PommelA + 0.02f, T.GuardA - 0.03f, 0.006f, 0.016f, GripLit, L - 0.0038f);
            Bar(T.GuardA - 0.035f, T.GuardA + 0.005f, -0.036f, 0.036f, GripRing, L - 0.0036f);
            Disc(Pt(T.PommelA, 0f), L - 0.0035f, 0.038f * Mathf.Max(0.4f, k), 0.034f, rot, GripC);
            if (w.Fuse < 0.99f) Disc(Pt(0f, 0f), L - 0.003f, 0.055f, 0.05f, rot, w.Skin);

            // Outlines (the source's black lines), then steel, then flesh.
            SteelBand(0, -0.014f, 0.014f, false, Outline, L);
            FleshBand(0.014f, 0.5f, 0.014f, 1f, Outline, L + 0.0005f);
            SteelBand(0, 0f, 0f, false, Steel, L + 0.001f);
            SteelBand(2, 0.006f, 0.02f, true, Fade(SteelLit, 0.85f), L + 0.0015f);
            if (w.Blood > 0f)
            {
                Sides(9, out Vector2[] a, out Vector2[] b);
                for (int i = 0; i <= 8; i++)
                {
                    float u = 0.5f + i / 8f * 0.42f;
                    a[i] = Bp(u, Edge(u) + 0.004f);
                    b[i] = Bp(u, Mathf.Min(Back(u), Edge(u) + 0.06f) - 0.004f);
                }
                Strip(a, b, Fade(Blood, 0.95f * w.Blood), solid, L + 0.002f);
            }
            FleshBand(0f, 0.5f, 0f, 1f, Flesh, L + 0.003f);
            {
                Sides(FleshN + 1, out Vector2[] a, out Vector2[] b);
                for (int i = 0; i <= FleshN; i++)
                {
                    float u = FleshU(i);
                    a[i] = Bp(u, Low(u) - Wob(u) * 0.5f);
                    b[i] = Bp(u, Mathf.Lerp(Low(u), Top(u), 0.3f));
                }
                Strip(a, b, Fade(Fibre, 0.35f), solid, L + 0.0032f);
            }
            {
                // The lit ridge along the top of the muscle, 0.04 to 0.72 of the blade.
                int count = 0;
                for (int i = 0; i <= FleshN; i++)
                    if (FleshU(i) >= 0.04f && FleshU(i) <= 0.72f) count++;
                Sides(count, out Vector2[] a, out Vector2[] b);
                int j = 0;
                for (int i = 0; i <= FleshN; i++)
                {
                    float u = FleshU(i);
                    if (u < 0.04f || u > 0.72f) continue;
                    a[j] = Bp(u, Top(u) - 0.03f + Wob(u));
                    b[j++] = Bp(u, Top(u) - 0.01f + Wob(u));
                }
                Strip(a, b, Fade(FleshLit, 0.85f), solid, L + 0.0034f);
            }
            // Fibres along the muscle, and three strands where the flesh runs out along the back of the steel.
            for (int i = 0; i < 4; i++)
            {
                float fr = 0.22f + i * 0.17f, u0 = 0.03f + 0.03f * i, u1 = FleshEnd - 0.04f - 0.05f * i;
                Vector2[] pts = GokuGraphics.Points(11);
                for (int j = 0; j <= 10; j++)
                {
                    float u = Mathf.Lerp(u0, u1, j / 10f);
                    pts[j] = Bp(u, Mathf.Lerp(Low(u), Top(u), fr) + 0.006f * Mathf.Sin(u * 40f + i * 2f));
                }
                GokuGraphics.Line(pts, 0.012f * g, Fade(Fibre, 0.75f), solid, L + 0.0036f, Taper.Both);
            }
            for (int i = 0; i < 3; i++)
            {
                float u0 = FleshEnd - 0.1f + i * 0.02f, u1 = FleshEnd + 0.05f + 0.04f * i, um = (u0 + u1) / 2f;
                Vector2[] pts = GokuGraphics.Points(3);
                pts[0] = Bp(u0, Back(u0) + 0.03f - i * 0.01f);
                pts[1] = Bp(um, Back(um) + 0.018f - i * 0.006f);
                pts[2] = Bp(u1, Back(u1) + 0.004f);
                GokuGraphics.Line(pts, 0.022f * g, Flesh, solid, L + 0.0031f, Taper.End);
            }
            // Bone spikes: three on the back, two small ones under the flesh near the hilt.
            for (int i = 0; i < BackSpikes.GetLength(0); i++)
            {
                float u = BackSpikes[i, 0];
                Spike(u, Top(u) - 0.01f, BackSpikes[i, 1], BackSpikes[i, 2], 1f, w.Length, L);
            }
            for (int i = 0; i < BellySpikes.GetLength(0); i++)
            {
                float u = BellySpikes[i, 0];
                Spike(u, Low(u) + 0.01f, BellySpikes[i, 1], BellySpikes[i, 2], -1f, w.Length, L);
            }
            // Eyes: the big green one near the hilt, the small blue one mid-blade.
            Vector2 big = Eye(0.22f, 0.02f, 0.06f, 0.045f, IrisGreen, 0.024f, w.Open, w.EyePulse, rot, L);
            Eye(0.5f, 0.054f, 0.032f, 0.024f, IrisBlue, 0.013f, w.Open, w.EyePulse, rot, L);
            // The heal: a red glow sliding along the flesh into the big eye, then the eye glowing.
            if (w.Pulse > 0f) Sprite(Bp(w.PulseAt, 0.05f), 0.24f * g, 0.17f * g, Fade(HealRed, 0.6f * w.Pulse), glow, L + 0.0072f, rot);
            if (w.EyePulse > 0f) Sprite(big, 0.3f * g, 0.26f * g, Fade(HealRed, 0.55f * w.EyePulse), glow, L + 0.0073f);
            if (swell > 0f) Sprite(Bp(0.4f, 0.02f), BL * 1.1f, 0.55f * g, Fade(RedStreak, 0.25f * swell), glow, L + 0.0074f, rot);
            // Corroded: flesh grows over the hand and the grip and joins the bulb.
            if (w.Fuse > 0f)
            {
                float fu = w.Fuse;
                Vector2[] A = FuseA, B = FuseB;
                for (int i = 0; i <= 6; i++)
                {
                    float a = Mathf.Lerp(T.PommelA + (1f - fu) * 0.2f, T.GuardA + 0.03f, i / 6f);
                    float half = (0.05f + 0.035f * (i / 6f)) * fu + 0.01f * Mathf.Sin(i * 2f + s * 6f) * fu;
                    A[i] = Pt(a, -half);
                    B[i] = Pt(a, half);
                }
                Sides(7, out Vector2[] la, out Vector2[] lb);
                for (int i = 0; i <= 6; i++) { la[i] = A[i] + nrm * (-0.012f * f); lb[i] = B[i] + nrm * (0.012f * f); }
                Strip(la, lb, Outline, solid, L + 0.0076f);
                Sides(7, out la, out lb);
                for (int i = 0; i <= 6; i++) { la[i] = A[i]; lb[i] = B[i]; }
                Strip(la, lb, Meat, solid, L + 0.0077f);
                Sides(7, out la, out lb);
                for (int i = 0; i <= 6; i++) { la[i] = Vector2.LerpUnclamped(A[i], B[i], 0.55f); lb[i] = Vector2.LerpUnclamped(A[i], B[i], 0.8f); }
                Strip(la, lb, Fade(MeatLit, 0.7f), solid, L + 0.0078f);
            }
            return hand;
        }

        /// <summary>The corroded flesh's two sides over the grip, 7 points each.</summary>
        private static readonly Vector2[] FuseA = new Vector2[7], FuseB = new Vector2[7];

        /// <summary>A straight bar along the sword from <paramref name="a0"/> to <paramref name="a1"/>, from <paramref name="v0"/> to <paramref name="v1"/> across.</summary>
        private static void Bar(float a0, float a1, float v0, float v1, Color colour, float altitude)
        {
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = Pt(a0, v0); a[1] = Pt(a1, v0);
            b[0] = Pt(a0, v1); b[1] = Pt(a1, v1);
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>
        /// A band along the steel from point <paramref name="from"/>: from the edge plus <paramref name="lo"/> to
        /// the back plus <paramref name="hi"/>, or, with <paramref name="lit"/>, from the edge plus lo to the edge plus hi.
        /// </summary>
        private static void SteelBand(int from, float lo, float hi, bool lit, Color colour, float altitude)
        {
            Sides(SteelN + 1 - from, out Vector2[] a, out Vector2[] b);
            for (int i = from; i <= SteelN; i++)
            {
                float u = SteelU(i);
                a[i - from] = Bp(u, Edge(u) + lo);
                b[i - from] = Bp(u, lit ? Edge(u) + hi : Back(u) + hi);
            }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>The flesh from its underside (less <paramref name="lo"/>, wobbling by <paramref name="loWob"/>) to its top (plus <paramref name="hi"/>, wobbling by <paramref name="hiWob"/>).</summary>
        private static void FleshBand(float lo, float loWob, float hi, float hiWob, Color colour, float altitude)
        {
            Sides(FleshN + 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= FleshN; i++)
            {
                float u = FleshU(i), wob = Wob(u);
                a[i] = Bp(u, Low(u) - lo - wob * loWob);
                b[i] = Bp(u, Top(u) + hi + wob * hiWob);
            }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A bone spike rising <paramref name="len"/> cells (more while the blade swells) from <paramref name="baseV"/>, leaning toward the tip; <paramref name="sgn"/> -1 points it under the blade.</summary>
        private static void Spike(float u, float baseV, float len, float lean, float sgn, float blade, float L)
        {
            float du = 0.022f / blade, apexU = u + lean * len / blade, apexV = baseV + sgn * (len * (1f + 0.25f * swell));
            Tri(Bp(u - du * 1.6f, baseV), Bp(u + du * 1.6f, baseV), Bp(apexU, apexV + sgn * 0.016f), Outline, L + 0.005f);
            Tri(Bp(u - du, baseV), Bp(u + du, baseV), Bp(apexU, apexV), Bone, L + 0.0052f);
            Tri(Bp(u - du * 0.2f, baseV), Bp(u + du * 0.7f, baseV), Bp(apexU, apexV - sgn * 0.01f), Fade(BoneLit, 0.8f), L + 0.0054f);
        }

        private static void Tri(Vector2 a0, Vector2 a1, Vector2 apex, Color colour, float altitude)
        {
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = a0; a[1] = a1;
            b[0] = apex; b[1] = apex;
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>One eye on the blade, squashed by the wrist and the foreshortening; the pupil narrows as the eyes open and widens with the heal.</summary>
        private static Vector2 Eye(float u, float v, float rx, float rz, Color iris, float ir, float open, float eyePulse, float rot, float L)
        {
            Vector2 c = Bp(u, v);
            float sx = rx * g * Mathf.Max(0.3f, k), sz = rz * g * Mathf.Max(0.15f, Mathf.Abs(f)) * (1f + 0.3f * open);
            Disc(c, L + 0.006f, sx + 0.012f, sz + 0.012f, rot, Outline);
            Disc(c, L + 0.0062f, sx, sz, rot, Sclera);
            Disc(c, L + 0.0064f, ir * g * Mathf.Max(0.3f, k), ir * g * Mathf.Max(0.3f, Mathf.Abs(f)), rot, iris);
            float pr = ir * 0.45f * (1f - 0.45f * open + 0.5f * eyePulse);
            Disc(c, L + 0.0066f, pr * g * Mathf.Max(0.3f, k), pr * g, rot, Pupil);
            return c;
        }
    }
}
