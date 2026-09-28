using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The shared drawing of the Obito kit, ported from the lab's lib/obito.js: the Kamui swirl (dark arms with a
    /// pale leading line, a dark core, a faint haze, in the dimension's void and edge colours), the swirl shutting,
    /// the Mangekyō's red flash, his hand, the stun marker and one Wood Release branch. Everything is
    /// flat on the ground plane (a swirl is a flat spiral on the screen; a branch lies at one height), so nothing
    /// here needs a per-facing method.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ObitoGraphics
    {
        internal static readonly Color Void = Rgb(10, 16, 22), Edge = Rgb(196, 222, 244);
        internal static readonly Color Sharingan = Rgb(222, 30, 24);
        internal static readonly Color Zetsu = Rgb(232, 229, 218), ZetsuShade = Rgb(176, 172, 160);
        internal static readonly Color Bark = Rgb(92, 64, 40), BarkLit = Rgb(146, 108, 68), BarkDark = Rgb(50, 34, 22), Heart = Rgb(226, 202, 150);
        internal static readonly Color DryBark = Rgb(96, 88, 78), DryLit = Rgb(150, 140, 126);
        internal static readonly Color Round = new Color(1f, 0.93f, 0.62f), Stun = new Color(1f, 0.88f, 0.45f), Blood = new Color(0.45f, 0.05f, 0.05f);

        /// <summary>The lab's layers: the floor just over filth, shadows, pawns, and the overhead effects.</summary>
        internal static readonly float LFloor = AltitudeLayer.Filth.AltitudeFor() + 0.01f;
        internal static readonly float LShadow = AltitudeLayer.Shadows.AltitudeFor();
        internal static readonly float LPawn = AltitudeLayer.Pawn.AltitudeFor();
        internal static readonly float LFx = AltitudeLayer.MoteOverhead.AltitudeFor();

        /// <summary>The lab's Lift: a height h above the floor is drawn h × Lift cells north.</summary>
        internal const float Lift = 0.6f;
        /// <summary>The sketches stand a pawn's feet on the point they are given; the game draws it centred on DrawPos, 0.3 cells higher.</summary>
        internal const float FeetBelowDrawPos = 0.3f;

        private static readonly Mesh ring = Ring(0.84f, "Obito ring");

        /// <summary>Kamui: Store's release button.</summary>
        internal static readonly Texture2D ReleaseIcon = ContentFinder<Texture2D>.Get("RimArt/Obito/IconRelease");

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
        internal static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;

        /// <summary>The ground point (the sketches' pawn origin) of a pawn drawn at <paramref name="drawPos"/>.</summary>
        internal static Vector2 Ground(Vector3 drawPos) => new Vector2(drawPos.x, drawPos.z - FeetBelowDrawPos);

        // ---- the right eye ---------------------------------------------------------------------------

        /// <summary>
        /// Obito's right eye (the source's Kamui is the right eye), where every swirl of his is centred. On a
        /// humanlike it sits on the real head: the head offset for the facing plus the eye on the vanilla head
        /// picture (eyes at x 52-59, y 67-72 of 128 px, the head drawn 1.5 cells wide). Facing south his right
        /// eye is on the screen's left; west is east mirrored; facing north it is drawn over the back of the head.
        /// </summary>
        internal static Vector2 Eye(Pawn pawn, Vector3 drawPos, Rot4 facing)
        {
            Vector3 head = pawn?.RaceProps?.Humanlike == true && pawn.Drawer?.renderer != null
                ? pawn.Drawer.renderer.BaseHeadOffsetAt(facing) : new Vector3(0f, 0f, 0.3f);
            Vector2 eye;
            switch (facing.AsInt)
            {
                case 1: eye = new Vector2(0.135f, -0.065f); break;       // east
                case 3: eye = new Vector2(-0.135f, -0.065f); break;      // west
                case 0: eye = new Vector2(0.054f, 0.02f); break;         // north
                default: eye = new Vector2(-0.1f, -0.065f); break;       // south
            }
            return new Vector2(drawPos.x + head.x + eye.x, drawPos.z + head.z + eye.y);
        }

        // ---- lines ---------------------------------------------------------------------------------

        /// <summary>The lab's trail: a ribbon along the points, widest in the middle (sine taper).</summary>
        internal static void Trail(Vector2[] pts, float width, Color colour, Material material, float altitude)
        {
            if (colour.a <= 0.001f || pts.Length < 2) return;
            Sides(pts.Length, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(pts.Length - 1, i + 1)];
                Vector2 d = next - prev;
                float len = d.magnitude;
                if (len < 1e-6f) len = 1f;
                float w = Mathf.Sin(i / (float)(pts.Length - 1) * Mathf.PI) * width / 2f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, material, altitude);
        }

        /// <summary>A straight strip of even width from a to b.</summary>
        internal static void Rod(Vector2 from, Vector2 to, float width, Color colour, float altitude)
        {
            if (colour.a <= 0.001f) return;
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 1e-6f) len = 1f;
            var n = new Vector2(-d.y / len * width / 2f, d.x / len * width / 2f);
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = from + n; a[1] = to + n;
            b[0] = from - n; b[1] = to - n;
            Strip(a, b, colour, solid, altitude);
        }

        internal static void Disc(Vector2 at, float radius, Color colour, float altitude) =>
            DrawMesh(disc, at, altitude, radius, radius, 0f, colour, solid);

        internal static void RingAt(Vector2 at, float radius, Color colour, float altitude) =>
            DrawMesh(ring, at, altitude, radius, radius, 0f, colour, solid);

        // ---- the Kamui swirl ------------------------------------------------------------------------

        private static readonly Vector2[] dark = new Vector2[19], edge = new Vector2[19];

        /// <summary>
        /// The spiral of space at <paramref name="c"/>. <paramref name="spin"/> is the arms' turn in radians
        /// (decrease it over time: clockwise).
        /// </summary>
        internal static void Swirl(Vector2 c, float radius, float spin, float alpha, int arms = 4, float wind = 0.85f,
            float? layer = null, float core = 0.13f, float haze = 1f, float lit = 1f)
        {
            if (alpha <= 0.01f || radius <= 0.01f) return;
            float y = layer ?? LFx;
            Begin(c);
            if (haze > 0f) Sprite(c, radius * 2.9f, radius * 2.9f, Fade(Void, 0.14f * alpha * haze), soft, y);
            const int N = 18;
            for (int i = 0; i < arms; i++)
            {
                float phase = spin + i * Mathf.PI * 2f / arms;
                for (int j = 0; j <= N; j++)
                {
                    float u = j / (float)N, r = radius * (0.08f + 0.92f * Mathf.Pow(u, 1.15f)), a = phase + wind * Mathf.PI * 2f * u;
                    dark[j] = new Vector2(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r);
                    float al = a - 0.2f - 0.1f * u, rl = r * 1.03f;
                    edge[j] = new Vector2(c.x + Mathf.Cos(al) * rl, c.y + Mathf.Sin(al) * rl);
                }
                Trail(dark, radius * 0.16f, Fade(Void, 0.42f * alpha), solid, y + 0.002f);
                if (lit > 0f) Trail(edge, Mathf.Max(0.018f, radius * 0.06f), Fade(Edge, 0.72f * alpha * lit), solid, y + 0.003f);
            }
            float cr = Mathf.Max(0.02f, radius * core);
            Disc(c, cr, Fade(Void, Mathf.Min(1f, alpha * 1.3f)), y + 0.004f);
            RingAt(c, cr * 1.3f, Fade(Edge, 0.55f * alpha * lit), y + 0.005f);
        }

        /// <summary>A pale ring pulled into a point as a swirl shuts (age 0 to 0.25 s).</summary>
        internal static void Shut(Vector2 c, float age, float radius = 0.5f, float? layer = null)
        {
            if (age < 0f || age > 0.25f) return;
            float y = layer ?? LFx, k = age / 0.25f, r = radius * (1f - Smooth(k));
            if (r > 0.01f) RingAt(c, r, Fade(Edge, 0.7f * (1f - k * 0.5f)), y + 0.006f);
            float s = 0.4f * (1f - k) + 0.05f;
            Sprite(c, s, s, Fade(Void, 0.5f * (1f - k)), soft, y + 0.001f);
        }

        /// <summary>The Mangekyō's red flash in the mask's eye hole (age 0 to 0.4 s).</summary>
        internal static void Glint(Vector2 at, float age, float size = 1f)
        {
            const float Life = 0.4f;
            if (age < 0f || age > Life) return;
            float k = age / Life, a = Mathf.Min(1f, age / 0.05f) * (1f - Smooth((k - 0.25f) / 0.75f));
            Sprite(at, 0.5f * size, 0.5f * size, Fade(Sharingan, 0.5f * a), glow, LFx + 0.02f);
            Sprite(at, 0.95f * size * (0.55f + 0.45f * k), 0.05f * size, Fade(Sharingan, 0.85f * a), glow, LFx + 0.021f);
            Sprite(at, 0.05f * size, 0.55f * size * (0.55f + 0.45f * k), Fade(Sharingan, 0.6f * a), glow, LFx + 0.021f);
            Disc(at, 0.03f * size, new Color(1f, 0.8f, 0.75f, a), LFx + 0.022f);
        }

        private static readonly Color Knuckle = new Color(0.16f, 0.12f, 0.1f);
        private static readonly Color DefaultSkin = new Color(0.83f, 0.70f, 0.54f);

        internal static Color SkinOf(Pawn pawn) => pawn?.story?.SkinColor ?? DefaultSkin;

        /// <summary>
        /// A hand: a round dot with a dark rim, the size Melee Animation draws hands at. Pawns have no arms, so the
        /// hand floats beside the body and moves on its own, as in Vergil's port. <paramref name="bark"/> 0 to 1
        /// turns it to wood (Wood Release).
        /// </summary>
        internal static void Hand(Vector2 at, Color skin, float altitude, float bark = 0f, float alpha = 1f)
        {
            if (alpha <= 0.01f) return;
            Disc(at, 0.07f, Fade(Color.Lerp(Knuckle, BarkDark, bark), alpha), altitude);
            Disc(at, 0.056f, Fade(Color.Lerp(skin, Bark, bark), alpha), altitude + 0.0001f);
            if (bark > 0.5f) Disc(at + new Vector2(-0.015f, 0.015f), 0.025f, Fade(BarkLit, alpha * (bark - 0.5f) * 2f), altitude + 0.0002f);
        }

        /// <summary>
        /// Where his right hand rests beside his body, per facing (Vergil's right-hand rest): facing south it is on
        /// the screen's left, facing north on the right, facing east or west in front of his middle.
        /// </summary>
        internal static Vector2 HandRest(Vector3 drawPos, Rot4 facing)
        {
            Vector2 rest;
            switch (facing.AsInt)
            {
                case 0: rest = new Vector2(0.2f, -0.12f); break;
                case 2: rest = new Vector2(-0.2f, -0.12f); break;
                default: rest = new Vector2(0.02f, -0.1f); break;
            }
            return new Vector2(drawPos.x + rest.x, drawPos.z + rest.y);
        }

        /// <summary>Three small stars circling over a stunned pawn's head; <paramref name="ground"/> is its feet.</summary>
        internal static void StunMark(Vector2 ground, float seconds)
        {
            var c = new Vector2(ground.x, ground.y + 0.95f);
            for (int i = 0; i < 3; i++)
            {
                float a = seconds * 5f + i * Mathf.PI * 2f / 3f;
                Sprite(new Vector2(c.x + Mathf.Cos(a) * 0.2f, c.y + Mathf.Sin(a) * 0.07f), 0.12f, 0.12f, Fade(Stun, 0.9f), glow, LFx + 0.03f);
            }
        }

        // ---- Wood Release ---------------------------------------------------------------------------

        private static readonly Vector2[] branchA = new Vector2[VfxDraw.MostPoints], branchB = new Vector2[VfxDraw.MostPoints];
        private static readonly float[] acc = new float[VfxDraw.MostPoints];

        /// <summary>
        /// One branch through spine points on the screen (height already folded in). <paramref name="h"/> is its
        /// height above the floor, for the shadow. Width <paramref name="w0"/> at the root, 55 % of it at the far
        /// end, then a point over the last <paramref name="tipLen"/> cells, which is pale fresh wood.
        /// </summary>
        internal static void Branch(Vector2[] pts, int count, float w0, float h, Vector2 sun, float strength, float alpha,
            float layer, float tipLen, float dry)
        {
            if (count < 2 || alpha <= 0.01f) return;
            acc[0] = 0f;
            for (int i = 1; i < count; i++) acc[i] = acc[i - 1] + (pts[i] - pts[i - 1]).magnitude;
            float len = acc[count - 1];
            if (len < 0.02f) return;
            Begin(pts[0]);
            // Shadow: the same outline dropped to the floor and pushed along the sun.
            if (strength > 0f)
            {
                Side(pts, count, len, w0, tipLen, 1f, h, sun, branchA);
                Side(pts, count, len, w0, tipLen, -1f, h, sun, branchB);
                Band(count, 0, new Color(0f, 0f, 0f, strength * alpha * 0.8f), LShadow);
            }
            Color barkCol = Color.Lerp(Bark, DryBark, dry), litCol = Color.Lerp(BarkLit, DryLit, dry);
            Side(pts, count, len, w0, tipLen, 1.18f, 0f, sun, branchA);
            Side(pts, count, len, w0, tipLen, -1.18f, 0f, sun, branchB);
            Band(count, 0, Fade(BarkDark, alpha), layer);
            Side(pts, count, len, w0, tipLen, 1f, 0f, sun, branchA);
            Side(pts, count, len, w0, tipLen, -1f, 0f, sun, branchB);
            Band(count, 0, Fade(barkCol, alpha), layer + 0.001f);
            // Lit half: the side away from the shadow.
            Vector2 d = pts[count - 1] - pts[0];
            bool litLeft = (-d.y * -sun.x + d.x * -sun.y) > 0f;
            Side(pts, count, len, w0, tipLen, litLeft ? 0.9f : 0.05f, 0f, sun, branchA);
            Side(pts, count, len, w0, tipLen, litLeft ? 0.05f : -0.9f, 0f, sun, branchB);
            Band(count, 0, Fade(litCol, alpha), layer + 0.002f);
            // Grain: a thin dark line down the shaded half.
            Side(pts, count, len, w0, tipLen, litLeft ? -0.45f : 0.45f, 0f, sun, branchA);
            var grain = new Vector2[count];
            System.Array.Copy(branchA, grain, count);
            Trail(grain, Mathf.Max(0.012f, w0 * 0.12f), Fade(BarkDark, 0.8f * alpha), solid, layer + 0.003f);
            // Fresh pale wood on the point.
            int cut = -1;
            for (int i = 0; i < count; i++) if (acc[i] >= len - tipLen) { cut = i; break; }
            if (cut >= 0 && cut < count - 1)
            {
                int from = Mathf.Max(0, cut - 1);
                Side(pts, count, len, w0, tipLen, 0.8f, 0f, sun, branchA);
                Side(pts, count, len, w0, tipLen, -0.8f, 0f, sun, branchB);
                Band(count, from, Fade(Color.Lerp(Heart, litCol, dry), alpha), layer + 0.004f);
            }
        }

        private static float Width(int i, float len, float w0, float tipLen) =>
            w0 * (1f - 0.45f * acc[i] / len) * Mathf.Pow(Clamp01((len - acc[i]) / tipLen), 0.75f);

        private static void Side(Vector2[] pts, int count, float len, float w0, float tipLen, float f, float off, Vector2 sun, Vector2[] into)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 q = pts[Mathf.Min(count - 1, i + 1)], o = pts[Mathf.Max(0, i - 1)];
                Vector2 d = q - o;
                float l = d.magnitude;
                if (l < 1e-6f) l = 1f;
                float w = Width(i, len, w0, tipLen) * f / 2f;
                into[i] = new Vector2(pts[i].x - d.y / l * w + off * sun.x, pts[i].y + d.x / l * w + off * (sun.y - Lift));
            }
        }

        /// <summary>The ribbon between branchA and branchB from point <paramref name="from"/> to <paramref name="count"/>.</summary>
        private static void Band(int count, int from, Color colour, float altitude)
        {
            int n = count - from;
            if (n < 2) return;
            Sides(n, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < n; i++)
            {
                a[i] = branchA[from + i];
                b[i] = branchB[from + i];
            }
            Strip(a, b, colour, solid, altitude);
        }
    }
}
