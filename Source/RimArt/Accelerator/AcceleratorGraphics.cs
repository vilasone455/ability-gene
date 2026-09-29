using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by Accelerator's pictures (Plasma, Vector shove, Vector Flick and the
    /// manipulation Apply): the palette, his arm held out toward an aim, and the plasma ball. The port
    /// of Tools/VfxLab/web/sketches/lib/accelerator.js; its numbers are that file's.
    ///
    /// Palette (agreed 2026-09-24): monochrome. Everything Accelerator controls is white light with a
    /// black edge (<see cref="Air"/>, <see cref="Edge"/>): the lane, the pull ring, caught rounds, the
    /// Apply stroke. The only hue is inside the plasma ball and its burst, a desaturated blue
    /// (<see cref="Plasma"/>, <see cref="PlasmaDeep"/>), never violet.
    ///
    /// Not ported (the lab's stand-ins): the pawn with its white hair disc. In game the real pawn is
    /// drawn by the game and only the arm is drawn here, in the pawn's own sleeve and skin colours.
    /// Everything is a level circle, a quad or a strip, so nothing has a per-facing method; the arm
    /// goes behind the body when it reaches north. Every routine takes times and keeps no state.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class AcceleratorGraphics
    {
        internal static readonly Color Shirt = new Color(0.13f, 0.13f, 0.15f), HairWhite = new Color(0.93f, 0.93f, 0.96f);
        internal static readonly Color Air = new Color(0.93f, 0.94f, 0.96f), AirDeep = new Color(0.72f, 0.78f, 0.86f), Edge = new Color(0.05f, 0.05f, 0.07f);
        internal static readonly Color Plasma = new Color(0.88f, 0.93f, 1f), PlasmaDeep = new Color(0.55f, 0.7f, 0.95f), PlasmaHot = new Color(1f, 1f, 1f);
        internal static readonly Color White = new Color(1f, 1f, 1f);
        /// <summary>The lab stand-in's skin, used when no pawn gives one (the previews).</summary>
        internal static readonly Color Skin = new Color(0.92f, 0.78f, 0.66f);

        private static readonly Mesh thinRing = VfxDraw.Ring(0.93f, "Accelerator thin ring");
        private static readonly Vector2[] arcA = new Vector2[9], arcB = new Vector2[9];
        private static readonly Vector2[] armA = new Vector2[6], armB = new Vector2[6];

        /// <summary>Arm width at the shoulder and at the wrist; the hand's radius.</summary>
        private const float ArmRoot = 0.1f, ArmWrist = 0.075f, Hand = 0.055f;

        /// <summary>
        /// One arm held out from the chest toward <paramref name="degrees"/> (0 east, 90 north),
        /// <paramref name="reach"/> cells long, a tapered sleeve with the hand at its end.
        /// <paramref name="feet"/> is the pawn's ground point and <paramref name="chest"/> the chest's
        /// height as drawn (GokuGraphics.ChestOn in game, Chest in the lab). Behind the body while the
        /// hand is north of the shoulder, as a raised arm would be seen from above and behind.
        /// </summary>
        internal static void Arm(Vector2 feet, float degrees, float reach, float chest, Color sleeve, Color skin, float alpha = 1f)
        {
            if (reach <= 0.02f || alpha <= 0f) return;
            Vector2 toward = Turn(degrees), shoulder = new Vector2(feet.x, feet.y + chest), hand = shoulder + toward * reach;
            float layer = toward.y > 0.35f ? GokuGraphics.PawnLayer - 0.012f : GokuGraphics.PawnLayer + 0.012f;
            var side = new Vector2(-toward.y, toward.x);
            int n = armA.Length;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1), w = Mathf.Lerp(ArmRoot, ArmWrist, u) / 2f;
                Vector2 p = shoulder + toward * (reach * u);
                armA[i] = p + side * w;
                armB[i] = p - side * w;
            }
            Strip(armA, armB, Fade(sleeve, alpha), solid, layer);
            DrawMesh(disc, hand, layer + 0.0004f, Hand * 1.25f, Hand * 1.25f, 0f, Fade(Edge, 0.5f * alpha), solid);
            DrawMesh(disc, hand, layer + 0.0006f, Hand, Hand, 0f, Fade(skin, alpha), solid);
        }

        /// <summary>
        /// The ball of air being compressed into plasma at <paramref name="at"/>. <paramref name="stage"/> 0 is loose
        /// air (large, faint, swirling), 1 is plasma (small, white core, blue shell, rays). <paramref name="size"/>
        /// is the ball across at stage 1. <paramref name="s"/> is the picture's clock in seconds.
        /// </summary>
        internal static void PlasmaBall(Vector2 at, float size, float s, float alpha, float stage)
        {
            if (size <= 0.01f || alpha <= 0f) return;
            float Y = Overhead;
            float r = size / 2f * (1.9f - 0.9f * stage), beat = 1f + 0.1f * Mathf.Sin(s * 41f) * stage, hot = Mathf.Clamp01((stage - 0.6f) / 0.4f);
            Color shell = Color.Lerp(Air, Plasma, stage), halo = Color.Lerp(AirDeep, PlasmaDeep, stage);
            Sprite(at, r * 7f, r * 7f, Fade(halo, (0.05f + 0.4f * stage) * alpha), glow, Y + 0.1f);
            DrawMesh(disc, at, Y + 0.11f, r, r, 0f, Fade(shell, (0.1f + 0.85f * stage) * alpha), solid);
            DrawMesh(thinRing, at, Y + 0.112f, r, r, 0f, Fade(Edge, (0.35f + 0.55f * stage) * alpha), solid);   // the black edge
            // Air spiralling into the ball: three arcs turning round it that tighten as it compresses.
            for (int i = 0; i < 3; i++)
            {
                float a0 = s * (5f + stage * 6f) + i * Mathf.PI * 2f / 3f, span = 1.6f - 0.5f * stage, rr = r * (1.25f - 0.15f * stage);
                for (int k = 0; k <= 8; k++)
                {
                    float q = a0 + k / 8f * span, w = r * 0.12f * Mathf.Sin(k / 8f * Mathf.PI) + 0.004f;
                    var p = new Vector2(at.x + Mathf.Cos(q) * rr, at.y + Mathf.Sin(q) * rr);
                    arcA[k] = new Vector2(p.x - Mathf.Cos(q) * w, p.y - Mathf.Sin(q) * w);
                    arcB[k] = new Vector2(p.x + Mathf.Cos(q) * w, p.y + Mathf.Sin(q) * w);
                }
                Strip(arcA, arcB, Fade(Color.Lerp(Air, White, stage), (0.5f + 0.4f * stage) * alpha), whiteGlow, Y + 0.114f);
            }
            if (hot > 0f)
            {
                for (int i = 0; i < 8; i++)
                {
                    float ang = (i * 45f + s * (i % 2 == 1 ? 70f : -50f) + Rand(i + 20) * 20f) * Mathf.Deg2Rad, flick = 0.55f + 0.45f * Mathf.Sin(s * 23f + i * 1.9f);
                    float reach = r * (1.4f + 2.2f * Rand(i + 40)) * hot * flick;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Streak(at + dir * (r * 0.3f), at + dir * (r + reach), r * 0.3f, Fade(PlasmaHot, 0.8f * alpha * flick * hot), whiteGlow, Y + 0.113f, 4);
                }
            }
            float core = r * (0.25f + 0.3f * stage) * beat;
            DrawMesh(disc, at, Y + 0.12f, core, core, 0f, Fade(White, (0.5f + 0.5f * stage) * alpha), solid);
            Sprite(at, core * 2.4f, core * 2.4f, Fade(White, 0.9f * stage * alpha), glow, Y + 0.121f);
        }
    }
}
