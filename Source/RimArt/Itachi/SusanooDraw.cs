using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// A design-unit frame: Itachi's feet on screen and the scale of the design (3.5 cells tall,
    /// 3.2 wide at kx = kz = 1). A point (u, v) is drawn at Feet + (u * kx, v * kz); +u is screen
    /// right (the Susanoo's left side, the mirror), +v is up.
    /// </summary>
    public struct SusanooFrame
    {
        public Vector2 Feet;
        public float kx, kz;

        public Vector2 At(float u, float v) => new Vector2(Feet.x + u * kx, Feet.y + v * kz);
        public Vector2 At(Vector2 uv) => new Vector2(Feet.x + uv.x * kx, Feet.y + uv.y * kz);
    }

    /// <summary>
    /// The low-level drawing of lib/itachi.js: materials, meshes, layers, colours, strips, lines
    /// that draw themselves on, flame edges and the aura. Points are Vector2 (x, z) in map cells,
    /// design points Vector2 (u, v). Nothing keeps state between frames except the strip pool and
    /// the point scratch buffers, which are handed out by size and slot so a caller can build one
    /// polyline while a helper works on another.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class SusanooDraw
    {
        internal const float TAU = Mathf.PI * 2f;

        internal static readonly Material flat = VfxDraw.solid, add = VfxDraw.whiteGlow, soft = VfxDraw.soft, glow = VfxDraw.glow;
        internal static readonly Material swirlMat = MaterialPool.MatFrom("RimArt/Itachi/SusanooSwirl", ShaderDatabase.MoteGlow);
        internal static readonly Material mirrorMat = MaterialPool.MatFrom("RimArt/Itachi/SusanooMirror", ShaderDatabase.MoteGlow);
        internal static readonly Material curlMat = MaterialPool.MatFrom("RimArt/Itachi/SusanooCurl", ShaderDatabase.MoteGlow);
        internal static readonly Material flameMat = MaterialPool.MatFrom("RimArt/Itachi/SusanooFlame", ShaderDatabase.MoteGlow);
        internal static readonly Material hazeMat = MaterialPool.MatFrom("RimArt/Itachi/SusanooFlame", ShaderDatabase.Transparent);
        internal static readonly Mesh disc = VfxDraw.disc;
        internal static readonly Mesh ringMesh = VfxDraw.Ring(0.93f, "RimArt itachi ring");

        internal static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor(), PawnLayer = AltitudeLayer.Pawn.AltitudeFor();
        /// <summary>Just under the pawn layer: what covers Itachi is drawn here, so he stays visible.</summary>
        internal static readonly float Back = AltitudeLayer.Projectile.AltitudeFor();
        internal static readonly float Wash = AltitudeLayer.MoteOverheadLow.AltitudeFor();
        internal static readonly float Front = VfxDraw.Overhead, Floor = VfxDraw.Floor, ItemLayer = AltitudeLayer.Item.AltitudeFor();

        // Colours (lib/itachi.js). Fill and blade values are sampled from the ep 138 still; Warm is
        // the flame-yellow the anime forms it from before it settles to red.
        internal static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
        internal static readonly Color Deep = C(.71f, .24f, .17f), Lit = C(.85f, .41f, .29f), MirrorRed = C(.70f, .18f, .13f), CapeRed = C(.52f, .12f, .09f);
        internal static readonly Color Warm = C(.98f, .60f, .24f), WarmLit = C(1f, .74f, .38f);
        internal static readonly Color Line = C(1f, .80f, .66f), LineWarm = C(1f, .92f, .66f);
        internal static readonly Color Bone = C(1f, .64f, .47f), BoneWarm = C(1f, .82f, .52f);
        internal static readonly Color BladeCore = C(.96f, .81f, .50f), BladeBody = C(.90f, .66f, .42f), BladeEdge = C(.74f, .40f, .25f), BladeHot = C(1f, .95f, .80f);
        internal static readonly Color Eye = C(.95f, .80f, .40f), EyeHot = C(1f, .96f, .76f), EyeGlow = C(1f, .72f, .18f);
        internal static readonly Color Mouth = C(.14f, .02f, .02f), Fang = C(1f, .90f, .76f);
        internal static readonly Color FlameDeep = C(1f, .32f, .10f), FlameMid = C(1f, .56f, .24f), FlamePale = C(1f, .86f, .62f);
        internal static readonly Color AuraRed = C(.80f, .10f, .06f), FlameBody = C(1f, .46f, .16f);
        internal static readonly Color Sharingan = C(.95f, .06f, .05f), Blood = C(.50f, .03f, .03f);

        internal static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
        internal static float Clamp(float t) => Mathf.Clamp01(t);
        internal static float Lerp(float a, float b, float t) => Mathf.Lerp(a, b, t);
        internal static float Deg(Vector2 a, Vector2 b) => Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        internal static Vector2 Unit(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float l = d.magnitude;
            return l > 1e-6f ? d / l : Vector2.right;
        }

        // ---- scratch points. Slots 1-5, 8, 10, 11 and 13 belong to the helpers here (a caller's
        // polyline must not live in them: Stroke reads its input while it writes its sides).
        // Callers use SLine, SLine2, the edge pair from Pair, the transformed edge and SA-SD.
        internal const int SLine = 0, SCut = 1, SRes = 2, SResN = 3, SSideA = 4, SSideB = 5, SEdgeL = 6, SEdgeR = 7, SSeg = 8,
            SLine2 = 9, SHaloA = 10, SHaloB = 11, SEdgeT = 12, SFan = 13, SA = 14, SB = 15, SC = 16, SD = 17;
        private const int Slots = 18;
        private static readonly Dictionary<int, Vector2[]>[] scratch = new Dictionary<int, Vector2[]>[Slots];

        /// <summary>An array of exactly <paramref name="n"/> points for this slot, reused across frames.</summary>
        internal static Vector2[] Pts(int slot, int n)
        {
            Dictionary<int, Vector2[]> bySize = scratch[slot] ?? (scratch[slot] = new Dictionary<int, Vector2[]>());
            if (!bySize.TryGetValue(n, out Vector2[] pts)) bySize[n] = pts = new Vector2[n];
            return pts;
        }

        // ---- strip pool: one mesh per strip drawn in a frame, by point count.
        private static readonly Dictionary<int, List<SixPathsStrip>> pool = new Dictionary<int, List<SixPathsStrip>>();
        private static readonly Dictionary<int, int> taken = new Dictionary<int, int>();
        private static int frame = -1;

        private static SixPathsStrip Next(int n)
        {
            if (Time.frameCount != frame)
            {
                frame = Time.frameCount;
                taken.Clear();
            }
            if (!pool.TryGetValue(n, out List<SixPathsStrip> made)) pool[n] = made = new List<SixPathsStrip>();
            taken.TryGetValue(n, out int used);
            if (used == made.Count) made.Add(new SixPathsStrip("RimArt itachi strip " + n + " " + made.Count, n));
            taken[n] = used + 1;
            return made[used];
        }

        /// <summary>A ribbon between two point lines of equal length, in map cells, drawn at the origin.</summary>
        internal static void Strip(Vector2[] a, Vector2[] b, Color colour, Material material, float layer)
        {
            if (colour.a <= .002f || a.Length < 2) return;
            SixPathsStrip strip = Next(a.Length);
            strip.Between(a, b);
            VfxDraw.DrawMesh(strip.mesh, Vector2.zero, layer, 1f, 1f, 0f, colour, material);
        }

        internal static void Sprite(Vector2 pos, float w, float h, Color colour, Material material, float layer, float angle = 0f)
        {
            if (colour.a <= .002f) return;
            VfxDraw.DrawMesh(MeshPool.plane10, pos, layer, w, h, angle, colour, material);
        }

        /// <summary>One flame tongue standing on <paramref name="baseAt"/>, w wide and h tall, leaning deg degrees (positive = tip to the right).</summary>
        internal static void Tongue(Vector2 baseAt, float w, float h, float deg, Color colour, Material material, float layer)
        {
            if (colour.a <= .002f || h <= .005f) return;
            float a = deg * Mathf.Deg2Rad;
            VfxDraw.DrawMesh(MeshPool.plane10, new Vector2(baseAt.x + Mathf.Sin(a) * h / 2f, baseAt.y + Mathf.Cos(a) * h / 2f), layer, w, h, deg, colour, material);
        }

        internal static void Blob(Vector2 pos, float rx, float rz, Color colour, float layer, float rot = 0f, Material material = null)
        {
            if (colour.a <= .002f) return;
            VfxDraw.DrawMesh(disc, pos, layer, rx, rz, rot, colour, material ?? flat);
        }

        internal static void DrawRing(Vector2 pos, float rx, float rz, float rot, Color colour, Material material, float layer)
        {
            if (colour.a <= .002f) return;
            VfxDraw.DrawMesh(ringMesh, pos, layer, rx, rz, rot, colour, material);
        }

        /// <summary>A flat fill that glows: a see-through layer for the colour plus an additive layer.</summary>
        internal static void FillStrip(Vector2[] a, Vector2[] b, Color colour, float alpha, float layer)
        {
            if (alpha <= .002f) return;
            Strip(a, b, A(colour, Mathf.Min(.95f, alpha)), flat, layer);
            Strip(a, b, new Color(colour.r * .7f, colour.g * .3f, colour.b * .26f, alpha * .22f), add, layer + .0001f);
        }

        internal static void FillBlob(Vector2 pos, float rx, float rz, Color colour, float alpha, float layer, float rot = 0f)
        {
            if (alpha <= .002f) return;
            VfxDraw.DrawMesh(disc, pos, layer, rx, rz, rot, A(colour, Mathf.Min(.95f, alpha)), flat);
            VfxDraw.DrawMesh(disc, pos, layer + .0001f, rx, rz, rot, new Color(colour.r * .7f, colour.g * .3f, colour.b * .26f, alpha * .22f), add);
        }

        /// <summary>A fan from <paramref name="centre"/> over <paramref name="pts"/> (all of the array), as a strip whose one side is the centre.</summary>
        internal static void Fan(Vector2 centre, Vector2[] pts, Color colour, Material material, float layer)
        {
            Vector2[] c = Pts(13, pts.Length);
            for (int i = 0; i < c.Length; i++) c[i] = centre;
            Strip(c, pts, colour, material, layer);
        }

        // ---- paths

        private static float Len(Vector2 a, Vector2 b) => (b - a).magnitude;

        /// <summary>The first <paramref name="share"/> (0..1) of a polyline's length, into slot 1; the input itself when share is 1.</summary>
        internal static Vector2[] CutPath(Vector2[] pts, int n, float share, out int outN)
        {
            if (share >= 1f)
            {
                outN = n;
                return pts;
            }
            float total = 0f;
            for (int i = 1; i < n; i++) total += Len(pts[i - 1], pts[i]);
            float want = total * Mathf.Max(0f, share);
            // Count first, so the scratch array has the right size.
            int count = 1;
            float left = want;
            for (int i = 1; i < n; i++)
            {
                float d = Len(pts[i - 1], pts[i]);
                count++;
                if (left <= d) break;
                left -= d;
            }
            Vector2[] o = Pts(1, count);
            o[0] = pts[0];
            left = want;
            int k = 1;
            for (int i = 1; i < n && k < count; i++)
            {
                float d = Len(pts[i - 1], pts[i]);
                if (left <= d)
                {
                    float f = d > 0f ? left / d : 0f;
                    o[k++] = Vector2.Lerp(pts[i - 1], pts[i], f);
                    break;
                }
                left -= d;
                o[k++] = pts[i];
            }
            outN = count;
            return o;
        }

        /// <summary>
        /// <paramref name="steps"/> + 1 evenly spaced points along a polyline (slot 2) with unit left
        /// normals (slot 3, left of the walking direction); point k sits at t = k / steps.
        /// </summary>
        internal static void Resample(Vector2[] pts, int n, int steps, out Vector2[] o, out Vector2[] normals)
        {
            float[] cum = Cum(n);
            cum[0] = 0f;
            for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Len(pts[i - 1], pts[i]);
            float total = cum[n - 1] > 0f ? cum[n - 1] : 1f;
            o = Pts(2, steps + 1);
            normals = Pts(3, steps + 1);
            int j = 1;
            for (int k = 0; k <= steps; k++)
            {
                float want = k / (float)steps * total;
                while (j < n - 1 && cum[j] < want) j++;
                float d = cum[j] - cum[j - 1];
                if (d <= 0f) d = 1f;
                float f = Clamp((want - cum[j - 1]) / d);
                o[k] = Vector2.Lerp(pts[j - 1], pts[j], f);
            }
            for (int i = 0; i <= steps; i++)
            {
                Vector2 a = o[Mathf.Max(0, i - 1)], b = o[Mathf.Min(steps, i + 1)];
                float dx = b.x - a.x, dz = b.y - a.y, l = Mathf.Sqrt(dx * dx + dz * dz);
                if (l <= 1e-6f) l = 1f;
                normals[i] = new Vector2(-dz / l, dx / l);
            }
        }

        private static float[] cumBuf = new float[64];
        private static float[] Cum(int n)
        {
            if (cumBuf.Length < n) cumBuf = new float[n * 2];
            return cumBuf;
        }

        /// <summary>
        /// A line of constant width along pts (taper 1 = thins to nothing at both ends). upTo draws
        /// only the first share of its length, for lines that draw themselves on.
        /// </summary>
        internal static void Stroke(Vector2[] pts, int n, float width, Color colour, Material material, float layer, float upTo = 1f, float taper = 0f)
        {
            if (upTo <= 0f || colour.a <= .002f || n < 2) return;
            Vector2[] p = CutPath(pts, n, upTo, out int m);
            if (m < 2) return;
            Vector2[] a = Pts(4, m), b = Pts(5, m);
            for (int i = 0; i < m; i++)
            {
                Vector2 q0 = p[Mathf.Max(0, i - 1)], q1 = p[Mathf.Min(m - 1, i + 1)];
                float dx = q1.x - q0.x, dz = q1.y - q0.y, l = Mathf.Sqrt(dx * dx + dz * dz);
                if (l <= 1e-6f) l = 1f;
                float f = taper > 0f ? Lerp(1f, Mathf.Max(0f, Mathf.Sin(i / (float)(m - 1) * Mathf.PI)), taper) : 1f;
                float w = width / 2f * f;
                a[i] = new Vector2(p[i].x - dz / l * w, p[i].y + dx / l * w);
                b[i] = new Vector2(p[i].x + dz / l * w, p[i].y - dx / l * w);
            }
            Strip(a, b, colour, material, layer);
        }

        /// <summary>A glowing line: a soft wide halo under a thin bright core.</summary>
        internal static void GlowLine(Vector2[] pts, int n, float width, Color colour, float alpha, float layer, float upTo = 1f, float taper = 0f)
        {
            Stroke(pts, n, width * 3.2f, A(colour, alpha * .22f), add, layer, upTo, taper);
            Stroke(pts, n, width, A(colour, alpha), add, layer + .0004f, upTo, taper);
        }

        /// <summary>A line whose width runs from w0 at its start to w1 at its end, with a halo.</summary>
        internal static void TaperLine(Vector2[] pts, int n, float w0, float w1, Color colour, float alpha, float layer, float upTo = 1f, Material material = null)
        {
            if (upTo <= 0f || alpha <= .002f || n < 2) return;
            Vector2[] p = CutPath(pts, n, upTo, out int m);
            if (m < 2) return;
            Vector2[] a = Pts(4, m), b = Pts(5, m), ha = Pts(10, m), hb = Pts(11, m);
            for (int i = 0; i < m; i++)
            {
                Vector2 q0 = p[Mathf.Max(0, i - 1)], q1 = p[Mathf.Min(m - 1, i + 1)];
                float dx = q1.x - q0.x, dz = q1.y - q0.y, l = Mathf.Sqrt(dx * dx + dz * dz);
                if (l <= 1e-6f) l = 1f;
                float w = Lerp(w0, w1, i / (float)(m - 1)) / 2f;
                var off = new Vector2(-dz / l * w, dx / l * w);
                a[i] = p[i] + off;
                b[i] = p[i] - off;
                ha[i] = p[i] + off * 3.2f;
                hb[i] = p[i] - off * 3.2f;
            }
            Strip(ha, hb, A(colour, alpha * .2f), add, layer);
            Strip(a, b, A(colour, alpha), material ?? add, layer + .0004f);
        }

        // ---- flame edge and aura

        private const int MostTips = 64;
        private static readonly float[] tipT = new float[MostTips], tipU = new float[MostTips], tipH = new float[MostTips];
        private static readonly int[] tipBig = new int[MostTips], tipSeed = new int[MostTips];
        private static float[] reach = new float[400];

        /// <summary>
        /// A row of flame tips along a path: 3 nested additive strips whose outer edge is the tallest
        /// tip at each point, each tip rising, flickering and surging on its own; a wisp breaks off
        /// the top of each surge. <paramref name="lit"/> (0..1 along the path) scales the tips, for
        /// flames that light from one end. <paramref name="up"/> bends them upward.
        /// </summary>
        internal static void FlameEdge(Vector2[] pts, int n, float s, float height, float alpha, int count, int seed, float layer,
            float up = .9f, Func<float, float> lit = null, float inset = .03f)
        {
            if (alpha <= .002f || height <= .001f || n < 2) return;
            count = Mathf.Min(count, MostTips);
            for (int k = 0; k < count; k++)
            {
                int sd = seed * 97 + k * 7;
                float phase = s / (.26f + .14f * Rand(sd + 1)) + Rand(sd + 2), u = phase - Mathf.Floor(phase);
                float flicker = .7f + .3f * Mathf.Sin(s * (17f + 8f * Rand(sd + 3)) + k * 2.1f);
                float t = (k + .5f + (Rand(sd + 4) - .5f) * .8f) / count, on = lit != null ? lit(t) : 1f;
                int big = Rand(sd + 6) > .8f ? 2 : 1;
                tipT[k] = t;
                tipU[k] = u;
                tipBig[k] = big;
                tipSeed[k] = sd;
                tipH[k] = height * (.45f + .55f * Rand(sd + 5)) * flicker * (.75f + .5f * Mathf.Sin(u * Mathf.PI)) * on;
            }
            int steps = count * 5;
            Resample(pts, n, steps, out Vector2[] p, out Vector2[] nm);
            float half = .9f / count;
            float total = 0f;
            for (int i = 1; i <= steps; i++) total += Len(p[i], p[i - 1]);
            float spacing = total / count;
            if (reach.Length < steps + 1) reach = new float[steps * 2];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps, h = 0f;
                for (int k = 0; k < count; k++)
                {
                    float d = Mathf.Abs(t - tipT[k]) / half;
                    if (d < 1f) h = Mathf.Max(h, tipH[k] * Mathf.Pow(1f - d, 1.8f));
                }
                reach[i] = h;
            }
            // The glowing rim the tongues stand on: three nested strips, low.
            for (int j = 0; j < 3; j++)
            {
                float share = j == 0 ? .55f : j == 1 ? .34f : .16f, a = j == 0 ? .38f : j == 1 ? .45f : .5f;
                Color colour = j == 0 ? FlameDeep : j == 1 ? FlameMid : FlamePale;
                Vector2[] inner = Pts(4, steps + 1), outer = Pts(5, steps + 1);
                for (int i = 0; i <= steps; i++)
                {
                    Vector2 d = Dir(nm[i], up);
                    float h = .01f + reach[i] * share;
                    inner[i] = p[i] - nm[i] * inset;
                    outer[i] = p[i] + d * h;
                }
                Strip(inner, outer, A(colour, a * alpha), add, layer + j * .0002f);
            }
            // Tongues: each rises from the rim, mostly up, sways, with a pale core; big licks on some.
            for (int k = 0; k < count; k++)
            {
                float qh = tipH[k];
                if (qh <= .01f) continue;
                int i = Mathf.Min(steps, Mathf.RoundToInt(tipT[k] * steps));
                Vector2 d = Dir(nm[i], up);
                int sd = tipSeed[k];
                float lean = Mathf.Clamp(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg * .4f, -25f, 25f) + 10f * Mathf.Sin(s * (4f + 3f * Rand(sd + 7)) + k * 1.7f);
                bool big = tipBig[k] > 1;
                float h = (qh * 1.5f + .04f) * (big ? 1.6f : 1f);
                float w = Mathf.Min(.55f, Mathf.Max(.12f, spacing * 2.4f)) * (big ? 1.25f : 1f) * (.8f + .4f * Rand(sd + 8));
                Vector2 baseAt = p[i] - d * .02f;
                Tongue(baseAt, w, h, lean, A(FlameBody, .55f * alpha), flameMat, layer + .0006f);
                Tongue(baseAt, w * .45f, h * .55f, lean * .8f, A(FlamePale, .4f * alpha), flameMat, layer + .0007f);
                float u = tipU[k];
                if (u > .55f && big)
                {
                    float v = (u - .55f) / .45f, a = lean * Mathf.Deg2Rad, rise = h * (1f + .9f * v);
                    var at = new Vector2(baseAt.x + Mathf.Sin(a) * rise + .08f * Mathf.Sin(s * 6f + k), baseAt.y + Mathf.Cos(a) * rise + .25f * v);
                    Tongue(at, w * .45f * (1f - .5f * v), h * .35f * (1f - .4f * v), lean, A(FlameMid, .55f * (1f - v) * alpha), flameMat, layer + .0008f);
                }
            }
        }

        private static Vector2 Dir(Vector2 normal, float up)
        {
            float dx = normal.x, dz = normal.y + up, l = Mathf.Sqrt(dx * dx + dz * dz);
            if (l <= 1e-6f) l = 1f;
            return new Vector2(dx / l, dz / l);
        }

        // The aura's hull round the figure (design units).
        private static readonly Vector2[] AuraHull =
        {
            new Vector2(-1.55f, .1f), new Vector2(-1.75f, .9f), new Vector2(-1.75f, 1.7f), new Vector2(-1.55f, 2.25f), new Vector2(-1.0f, 2.5f),
            new Vector2(-.6f, 2.85f), new Vector2(-.4f, 3.25f), new Vector2(0f, 3.42f), new Vector2(.4f, 3.25f), new Vector2(.6f, 2.85f),
            new Vector2(1.0f, 2.5f), new Vector2(1.55f, 2.25f), new Vector2(1.95f, 1.85f), new Vector2(2.05f, 1.2f), new Vector2(1.85f, .5f), new Vector2(1.55f, .1f),
        };

        /// <summary>
        /// The aura: a red haze round the whole Susanoo, rising a little above the head in soft flame
        /// tongues on their own cycles, over a steady haze behind the body. grow 0..1 raises the hull
        /// from half height with the figure; warm tints it the flame-yellow it forms from; size is
        /// the Outer flame setting (1 = 70 % of the first height, 2 = 115 %).
        /// </summary>
        internal static void Aura(in SusanooFrame F, float s, float k, float layer, float grow = 1f, float warm = 0f, float size = 1f)
        {
            if (k <= .002f || size <= .002f) return;
            Color red = Color.Lerp(AuraRed, C(.95f, .45f, .12f), warm * .8f);
            float vs = Lerp(.45f, 1f, grow);
            float hs = .25f + .45f * size, asz = Mathf.Min(1f, .35f + .45f * size);
            Sprite(F.At(0f, 1.5f * vs), 3.3f, 3.1f * vs, A(red, .12f * k * asz), soft, layer - .002f);
            Sprite(F.At(0f, 2.8f * vs), 1.6f, 1.4f, A(red, .1f * k * grow * asz), soft, layer - .0019f);
            Vector2[] hull = Pts(0, AuraHull.Length);
            for (int i = 0; i < hull.Length; i++) hull[i] = F.At(AuraHull[i].x, AuraHull[i].y * vs);
            Resample(hull, hull.Length, 36, out Vector2[] p, out Vector2[] nm);
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                int sd = i * 17 + 1200;
                float t = (i + .5f + (Rand(sd + 1) - .5f) * .6f) / n;
                int at = Mathf.Clamp(Mathf.RoundToInt(t * 36f), 0, 36);
                Vector2 pp = p[at], nn = nm[at];
                float top = 1f - Mathf.Abs(2f * t - 1f);
                for (int c = 0; c < 2; c++)
                {
                    float period = .9f + .5f * Rand(sd + 2 + c), ph = s / period + Rand(sd + 3 + c) + c * .5f, u = ph - Mathf.Floor(ph);
                    float a = Mathf.Sin(u * Mathf.PI) * k;
                    float h = (1.0f + 1.2f * top + .4f * Rand(sd + 5)) * (.7f + .3f * u) * hs, w = .8f + .4f * Rand(sd + 6);
                    float lean = Mathf.Clamp(Mathf.Atan2(nn.x, nn.y + 1.2f) * Mathf.Rad2Deg, -40f, 40f) * .6f + 10f * Mathf.Sin(s * 2f + i);
                    var baseAt = new Vector2(pp.x - nn.x * .35f + nn.x * u * .2f, pp.y - nn.y * .35f + u * .3f);
                    Tongue(baseAt, w, h * Lerp(.6f, 1f, grow), lean, A(red, .3f * a * asz), hazeMat, layer + c * .0001f);
                    Tongue(baseAt, w * .7f, h * .8f * Lerp(.6f, 1f, grow), lean, A(FlameDeep, .14f * a * asz), flameMat, layer + .0002f + c * .0001f);
                }
            }
        }

        // ---- edges: a few (v, half width) points smoothed into curves (Catmull-Rom).

        internal static Vector2[] SmoothEdge(float[][] edge, int per = 4)
        {
            int n = edge.Length;
            var o = new List<Vector2>();
            float[] At(int i) => edge[Mathf.Clamp(i, 0, n - 1)];
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < per; k++)
                {
                    float t = k / (float)per;
                    float[] p0 = At(i - 1), p1 = At(i), p2 = At(i + 1), p3 = At(i + 2);
                    float Cr(int j) => .5f * (2f * p1[j] + (-p0[j] + p2[j]) * t + (2f * p0[j] - 5f * p1[j] + 4f * p2[j] - p3[j]) * t * t + (-p0[j] + 3f * p1[j] - 3f * p2[j] + p3[j]) * t * t * t);
                    o.Add(new Vector2(Cr(0), Cr(1)));
                }
            o.Add(new Vector2(edge[n - 1][0], edge[n - 1][1]));
            return o.ToArray();
        }

        /// <summary>The part of an edge (v, half width) between v0 and v1, ends interpolated, into slot 8.</summary>
        internal static Vector2[] EdgeBetween(Vector2[] edge, int n, float v0, float v1, out int outN)
        {
            int count = 2;
            for (int i = 0; i < n; i++) if (edge[i].x > v0 && edge[i].x < v1) count++;
            Vector2[] o = Pts(8, count);
            o[0] = EdgeAt(edge, n, v0);
            int k = 1;
            for (int i = 0; i < n; i++) if (edge[i].x > v0 && edge[i].x < v1) o[k++] = edge[i];
            o[count - 1] = EdgeAt(edge, n, v1);
            outN = count;
            return o;
        }

        private static Vector2 EdgeAt(Vector2[] edge, int n, float v)
        {
            for (int i = 1; i < n; i++)
                if (edge[i].x >= v)
                {
                    Vector2 a = edge[i - 1], b = edge[i];
                    float span = b.x - a.x;
                    float f = span != 0f ? (v - a.x) / span : 1f;
                    return new Vector2(v, a.y + (b.y - a.y) * f);
                }
            return edge[n - 1];
        }

        /// <summary>An edge as its two screen sides: left at (-h, v + dv), right at (h, v + dv), slots 6 and 7.</summary>
        internal static void Pair(Vector2[] edge, int n, in SusanooFrame F, float dv, out Vector2[] left, out Vector2[] right)
        {
            left = Pts(6, n);
            right = Pts(7, n);
            for (int i = 0; i < n; i++)
            {
                left[i] = F.At(-edge[i].y, edge[i].x + dv);
                right[i] = F.At(edge[i].y, edge[i].x + dv);
            }
        }

        /// <summary>
        /// 2-bone reach: the elbow for a hand at <paramref name="hand"/> from <paramref name="root"/>,
        /// bent to the outside (side -1 = screen left, +1 = right), or with down 0..1 moved toward the
        /// lower of the two elbows (an arm hanging at the side with the forearm out). Design units.
        /// </summary>
        internal static void ElbowFor(Vector2 root, Vector2 hand, int side, float down, float L1, float L2, out Vector2 elbow, out Vector2 handOut)
        {
            float dx = hand.x - root.x, dv = hand.y - root.y, d = Mathf.Sqrt(dx * dx + dv * dv);
            if (d < 1e-4f) d = 1e-4f;
            float ux = dx / d, uv = dv / d;
            d = Mathf.Min(L1 + L2 - 1e-3f, Mathf.Max(Mathf.Abs(L1 - L2) + 1e-3f, d));
            float a = (L1 * L1 - L2 * L2 + d * d) / (2f * d), h = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
            float mx = root.x + ux * a, mv = root.y + uv * a;
            var e1 = new Vector2(mx - uv * h, mv + ux * h);
            var e2 = new Vector2(mx + uv * h, mv - ux * h);
            Vector2 outer = side < 0 ? (e1.x < e2.x ? e1 : e2) : (e1.x > e2.x ? e1 : e2), low = e1.y < e2.y ? e1 : e2;
            elbow = Vector2.Lerp(outer, low, down);
            handOut = new Vector2(root.x + ux * d, root.y + uv * d);
        }
    }
}
