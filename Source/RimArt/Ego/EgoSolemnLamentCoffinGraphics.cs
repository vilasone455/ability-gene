using UnityEngine;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using G = RimArt.EgoSolemnLamentGraphics;
using T = RimArt.EgoSolemnLamentTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Solemn Lament's coffin (the corrosion action, from the Funeral's breach and Limbus Skill 3): it rises
    /// out of the floor behind the wielder, its front facing the viewer at every aim (a fixed screen
    /// orientation) and 1.2 cells tall on screen, a little over the pawn's 1.17; a thin top face, a white
    /// edge, a shadow along the sun from its base. It rises by drawing only the part above the floor. The lid
    /// swings open on its right edge onto a lit white inside. Dust at its foot while it moves; the opening's
    /// flash, rays, floor glow, beams rising out of the floor and smoke puffs. Drawn under the pawn layer, so
    /// the wielder stands in front of it.
    /// </summary>
    public static class EgoSolemnLamentCoffinGraphics
    {
        private static readonly float Layer = G.PawnLayer - 0.03f;
        private const float Tau = 6.2831855f;

        // The frame's coffin for Edge: its foot, how deep it still is in the floor, the lid's hinge.
        private static Vector2 foot;
        private static float sunk, lid, hingeX;

        /// <summary>A point on the coffin's front <paramref name="h"/> up its height and <paramref name="w"/> across, as drawn above the floor.</summary>
        private static Vector2 Front(float h, float w) => new Vector2(foot.x + w, foot.y + (h - sunk) * G.Lift);

        /// <summary>
        /// The coffin with its foot at <paramref name="ground"/>; <paramref name="rise"/> 0 to 1 of it is above the
        /// floor and <paramref name="open"/> 0 to 1 is how far the lid is open. Returns the mouth (the lit inside,
        /// 62 % up, where the butterflies come out and go back in).
        /// </summary>
        public static EgoSolemnLamentPoint Coffin(Vector2 ground, float rise, float open, Vector2 sun, float strength)
        {
            foot = ground;
            sunk = (1f - rise) * T.CoffinH;
            lid = open;
            hingeX = ground.x + T.CoffinHalf(T.CoffinH * 0.7f);
            float vis = T.CoffinH - sunk;
            EgoSolemnLamentPoint mouth = T.Mouth(ground, rise);
            if (rise <= 0f) return mouth;
            float w0 = T.CoffinHalf(sunk);
            Sides(2, out Vector2[] sa, out Vector2[] sb);
            sa[0] = new Vector2(ground.x - w0, ground.y);
            sa[1] = new Vector2(ground.x - 0.22f + sun.x * vis, ground.y + sun.y * vis);
            sb[0] = new Vector2(ground.x + w0, ground.y);
            sb[1] = new Vector2(ground.x + 0.22f + sun.x * vis, ground.y + sun.y * vis);
            Strip(sa, sb, Fade(G.Ink, strength * 0.85f), solid, G.ShadowLayer);
            Vector2 top = Front(T.CoffinH, 0f), topFace = new Vector2(top.x, top.y + T.CoffinDepth / 2f);
            Sprite(topFace, 0.48f + 0.05f, T.CoffinDepth + 0.04f, G.CoffinEdge, solid, Layer);
            Sprite(topFace, 0.48f, T.CoffinDepth, G.CoffinTop, solid, Layer + 0.0005f);
            Face(-0.025f, false, G.CoffinEdge, Layer + 0.001f);
            Face(0f, false, G.Soot, Layer + 0.002f);
            if (open > 0f)
            {
                // The inside, lit: a pale lining and a white glow.
                Face(0.05f, false, Fade(G.Pale, 0.9f * open), Layer + 0.003f);
                Sprite(Front(T.CoffinH * 0.55f, 0f), 0.9f * open, 1.1f * open, Fade(G.White, 0.55f * open), glow, Layer + 0.004f);
            }
            // The lid: the front face, hinged on the right edge. Opening, it narrows toward the hinge and comes 0.1
            // toward the viewer. Its inner line and the butterfly emblem go with it.
            Face(-0.012f, true, G.CoffinEdge, Layer + 0.005f);
            Face(0.012f, true, G.Ink, Layer + 0.006f);
            Face(0.05f, true, Fade(G.Ash, 0.8f), Layer + 0.007f);
            Face(0.065f, true, G.Ink, Layer + 0.008f);
            if (vis > T.CoffinH * 0.3f)
            {
                Vector2 e = Hinge(Front(T.CoffinH * 0.66f, 0f));
                float squeeze = 1f - 0.85f * lid;
                DrawMesh(EgoSolemnLamentButterflies.Lines, e, Layer + 0.009f, 0.16f * squeeze, 0.16f, 0f, Fade(G.White, 0.85f), solid);
                DrawMesh(disc, e, Layer + 0.0095f, 0.018f * squeeze, 0.07f, 0f, Fade(G.White, 0.85f), solid);
            }
            return mouth;
        }

        /// <summary>Where a point of the front goes on the lid open by <see cref="lid"/>: squeezed toward the hinge and 0.12 east, 0.1 south.</summary>
        private static Vector2 Hinge(Vector2 q) =>
            new Vector2(hingeX + (q.x - hingeX) * (1f - 0.85f * lid) + lid * 0.12f, q.y - lid * 0.1f);

        /// <summary>
        /// The front's outline from the floor line to the top, <paramref name="inset"/> in from the edge (negative
        /// is out) at the floor line, the shoulders (0.7 up) and the top; on the lid when <paramref name="onLid"/>.
        /// </summary>
        private static void Face(float inset, bool onLid, Color colour, float altitude)
        {
            Sides(3, out Vector2[] a, out Vector2[] b);
            float shoulder = Mathf.Max(sunk, T.CoffinH * 0.7f);
            for (int i = 0; i < 3; i++)
            {
                float h = i == 0 ? sunk : i == 1 ? shoulder : T.CoffinH, half = T.CoffinHalf(h);
                a[i] = Front(h, -half + inset);
                b[i] = Front(h, half - inset);
                if (!onLid) continue;
                a[i] = Hinge(a[i]);
                b[i] = Hinge(b[i]);
            }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>Dust at the coffin's foot while it rises or sinks, <paramref name="u"/> 0 to 1 over that move: six puffs spreading 0.25 to 0.6 cells.</summary>
        public static void FootDust(Vector2 ground, float u)
        {
            if (u <= 0f || u >= 1f) return;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Tau + Rand(i + 500), r = 0.25f + 0.35f * u * (0.6f + 0.4f * Rand(i + 510)), size = 0.2f + 0.3f * u;
                Sprite(new Vector2(ground.x + Mathf.Cos(a) * r, ground.y + Mathf.Sin(a) * r * 0.5f + u * 0.12f), size, size * 0.8f,
                    Fade(G.Dust, 0.4f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), G.Puff, Overhead + 0.01f + i * 0.00001f);
            }
        }

        /// <summary>
        /// The opening, as Skill 3 draws it; <paramref name="age"/> is from <see cref="EgoSolemnLamentTiming.Open"/>.
        /// A white flash 3.2 cells wide on the coffin's face, gone in 0.35 s, with a bright core; twelve white rays
        /// out to 1 to 2 cells for 0.2 s; a white glow on the floor round the foot for 0.6 s; from 0.08 s twelve
        /// white beams 1.8 cells across shoot up out of the floor and past the top, each 0.8 to 1.6 long, rising
        /// 2.6 cells in 0.7 s, a white core 0.05 wide in a soft glow 0.24 wide; fourteen pale puffs pushed out
        /// from the mouth 0.6 to 1.6 cells over 0.8 s, growing 0.35 to 1.0, four of them rolling along the floor.
        /// </summary>
        public static void Opening(Vector2 ground, EgoSolemnLamentPoint mouth, float age)
        {
            if (age < 0f || age > 0.9f) return;
            var face = new Vector2(ground.x, ground.y + T.CoffinH * 0.5f * G.Lift);
            if (age < 0.35f)
            {
                float u = age / 0.35f, g = Mathf.Pow(1f - u, 1.5f);
                Sprite(face, 3.2f * (0.6f + 0.4f * u), 3f * (0.6f + 0.4f * u), Fade(G.White, 0.85f * g), glow, Overhead + 0.075f);
                Sprite(face, 1.2f, 1.4f, Fade(G.White, g), glow, Overhead + 0.076f);
            }
            if (age < 0.2f)
            {
                float u = age / 0.2f;
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 12f * Tau + Rand(i + 540) * 0.3f, l = (1f + Rand(i + 550)) * (0.4f + 0.6f * Mathf.Sqrt(u));
                    Streak(face, face + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * l, 0.06f, Fade(G.White, 0.9f * (1f - u)), whiteGlow, Overhead + 0.077f + i * 0.00001f, 4);
                }
            }
            if (age < 0.6f) Sprite(ground, 3f, 3f, Fade(G.White, 0.45f * (1f - age / 0.6f)), glow, Floor + 0.025f);
            for (int i = 0; i < 12; i++)
            {
                float a = age - 0.08f - Rand(i + 560) * 0.12f;
                if (a < 0f || a > 0.7f) continue;
                float u = a / 0.7f, x = ground.x + (i / 11f - 0.5f) * 1.8f + (Rand(i + 570) - 0.5f) * 0.08f, len = 0.8f + 0.8f * Rand(i + 580);
                float bottom = ground.y + (-0.1f + 2.6f * Mathf.Pow(u, 0.7f)) * G.Lift, top = bottom + len * G.Lift * Mathf.Min(1f, u * 5f), f = 1f - u * u;
                Sprite(new Vector2(x, (bottom + top) / 2f), 0.24f, (top - bottom) * 1.3f + 0.1f, Fade(G.White, 0.45f * f), glow, Overhead + 0.078f + i * 0.00001f);
                EgoSolemnLamentShotGraphics.Segment(new Vector2(x, bottom), new Vector2(x, top), 0.05f, Fade(G.White, f), solid, Overhead + 0.079f + i * 0.00001f, Taper.Both);
            }
            for (int i = 0; i < 14; i++)
            {
                float a = age - Rand(i + 590) * 0.08f;
                if (a < 0f || a > 0.8f) continue;
                float u = a / 0.8f, e = 1f - (1f - u) * (1f - u) * (1f - u);
                bool low = i < 4;
                float t = low ? (i < 2 ? Mathf.PI : 0f) + (Rand(i + 600) - 0.5f) * 0.8f : Rand(i + 610) * Tau, r = (0.6f + Rand(i + 620)) * e;
                Vector2 from = low ? ground : mouth.Screen;
                float size = 0.35f + 0.65f * e;
                // Raised 0.00005 over the sketch's altitudes: two of them tie with the flash and its core there, and the sketch draws them after.
                Sprite(new Vector2(from.x + Mathf.Cos(t) * r, from.y + Mathf.Sin(t) * r * (low ? 0.3f : 0.8f) + (low ? 0f : 0.2f * e)), size, size * 0.85f,
                    Fade(G.Pale, 0.75f * Mathf.Pow(1f - u, 1.2f)), G.Puff, Overhead + 0.07f + i * 0.0005f + 0.00005f);
            }
        }
    }
}
