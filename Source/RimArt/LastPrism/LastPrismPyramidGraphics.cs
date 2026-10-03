using UnityEngine;
using Verse;
using static RimArt.LastPrismGraphics;
using static RimArt.VfxDraw;
using T = RimArt.LastPrismTiming;

namespace RimArt
{
    /// <summary>
    /// The weapon itself, for <see cref="LastPrismGraphics.Draw"/>: a pyramid lying level at chest height, tip at the
    /// target, base toward the wielder, drawn from Terraria's sprite (checked 2026-10-01). Its five corners are placed
    /// in 3D (east, up, north) each frame and height is drawn as <see cref="SixPathsHeight.Lift"/> cells north. Only
    /// the faces turned to the viewer are drawn: they never overlap, so there is no sorting, and the prism turns with
    /// the aim without a per-facing method (aimed east or west its outline stays a triangle and the faces slide past,
    /// as the sprite's do). The three long faces keep the sprite's pale, lavender and slate blue and are lit by the
    /// sun, the base is violet, ridges between two shown faces are pale and the outline is navy. Also the glint that
    /// runs over it in sun and the small rainbow it throws on the floor.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class LastPrismPyramidGraphics
    {
        private static readonly Color FacePale = new Color(0.95f, 0.95f, 1f), FaceLavender = new Color(0.80f, 0.70f, 0.98f), FaceSlate = new Color(0.46f, 0.56f, 0.80f);
        private static readonly Color FaceBase = new Color(0.45f, 0.32f, 0.72f), Navy = new Color(0.12f, 0.12f, 0.34f), FaceDark = new Color(0.20f, 0.18f, 0.40f);
        private static readonly Color Ridge = new Color(0.94f, 0.95f, 1f), Dull = new Color(0.45f, 0.5f, 0.56f);
        // The faces as corner indices (3 is the tip) with their colours: three long faces, then the base.
        private static readonly int[,] Faces = { { 3, 0, 1 }, { 3, 1, 2 }, { 3, 2, 0 }, { 0, 2, 1 } };
        private static readonly Color[] FaceColour = { FacePale, FaceLavender, FaceSlate, FaceBase };
        private static readonly Vector3[] corner = new Vector3[4];
        // The edges of the shown faces in the order first met, and how many shown faces share each (2 = a ridge).
        private static readonly int[] edgeA = new int[6], edgeB = new int[6], edgeCount = new int[6];
        private const float Lift = SixPathsHeight.Lift;

        private static Vector2 Screen(Vector3 q) => new Vector2(q.x, q.z + q.y * Lift);

        /// <summary>
        /// The prism with its base centre at <paramref name="basePt"/> (a drawn point), pointing along <paramref name="aim"/>
        /// (radians) and rolled <paramref name="turns"/> about its long axis. <paramref name="power"/> 0 to 1 is the charge:
        /// the faces shimmer pink and green and the glow inside brightens. <paramref name="dull"/>: under a roof, greyed and unlit.
        /// </summary>
        internal static void Draw(Vector2 basePt, float aim, float turns, Vector2 sun, float layer, bool dull, float power, float s)
        {
            float ca = Mathf.Cos(aim), sa = Mathf.Sin(aim), spin = (turns - Mathf.Floor(turns)) * Tau;
            for (int k = 0; k < 3; k++)
            {
                float a = spin + k * Tau / 3f, across = T.PrismRad * Mathf.Cos(a);
                corner[k] = new Vector3(basePt.x - across * sa, T.PrismRad * Mathf.Sin(a), basePt.y + across * ca);
            }
            corner[3] = new Vector3(basePt.x + T.PrismLen * ca, 0f, basePt.y + T.PrismLen * sa);
            Vector3 mid = (corner[0] + corner[1] + corner[2] + corner[3]) / 4f;
            Vector3 toSun = new Vector3(-sun.x, 1f, -sun.y).normalized;
            int edges = 0;
            for (int f = 0; f < 4; f++)
            {
                int i = Faces[f, 0], j = Faces[f, 1], k = Faces[f, 2];
                Vector3 n = Vector3.Cross(corner[j] - corner[i], corner[k] - corner[i]);
                if (Vector3.Dot(n, (corner[i] + corner[j] + corner[k]) / 3f - mid) < 0f) n = -n;
                // Shown when its outward normal points at the viewer, (0, 1, -Lift): up, or south.
                if (n.y - Lift * n.z <= 0f) continue;
                float lit = Mathf.Max(0f, Vector3.Dot(n, toSun) / n.magnitude);
                Color colour = Color.Lerp(FaceDark, FaceColour[f], 0.45f + 0.55f * lit);
                if (power > 0f) colour = Color.Lerp(colour, Hue(s * 0.9f + f / 3f, 0.5f), 0.3f * power);
                if (dull) colour = Color.Lerp(colour, Dull, 0.7f);
                // A triangle as a strip whose second side is one point twice; the strip writes it clockwise on screen.
                Sides(2, out Vector2[] sideA, out Vector2[] sideB);
                sideA[0] = Screen(corner[i]);
                sideA[1] = Screen(corner[j]);
                sideB[0] = sideB[1] = Screen(corner[k]);
                Strip(sideA, sideB, Fade(colour, 0.94f), solid, layer + f * 0.0003f);
                AddEdge(ref edges, i, j);
                AddEdge(ref edges, j, k);
                AddEdge(ref edges, k, i);
            }
            if (!dull)
            {
                Vector2 c = Screen(mid);
                float core = 0.18f + 0.12f * power;
                Sprite(c, 0.5f, 0.5f, Fade(Hue(s * 0.3f, 0.6f), 0.1f + 0.25f * power), glow, layer + 0.0015f);
                Sprite(c, core, core, Fade(White, 0.15f + 0.35f * power), glow, layer + 0.0016f);
            }
            for (int e = 0; e < edges; e++)
            {
                Vector2 a = Screen(corner[edgeA[e]]), b = Screen(corner[edgeB[e]]), run = b - a;
                bool ridge = edgeCount[e] > 1;
                Color colour = ridge ? Fade(dull ? Dull : Ridge, 0.8f) : Fade(Navy, 0.9f);
                // Each edge its own small step, so overlapping ends keep the sketch's order in Unity.
                ChainSickleGraphics.Rect((a + b) / 2f, run.magnitude + 0.02f, ridge ? 0.02f : 0.03f, Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg, colour,
                    layer + 0.002f + (ridge ? 0f : 0.0005f) + e * 0.00005f);
            }
        }

        // Counts an edge, lower corner first, in the order the sketch's Map meets it.
        private static void AddEdge(ref int edges, int a, int b)
        {
            int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            for (int e = 0; e < edges; e++)
                if (edgeA[e] == lo && edgeB[e] == hi)
                {
                    edgeCount[e]++;
                    return;
                }
            edgeA[edges] = lo;
            edgeB[edges] = hi;
            edgeCount[edges++] = 1;
        }

        /// <summary>A glint that runs over the idle prism in sun, 0.25 s in every 1.25 s, <paramref name="light"/> 0 to 1 with the sky.</summary>
        internal static void Glint(Vector2 midS, float s, float light)
        {
            float v = s * 0.8f % 1f;
            GokuGraphics.Glint(new Vector2(midS.x, midS.y + T.PrismRad * Lift * 0.8f), 0.22f, 0.9f * ChainSickleGraphics.Bump(v / 0.25f) * light, White, 20f);
        }

        /// <summary>The spectrum the idle prism throws on the floor in sun at <paramref name="c"/>, where its shadow falls: six stripes red to violet along the sun, shimmering.</summary>
        internal static void Rainbow(Vector2 c, Vector2 sun, float amount, float s)
        {
            if (amount <= 0f) return;
            float l = sun.magnitude;
            if (l == 0f) l = 1f;
            float ux = sun.x / l, uz = sun.y / l, degrees = Mathf.Atan2(uz, ux) * Mathf.Rad2Deg;
            for (int k = 0; k < T.Beams; k++)
            {
                float off = (k - 2.5f) * 0.07f + 0.012f * Mathf.Sin(s * 2.4f + k);
                Sprite(new Vector2(c.x - uz * off + ux * 0.3f, c.y + ux * off + uz * 0.3f), 0.9f, 0.11f, Fade(Hue(k / (float)T.Beams, 0.9f), 0.5f * amount), glow,
                    Floor + 0.015f + k * 0.0005f, -degrees);
            }
        }
    }
}
