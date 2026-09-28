using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by the Vergil effects: a cut, a curved cut, the ball of cut space and
    /// a glint. The port of the drawing half of Tools/VfxLab/web/sketches/lib/vergil.js; its numbers
    /// are that file's.
    ///
    /// A cut is light, not a solid: a dark slit on the floor side, a wide additive glow and a thin
    /// white core. Blue-white with a dark blue slit, so it is not read as Six Paths violet, Flying
    /// Thunder God yellow or Anchor blue. Everything here is a level circle or a flat line lying at
    /// one height, so it turns freely with the aim and there is no per-facing method.
    ///
    /// The afterimage and the cut across a pawn's chest are here too: they are light drawn where a pawn
    /// is, not pawns. The carrier and the held katana are the real pawn in game (Kit/YamatoDraw.cs).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VergilGraphics
    {
        internal static readonly Color Blue = new Color(0.25f, 0.5f, 1f), Deep = new Color(0.05f, 0.12f, 0.5f),
            Ice = new Color(0.78f, 0.9f, 1f), Void = new Color(0.01f, 0.015f, 0.07f),
            Snow = new Color(1f, 1f, 1f), Grit = new Color(0.52f, 0.45f, 0.37f);

        /// <summary>
        /// The ball is two round textures rather than a flat disc and a ring: it needs a soft edge, and
        /// the shell has to be clear at the centre and bright at the rim. Both are written by
        /// make_vergil_textures.py from the lab stand-ins the sketch was tuned with.
        /// </summary>
        internal static readonly Material ball = MaterialPool.MatFrom("RimArt/Vergil/Ball", ShaderDatabase.Transparent);
        internal static readonly Material shell = MaterialPool.MatFrom("RimArt/Vergil/Shell", ShaderDatabase.MoteGlow);

        /// <summary>The pawn layer and the shadow layer, as the lab's pawnLayer and shadowLayer.</summary>
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        /// <summary>Where a blade outline turns: its root, its middle, where the point starts, and the tip.</summary>
        private static readonly float[] TaperStops = { 0f, 0.35f, 0.7f, 0.88f, 1f };

        /// <summary>
        /// A cut hanging in the air from <paramref name="a"/> to <paramref name="b"/>, drawn up to the
        /// share <paramref name="u"/> of its length. <paramref name="hot"/> turns the whole thing white,
        /// which is the draw and the click.
        /// </summary>
        internal static void Cut(Vector2 a, Vector2 b, float u, float alpha, float width, float hot = 0f, float flicker = 1f)
        {
            if (u <= 0f || alpha <= 0f) return;
            Vector2 tip = a + (b - a) * u;
            Streak(a, tip, width * 2.2f, Fade(Void, 0.7f * alpha * (1f - hot)), solid, Overhead + 0.03f, 10);
            Streak(a, tip, width * (7f + 4f * hot), Fade(Color.Lerp(Blue, Ice, hot), (0.34f + 0.2f * hot) * alpha * flicker), whiteGlow, Overhead + 0.031f, 10);
            Streak(a, tip, width * (1f + 0.8f * hot), Fade(Snow, alpha * flicker), whiteGlow, Overhead + 0.032f, 10);
        }

        /// <summary>
        /// A cut along a curve, drawn up to the share <paramref name="u"/> of its length and thin at
        /// both ends. The same three layers as <see cref="Cut"/>.
        /// </summary>
        internal static void ArcCut(Vector2[] points, float u, float alpha, float width, float hot = 0f)
        {
            if (u <= 0f || alpha <= 0f || points.Length < 2) return;
            float upto = u * (points.Length - 1);
            int whole = Mathf.FloorToInt(upto);
            float part = upto - whole;
            bool partial = part > 0.001f && whole + 1 < points.Length;
            int count = whole + 1 + (partial ? 1 : 0);
            if (count < 2) return;
            Vector2[] shown = GokuGraphics.Points(count);
            for (int i = 0; i <= whole; i++) shown[i] = points[i];
            if (partial) shown[count - 1] = points[whole] + (points[whole + 1] - points[whole]) * part;
            GokuGraphics.Line(shown, width * 2.4f, Fade(Void, 0.75f * alpha * (1f - hot)), solid, Overhead + 0.03f, GokuGraphics.Taper.Both);
            GokuGraphics.Line(shown, width * (5f + 2f * hot), Fade(Color.Lerp(Blue, Ice, hot), (0.4f + 0.2f * hot) * alpha), whiteGlow, Overhead + 0.031f, GokuGraphics.Taper.Both);
            GokuGraphics.Line(shown, width * (1.2f + 0.8f * hot), Fade(Snow, alpha), whiteGlow, Overhead + 0.032f, GokuGraphics.Taper.Both);
        }

        /// <summary>
        /// The ball of cut space seen from above: a glow, a dark inside with a soft edge, a blue shell
        /// that brightens toward the rim and beats, a pale patch on the side toward the light, and a
        /// thin rim. <paramref name="live"/> fades the whole thing; <paramref name="dark"/> is how much
        /// the inside hides, and 0 leaves only the light.
        /// </summary>
        internal static void Sphere(Vector2 centre, float radius, float seconds, float live, float dark = 0.45f)
        {
            if (radius <= 0f || live <= 0f) return;
            float beat = 0.5f + 0.5f * Mathf.Sin(seconds * 42f), d = radius * 2f;
            Sprite(centre, d * 1.7f, d * 1.7f, Fade(Blue, 0.28f * live), glow, Overhead + 0.015f);
            if (dark > 0f)
            {
                Sprite(centre, d, d, Fade(Void, dark * live), ball, Overhead + 0.016f);
                Sprite(centre, d * 0.7f, d * 0.7f, Fade(Void, dark * 1.2f * live), soft, Overhead + 0.0165f);
            }
            Sprite(centre, d * 1.02f, d * 1.02f, Fade(Blue, 0.95f * live), shell, Overhead + 0.017f);
            Sprite(centre, d * 1.02f, d * 1.02f, Fade(Ice, 0.45f * beat * live), shell, Overhead + 0.0175f);
            Sprite(new Vector2(centre.x - radius * 0.4f, centre.y + radius * 0.45f), radius * 0.9f, radius * 0.7f,
                Fade(Ice, 0.22f * live), glow, Overhead + 0.018f);
            PaperBombGraphics.RingAt(centre, radius, Fade(Ice, (0.45f + 0.4f * beat) * live), Overhead + 0.019f, false, whiteGlow);
        }

        /// <summary>
        /// A blade outline from <paramref name="root"/> along <paramref name="d"/>: parallel edges for the
        /// first 70 % of its length, then a point. The lib's tapered(), which every sword in the kit uses.
        /// </summary>
        internal static void Tapered(Vector2 root, Vector2 d, float length, float width, Color colour, Material material, float altitude)
        {
            if (length <= 0f || colour.a <= 0.001f) return;
            Sides(TaperStops.Length, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < TaperStops.Length; i++)
            {
                float u = TaperStops[i], w = width / 2f * (u <= 0.7f ? 1f : (1f - u) / 0.3f) + 0.004f;
                Vector2 at = root + d * (length * u), side = new Vector2(-d.y, d.x) * w;
                a[i] = at + side;
                b[i] = at - side;
            }
            Strip(a, b, colour, material, altitude);
        }

        /// <summary>
        /// A short cut across a pawn's chest when a hit lands: light, 0.14 s, and a spark. The lib's hitCut.
        /// <paramref name="victim"/> is the pawn's ground point (its feet), as in the sketches.
        /// </summary>
        internal static void HitCut(Vector2 victim, float deg, float age, float life = 0.14f)
        {
            if (age < 0f || age >= life) return;
            float u = age / life;
            var mid = new Vector2(victim.x, victim.y + 0.32f);
            Vector2 half = Turn(deg) * 0.62f;
            Cut(mid - half, mid + half, Mathf.Clamp01(u * 3f), 1f - u * u, 0.025f, 0.5f * (1f - u));
            Sprite(mid, 0.5f * (1f - u) + 0.25f, 0.5f * (1f - u) + 0.25f, Fade(Ice, 0.5f * (1f - u)), glow, Overhead + 0.04f);
        }

        /// <summary>The ellipses of the lab's stand-in body: (east, north, half width, half height) from the feet.</summary>
        private static readonly float[] GhostParts =
        {
            0f, 0.18f, 0.22f, 0.32f,
            0.03f, 0.22f, 0.08f, 0.24f,
            0f, 0.58f, 0.16f, 0.17f,
            0f, 0.69f, 0.19f, 0.1f,
        };

        /// <summary>
        /// Vergil seen for a moment behind a dash, or at the end of a Judgement Cut End chord: a blue silhouette
        /// of the stand-in's ellipses with one thin line of light tapering back toward <paramref name="from"/>. It
        /// is light, not a solid: no shadow and no weapon. The lib's afterimage; <paramref name="pos"/> is the feet.
        /// </summary>
        internal static void Afterimage(Vector2 pos, Vector2 from, float age, float life = 0.22f)
        {
            if (age < 0f || age >= life) return;
            float f = 1f - age / life;
            Vector2 away = pos - from;
            float d = away.magnitude;
            Vector2 back = d > 1e-4f ? away / d : Vector2.up;
            Vector2[] trail = GokuGraphics.Points(2);
            trail[0] = new Vector2(pos.x, pos.y + 0.3f);
            trail[1] = trail[0] - back * 0.95f;
            GokuGraphics.Line(trail, 0.18f, Fade(Blue, 0.55f * f * f), whiteGlow, Overhead + 0.02f, GokuGraphics.Taper.End);
            for (int k = 0; k < GhostParts.Length / 4; k++)
            {
                Color colour = Color.Lerp(k == 1 ? Blue : k == 3 ? Ice : Deep, Ice, 0.3f);
                DrawMesh(disc, new Vector2(pos.x + GhostParts[k * 4], pos.y + GhostParts[k * 4 + 1]), Overhead + 0.021f + k * 0.001f,
                    GhostParts[k * 4 + 2], GhostParts[k * 4 + 3], 0f, Fade(colour, 0.62f * f * f), solid);
            }
        }

        /// <summary>A four-point glint: a soft core and two crossed rays. The lib's, and the Goku kit's.</summary>
        internal static void Glint(Vector2 at, float size, float alpha, Color colour, float turn = 0f)
        {
            if (alpha <= 0f || size <= 0f) return;
            Sprite(at, size * 0.9f, size * 0.9f, Fade(colour, alpha), glow, Overhead + 0.06f);
            for (int i = 0; i < 2; i++)
            {
                Vector2 ray = Turn(i * 90f + turn) * (size * (i == 0 ? 1f : 0.7f));
                Streak(at - ray, at + ray, size * 0.16f, Fade(colour, alpha), whiteGlow, Overhead + 0.061f, 4);
            }
        }
    }
}
