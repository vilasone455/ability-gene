using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>One piece of the figure for flakes and chunks: a line from A to B in the figure's (u, h), W cells wide.</summary>
    internal struct BlackGhostSeg
    {
        internal Vector2 A, B;
        internal float W;
    }

    /// <summary>
    /// The pieces of the lab's lib/ajin.js and lib/six-paths-impact.js that the Black Ghost and Tear pictures use: the
    /// kit's colours, the lab's band, trail and taper strips, the six flake shards, black matter flaking off the figure,
    /// the line of flakes on a rising or falling edge, the ooze strands, and a drawn limb with an outline. Kept here as
    /// private copies so this port does not clash with the Ajin helpers ported for the other Satō sketches.
    /// Everything is a function of time. The only state is the scratch point arrays (handed out again after each
    /// <see cref="Reset"/>) and the running altitude <see cref="Alt"/>.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class BlackGhostMatter
    {
        internal const float TAU = Mathf.PI * 2f;
        /// <summary>The ghost stands like a pawn sprite: its height is drawn at 0.75 per cell (lib/ajin.js Stand).</summary>
        internal const float Stand = 0.75f;

        // lib/ajin-ghost-v2.js: the figure's ink, its bandages, the mouth.
        internal static readonly Color InkBody = new Color(0.10f, 0.10f, 0.12f), InkEdge = new Color(0.02f, 0.02f, 0.025f), InkLit = new Color(0.21f, 0.21f, 0.24f);
        internal static readonly Color Bandage = new Color(0.52f, 0.52f, 0.56f), LooseBandage = new Color(0.33f, 0.33f, 0.37f);
        internal static readonly Color Maw = new Color(0.30f, 0.05f, 0.06f), TongueRed = new Color(0.62f, 0.13f, 0.15f),
            Teeth = new Color(0.90f, 0.89f, 0.84f), Spit = new Color(0.86f, 0.88f, 0.92f);
        // lib/ajin.js: flakes, people, blood and the claw streaks; the torn piece's outline.
        internal static readonly Color Flake = new Color(0.045f, 0.045f, 0.055f);
        internal static readonly Color Skin = new Color(0.83f, 0.70f, 0.54f), Shirt = new Color(0.86f, 0.86f, 0.83f), Cap = new Color(0.30f, 0.33f, 0.33f);
        internal static readonly Color Enemy = new Color(0.55f, 0.38f, 0.27f), Blood = new Color(0.45f, 0.05f, 0.05f), Slash = new Color(0.90f, 0.90f, 0.95f);
        internal static readonly Color Outline = new Color(0.10f, 0.08f, 0.07f);
        /// <summary>The sketches' Body (lib/six-paths-solid.js), used for shadows.</summary>
        internal static readonly Color ShadowBody = new Color(0.035f, 0.028f, 0.050f);

        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        internal static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly Mesh[] shards = MakeShards();

        // ---- the running altitude ----

        /// <summary>
        /// The lab sorts equal altitudes by call order; Unity does not. Draws the sketch makes at one altitude (the whole
        /// figure at the pawn layer) take the next step each, so they stack in the sketch's order.
        /// </summary>
        internal const float Step = 0.00003f;
        internal static float Alt;
        internal static float Next() => Alt += Step;

        // ---- scratch points ----

        private const int MostPoints = 16;
        private static readonly List<Vector2[]>[] scratch = new List<Vector2[]>[MostPoints + 1];
        private static readonly int[] taken = new int[MostPoints + 1];

        /// <summary>Call at the start of every public draw: every scratch array handed out before is free again.</summary>
        internal static void Reset()
        {
            for (int i = 0; i < taken.Length; i++) taken[i] = 0;
        }

        /// <summary>An array of <paramref name="n"/> points, free until the next <see cref="Reset"/>.</summary>
        internal static Vector2[] Buf(int n)
        {
            List<Vector2[]> made = scratch[n] ?? (scratch[n] = new List<Vector2[]>());
            if (taken[n] == made.Count) made.Add(new Vector2[n]);
            return made[taken[n]++];
        }

        // ---- the lab's hash and rounding ----

        /// <summary>The lab's rand(x) for any argument (the sketches hash i * 3.1 and the like), in double as it is there.</summary>
        internal static double Rd(double x)
        {
            double n = Math.Sin(x * 127.1 + 17) * 43758.5453;
            return n - Math.Floor(n);
        }

        internal static float R(double x) => (float)Rd(x);

        /// <summary>JavaScript's Math.round: halves go up, not to even.</summary>
        internal static int Round(float x) => Mathf.FloorToInt(x + 0.5f);

        internal static float Bump(float x) => x > 0f && x < 1f ? Mathf.Sin(x * Mathf.PI) : 0f;

        // ---- strips ----

        /// <summary>The lab's band(): a strip between two lines of screen points of the same length.</summary>
        internal static void Band(Vector2[] a, Vector2[] b, Color colour, float alt)
        {
            if (colour.a <= 0.001f) return;
            Sides(a.Length, out Vector2[] sa, out Vector2[] sb);
            for (int i = 0; i < a.Length; i++) { sa[i] = a[i]; sb[i] = b[i]; }
            Strip(sa, sb, colour, solid, alt);
        }

        /// <summary>The lab's trail(): a strip along the points, widest in the middle (sin), nothing at the ends.</summary>
        internal static void Trail(Vector2[] pts, float width, Color colour, float alt)
        {
            if (colour.a <= 0.001f) return;
            int n = pts.Length;
            Sides(n, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)];
                float len = d.magnitude;
                if (len == 0f) len = 1f;
                float w = Mathf.Sin(i / (float)(n - 1) * Mathf.PI) * width / 2f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, solid, alt);
        }

        /// <summary>lib/ajin-ghost-v2.js taper(): a strip through the points whose width runs from w0 to w1.</summary>
        internal static void Taper(Vector2[] pts, float w0, float w1, Color colour, float alt)
        {
            if (colour.a <= 0.001f) return;
            int n = pts.Length - 1;
            Sides(pts.Length, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                Vector2 d = pts[Mathf.Min(n, i + 1)] - pts[Mathf.Max(0, i - 1)];
                float len = d.magnitude;
                if (len == 0f) len = 1f;
                float w = Mathf.Lerp(w0, w1, i / (float)n) / 2f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, solid, alt);
        }

        /// <summary>
        /// lib/ajin.js limbBand(): a limb through screen points with a width per point. <paramref name="lit"/> draws only
        /// the east half, from <paramref name="from"/> to <paramref name="to"/> of the half width. False when two points
        /// coincide (nothing drawn).
        /// </summary>
        internal static bool LimbBand(Vector2[] P, float[] widths, Color colour, float alt, float grow = 0f, bool lit = false, float from = 0.15f, float to = 0.92f)
        {
            int n = P.Length;
            Vector2[] a = Buf(n), b = Buf(n);
            float sign = 1f;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = P[Mathf.Min(n - 1, i + 1)] - P[Mathf.Max(0, i - 1)];
                float len = d.magnitude;
                if (len < 1e-5f) return false;
                var normal = new Vector2(-d.y / len, d.x / len);
                float w = (widths[i] + grow) / 2f;
                if (lit)
                {
                    if (i == 0) sign = normal.x >= 0f ? 1f : -1f;
                    a[i] = P[i] + normal * (sign * w * to);
                    b[i] = P[i] + normal * (sign * w * from);
                }
                else
                {
                    a[i] = P[i] + normal * w;
                    b[i] = P[i] - normal * w;
                }
            }
            Band(a, b, colour, alt);
            return true;
        }

        /// <summary>lib/ajin.js polyAt(): the point at fractional index f along a line of points.</summary>
        internal static Vector2 PolyAt(Vector2[] P, float f)
        {
            int i = Mathf.Min(P.Length - 2, Mathf.Max(0, Mathf.FloorToInt(f)));
            return Vector2.Lerp(P[i], P[i + 1], f - i);
        }

        internal static void Disc(Vector2 at, float alt, float rx, float rz, float rot, Color colour) =>
            DrawMesh(disc, at, alt, rx, rz, rot, colour, solid);

        /// <summary>lib/ajin.js bar(): a straight bar from a to b, w wide.</summary>
        internal static void Bar(Vector2 a, Vector2 b, float w, Color colour, float alt)
        {
            Vector2 d = b - a;
            float l = d.magnitude;
            if (l == 0f) l = 1f;
            var n = new Vector2(-d.y / l * w / 2f, d.x / l * w / 2f);
            Vector2[] p = Buf(2), q = Buf(2);
            p[0] = a + n; p[1] = b + n;
            q[0] = a - n; q[1] = b - n;
            Band(p, q, colour, alt);
        }

        /// <summary>lib/ajin.js limbSeg(): a limb from a (sleeve or trouser colour) to b (hand or boot), with a dark outline.</summary>
        internal static void LimbSeg(Vector2 a, Vector2 b, float w, Color col, Color tipCol, float alt, float alpha = 1f)
        {
            Vector2 m = Vector2.Lerp(a, b, 0.62f);
            Bar(a, b, w + 0.03f, Fade(Outline, alpha), alt);
            Bar(a, m, w, Fade(col, alpha), alt + Step);
            Bar(m, b, w * 0.9f, Fade(tipCol, alpha), alt + 2f * Step);
        }

        // ---- flakes ----

        /// <summary>
        /// lib/ajin.js shards: six irregular triangles and quads for flakes, built once. The lab's triangles run
        /// counter-clockwise seen from above; here they are reversed so they survive backface culling.
        /// </summary>
        private static Mesh[] MakeShards()
        {
            var made = new Mesh[6];
            for (int k = 0; k < 6; k++)
            {
                int n = 3 + k % 2;
                var v = new Vector3[n];
                float squash = 0.5f + R(k + 40) * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * TAU + R(k * 11 + i) * 0.9f, r = 0.55f + R(k * 7 + i + 3) * 0.45f;
                    v[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * squash);
                }
                var mesh = new Mesh { name = "RimArt black ghost shard " + k, vertices = v };
                mesh.triangles = n == 3 ? new[] { 0, 2, 1 } : new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                made[k] = mesh;
            }
            return made;
        }

        internal static void Shard(int i, Vector2 at, float alt, float size, float rot, Color colour) =>
            DrawMesh(shards[i % 6], at, alt, size, size, rot, colour, solid);

        /// <summary>
        /// lib/ajin.js flakes(): black matter coming off the figure's edges and drifting up, hard flakes and every third a
        /// soft smoke puff. <paramref name="segs"/> are in the figure's (u, h); heights outside [lo, hi] are skipped. Each
        /// flake respawns every life seconds at a new place picked by hash.
        /// </summary>
        internal static void Flakes(Vector2 feet, List<BlackGhostSeg> segs, float t, float amount, float lo, float hi, float mirror, float layer, float alpha = 1f)
        {
            if (segs.Count == 0) return;
            int count = Round(78f * amount);
            for (int i = 0; i < count; i++)
            {
                double life = 0.8 + Rd(i * 3.1) * 0.7, ph = t / life + Rd(i * 7.3);
                int cyc = (int)Math.Floor(ph), sd = i * 131 + cyc * 17;
                float u = (float)(ph - cyc);
                BlackGhostSeg sg = segs[(int)Math.Floor(Rd(sd) * segs.Count) % segs.Count];
                float tt = R(sd + 1), side = R(sd + 2) < 0.5f ? -1f : 1f;
                float h0 = Mathf.Lerp(sg.A.y, sg.B.y, tt);
                if (h0 < lo || h0 > hi) continue;
                float du = sg.B.x - sg.A.x, dh = sg.B.y - sg.A.y, L = Mathf.Sqrt(du * du + dh * dh);
                if (L == 0f) L = 1f;
                float u0 = Mathf.Lerp(sg.A.x, sg.B.x, tt) + (-dh / L) * side * sg.W * 0.5f;
                bool smoke = i % 3 == 0;
                float driftU = (-dh / L) * side * 0.14f * u + (R(sd + 3) - 0.5f) * 0.35f * u + 0.12f * u;
                float driftH = (0.45f + R(sd + 4) * 0.5f) * u * (smoke ? 1.3f : 1f);
                var at = new Vector2(feet.x + (u0 + driftU) * mirror, feet.y + (h0 + driftH) * Stand);
                if (smoke)
                {
                    float s = (0.16f + 0.14f * R(sd + 7)) * (1f + u * 0.8f);
                    Sprite(at, s, s * 0.8f, Fade(Flake, Mathf.Sin(Mathf.Min(1f, u * 1.4f) * Mathf.PI) * 0.42f * alpha), puff, layer - 0.002f);
                    continue;
                }
                float size = (0.045f + 0.06f * R(sd + 5)) * (1f - u * 0.5f);
                Shard(i, at, layer + i * 0.0002f, size, R(sd + 6) * 360f + u * 260f, Fade(Flake, (1f - u) * 0.95f * alpha));
            }
        }

        /// <summary>lib/ajin.js edgeFlakes(): a line of flakes on the level edge at height h (forming or dissolving), width cells wide, thrown up.</summary>
        internal static void EdgeFlakes(Vector2 feet, float h, float width, float t, float amount, float rise, float layer, float alpha = 1f)
        {
            int count = Round(26f * amount);
            for (int i = 0; i < count; i++)
            {
                double life = 0.45 + Rd(i * 5.7 + 1) * 0.35, ph = t / life + Rd(i * 2.9 + 4);
                int cyc = (int)Math.Floor(ph), sd = i * 97 + cyc * 29 + 5;
                float u = (float)(ph - cyc);
                float u0 = (R(sd) - 0.5f) * width, lift = rise * (0.5f + R(sd + 1) * 0.7f) * u, side = (R(sd + 2) - 0.5f) * 0.5f * u;
                var at = new Vector2(feet.x + u0 + side, feet.y + (h + lift) * Stand);
                float size = (0.05f + 0.06f * R(sd + 3)) * (1f - u * 0.5f);
                Shard(i + 3, at, layer + i * 0.0002f, size, R(sd + 4) * 360f + u * 300f, Fade(Flake, (1f - u) * alpha));
                if (i % 3 == 0) Sprite(at, 0.26f * (1f - u * 0.3f), 0.20f * (1f - u * 0.3f), Fade(Flake, (1f - u) * 0.38f * alpha), puff, layer - 0.001f);
            }
        }

        /// <summary>
        /// lib/ajin.js ooze(): <paramref name="n"/> black strands pouring from one screen point to another, spread
        /// <paramref name="spread"/> cells at the far end, bowed to alternate sides. <paramref name="k"/> 0..1 is how far
        /// the stream has travelled.
        /// </summary>
        internal static void Ooze(Vector2 from, Vector2 to, float t, int n, float spread, float k, float alpha, float layer,
            float thick = 0.07f, float arc = 0.45f)
        {
            const int steps = 12;
            for (int i = 0; i < n; i++)
            {
                float off = (i / (float)(n - 1) - 0.5f) * spread, wig = Mathf.Sin(t * 9f + i * 1.7f) * 0.04f;
                var A = new Vector2(from.x + (R(i + 61) - 0.5f) * 0.12f, from.y + (R(i + 62) - 0.5f) * 0.08f);
                var B = new Vector2(to.x + off, to.y + (R(i + 63) - 0.5f) * 0.08f);
                Vector2 d = B - A;
                float L = d.magnitude;
                if (L == 0f) L = 1f;
                float side = (i % 2 == 1 ? 1f : -1f) * (0.22f + R(i + 67) * 0.38f) * Mathf.Min(1f, L);
                var C = new Vector2((A.x + B.x) / 2f - d.y / L * side + wig, (A.y + B.y) / 2f + d.x / L * side + arc * (0.2f + R(i + 64) * 0.4f));
                float end = Mathf.Clamp01(k * (1.1f - R(i + 65) * 0.2f));
                if (end <= 0.02f) continue;
                Vector2[] pts = Buf(steps + 1);
                for (int s = 0; s <= steps; s++)
                {
                    float q = s / (float)steps * end, a = (1f - q) * (1f - q), b = 2f * (1f - q) * q, c = q * q;
                    pts[s] = new Vector2(a * A.x + b * C.x + c * B.x + Mathf.Sin(q * 12f + t * 7f + i) * 0.015f, a * A.y + b * C.y + c * B.y);
                }
                Trail(pts, thick * (0.7f + R(i + 66) * 0.6f), Fade(Flake, 0.9f * alpha), layer + i * 0.0003f);
                Sprite(pts[steps], 0.13f, 0.10f, Fade(Flake, 0.5f * alpha), puff, layer + 0.002f);
            }
        }
    }
}
