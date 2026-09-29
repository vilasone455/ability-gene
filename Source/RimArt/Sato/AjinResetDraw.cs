using System;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// The parts of the lab's lib/ajin.js that the Reset and Headshot Reset pictures use: the black matter's shard
    /// flakes, the line of flakes thrown off an edge, the seep from a wound, the timer ring, the cover over a pawn, the
    /// shell in his shape, the severed hand, straight bars and drawn limbs, and the kit's colours. Everything lies flat
    /// on the screen and is a function of the time it is given; the only state is VfxDraw's strip pool.
    ///
    /// Draws the lab makes at one altitude in call order get their own small offsets here (0.00002 apart), because two
    /// draws at one altitude have no set order in Unity.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class AjinResetDraw
    {
        // lib/ajin.js colours.
        internal static readonly Color Ghost = new Color(0.17f, 0.17f, 0.20f), GhostEdge = new Color(0.03f, 0.03f, 0.04f),
            GhostLit = new Color(0.29f, 0.29f, 0.33f), Wrap = new Color(0.50f, 0.50f, 0.55f), Flake = new Color(0.045f, 0.045f, 0.055f);
        internal static readonly Color Skin = new Color(0.83f, 0.70f, 0.54f), Shirt = new Color(0.86f, 0.86f, 0.83f),
            Blood = new Color(0.45f, 0.05f, 0.05f), Slash = new Color(0.90f, 0.90f, 0.95f), Outline = new Color(0.10f, 0.08f, 0.07f),
            TimerColour = new Color(0.85f, 0.85f, 0.9f);

        /// <summary>Screen cells per cell of height for a pawn-sized figure (lib/ajin.js Stand).</summary>
        internal const float Stand = 0.75f;
        /// <summary>The shell's rows run from Lo to Hi cells north of the pawn's draw position (lib/ajin.js Shell).</summary>
        internal const float ShellLo = -0.55f, ShellHi = 0.67f;
        /// <summary>
        /// The body angle of the lab's downed layout (head west). PawnRenderer turns a lying pawn clockwise by its body
        /// angle, so the head points along (sin a, cos a); the lying layouts below are written at this angle and turned by
        /// the real angle minus this.
        /// </summary>
        internal const float LabDownedAngle = 270f;
        private const float Tau = Mathf.PI * 2f;

        internal static readonly Material Puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), LyingLayer = AltitudeLayer.LayingPawn.AltitudeFor(),
            ItemLayer = AltitudeLayer.Item.AltitudeFor();
        /// <summary>
        /// Just over the top of a real pawn's render tree, standing and lying: the game draws a pawn's parts up to
        /// 100 x 0.000366 = 0.0366 above its altitude (PawnRenderUtility.AltitudeForLayer). What the sketch lays on the
        /// stand-in at Pawn + a small offset is drawn at these + the same offset.
        /// </summary>
        internal static readonly float OnPawn = PawnLayer + 0.037f, OnLyingPawn = LyingLayer + 0.037f;
        /// <summary>Under the lowest part of a standing pawn's render tree (-10 x 0.000366).</summary>
        internal static readonly float BehindPawn = PawnLayer - 0.004f;

        private static readonly Mesh[] shards = MakeShards();
        private static readonly Mesh timerBand = Ring(0.9f, "Ajin reset timer ring");
        private static readonly Vector2[] three = new Vector2[3], five = new Vector2[5];

        /// <summary>
        /// The lab's rand(i), the sine hash, in double as there. The sketches call it with fractional arguments
        /// (i * 5.7 + 1), so it takes a double; for whole numbers it equals VfxMath.Rand.
        /// </summary>
        internal static float R(double i)
        {
            double n = Math.Sin(i * 127.1 + 17) * 43758.5453;
            return (float)(n - Math.Floor(n));
        }

        /// <summary>JavaScript's Math.round (halves go up), not Unity's banker's rounding.</summary>
        internal static int Round(float v) => Mathf.FloorToInt(v + 0.5f);

        // Six irregular shard outlines (3 or 4 corners), built once as lib/ajin.js does.
        private static Mesh[] MakeShards()
        {
            var meshes = new Mesh[6];
            for (int k = 0; k < 6; k++)
            {
                int n = 3 + k % 2;
                var v = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    double a = (double)i / n * Tau + R(k * 11 + i) * 0.9, r = 0.55 + R(k * 7 + i + 3) * 0.45;
                    v[i] = new Vector3((float)(Math.Cos(a) * r), 0f, (float)(Math.Sin(a) * r * (0.5 + R(k + 40) * 0.5)));
                }
                int[] tri = n == 3 ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                // Clockwise seen from above, so the shard survives backface culling.
                for (int t = 0; t < tri.Length; t += 3)
                {
                    Vector3 p = v[tri[t]], q = v[tri[t + 1]], s = v[tri[t + 2]];
                    if ((q.x - p.x) * (s.z - p.z) - (q.z - p.z) * (s.x - p.x) > 0f) (tri[t + 1], tri[t + 2]) = (tri[t + 2], tri[t + 1]);
                }
                var mesh = new Mesh { name = "Ajin shard " + k, vertices = v, triangles = tri };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                meshes[k] = mesh;
            }
            return meshes;
        }

        internal static void Shard(int i, Vector2 at, float layer, float size, float rot, Color colour) =>
            DrawMesh(shards[i % 6], at, layer, size, size, rot, colour, solid);

        /// <summary>
        /// A line of flakes on a level edge at height <paramref name="h"/> (cells up from <paramref name="feet"/>),
        /// <paramref name="width"/> cells wide, thrown upward <paramref name="rise"/>: the forming or peeling edge
        /// (lib/ajin.js edgeFlakes). Every flake respawns each life at a place picked by hash, so it is a function of t.
        /// </summary>
        internal static void EdgeFlakes(Vector2 feet, float h, float width, float t, float amount, float rise, float layer, float alpha = 1f)
        {
            int n = Round(26f * amount);
            for (int i = 0; i < n; i++)
            {
                double life = 0.45 + R(i * 5.7 + 1) * 0.35, ph = t / life + R(i * 2.9 + 4), cyc = Math.Floor(ph);
                float u = (float)(ph - cyc);
                double sd = i * 97 + cyc * 29 + 5;
                float u0 = (R(sd) - 0.5f) * width, lift = rise * (0.5f + R(sd + 1) * 0.7f) * u, side = (R(sd + 2) - 0.5f) * 0.5f * u;
                var at = new Vector2(feet.x + u0 + side, feet.y + (h + lift) * Stand);
                float size = (0.05f + 0.06f * R(sd + 3)) * (1f - u * 0.5f);
                Shard(i + 3, at, layer + i * 0.0002f, size, R(sd + 4) * 360f + u * 300f, Fade(Flake, (1f - u) * alpha));
                if (i % 3 == 0)
                    Sprite(at, 0.26f * (1f - u * 0.3f), 0.20f * (1f - u * 0.3f), Fade(Flake, (1f - u) * 0.38f * alpha), Puff, layer - 0.001f);
            }
        }

        /// <summary>
        /// Black matter seeping from a wound or a piece at <paramref name="at"/>: flakes and three smoke puffs rising,
        /// <paramref name="k"/> how strong (lib/ajin.js seep). <paramref name="seed"/> is the length of the sketch's key
        /// for that seep ("reset wound 0" is 13), which offsets the puffs' cycle.
        /// </summary>
        internal static void Seep(Vector2 at, float t, float k, float amount, int seed)
        {
            if (k <= 0f) return;
            EdgeFlakes(at, 0f, 0.16f, t, 0.45f * amount * k, 0.35f, Overhead + 0.03f);
            for (int i = 0; i < 3; i++)
            {
                double c = t * 1.6 + R(i + seed);
                float u = (float)(c - Math.Floor(c));
                Sprite(new Vector2(at.x + (R(i + 5) - 0.5f) * 0.12f, at.y + u * 0.22f), 0.14f * (1f + u), 0.11f * (1f + u),
                    Fade(Flake, (1f - u) * 0.45f * k), Puff, Overhead + 0.004f + i * 0.0003f);
            }
        }

        /// <summary>The Reset timer on the floor round <paramref name="c"/>: a faint full ring and the part filled so far, clockwise from the top.</summary>
        internal static void TimerRing(Vector2 c, float r, float frac, float fade)
        {
            DrawMesh(timerBand, c, Floor + 0.02f, r + 0.02f, r + 0.02f, 0f, Fade(TimerColour, 0.18f * fade), solid);
            Arc(c, r, frac, 0.045f, Fade(TimerColour, 0.8f * fade), Floor + 0.025f);
        }

        private static void Arc(Vector2 c, float r, float frac, float w, Color colour, float layer)
        {
            if (frac <= 0.005f || colour.a <= 0.001f) return;
            int n = Mathf.Max(2, Round(48f * frac));
            Sides(n + 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                float ang = Mathf.PI / 2f - frac * Tau * i / n;
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                a[i] = c + d * (r + w / 2f);
                b[i] = c + d * (r - w / 2f);
            }
            Strip(a, b, colour, solid, layer);
        }

        /// <summary>An ellipse in a layout: centre and radii across (x) and up the screen (z) before any turn.</summary>
        internal struct Oval
        {
            public Vector2 c;
            public float rx, rz;
            public Oval(float x, float z, float rx, float rz) { c = new Vector2(x, z); this.rx = rx; this.rz = rz; }
        }

        /// <summary>
        /// Black matter over a pawn (lib/ajin.js coverStandIn): dark ovals over the body <paramref name="B"/> and head
        /// <paramref name="H"/>, then five light bands across the body's long axis (up the screen while lying at 270,
        /// across once standing: <paramref name="k"/> 0 or 1). body and head are how covered each is, grow the cover's
        /// size against the ovals. B and H are in <paramref name="f"/>'s layout, so a lying cover turns with the body.
        /// </summary>
        internal static void Cover(AjinFrame f, Oval B, Oval H, float k, float body, float head, float grow, float layer)
        {
            if (body > 0.01f)
            {
                DrawMesh(disc, f.P(B.c), layer, (B.rx + 0.04f) * grow, (B.rz + 0.04f) * grow, f.turn, Fade(GhostEdge, body), solid);
                DrawMesh(disc, f.P(B.c), layer + 0.00002f, B.rx * grow, B.rz * grow, f.turn, Fade(Ghost, body), solid);
            }
            if (head > 0.01f)
            {
                DrawMesh(disc, f.P(H.c), layer + 0.00004f, (H.rx + 0.035f) * grow, (H.rz + 0.035f) * grow, f.turn, Fade(GhostEdge, head), solid);
                DrawMesh(disc, f.P(H.c), layer + 0.00006f, H.rx * grow, H.rz * grow, f.turn, Fade(Ghost, head), solid);
            }
            if (body <= 0.01f) return;
            for (int j = 0; j < 5; j++)
            {
                float s = (j + 0.5f) / 5f * 2f - 1f;
                float cx = B.c.x + s * B.rx * 0.75f * (1f - k), cz = B.c.y + s * B.rz * 0.75f * k;
                float w = (k > 0f ? B.rx : B.rz) * 0.8f * grow, hx = k * w, hz = (1f - k) * w;
                three[0] = f.P(cx - hx, cz - hz);
                three[1] = f.P(cx + (1f - k) * 0.02f, cz - k * 0.02f);
                three[2] = f.P(cx + hx, cz + hz);
                Trail(three, 0.032f, Fade(Wrap, 0.55f * body), layer + 0.00008f + j * 0.00002f);
            }
        }

        private static float ShellWidth(float z)
        {
            float b = (z + 0.096f) / 0.45f, h = (z - 0.424f) / 0.25f;
            float wb = Mathf.Abs(b) < 1f ? 0.31f * Mathf.Sqrt(1f - b * b) : 0f, wh = Mathf.Abs(h) < 1f ? 0.235f * Mathf.Sqrt(1f - h * h) : 0f;
            return Mathf.Max(wb, Mathf.Max(wh, 0.03f));
        }

        /// <summary>
        /// A standing pawn's outline filled with black matter from its feet up to <paramref name="hi"/> cells north of
        /// <paramref name="pos"/> (lib/ajin.js shell): edge, body, the lit east side, and light bands every 0.11 cells.
        /// </summary>
        internal static void Shell(Vector2 pos, float hi, float layer, float alpha = 1f)
        {
            float top = Mathf.Min(hi, ShellHi);
            if (top <= ShellLo + 0.01f) return;
            const int n = 22;
            Sides(n + 1, out Vector2[] a, out Vector2[] b);
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i <= n; i++)
                {
                    float z = Mathf.Lerp(ShellLo, top, i / (float)n), w = ShellWidth(z);
                    float left = pos.x - w, right = pos.x + w;
                    if (pass == 0) { left -= 0.03f; right += 0.03f; }
                    else if (pass == 2) left = Mathf.Lerp(left, right, 0.58f);
                    a[i] = new Vector2(left, pos.y + z);
                    b[i] = new Vector2(right, pos.y + z);
                }
                Color colour = pass == 0 ? Fade(GhostEdge, alpha) : pass == 1 ? Fade(Ghost, alpha) : Fade(GhostLit, alpha * 0.8f);
                Strip(a, b, colour, solid, layer + pass * 0.00002f);
            }
            int j = 0;
            for (float z = ShellLo + 0.08f; z < top - 0.03f; z += 0.11f, j++)
            {
                float w = ShellWidth(z) * 0.92f;
                three[0] = new Vector2(pos.x - w, pos.y + z);
                three[1] = new Vector2(pos.x, pos.y + z - 0.03f);
                three[2] = new Vector2(pos.x + w, pos.y + z);
                Trail(three, 0.034f, Fade(Wrap, 0.6f * alpha), layer + 0.00006f + j * 0.00001f);
            }
        }

        /// <summary>
        /// Ooze strands pouring from <paramref name="from"/> up into a shell whose top is <paramref name="top"/>: five
        /// wavy strands fanning out 0.45 cells (the at-anchor sketch's strands).
        /// </summary>
        internal static void Strands(Vector2 from, float top, float t, float alpha, float layer)
        {
            for (int i = 0; i < 5; i++)
            {
                float x0 = from.x + (R(i + 90) - 0.5f) * 0.12f, x1 = from.x + (i / 4f - 0.5f) * 0.45f;
                for (int q = 0; q < 5; q++)
                {
                    float u = q / 4f;
                    five[q] = new Vector2(Mathf.Lerp(x0, x1, u) + Mathf.Sin(u * 6f + t * 8f + i) * 0.02f, Mathf.Lerp(from.y - 0.02f, top, u));
                }
                Trail(five, 0.06f, Fade(Flake, 0.85f * alpha), layer + i * 0.00002f);
            }
        }

        /// <summary>
        /// A severed hand (lib/ajin.js hand): a red stump, the palm, four finger nubs and a thumb. <paramref name="rot"/>
        /// is the way the fingers point, degrees anticlockwise from east.
        /// </summary>
        internal static void Hand(Vector2 pos, float rot, float layer, float alpha)
        {
            float a = rot * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector2 At(float x, float z) => new Vector2(pos.x + x * c - z * s, pos.y + x * s + z * c);
            DrawMesh(disc, At(-0.075f, 0f), layer, 0.04f, 0.045f, -rot, Fade(Blood, alpha), solid);
            DrawMesh(disc, pos, layer + 0.0005f, 0.075f, 0.05f, -rot, Fade(Skin, alpha), solid);
            for (int i = 0; i < 4; i++)
                DrawMesh(disc, At(0.085f, (i - 1.5f) * 0.024f), layer + 0.001f + i * 0.00002f, 0.032f, 0.011f, -rot, Fade(Skin, alpha), solid);
            DrawMesh(disc, At(0.02f, 0.058f), layer + 0.00109f, 0.026f, 0.012f, -rot + 40f, Fade(Skin, alpha), solid);
        }

        /// <summary>A straight bar <paramref name="w"/> cells wide from a to b (lib/ajin.js bar).</summary>
        internal static void Bar(Vector2 a, Vector2 b, float w, Color colour, float layer)
        {
            if (colour.a <= 0.001f) return;
            Vector2 d = b - a;
            float l = d.magnitude;
            if (l == 0f) l = 1f;
            var n = new Vector2(-d.y / l * w / 2f, d.x / l * w / 2f);
            Sides(2, out Vector2[] p, out Vector2[] q);
            p[0] = a + n; p[1] = b + n;
            q[0] = a - n; q[1] = b - n;
            Strip(p, q, colour, solid, layer);
        }

        /// <summary>
        /// A drawn limb from a (sleeve colour) to b (hand colour) with a dark outline, the colour changing 62 % of the
        /// way along (lib/ajin.js limbSeg).
        /// </summary>
        internal static void LimbSeg(Vector2 a, Vector2 b, float w, Color colour, Color tip, float layer, float alpha = 1f)
        {
            Vector2 m = Vector2.Lerp(a, b, 0.62f);
            Bar(a, b, w + 0.03f, Fade(Outline, alpha), layer);
            Bar(a, m, w, Fade(colour, alpha), layer + 0.00002f);
            Bar(m, b, w * 0.9f, Fade(tip, alpha), layer + 0.00004f);
        }

        /// <summary>The lab's trail(): a band along the points, widest in the middle (sin), zero at both ends.</summary>
        internal static void Trail(Vector2[] pts, float width, Color colour, float layer)
        {
            if (colour.a <= 0.001f) return;
            int n = pts.Length;
            Sides(n, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)];
                float len = d.magnitude;
                if (len == 0f) len = 1f;
                float w = Mathf.Max(0f, Mathf.Sin(i / (float)(n - 1) * Mathf.PI)) * width / 2f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, solid, layer);
        }
    }

    /// <summary>
    /// A layout turned clockwise (seen from above, as Unity and PawnRenderer turn) by <see cref="turn"/> degrees about
    /// <see cref="origin"/>. A standing layout has turn 0; a lying one is written at the lab's downed angle (270, head
    /// west) and turned by the real body angle minus 270.
    /// </summary>
    internal readonly struct AjinFrame
    {
        public readonly Vector2 origin;
        public readonly float turn;
        private readonly float cos, sin;

        public AjinFrame(Vector2 origin, float turn)
        {
            this.origin = origin;
            this.turn = turn;
            cos = Mathf.Cos(turn * Mathf.Deg2Rad);
            sin = Mathf.Sin(turn * Mathf.Deg2Rad);
        }

        public Vector2 P(float x, float z) => new Vector2(origin.x + x * cos + z * sin, origin.y - x * sin + z * cos);
        public Vector2 P(Vector2 local) => P(local.x, local.y);

        /// <summary>A lying body drawn by PawnRenderer at <paramref name="bodyAngle"/> round <paramref name="body"/>.</summary>
        public static AjinFrame Lying(Vector3 body, float bodyAngle) =>
            new AjinFrame(new Vector2(body.x, body.z), bodyAngle - AjinResetDraw.LabDownedAngle);

        public static AjinFrame Standing(Vector3 pawn) => new AjinFrame(new Vector2(pawn.x, pawn.z), 0f);
    }
}
