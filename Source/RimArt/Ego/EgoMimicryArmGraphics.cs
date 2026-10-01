using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoMimicryGraphics;
using T = RimArt.EgoMimicryTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// The flesh taking the sword arm while corroded or overclocked (Limbus's Corroded Inquisitors), the port
    /// of ego-mimicry.js's arm(). Stage 1 is the hand and grip (EgoMimicrySwordGraphics draws it as the fuse);
    /// stage 2 the forearm, a muscle tube from the hand toward the shoulder with an eye opening on it; stage 3
    /// the whole arm swollen about as wide as the body (0.5 cells at mid-arm), a shoulder bulge, a mouth with
    /// two rows of white teeth on the upper arm, two long bone blades rising and curving out with their
    /// shadows, five tendrils hanging; stage 4 flesh up the neck and over the face, one round eye and a
    /// lipless grin (aiming north the back of the head: the eye but no teeth). Fractional stages grow each part.
    ///
    /// Drawn over the pawn as flat shapes placed on the south-facing stand-in for every facing (the Vergil pose
    /// trick); aiming north the arm draws under the pawn. The shoulder's across offset goes on the screen's x
    /// only, so facing east or west it sits on the upper chest instead of sliding down to mid-body (height and
    /// north share the screen's vertical axis). Neck and head are the average body's (lib/pawn.js).
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class EgoMimicryArmGraphics
    {
        private const int N = 12;
        /// <summary>The bone blades: where along the arm (share of its reach), how far out to the hand side, forward along the aim and up (cells).</summary>
        private static readonly float[,] Blades = { { 0.58f, 0.35f, 0.15f, 1.4f }, { 0.82f, 0.45f, 0.3f, 1.0f } };
        private static readonly Vector2[] Arm = new Vector2[N + 1], Bones = new Vector2[9], BoneShadow = new Vector2[9], Neck = new Vector2[4];

        // The frame's arm: the hand, the elbow control point and the shoulder of a quadratic curve, its reach (0.5 at
        // stage 2, 1 at stage 3), the stage fractions f2, f3 and the clock.
        private static Vector2 H, E, S;
        private static float reach, f2, f3, clock;

        private static Vector2 Bez(float t) => (1f - t) * (1f - t) * H + 2f * (1f - t) * t * E + t * t * S;

        /// <summary>The arm's half-width at share <paramref name="u"/> of its drawn length: thin forearm, swollen mid-arm at stage 3, breathing.</summary>
        private static float Width(float u)
        {
            float t = u * reach;
            return (0.065f + 0.025f * (1f - t)) * (0.4f + 0.6f * Mathf.Clamp01(f2 * 1.5f)) + 0.2f * f3 * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.05f))
                + 0.015f * Mathf.Sin(t * 18f + clock * 5f) * f3;
        }

        /// <summary>
        /// The arm at <paramref name="stage"/> (nothing below 1), from the hand on screen <paramref name="hand"/> to the
        /// shoulder of the wielder at <paramref name="pos"/>, aiming <paramref name="d"/> with the hand side <paramref name="hs"/>.
        /// </summary>
        internal static void Draw(Vector2 pos, Vector2 hand, Vector2 d, Vector2 hs, float stage, float s, Vector2 sun, float strength)
        {
            f2 = Mathf.Clamp01(stage - 1f);
            f3 = Mathf.Clamp01(stage - 2f);
            float f4 = Mathf.Clamp01(stage - 3f);
            if (f2 <= 0f) return;
            clock = s;
            bool north = d.y > 0.5f;
            float L = north ? PawnLayer - 0.02f : PawnLayer + 0.03f;
            H = hand;
            S = new Vector2(pos.x + hs.x * 0.22f, pos.y + 0.15f + hs.y * 0.06f);
            E = new Vector2((S.x + H.x) / 2f + hs.x * 0.08f, (S.y + H.y) / 2f + hs.y * 0.04f - 0.05f);
            reach = 0.5f * f2 + 0.5f * f3;
            for (int i = 0; i <= N; i++) Arm[i] = Bez(i / (float)N * reach);

            // The shoulder bulge first, so the arm runs into it.
            if (f3 > 0f)
            {
                var c = new Vector2(S.x + hs.x * 0.06f, S.y + 0.02f);
                Disc(c, L, 0.26f * f3 + 0.016f, 0.22f * f3 + 0.016f, 0f, Outline);
                Disc(c, L + 0.0002f, 0.26f * f3, 0.22f * f3, 0f, Meat);
                Disc(new Vector2(c.x - 0.05f, c.y + 0.06f), L + 0.0004f, 0.13f * f3, 0.08f * f3, 0f, Fade(MeatLit, 0.6f));
            }
            float[] w = Widths;
            for (int i = 0; i <= N; i++) w[i] = Width(i / (float)N) + 0.014f;
            Tube(Arm, N + 1, Outline, L + 0.001f);
            for (int i = 0; i <= N; i++) w[i] = Width(i / (float)N);
            Tube(Arm, N + 1, Meat, L + 0.0012f);
            Tube(Arm, N + 1, Fade(MeatLit, 0.6f), L + 0.0014f, 0.25f, 0.7f);
            Vector2 across = T.Side(T.Unit(S - H));
            for (int k = 0; k < 2; k++)
            {
                Vector2[] fibre = GokuGraphics.Points(N + 1);
                for (int j = 0; j <= N; j++) fibre[j] = Arm[j] + across * ((k == 1 ? 0.4f : -0.35f) * Width(j / (float)N));
                GokuGraphics.Line(fibre, 0.012f, Fade(MeatDark, 0.8f), solid, L + 0.0016f, Taper.Both);
            }
            // The forearm eye.
            if (f2 > 0.2f)
            {
                Vector2 c = Bez(Mathf.Min(0.22f, reach * 0.7f));
                float o = Smooth((f2 - 0.2f) / 0.5f), r = 0.07f * (1f + 0.3f * f3);
                Disc(c, L + 0.002f, r + 0.012f, r * 0.8f * o + 0.012f, 0f, Outline);
                Disc(c, L + 0.0022f, r, r * 0.8f * o, 0f, Sclera);
                Disc(c, L + 0.0024f, r * 0.42f, r * 0.42f * o, 0f, IrisGreen);
                Disc(c, L + 0.0026f, r * 0.18f, r * 0.18f * o, 0f, Pupil);
            }
            if (f3 > 0f) Swollen(pos, d, hs, L, sun, strength);
            if (f4 > 0f) Face(pos, hs, f4, north);
        }

        /// <summary>Stage 3: the mouth on the outer side of the upper arm, the tendrils and the two bone blades.</summary>
        private static void Swollen(Vector2 pos, Vector2 d, Vector2 hs, float L, Vector2 sun, float strength)
        {
            float t0 = 0.5f * reach;
            Vector2 c = Bez(t0) + new Vector2(hs.x * 0.1f, hs.y * 0.05f), tan = T.Unit(Bez(t0 + 0.05f) - Bez(t0 - 0.05f)), nn = T.Side(tan);
            float mw = 0.17f * f3, mh = 0.07f * f3 * (0.7f + 0.3f * Mathf.Sin(clock * 3f)), rot = -T.DegOf(tan);
            Disc(c, L + 0.003f, mw + 0.012f, mh + 0.012f, rot, Outline);
            Disc(c, L + 0.0032f, mw, mh, rot, Mouth);
            for (int i = 0; i < 7; i++)
                for (int k = 0; k < 2; k++)
                {
                    float sg = k == 0 ? 1f : -1f, u = (i + 0.5f) / 7f * 2f - 1f, e = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
                    Vector2 root = c + tan * (u * mw) + nn * (sg * mh * e), tip = root + nn * (-sg * mh * 0.7f * e);
                    Tooth(root - tan * 0.018f, root + tan * 0.018f, tip, L + 0.0034f);
                }
            // Tendrils hanging from the arm.
            for (int i = 0; i < 5; i++)
            {
                Vector2 q = Bez((0.25f + i * 0.14f) * reach);
                float l = (0.2f + 0.15f * Rand(i + 300)) * f3, sw = 0.04f * Mathf.Sin(clock * 2.6f + i * 1.7f);
                Vector2[] pts = GokuGraphics.Points(3);
                pts[0] = q;
                pts[1] = new Vector2(q.x + sw * 0.5f, q.y - l * 0.5f);
                pts[2] = new Vector2(q.x + sw, q.y - l);
                GokuGraphics.Line(pts, 0.032f, MeatDark, solid, L - 0.0005f, Taper.End);
            }
            // Two bone blades rising from the arm and curving out (Limbus's scythe), with their shadows. A blade's root
            // is 0.85 cells up where the arm is drawn, so it stands on the floor below it.
            float[] w = Widths;
            for (int i = 0; i < 2; i++)
            {
                Vector2 q = Bez(Blades[i, 0] * reach);
                float outward = Blades[i, 1], fwd = Blades[i, 2], rise = Blades[i, 3];
                var root = new Vector3(q.x, 0.85f, q.y - PawnBody.Ground - 0.85f * T.Lift);
                for (int j = 0; j <= 8; j++)
                {
                    float u = j / 8f, side = outward * Mathf.Sin(u * 1.4f) * f3, ahead = fwd * u * u * f3;
                    var p = new Vector3(root.x + hs.x * side + d.x * ahead, root.y + rise * u * f3, root.z + hs.y * side + d.y * ahead);
                    Bones[j] = Screen(p);
                    BoneShadow[j] = Shadow(p, sun);
                }
                Vector2[] sh = GokuGraphics.Points(9);
                for (int j = 0; j <= 8; j++) sh[j] = BoneShadow[j];
                GokuGraphics.Line(sh, 0.1f, Fade(Outline, strength * 0.5f), solid, ShadowLayer, Taper.End);
                // Blade 1's outline sits a hair over blade 0's lit stripe, which shares its altitude in the sketch.
                float alt = L + 0.004f + i * 0.00041f;
                for (int j = 0; j <= 8; j++) w[j] = 0.07f * (1f - j / 8f) + 0.006f + 0.014f;
                Tube(Bones, 9, Outline, alt);
                for (int j = 0; j <= 8; j++) w[j] = 0.07f * (1f - j / 8f) + 0.006f;
                Tube(Bones, 9, Bone, alt + 0.0002f);
                Tube(Bones, 9, Fade(BoneLit, 0.8f), alt + 0.0004f, 0.1f, 0.7f);
            }
        }

        /// <summary>Stage 4: flesh up the neck and over the head, one round eye, and (not from behind) a grin of teeth.</summary>
        private static void Face(Vector2 pos, Vector2 hs, float f4, bool north)
        {
            Vector2 neck = new Vector2(pos.x, pos.y + PawnBody.Neck), head = new Vector2(pos.x, pos.y + PawnBody.Head);
            float FL = PawnLayer + 0.045f;
            Neck[0] = S;
            Neck[1] = Vector2.LerpUnclamped(S, neck, 0.6f);
            Neck[2] = neck;
            Neck[3] = head;
            float[] w = Widths;
            for (int i = 0; i < 4; i++) w[i] = 0.07f * f4 + 0.014f;
            Tube(Neck, 4, Outline, FL);
            for (int i = 0; i < 4; i++) w[i] = 0.07f * f4;
            Tube(Neck, 4, Meat, FL + 0.0002f);
            Vector2 c = head + hs * (0.05f * (1f - f4));
            float rx = 0.2f * f4, rz = 0.2f * f4;
            Disc(c, FL + 0.0004f, rx + 0.014f, rz + 0.014f, 0f, Outline);
            Disc(c, FL + 0.0006f, rx, rz, 0f, Meat);
            Disc(new Vector2(c.x - 0.05f, c.y + 0.07f), FL + 0.0008f, rx * 0.45f, rz * 0.3f, 0f, Fade(MeatLit, 0.6f));
            for (int i = 0; i < 3; i++)
            {
                Vector2[] pts = GokuGraphics.Points(3);
                pts[0] = c + new Vector2(-0.15f, -0.05f + i * 0.06f) * f4;
                pts[1] = c + new Vector2(0f, 0.02f + i * 0.05f) * f4;
                pts[2] = c + new Vector2(0.15f, -0.04f + i * 0.06f) * f4;
                GokuGraphics.Line(pts, 0.012f, Fade(MeatDark, 0.7f), solid, FL + 0.0009f, Taper.Both);
            }
            if (f4 <= 0.4f) return;
            float o = Smooth((f4 - 0.4f) / 0.4f), r = 0.065f;
            Vector2 e = c + hs * 0.05f + new Vector2(0f, 0.03f);
            Disc(e, FL + 0.001f, r + 0.012f, r * o + 0.012f, 0f, Outline);
            Disc(e, FL + 0.0012f, r, r * o, 0f, Sclera);
            Disc(e, FL + 0.0014f, r * 0.3f, r * 0.3f * o, 0f, Pupil);
            if (north) return;
            var m = new Vector2(c.x, c.y - 0.09f);
            float mw = 0.12f * o, mh = 0.04f * o;
            Disc(m, FL + 0.0016f, mw + 0.01f, mh + 0.01f, 0f, Outline);
            Disc(m, FL + 0.0017f, mw, mh, 0f, Mouth);
            for (int i = 0; i < 7; i++)
                for (int k = 0; k < 2; k++)
                {
                    float sg = k == 0 ? 1f : -1f, u = (i + 0.5f) / 7f * 2f - 1f, ee = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
                    float bx = m.x + u * mw, bz = m.y + sg * mh * ee;
                    Tooth(new Vector2(bx - 0.01f, bz), new Vector2(bx + 0.01f, bz), new Vector2(bx, bz - sg * mh * 0.75f * ee), FL + 0.0018f);
                }
        }

        private static void Tooth(Vector2 a0, Vector2 a1, Vector2 tip, float altitude)
        {
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = a0; a[1] = a1;
            b[0] = tip; b[1] = tip;
            Strip(a, b, Teeth, solid, altitude);
        }
    }
}
