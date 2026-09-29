using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by the Pain kit's pictures (Banshō Ten'in, Black Receiver): the port of
    /// Tools/VfxLab/web/sketches/lib/pain.js; its numbers are that file's.
    ///
    /// Used in the game: the kit colours, <see cref="Arm"/> (Pain's arm, drawn by the ability over the real pawn),
    /// <see cref="Poly"/> and <see cref="Place"/>. Used only by previews (the lab's stand-ins): <see cref="Pain"/>,
    /// <see cref="PainDown"/>, <see cref="Standing"/> and <see cref="Lying"/>.
    ///
    /// A ground point is a pawn's feet: the stand-in's body disc is centred <see cref="BodyZ"/> (0.18) north of it and
    /// its head 0.58 north. Height is drawn as a shift north of Lift (0.6) cells per cell up. Everything is a disc, a
    /// quad or a flat strip, so nothing has a per-facing method, and nothing keeps state. Strips are VfxDraw's: the
    /// caller runs VfxDraw.Begin first. Taken from elsewhere rather than copied: the lib's line is GokuGraphics.Line,
    /// its rect is ChainSickleGraphics.Rect, sprite and band are VfxDraw.Sprite and VfxDraw.Strip.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PainGraphics
    {
        // The kit's colours: Gravity Well's black core and pale blue rim, Shinra Tensei's blue-white.
        public static readonly Color Core = new Color(0.015f, 0.015f, 0.035f), PaleBlue = new Color(0.85f, 0.94f, 1f),
            DustC = new Color(0.76f, 0.70f, 0.59f);
        public static readonly Color Cloak = new Color(0.07f, 0.065f, 0.085f), Cloud = new Color(0.74f, 0.1f, 0.12f),
            CloudEdge = new Color(0.95f, 0.93f, 0.9f), Hair = new Color(0.93f, 0.46f, 0.16f), Cuff = new Color(0.22f, 0.21f, 0.24f);
        /// <summary>Stand-in skin and a hostile stand-in's body (lib/flying-thunder-god.js Skin and EnemyColour).</summary>
        public static readonly Color Skin = new Color(0.83f, 0.70f, 0.54f), EnemyColour = new Color(0.55f, 0.38f, 0.27f);

        /// <summary>A stand-in pawn's body centre, cells north of its ground point on screen.</summary>
        public const float BodyZ = 0.18f;
        /// <summary>Pain's arm: shoulder and hand height (cells up), and how far the hand goes out (cells).</summary>
        public const float ShoulderH = 0.52f, HandH = 0.56f, Reach = 0.48f;
        public const float Lift = SixPathsHeight.Lift;

        public static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor(), PawnLayer = AltitudeLayer.Pawn.AltitudeFor(),
            LyingLayer = AltitudeLayer.LayingPawn.AltitudeFor();

        // Pain's two red clouds: offset x, offset z, radius x, radius z.
        private static readonly float[,] Clouds = { { -0.08f, 0.1f, 0.07f, 0.045f }, { 0.075f, 0.28f, 0.06f, 0.04f } };
        // The fingers: angle as a share of the spread, length. The thumb out to one side, the middle fingers longest.
        private static readonly float[,] Fingers = { { -1.2f, 0.09f }, { -0.5f, 0.125f }, { -0.17f, 0.14f }, { 0.17f, 0.13f }, { 0.5f, 0.105f } };

        /// <summary>
        /// A point <paramref name="along"/> cells along the unit <paramref name="aim"/> from <paramref name="ground"/>,
        /// <paramref name="across"/> cells to its left, <paramref name="h"/> cells up, as drawn (the lab's frame().place).
        /// </summary>
        public static Vector2 Place(Vector2 ground, Vector2 aim, float along, float across, float h = 0f) =>
            new Vector2(ground.x + along * aim.x - across * aim.y, ground.y + along * aim.y + across * aim.x + h * Lift);

        /// <summary>
        /// A filled polygon through the first <paramref name="count"/> points of <paramref name="pts"/>, fanned from the
        /// first point. One strip whose one side is the first point, so each triangle is turned to face the camera.
        /// </summary>
        public static void Poly(Vector2[] pts, int count, Color colour, float altitude, Material material = null)
        {
            if (count < 3 || colour.a <= 0.001f) return;
            Sides(count - 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < count - 1; i++)
            {
                a[i] = pts[0];
                b[i] = pts[i + 1];
            }
            Strip(a, b, colour, material ?? solid, altitude);
        }

        private static void Disc(float x, float z, float altitude, float rx, float rz, float degrees, Color colour) =>
            DrawMesh(disc, new Vector2(x, z), altitude, rx, rz, degrees, colour, solid);

        private static void Line2(Vector2 a, Vector2 b, float width, Color colour, float altitude)
        {
            Vector2[] pts = GokuGraphics.Points(2);
            pts[0] = a;
            pts[1] = b;
            GokuGraphics.Line(pts, width, colour, solid, altitude, GokuGraphics.Taper.None);
        }

        /// <summary>
        /// Pain's arm, drawn by the ability: a sleeve in the cloak's colour that narrows from the shoulder (0.13 across)
        /// to the wrist (0.084), a grey cuff, and an open hand, a palm with the thumb and four fingers spread 64 degrees
        /// at the target. <paramref name="grip"/> 0..1 closes the fingers to 30 degrees and 0.6 of their length (round
        /// a head, or round a rod). <paramref name="shoulder"/> and <paramref name="hand"/> are drawn points (height
        /// already shifted north); <paramref name="dir"/> is the unit way the fingers point. The hand lies level at its
        /// height, so it turns with the aim. Drawn 0.01 over the pawn layer, so over the real pawn.
        /// </summary>
        public static void Arm(Vector2 shoulder, Vector2 hand, Vector2 dir, float grip)
        {
            Vector2 d = hand - shoulder;
            float L = d.magnitude;
            if (L <= 0f) L = 1f;
            Sleeve(shoulder, hand - new Vector2(d.x / L, d.y / L) * 0.045f, PawnLayer + 0.01f);
            float baseDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, spread = Mathf.Lerp(64f, 30f, grip), reach = Mathf.Lerp(1f, 0.6f, grip);
            for (int i = 0; i < 5; i++)
            {
                float ang = (baseDeg + Fingers[i, 0] * spread) * Mathf.Deg2Rad;
                var c = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 root = hand + c * 0.04f;
                Line2(root, root + c * (Fingers[i, 1] * reach), 0.034f, Skin, PawnLayer + 0.0106f);
            }
            Disc(hand.x, hand.y, PawnLayer + 0.0108f, 0.062f, 0.062f, 0f, Skin);
        }

        /// <summary>
        /// <see cref="Arm"/>'s sleeve and grey cuff alone, from <paramref name="shoulder"/> to <paramref name="wrist"/>:
        /// the strip at <paramref name="altitude"/>, the cuff 0.0002 over it. Shinra Tensei draws it under Melee
        /// Animation's hands (<see cref="ShinraSleeves"/>).
        /// </summary>
        public static void Sleeve(Vector2 shoulder, Vector2 wrist, float altitude)
        {
            Vector2 d = wrist - shoulder;
            float L = d.magnitude;
            if (L <= 0f) L = 1f;
            var u = new Vector2(d.x / L, d.y / L);
            var p = new Vector2(-u.y, u.x);
            const float w0 = 0.065f, w1 = 0.042f;
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = shoulder + p * w0; a[1] = wrist + p * w1;
            b[0] = shoulder - p * w0; b[1] = wrist - p * w1;
            Strip(a, b, Cloak, solid, altitude);
            ChainSickleGraphics.Rect(wrist, 0.03f, w1 * 2.3f, Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg, Cuff, altitude + 0.0002f);
        }

        // ------------------------------------------------------------------ stand-ins (previews only)

        /// <summary>Stand-in: Pain, the two-disc pawn in the black cloak with two red clouds and spiky orange hair.</summary>
        public static void Pain(Vector2 g, Vector2 sun, float strength)
        {
            Sprite(g + sun * 0.45f, 0.85f, 0.4f, Fade(Ink, strength), soft, ShadowLayer);
            Disc(g.x, g.y + BodyZ, PawnLayer, 0.22f, 0.32f, 0f, Cloak);
            for (int i = 0; i < 2; i++)
            {
                float x = g.x + Clouds[i, 0], z = g.y + Clouds[i, 1];
                Disc(x, z, PawnLayer + 0.0005f + i * 0.0002f, Clouds[i, 2] + 0.016f, Clouds[i, 3] + 0.016f, 0f, CloudEdge);
                Disc(x, z, PawnLayer + 0.0006f + i * 0.0002f, Clouds[i, 2], Clouds[i, 3], 0f, Cloud);
            }
            Disc(g.x, g.y + 0.58f, PawnLayer + 0.002f, 0.16f, 0.17f, 0f, Skin);
            for (int i = 0; i < 5; i++)
            {
                float deg = -60f + i * 30f, a = deg * Mathf.Deg2Rad;
                Disc(g.x + Mathf.Sin(a) * 0.14f, g.y + 0.67f + Mathf.Cos(a) * 0.1f, PawnLayer + 0.003f, 0.045f, 0.08f, deg, Hair);
            }
            Disc(g.x, g.y + 0.69f, PawnLayer + 0.0032f, 0.16f, 0.085f, 0f, Hair);
        }

        /// <summary>Stand-in: Pain downed, on the floor with his head toward the unit <paramref name="toward"/>, the cloak and one cloud showing, hair round the head.</summary>
        public static void PainDown(Vector2 g, Vector2 toward, Vector2 sun, float strength)
        {
            Sprite(g + sun * 0.15f, 1f, 0.5f, Fade(Ink, strength), soft, ShadowLayer);
            var c = new Vector2(g.x, g.y + 0.08f);
            float deg = Mathf.Atan2(toward.x, toward.y) * Mathf.Rad2Deg;
            var p = new Vector2(-toward.y, toward.x);
            Line2(c + toward * 0.18f + p * 0.12f, c + toward * 0.05f + p * 0.3f, 0.07f, Cloak, LyingLayer - 0.001f);
            Disc(c.x, c.y, LyingLayer, 0.21f, 0.34f, deg, Cloak);
            Vector2 cloud = c - toward * 0.06f + p * 0.05f;
            Disc(cloud.x, cloud.y, LyingLayer + 0.0005f, 0.07f, 0.05f, deg, CloudEdge);
            Disc(cloud.x, cloud.y, LyingLayer + 0.0006f, 0.055f, 0.038f, deg, Cloud);
            Vector2 head = c + toward * 0.4f;
            float at = Mathf.Atan2(toward.y, toward.x);
            for (int i = 0; i < 5; i++)
            {
                float a = at + (i - 2) * 0.55f;
                Disc(head.x + Mathf.Cos(a) * 0.12f, head.y + Mathf.Sin(a) * 0.12f, LyingLayer + 0.0015f, 0.045f, 0.08f, 90f - a * Mathf.Rad2Deg, Hair);
            }
            Disc(head.x, head.y, LyingLayer + 0.002f, 0.15f, 0.16f, 0f, Skin);
        }

        /// <summary>Stand-in: a pawn standing, body in <paramref name="colour"/>.</summary>
        public static void Standing(Vector2 g, Color colour, Vector2 sun, float strength, float alpha = 1f)
        {
            Sprite(g + sun * 0.45f, 0.85f, 0.4f, Fade(Ink, strength * alpha), soft, ShadowLayer);
            Disc(g.x, g.y + BodyZ, PawnLayer, 0.22f, 0.32f, 0f, Fade(colour, alpha));
            Disc(g.x, g.y + 0.58f, PawnLayer + 0.002f, 0.16f, 0.17f, 0f, Fade(Skin, alpha));
        }

        /// <summary>Stand-in: a pawn face-down on the floor, head toward the unit <paramref name="toward"/>, arms out to the sides.</summary>
        public static void Lying(Vector2 g, Color colour, Vector2 toward, Vector2 sun, float strength, float alpha = 1f)
        {
            Sprite(g + sun * 0.15f, 1f, 0.5f, Fade(Ink, strength * alpha), soft, ShadowLayer);
            var p = new Vector2(-toward.y, toward.x);
            var c = new Vector2(g.x, g.y + 0.08f);
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                Vector2 sh = c + toward * 0.2f + p * (side * 0.12f);
                Line2(sh, sh + toward * 0.16f + p * (side * 0.2f), 0.07f, Fade(Skin, alpha), LyingLayer - 0.001f);
            }
            Disc(c.x, c.y, LyingLayer, 0.21f, 0.34f, Mathf.Atan2(toward.x, toward.y) * Mathf.Rad2Deg, Fade(colour, alpha));
            Disc(c.x + toward.x * 0.4f, c.y + toward.y * 0.4f, LyingLayer + 0.002f, 0.15f, 0.16f, 0f, Fade(Skin, alpha));
        }
    }
}
