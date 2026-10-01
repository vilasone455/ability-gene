using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoMagicBulletGraphics;
using T = RimArt.EgoMagicBulletTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Magic Bullet's beam, left behind the bullet at chest height. It is drawn in 2-cell pieces that each fade
    /// (e^(-t / 0.6 s)) from when the bullet passed them: a thin sharp core, a body, a faint wide halo with long
    /// streaks drifting along it at 6 cells/s, and one to three long jagged bolts that run the beam's length,
    /// bending every 0.5 cells and swinging up to 0.4, 0.55 or 0.7 cells to either side (three bends in ten a
    /// full swing), with a short branch off most 4-cell pieces. The bolts are re-drawn every 1/16 s so they
    /// flicker, and fade faster than the beam (the fade to the power 2.5). The light sits at the first circle,
    /// not the muzzle: a glow and ten short rays there while the beam is up. Tier 0 (shots 1 to 3): thin blue,
    /// one bolt. Tier 1 (4 to 6): wider, blue-violet, two. Tier 2 (the seventh): a cyan-white core in a wide
    /// pale halo, three.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class EgoMagicBulletBeamGraphics
    {
        private static readonly float[] Amp = { 0.4f, 0.55f, 0.7f }, HaloW = { 2.4f, 3.0f, 4.5f }, BodyW = { 0.8f, 1.1f, 1.6f },
            CoreW = { 0.35f, 0.4f, 0.5f }, GateSize = { 0.7f, 1.0f, 1.7f };

        // The beam being drawn, as locals for Bend.
        private static Vector2 lineStart, lineDir, lineSide;
        private static float lineFlown, bendAmp;
        private static int boltIndex, boltFrame;

        /// <summary>
        /// The beam from <paramref name="start"/> (the muzzle's ground point) along <paramref name="d"/>, as far as the
        /// bullet has <paramref name="flown"/>, <paramref name="age"/> s after the shot. <paramref name="gate"/> is the
        /// first circle's centre, where the light sits.
        /// </summary>
        internal static void Draw(Vector2 start, Vector2 d, float flown, float age, float width, float life, float s, int tier, Vector2 gate)
        {
            Vector2 side = new Vector2(-d.y, d.x);
            int frame = Mathf.FloorToInt(s * 16f), bolts = 1 + tier;
            Color body = tier == 0 ? Beam : tier == 1 ? BeamViolet : Cyan, halo = tier == 0 ? BeamEdge : tier == 1 ? BeamViolet : Halo;
            float haloW = HaloW[tier], bodyW = BodyW[tier], coreW = CoreW[tier];
            Color core = tier > 0 ? White : CircleBright;
            for (float d0 = 0f; d0 < flown; d0 += T.Chunk)
            {
                float d1 = Mathf.Min(flown, d0 + T.Chunk), fade = FadeAt((d0 + d1) / 2f, age, life);
                if (fade < 0.02f) continue;
                Vector2[] pts = GokuGraphics.Points(2);
                pts[0] = AtChest(start + d * d0);
                pts[1] = AtChest(start + d * d1);
                GokuGraphics.Line(pts, width * haloW * (0.6f + 0.4f * fade), Fade(halo, 0.22f * fade), whiteGlow, Overhead + 0.079f, Taper.None);
                GokuGraphics.Line(pts, width * bodyW, Fade(body, 0.7f * fade), whiteGlow, Overhead + 0.081f, Taper.None);
                GokuGraphics.Line(pts, width * coreW, Fade(core, 0.9f * fade), whiteGlow, Overhead + 0.082f, Taper.None);
            }
            // Streaks drifting along the halo at 6 cells/s.
            for (int j = 0; j < 4 + tier * 3; j++)
            {
                float len = 1f + Rand(j + 700) * 2.5f, at = (Rand(j + 710) * flown + age * 6f) % Mathf.Max(1f, flown), off = (Rand(j + 720) - 0.5f) * width * haloW;
                if (at + len > flown) continue;
                Vector2 a0 = AtChest(start + d * at) + side * off;
                Streak(a0, a0 + d * len, width * 0.5f, Fade(halo, 0.35f * FadeAt(at, age, life)), whiteGlow, Overhead + 0.080f, 3);
            }
            // The long bolts, in 4-cell pieces so each fades with its part of the beam.
            lineStart = start;
            lineDir = d;
            lineSide = side;
            lineFlown = flown;
            bendAmp = Amp[tier];
            boltFrame = frame;
            int steps = Mathf.FloorToInt(flown / T.BoltStep);
            for (int b = 0; b < bolts; b++)
            {
                if (Rand(b * 3 + frame) < 0.15f) continue;   // a bolt drops out now and then
                boltIndex = b;
                for (int n0 = 0, piece = 0; n0 < steps; n0 += 8, piece++)
                {
                    int n1 = Mathf.Min(steps, n0 + 8);
                    float f = Mathf.Pow(FadeAt((n0 + n1) / 2f * T.BoltStep, age, life), 2.5f);
                    if (f < 0.2f || n1 - n0 < 2) continue;
                    Vector2[] pts = GokuGraphics.Points(n1 - n0 + 1);
                    for (int n = n0; n <= n1; n++) pts[n - n0] = Bend(n);
                    GokuGraphics.Line(pts, 0.045f + tier * 0.015f, Fade(White, 0.95f * f), whiteGlow, Overhead + 0.084f, Taper.None);
                    GokuGraphics.Line(pts, 0.14f + tier * 0.05f, Fade(Violet, 0.45f * f), whiteGlow, Overhead + 0.083f, Taper.None);
                    if (Rand(piece * 7 + b + frame) > 0.4f)
                    {
                        // A branch off one bend of this piece, to one side or the other.
                        int n = n0 + 1 + Mathf.FloorToInt(Rand(piece * 11 + b * 5 + frame) * (n1 - n0 - 1));
                        float t = Mathf.Atan2(side.y, side.x) + (Rand(n + frame) > 0.5f ? 0f : Mathf.PI) + (Rand(n * 3 + frame) - 0.5f) * 1.2f;
                        Zigzag(Bend(n), t, (0.4f + 0.6f * Rand(n * 5 + frame)) * (1f + tier * 0.4f), 4, n * 17 + frame, 0.04f,
                            Fade(White, 0.9f * f), Fade(Violet, 0.4f * f), Overhead + 0.083f);
                    }
                }
            }
            // The light at the circle and its rays, while the beam is up.
            float up = 1f - Smooth((age - flown / T.Speed - 0.15f) / (life * 0.8f)), size = GateSize[tier];
            if (up <= 0f) return;
            float pulse = 0.92f + 0.08f * Mathf.Sin(s * 30f);
            Sprite(gate, 2.0f * size * up * pulse, 1.8f * size * up * pulse, Fade(halo, 0.45f * up), glow, Overhead + 0.085f);
            Sprite(gate, 1.0f * size * up, 0.9f * size * up, Fade(body, 0.85f * up), glow, Overhead + 0.086f);
            Sprite(gate, 0.4f * size * up, 0.36f * size * up, Fade(White, up), glow, Overhead + 0.087f);
            for (int i = 0; i < 10; i++)
            {
                float t = i / 10f * Tau + Rand(i + frame) * 0.6f, r0 = size * (0.3f + 0.2f * Rand(i * 3 + frame)), r1 = r0 + size * (0.3f + 0.5f * Rand(i * 5 + frame));
                Streak(new Vector2(gate.x + Mathf.Cos(t) * r0, gate.y + Mathf.Sin(t) * r0 * 0.8f), new Vector2(gate.x + Mathf.Cos(t) * r1, gate.y + Mathf.Sin(t) * r1 * 0.8f),
                    0.035f, Fade(White, 0.8f * up), whiteGlow, Overhead + 0.088f, 3);
            }
        }

        /// <summary>How much of a beam piece <paramref name="along"/> cells from the muzzle is left: e^(-t / life), t from when the bullet passed it.</summary>
        private static float FadeAt(float along, float age, float life) => Mathf.Exp(-Mathf.Max(0f, age - along / T.Speed) / life);

        /// <summary>
        /// Bend <paramref name="n"/> of the current bolt this 1/16 s, indexed by position so the pieces join; bend 0
        /// sits on the line at the muzzle. Most bends swing 30 % of the amplitude, three in ten the full amount.
        /// </summary>
        private static Vector2 Bend(int n)
        {
            int b = boltIndex, frame = boltFrame;
            bool big = Rand(n * 29 + b * 53 + frame * 3) > 0.7f;
            float off = n == 0 ? 0f : bendAmp * (Rand(n * 13 + b * 101 + frame * 7) - 0.5f) * 2f * (big ? 1f : 0.3f);
            return AtChest(lineStart + lineDir * Mathf.Min(lineFlown, n * T.BoltStep)) + lineSide * off;
        }

        /// <summary>
        /// A zigzag bolt from <paramref name="from"/>: <paramref name="n"/> bends reaching <paramref name="reach"/>
        /// cells toward <paramref name="t"/> (radians), each bend off the straight line by up to 0.225 x reach
        /// (the last by 30 % of that), jittered by <paramref name="seed"/>. A core tapering to the far end over a glow
        /// 2.8 times as wide.
        /// </summary>
        internal static void Zigzag(Vector2 from, float t, float reach, int n, int seed, float width, Color colour, Color glowColour, float altitude)
        {
            Vector2[] pts = GokuGraphics.Points(n + 1);
            float dx = Mathf.Cos(t), dz = Mathf.Sin(t);
            pts[0] = from;
            for (int k = 1; k <= n; k++)
            {
                float u = k / (float)n, off = (Rand(seed + k * 7) - 0.5f) * reach * 0.45f * (k < n ? 1f : 0.3f);
                pts[k] = new Vector2(from.x + dx * reach * u - dz * off, from.y + dz * reach * u + dx * off);
            }
            GokuGraphics.Line(pts, width, colour, whiteGlow, altitude + 0.001f, Taper.End);
            GokuGraphics.Line(pts, width * 2.8f, glowColour, whiteGlow, altitude, Taper.End);
        }
    }
}
