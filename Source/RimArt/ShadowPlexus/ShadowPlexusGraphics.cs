using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.ShadowPlexusTiming;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by the Shadow plexus effects: the shadow line, a pool, the threads
    /// that climb a held body, shreds, dust, the range ring and the hand. The port of
    /// Tools/VfxLab/web/sketches/lib/shadow-plexus.js; its numbers are that file's. Everything lies
    /// on the floor except the threads and the shreds, which rise out of it, so it turns freely with
    /// the aim and there is no per-facing method. Every routine takes ages and amounts and keeps no
    /// state. The lib's stand-ins are not ported: the night overlay, the campfire, the pawns, the
    /// centipede, the pawns' cast shadows and the grenade blast.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ShadowPlexusGraphics
    {
        internal static readonly Color Shade = new Color(0.012f, 0.012f, 0.028f), Fringe = new Color(0.17f, 0.13f, 0.33f),
            RangeTint = new Color(0.74f, 0.70f, 0.92f), Dust = new Color(0.52f, 0.45f, 0.37f);
        /// <summary>The line's own layer: on the floor, over filth, under items and pawns.</summary>
        internal static readonly float LineLayer = Floor + 0.02f;

        /// <summary>Points in a full-length shadow line, and in a pool's rim.</summary>
        internal const int LinePoints = 29, PoolPoints = 25;

        private const float PalmLong = 0.22f, PalmWide = 0.19f, PalmBack = -0.04f, FingerWidth = 0.05f, Arch = 0.5f;
        // One finger: its knuckle on the palm (along, across), how far it spreads when flat (degrees)
        // and its length; the share of the length in each joint, and how far each joint folds at full curl.
        private static readonly float[] FingerAlong = { 0.1f, 0.16f, 0.16f, 0.1f, -0.08f }, FingerAcross = { -0.145f, -0.05f, 0.05f, 0.145f, 0.16f },
            FingerSpread = { -26f, -8f, 8f, 26f, 64f }, FingerLength = { 0.32f, 0.4f, 0.37f, 0.29f, 0.27f };
        private static readonly float[] FingerJoints = { 0.45f, 0.32f, 0.23f }, FingerFolds = { 40f, 65f, 45f };
        private static readonly float[] ThumbJoints = { 0.55f, 0.45f }, ThumbFolds = { 30f, 70f };

        private static readonly Vector2[] line = new Vector2[LinePoints], thread = new Vector2[9], shred = new Vector2[3];

        /// <summary>
        /// The shadow line: a black strip on the floor with a dim indigo fringe so it still reads on dark
        /// soil. Bulges travel along it toward the far end. <paramref name="flare"/> widens the first points
        /// where it leaves a body's shadow; the far end runs to a point unless <paramref name="point"/> is false.
        /// </summary>
        internal static void ShadowLine(Vector2[] points, float width, float alpha, float seconds, bool flare = true, bool point = true,
            bool pointStart = false, float? layer = null)
        {
            int n = points.Length - 1;
            if (n < 1 || alpha <= 0f || Vector2.Distance(points[n], points[0]) < 0.02f) return;
            float altitude = layer ?? LineLayer;
            for (int pass = 0; pass < 2; pass++)
            {
                Sides(n + 1, out Vector2[] a, out Vector2[] b);
                for (int i = 0; i <= n; i++)
                {
                    Vector2 run = points[Mathf.Min(n, i + 1)] - points[Mathf.Max(0, i - 1)];
                    float length = run.magnitude;
                    if (length < 1e-5f) length = 1f;
                    float w = width / 2f * (1f + 0.22f * Mathf.Sin(i * 0.9f - seconds * 7f));
                    if (flare) w *= 1f + 0.9f * Mathf.Clamp01(1f - i / 3f);
                    if (point) w *= Mathf.Clamp01((n - i) / 4f + 0.12f);
                    if (pointStart) w *= Mathf.Clamp01(i / 4f + 0.12f);
                    if (pass == 0) w += 0.035f;
                    var side = new Vector2(-run.y / length, run.x / length) * w;
                    a[i] = points[i] + side;
                    b[i] = points[i] - side;
                }
                if (pass == 0) Strip(a, b, Fade(Fringe, 0.5f * alpha), solid, altitude);
                else Strip(a, b, Fade(Shade, 0.94f * alpha), solid, altitude + 0.002f);
            }
        }

        /// <summary>A whole line from..to between the shares <paramref name="u0"/> and <paramref name="u1"/>, with the lib's sway.</summary>
        internal static void Line(Vector2 from, Vector2 to, float u0, float u1, float seconds, float sway, float width, float alpha = 1f,
            bool flare = true, bool point = true, bool pointStart = false)
        {
            T.Path(line, from, to, u0, u1, seconds, sway);
            ShadowLine(line, width, alpha, seconds, flare, point, pointStart);
        }

        /// <summary>
        /// A line that was cut at share <paramref name="cutU"/> of from..to, <paramref name="age"/> seconds
        /// ago: the near half runs back to its root, the far half runs on into the target, and shreds fly
        /// from the cut.
        /// </summary>
        internal static void BrokenLine(Vector2 from, Vector2 to, float cutU, float age, float width, float seconds, float sway)
        {
            float r = VfxMath.Smooth(age / T.SnapTime);
            if (r < 1f)
            {
                Line(from, to, 0f, cutU * (1f - r), seconds, sway, width);
                Line(from, to, cutU + (1f - cutU) * r, 1f, seconds, sway, width, flare: false, point: false, pointStart: true);
            }
            Shreds(T.PointOn(from, to, cutU), age, 9);
        }

        /// <summary>A pool of shadow under something: an uneven blob, flatter north-south, that keeps moving.</summary>
        internal static void Pool(Vector2 at, float radius, float alpha, float seconds, float? layer = null)
        {
            if (radius <= 0f || alpha <= 0f) return;
            float altitude = layer ?? LineLayer;
            for (int pass = 0; pass < 2; pass++)
            {
                float grow = pass == 0 ? 0.06f : 0f;
                Sides(PoolPoints, out Vector2[] centre, out Vector2[] rim);
                for (int i = 0; i < PoolPoints; i++)
                {
                    float turn = i / (float)(PoolPoints - 1) * Mathf.PI * 2f;
                    float r = (radius + grow) * (1f + 0.12f * Mathf.Sin(turn * 3f + seconds * 2f) + 0.07f * Mathf.Sin(turn * 5f - seconds * 3f));
                    centre[i] = at;
                    rim[i] = new Vector2(at.x + Mathf.Cos(turn) * r, at.y + Mathf.Sin(turn) * r * 0.62f);
                }
                if (pass == 0) Strip(centre, rim, Fade(Fringe, 0.5f * alpha), solid, altitude);
                else Strip(centre, rim, Fade(Shade, 0.94f * alpha), solid, altitude + 0.002f);
            }
        }

        /// <summary>Threads of shadow climbing a held body from the pool at its feet. <paramref name="amount"/> 0 to 1, <paramref name="reach"/> in cells up.</summary>
        internal static void Grip(Vector2 at, float amount, float seconds, int count = 4, float reach = 0.55f, float big = 1f)
        {
            if (amount <= 0f) return;
            for (int i = 0; i < count; i++)
            {
                float phase = i * Mathf.PI * 2f / count + seconds * 0.8f;
                for (int j = 0; j < thread.Length; j++)
                {
                    float h = amount * reach * j / 8f;
                    thread[j] = new Vector2(at.x + Mathf.Sin(phase + h * 6f) * 0.2f * big * (1f - h * 0.3f), at.y + 0.03f + h * SixPathsHeight.Lift);
                }
                PowerPoleGraphics.Tapered(thread, 0.075f * big, Fade(Shade, 0.92f), Overhead + 0.01f);
            }
        }

        /// <summary>Shreds of shadow flying off a cut, a released body or a double that ends. They lift a little and fade.</summary>
        internal static void Shreds(Vector2 at, float age, int count = 8, float life = 0.45f, float spread = 0.7f)
        {
            if (age < 0f || age > life) return;
            for (int i = 0; i < count; i++)
            {
                float u = age / (life * (0.6f + 0.4f * Rand(i + 5)));
                if (u > 1f) continue;
                float turn = i * 2.399f + Rand(i) * 0.9f, d = spread * (0.15f + u * (0.5f + Rand(i + 11) * 0.6f));
                float c = Mathf.Cos(turn), z = Mathf.Sin(turn) * 0.7f;
                var tip = new Vector2(at.x + c * d, at.y + z * d + u * 0.25f);
                var tail = new Vector2(tip.x - c * 0.2f, tip.y - z * 0.2f - 0.05f);
                shred[0] = tail;
                shred[1] = T.PointOn(tail, tip, 0.5f);
                shred[2] = tip;
                PowerPoleGraphics.Tapered(shred, 0.09f * (1f - u), Fade(Shade, 0.9f * (1f - u)), Overhead + 0.02f);
            }
        }

        /// <summary>Dust kicked up by a body pulled over the ground. <paramref name="age"/> in seconds.</summary>
        internal static void Scuff(Vector2 at, float age, float size = 1f)
        {
            if (age < 0f || age > 0.5f) return;
            float u = age / 0.5f;
            Sprite(new Vector2(at.x, at.y + 0.05f + u * 0.12f), (0.35f + u * 0.5f) * size, (0.25f + u * 0.35f) * size,
                Fade(Dust, 0.5f * Mathf.Sin(u * Mathf.PI)), PowerPoleGraphics.puff, Overhead + 0.005f);
        }

        /// <summary>The range ring at the rule's true radius (<see cref="ShadowPlexusTiming.Range"/>). One thin width whatever its radius.</summary>
        internal static void RangeRing(Vector2 at, float radius, float alpha) =>
            PaperBombGraphics.RingAt(at, radius, Fade(RangeTint, 0.6f * alpha), Floor + 0.012f);

        /// <summary>
        /// The hand, as the source draws its shadow hands: a flat black hand with a wrist, a palm, four
        /// jointed fingers and a thumb. Everything is laid out along the way the fingers point
        /// (<paramref name="degrees"/>, 0 east, 90 north) and across it. <paramref name="open"/> 0 to 1 grows
        /// the fingers out of the palm, spread wide. <paramref name="curl"/> 0 to 1 folds each finger at its
        /// joints up and back over what it holds and brings the fingers together; a folded joint is shorter
        /// on the ground and Arch of its height is drawn as a shift north. <paramref name="mirror"/> makes it
        /// the other hand. <paramref name="onto"/> puts the whole hand on one layer, for a hand lying on a body.
        /// </summary>
        internal static void Hand(Vector2 c, float degrees, float open, float curl, float size = 1f, bool mirror = false, float? onto = null)
        {
            if (open <= 0f) return;
            open *= size;
            Vector2 f = Turn(degrees);
            float side = mirror ? -1f : 1f;
            Vector2 Local(float along, float across) =>
                new Vector2(c.x + (along * f.x - across * side * f.y) * open, c.y + (along * f.y + across * side * f.x) * open);
            float floor = onto ?? LineLayer + 0.006f, over = onto.HasValue ? onto.Value + 0.002f : curl > 0.15f ? Overhead + 0.01f : LineLayer + 0.008f;

            for (int pass = 0; pass < 2; pass++)
            {
                float grow = pass == 0 ? 0.03f : 0f, up = pass == 0 ? 0f : 0.001f;
                Color colour = pass == 0 ? Fade(Fringe, 0.5f) : Fade(Shade, 0.95f);
                Sides(21, out Vector2[] centre, out Vector2[] rim);
                for (int i = 0; i <= 20; i++)
                {
                    float turn = i / 20f * Mathf.PI * 2f;
                    rim[i] = Local(PalmBack + (PalmLong + grow) * Mathf.Cos(turn), (PalmWide + grow) * Mathf.Sin(turn));
                    centre[i] = Local(PalmBack, 0f);
                }
                Strip(centre, rim, colour, solid, floor + up);
                Sides(2, out Vector2[] a, out Vector2[] b);
                a[0] = Local(-0.55f, 0.06f + grow); a[1] = Local(-0.18f, 0.12f + grow);
                b[0] = Local(-0.55f, -0.06f - grow); b[1] = Local(-0.18f, -0.12f - grow);
                Strip(a, b, colour, solid, floor + up);
            }

            Color edge = Fade(Fringe, 0.5f), body = Fade(Shade, 0.95f);
            for (int i = 0; i < FingerAlong.Length; i++)
            {
                bool thumb = i == FingerAlong.Length - 1;
                float[] joints = thumb ? ThumbJoints : FingerJoints, folds = thumb ? ThumbFolds : FingerFolds;
                Vector2 way = Turn(degrees + side * FingerSpread[i] * (1f - 0.75f * curl));
                float layer = over + i * 0.0006f, bend = 0f, h = 0f;
                Vector2 ground = Local(FingerAlong[i], FingerAcross[i]), from = ground;
                DrawMesh(disc, from, layer + 0.0003f, FingerWidth * size, FingerWidth * size, 0f, body, solid);
                for (int j = 0; j < joints.Length; j++)
                {
                    bend += folds[j] * curl * Mathf.Deg2Rad;
                    float len = FingerLength[i] * joints[j] * open, w = FingerWidth * size * (1f - j * 0.12f);
                    ground += way * (len * Mathf.Cos(bend));
                    h += len * Mathf.Sin(bend) * Arch;
                    var to = new Vector2(ground.x, ground.y + h * SixPathsHeight.Lift);
                    Vector2 mid = (from + to) / 2f, run = to - from;
                    float turn = -Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg, up = j * 0.0001f;
                    DrawMesh(MeshPool.plane10, mid, layer + up, run.magnitude + 0.05f, w * 2f + 0.05f, turn, edge, solid);
                    DrawMesh(disc, to, layer + up, w + 0.025f, w + 0.025f, 0f, edge, solid);
                    DrawMesh(MeshPool.plane10, mid, layer + 0.0003f + up, run.magnitude, w * 2f, turn, body, solid);
                    DrawMesh(disc, to, layer + 0.0003f + up, w, w, 0f, body, solid);
                    from = to;
                }
            }
        }
    }
}
