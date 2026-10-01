using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using G = RimArt.EgoSolemnLamentGraphics;
using T = RimArt.EgoSolemnLamentTiming;

namespace RimArt
{
    /// <summary>
    /// Solemn Lament's lace butterflies, as the Limbus frames draw them: a white outline and a web of white
    /// veins cutting each wing into small irregular cells, see-through between the lines; big rounded
    /// forewings, smaller hindwings, scalloped outer edges. A butterfly is four flat meshes built once (the
    /// wing fill, a wide outline for the additive glow, the veins, the edge with the antennae) and a
    /// white-edged body dash. The pale ones (the white shot's, The Living) are a faint film with a glow; the
    /// dark ones (the black shot's, The Departed) are near-opaque ink with grey veins and no glow, so they stay
    /// dark at normal zoom.
    ///
    /// Also here: a marked pawn's butterflies flying in and resting on the body, its stack pips over the head,
    /// and the Abnormality's face (a big butterfly, left wings white, right wings black) over a corroded head.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoSolemnLamentButterflies
    {
        // The wing outlines in unit size: the span is about 2 across, the body along +z (north = head). Each is
        // star-shaped from its root (its first point), so its fill is a fan from the root.
        private static readonly float[] ForeRaw = { .05f, .06f, .15f, .30f, .32f, .55f, .52f, .74f, .72f, .86f, .88f, .88f, .97f, .78f, .98f, .62f, .93f, .50f, .90f, .38f, .82f, .28f, .72f, .20f, .52f, .12f, .30f, .05f, .12f, .02f };
        private static readonly float[] HindRaw = { .05f, -.02f, .25f, -.02f, .48f, -.08f, .66f, -.20f, .74f, -.36f, .70f, -.52f, .58f, -.66f, .42f, -.74f, .28f, -.70f, .17f, -.56f, .10f, -.36f, .06f, -.16f };
        /// <summary>The veins run from the root to these outline points.</summary>
        private static readonly int[] ForeVeins = { 2, 3, 5, 7, 9, 11, 13 }, HindVeins = { 2, 4, 6, 8, 10 };
        /// <summary>Line widths in unit size (x 0.15 cells at the 0.3 span): the edge, a vein, the glow round the edge.</summary>
        private const float Outline = 0.065f, Vein = 0.04f, HaloW = 0.17f;

        internal static readonly Mesh Fill, Halo, Veins, Edge, Lines, FillL, FillR, HaloL, HaloR, LinesL, LinesR;
        /// <summary>Which counted stacks came from the black gun, for the pips. The swarm fills every slot, 38 at most.</summary>
        private static readonly bool[] darks = new bool[T.TorsoSlots + T.HeadSlots];

        static EgoSolemnLamentButterflies()
        {
            Vector2[] foreRaw = Points(ForeRaw), hindRaw = Points(HindRaw);
            Vector2[] fore = Scallop(foreRaw, 5, 11), hind = Scallop(hindRaw, 2, 9);
            int[] both = { 1, -1 }, right = { 1 }, left = { -1 };
            Fill = FillMesh("Solemn Lament lace fill", both, fore, hind);
            Halo = LaceMesh("Solemn Lament lace halo", both, fore, hind, foreRaw, hindRaw, HaloW, 0f, 0f);
            Veins = LaceMesh("Solemn Lament lace veins", both, fore, hind, foreRaw, hindRaw, 0f, Vein, 0f);
            Edge = LaceMesh("Solemn Lament lace edge", both, fore, hind, foreRaw, hindRaw, Outline, 0f, Vein);
            Lines = LaceMesh("Solemn Lament lace", both, fore, hind, foreRaw, hindRaw, Outline, Vein, Vein);
            FillL = FillMesh("Solemn Lament lace fill l", left, fore, hind);
            FillR = FillMesh("Solemn Lament lace fill r", right, fore, hind);
            HaloL = LaceMesh("Solemn Lament lace halo l", left, fore, hind, foreRaw, hindRaw, HaloW, 0f, 0f);
            HaloR = LaceMesh("Solemn Lament lace halo r", right, fore, hind, foreRaw, hindRaw, HaloW, 0f, 0f);
            LinesL = LaceMesh("Solemn Lament lace l", left, fore, hind, foreRaw, hindRaw, Outline, Vein, Vein);
            LinesR = LaceMesh("Solemn Lament lace r", right, fore, hind, foreRaw, hindRaw, Outline, Vein, Vein);
        }

        /// <summary>
        /// One butterfly at drawn point <paramref name="q"/>, <paramref name="size"/> cells across, its head
        /// toward <paramref name="heading"/> degrees (0 east, 90 north), its wings at <paramref name="flap"/> of
        /// the full span (the beat scales it across the body).
        /// </summary>
        internal static void Butterfly(Vector2 q, float size, float heading, float flap, bool dark, float alpha, float layer)
        {
            if (alpha <= 0.01f) return;
            float rot = 90f - heading, sx = size * 0.5f * flap, sz = size * 0.5f;
            DrawMesh(Fill, q, layer, sx, sz, rot, dark ? Fade(G.Ink, 0.94f * alpha) : Fade(G.Pale, 0.22f * alpha), solid);
            if (!dark) DrawMesh(Halo, q, layer + 0.0002f, sx, sz, rot, Fade(G.White, 0.14f * alpha), whiteGlow);
            DrawMesh(Veins, q, layer + 0.0003f, sx, sz, rot, Fade(dark ? G.Ash : G.White, (dark ? 0.75f : 0.95f) * alpha), solid);
            DrawMesh(Edge, q, layer + 0.0004f, sx, sz, rot, Fade(G.White, (dark ? 0.85f : 0.95f) * alpha), solid);
            DrawMesh(disc, q, layer + 0.0006f, size * 0.04f, size * 0.17f, rot, Fade(G.White, alpha), solid);
            DrawMesh(disc, q, layer + 0.0007f, size * 0.018f, size * 0.13f, rot, Fade(G.Soot, alpha), solid);
        }

        /// <summary>A flying butterfly's shadow: from its ground point <paramref name="g"/> along the sun by its height.</summary>
        internal static void Shadow(Vector2 g, float h, float size, Vector2 sun, float strength, float alpha = 1f) =>
            Sprite(g + sun * h, size * 0.7f, size * 0.45f, Fade(G.Ink, strength * 0.45f * alpha), soft, G.ShadowLayer);

        /// <summary>
        /// A marked pawn's butterflies and pips. <paramref name="pos"/> is where the pawn is drawn now,
        /// <paramref name="fall"/> how far it has fallen (0 to 1) and <paramref name="turn"/> the degrees it has
        /// turned: resting butterflies sit on fixed points of the body and turn with it. A flying one lands in
        /// its flight time, eased out, and turns to its resting heading over the last 15 %; resting ones beat
        /// 0.7 times a second and draw over the hits, flying ones over those. The pips fade as the pawn falls.
        /// <paramref name="shift"/> raises this pawn's pieces a little over another's at the same index.
        /// </summary>
        public static void DrawMark(EgoSolemnLamentMark mark, float s, Vector2 pos, float fall, float turn, float span, Map map, float shift = 0f)
        {
            if (!Shown(pos, map)) return;
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            for (int i = 0; i < mark.Flights.Count; i++)
            {
                EgoSolemnLamentFlight e = mark.Flights[i];
                float age = s - e.Launch;
                if (age < 0f) continue;
                Vector2 slot = T.SlotAt(pos, e.Slot, turn, out float slotHeading);
                float gz = Mathf.Lerp(mark.Home.y + PawnBody.Ground, slot.y, fall), size = span * (e.Swarm ? 0.9f : 1f), u = age / e.Fly;
                var toGround = new Vector2(slot.x, gz);
                float toHeight = (slot.y - gz) / G.Lift;
                if (u < 1f)
                {
                    EgoSolemnLamentPoint q = T.FlyAt(e.FromGround, e.FromHeight, e.Via, e.Arc, e.Seed, u, toGround, toHeight);
                    float heading = G.Heading(q, T.FlyAt(e.FromGround, e.FromHeight, e.Via, e.Arc, e.Seed, Mathf.Min(1f, u + 0.03f), toGround, toHeight));
                    float fade = e.Swarm ? Mathf.Clamp01(u / 0.25f) : 1f;
                    Shadow(q.Ground, Mathf.Max(0f, q.Height), size, sun, strength, fade);
                    Butterfly(q.Screen, size, u > 0.85f ? Mathf.Lerp(heading, slotHeading, (u - 0.85f) / 0.15f) : heading, T.FlapAt(s, 3f, 0.15f, i), e.Dark, fade,
                        Overhead + 0.14f + i * 0.0008f + shift);
                }
                else
                    Butterfly(slot, size, slotHeading + 8f * Mathf.Sin(s * 1.3f + i), T.FlapAt(s, 0.7f, 0.55f, i), e.Dark, 1f, Overhead + 0.1f + i * 0.0008f + shift);
            }
            if (mark.Flights.Count > 0 && fall < 0.6f) Pips(mark, s, new Vector2(pos.x, pos.y + PawnBody.HeadTop), 1f - fall / 0.6f, Overhead + 0.2f + shift);
        }

        /// <summary>The stack count 0.2 over the head top: one pip per stack of the cap, 0.1 apart at most, each filled in the colour of the butterfly that made it.</summary>
        private static void Pips(EgoSolemnLamentMark mark, float s, Vector2 top, float alpha, float layer)
        {
            int cap = mark.Cap, filled = 0;
            foreach (EgoSolemnLamentFlight f in mark.Flights)
                if (!f.Swarm && f.Count <= s && filled < darks.Length) darks[filled++] = f.Dark;
            float pitch = Mathf.Min(0.1f, 1.1f / cap), x0 = top.x - pitch * (cap - 1) / 2f, z = top.y + 0.2f, r = pitch * 0.38f;
            for (int i = 0; i < cap; i++)
            {
                bool has = i < filled;
                var at = new Vector2(x0 + i * pitch, z);
                DrawMesh(disc, at, layer, r + 0.012f, r + 0.012f, 0f, has ? Fade(G.White, 0.9f * alpha) : Fade(G.Ink, 0.35f * alpha), solid);
                if (has) DrawMesh(disc, at, layer + 0.001f, r, r, 0f, Fade(darks[i] ? G.Ink : G.Pale, alpha), solid);
            }
        }

        /// <summary>
        /// The Abnormality's face over the corroded wielder's head (<paramref name="head"/>, its centre): a
        /// butterfly 0.66 across, left wings pale, right wings ink, beating 0.6 times a second and bobbing
        /// 0.02, a dim halo behind. Drawn 0.04 over the pawn layer.
        /// </summary>
        internal static void Face(Vector2 head, float s, float alpha)
        {
            if (alpha <= 0f) return;
            float size = T.FaceSpan, flap = 0.75f + 0.25f * Mathf.Abs(Mathf.Cos(Mathf.PI * T.FaceBeat * s)), sx = size * 0.5f * flap, sz = size * 0.5f;
            float L = G.PawnLayer + 0.04f;
            var q = new Vector2(head.x, head.y + 0.02f * Mathf.Sin(s * 2f));
            Sprite(q, 0.9f, 0.7f, Fade(G.Smoke, 0.25f * alpha), glow, L - 0.001f);
            DrawMesh(FillL, q, L, sx, sz, 0f, Fade(G.Pale, 0.55f * alpha), solid);
            DrawMesh(FillR, q, L + 0.00005f, sx, sz, 0f, Fade(G.Ink, 0.9f * alpha), solid);
            DrawMesh(HaloL, q, L + 0.0002f, sx, sz, 0f, Fade(G.White, 0.14f * alpha), whiteGlow);
            DrawMesh(HaloR, q, L + 0.00025f, sx, sz, 0f, Fade(G.White, 0.14f * alpha), whiteGlow);
            DrawMesh(LinesL, q, L + 0.0004f, sx, sz, 0f, Fade(G.White, alpha), solid);
            DrawMesh(LinesR, q, L + 0.00045f, sx, sz, 0f, Fade(G.White, alpha), solid);
            DrawMesh(disc, q, L + 0.0006f, size * 0.04f, size * 0.17f, 0f, Fade(G.White, alpha), solid);
            DrawMesh(disc, q, L + 0.0007f, size * 0.018f, size * 0.13f, 0f, Fade(G.Soot, alpha), solid);
        }

        private static Vector2[] Points(float[] xz)
        {
            var pts = new Vector2[xz.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
            return pts;
        }

        /// <summary>The scallops: between outline points a..b a midpoint pulled 7 % toward the root.</summary>
        private static Vector2[] Scallop(Vector2[] w, int a, int b)
        {
            var outline = new List<Vector2>();
            Vector2 root = w[0];
            for (int i = 0; i < w.Length; i++)
            {
                outline.Add(w[i]);
                if (i >= a && i < b) outline.Add(root + ((w[i] + w[i + 1]) / 2f - root) * 0.93f);
            }
            return outline.ToArray();
        }

        private static Vector2 Mirror(Vector2 p, int sign) => new Vector2(sign * p.x, p.y);

        private static Vector2 Norm(Vector2 v)
        {
            float l = v.magnitude;
            return l > 0f ? v / l : v;
        }

        /// <summary>The wing fill: each wing a fan from its root.</summary>
        private static Mesh FillMesh(string name, int[] sides, Vector2[] fore, Vector2[] hind)
        {
            var b = new Builder();
            foreach (int sign in sides)
                foreach (Vector2[] wing in new[] { fore, hind })
                {
                    int first = b.Count;
                    foreach (Vector2 p in wing) b.Add(Mirror(p, sign));
                    for (int i = 1; i < wing.Length - 1; i++) b.Tri(first, first + i, first + i + 1);
                }
            return b.Build(name);
        }

        /// <summary>
        /// The lines: the closed outlines (mitred corners, the mitre capped at 2x), the veins, the antennae; a
        /// width of 0 leaves that part out. Veins run straight from the root to the vein points; in each gap
        /// between two neighbouring veins three or four cross veins sit at uneven heights, each end at its own
        /// height and most bent at a middle point, so the cells come out uneven, like the source's lace.
        /// </summary>
        private static Mesh LaceMesh(string name, int[] sides, Vector2[] fore, Vector2[] hind, Vector2[] foreRaw, Vector2[] hindRaw,
            float outline, float veins, float antennae)
        {
            var b = new Builder();
            foreach (int sign in sides)
            {
                if (outline > 0f)
                {
                    b.Ring(fore, sign, outline);
                    b.Ring(hind, sign, outline);
                }
                if (veins > 0f)
                {
                    VeinSet(b, foreRaw, ForeVeins, 1, sign, veins);
                    VeinSet(b, hindRaw, HindVeins, 2, sign, veins);
                }
                if (antennae > 0f)
                {
                    b.Quad(new Vector2(sign * 0.03f, 0.22f), new Vector2(sign * 0.16f, 0.5f), antennae);
                    b.Quad(new Vector2(sign * 0.15f, 0.48f), new Vector2(sign * 0.2f, 0.56f), antennae * 2.2f);
                }
            }
            return b.Build(name);
        }

        private static void VeinSet(Builder b, Vector2[] w, int[] ids, int seed, int sign, float width)
        {
            Vector2 root = Mirror(w[0], sign);
            Vector2 Along(int j, float f) => root + (Mirror(w[ids[j]], sign) - root) * f;
            for (int j = 0; j < ids.Length; j++) b.Quad(Along(j, 0.15f), Along(j, 0.98f), width);
            for (int j = 0; j + 1 < ids.Length; j++)
            {
                int n = 3 + (Rand(seed * 31 + j) > 0.5f ? 1 : 0);
                for (int k = 0; k < n; k++)
                {
                    float f = 0.28f + (k + 0.5f) / n * 0.66f;
                    float r1 = Rand(seed * 97 + j * 7 + k), r2 = Rand(seed * 89 + j * 5 + k + 40), r3 = Rand(seed * 83 + j * 3 + k + 80);
                    Vector2 a = Along(j, f + (r1 - 0.5f) * 0.16f), c = Along(j + 1, f + (r2 - 0.5f) * 0.16f);
                    if (r3 < 0.35f)
                    {
                        b.Quad(a, c, width);
                        continue;
                    }
                    Vector2 mid = (a + c) / 2f;
                    mid += Norm(mid - root) * ((r3 - 0.67f) * 0.12f);
                    b.Quad(a, mid, width);
                    b.Quad(mid, c, width);
                }
            }
        }

        /// <summary>A flat mesh of triangles each written clockwise on screen (north up), so none is culled as a back face.</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();

            public int Count => vertices.Count;

            public int Add(Vector2 p)
            {
                vertices.Add(new Vector3(p.x, 0f, p.y));
                return vertices.Count - 1;
            }

            public void Tri(int a, int b, int c)
            {
                Vector3 p = vertices[a], q = vertices[b], r = vertices[c];
                // Positive is counter-clockwise with north up, which is the back side.
                bool back = (q.x - p.x) * (r.z - p.z) - (q.z - p.z) * (r.x - p.x) > 0f;
                triangles.Add(a);
                triangles.Add(back ? c : b);
                triangles.Add(back ? b : c);
            }

            /// <summary>A straight line <paramref name="width"/> wide from a to b.</summary>
            public void Quad(Vector2 a, Vector2 b, float width)
            {
                Vector2 d = b - a;
                float l = d.magnitude;
                if (l <= 0f) l = 1f;
                var n = new Vector2(-d.y / l * width / 2f, d.x / l * width / 2f);
                int first = Count;
                Add(a + n);
                Add(a - n);
                Add(b - n);
                Add(b + n);
                Tri(first, first + 1, first + 2);
                Tri(first, first + 2, first + 3);
            }

            /// <summary>A closed outline <paramref name="width"/> wide through <paramref name="pts"/>, mirrored by <paramref name="sign"/>, mitred at the corners.</summary>
            public void Ring(Vector2[] pts, int sign, float width)
            {
                int count = pts.Length, first = Count;
                for (int i = 0; i < count; i++)
                {
                    Vector2 p = Mirror(pts[(i - 1 + count) % count], sign), q = Mirror(pts[i], sign), r = Mirror(pts[(i + 1) % count], sign);
                    Vector2 n1 = Norm(new Vector2(-(q.y - p.y), q.x - p.x)), n2 = Norm(new Vector2(-(r.y - q.y), r.x - q.x)), m = Norm(n1 + n2);
                    float k = width / 2f / Mathf.Max(0.5f, m.x * n1.x + m.y * n1.y);
                    Add(q + m * k);
                    Add(q - m * k);
                }
                for (int i = 0; i < count; i++)
                {
                    int a = first + i * 2, c = first + (i + 1) % count * 2;
                    Tri(a, a + 1, c + 1);
                    Tri(a, c + 1, c);
                }
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
