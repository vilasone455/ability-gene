using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.GojoGraphics;
using T = RimArt.UnlimitedVoidOpenTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Unlimited Void's opening on the home map round Gojo's cell: the rim light on Gojo and light
    /// gathering at the raised hand during the sign; the barrier closing over the radius, a dark sphere
    /// behind a thin white front; the sphere shrinking and rising into a black ball 0.5 cells across,
    /// 1.2 cells up, with a faint ring left at the true radius; the ball hanging; the ball breaking in
    /// cracks of light, a soft white flash and a thin ring out to the radius.
    ///
    /// The port of Tools/VfxLab/web/sketches/gojo-unlimited-void-open.js up to the break. The dark
    /// sphere is the dome's outline under the 0.6 lift filled four times, see-through, so its edge is
    /// soft; the ball, rings and flashes are level circles and quads, so nothing needs a per-facing
    /// method. The sketch's stand-in pawns are not ported, and with them go Gojo's body, the hand sign
    /// and the blindfold, and the whole return: who comes back standing, falling or fighting is the
    /// ability's.
    /// </summary>
    internal static class UnlimitedVoidOpenGraphics
    {
        private static readonly float[] FillScale = { 1f, 0.95f, 0.88f, 0.8f }, FillAlpha = { 0.22f, 0.28f, 0.32f, 0.3f };

        /// <summary>The preview. <paramref name="centre"/> is the chosen cell, Gojo's.</summary>
        public static void DrawPreview(Vector3 centre, float seconds, Map map) => Draw(new Vector2(centre.x, centre.z), seconds, map);

        public static void Draw(Vector2 o, float s, Map map)
        {
            if (s < 0f || s >= T.Duration || !Shown(o, map)) return;
            Begin(o);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);
            const float R = T.Radius;

            // --- the barrier, the shrink, the ball, the break ------------------------------------------------
            if (s >= T.OpenAt && s < T.FullAt)
            {
                float f = Smooth((s - T.OpenAt) / T.Close), r = R * f;
                DarkDome(o, r, 1f);
                PaperBombGraphics.RingAt(o, r, Fade(White, 0.55f), Overhead + 0.1f, false, whiteGlow);
                Sprite(new Vector2(o.x, o.y + 0.5f), 1.5f + 3f * f, 1.5f + 3f * f, Fade(Ice, 0.5f * (1f - f)), glow, Overhead + 0.101f);
            }
            if (s >= T.FullAt && s < T.HangAt)
            {
                float k = 1f - (s - T.FullAt) / T.Shrink, f = 1f - k * k * k;   // fast first, slow at the end
                DarkDome(PowerPoleGraphics.Raised(o, T.BallHeight * f), Mathf.Lerp(R, T.BallSize / 2f, f), 1f);
            }
            if (s >= T.FullAt && s < T.FullAt + T.RingFade)
                PaperBombGraphics.RingAt(o, R, Fade(Ice, 0.5f * (1f - (s - T.FullAt) / T.RingFade)), Floor + 0.02f, false, whiteGlow);
            if (s >= T.HangAt && s < T.BurstAt + 0.1f)
                Ball(o, T.BallHeight, T.BallSize, s, 1f - Mathf.Clamp01((s - T.BurstAt) / 0.1f), sun, shadow);
            BallBurst(o, T.BallHeight, T.BallSize, s - T.BurstAt, R);

            // --- Gojo until he is taken: the rim light, and light gathering at the raised hand ---------------------
            if (s < T.FullAt) RimLight(o, Smooth((s - T.CastAt) / (T.Warm * 0.5f)), 1f);
            if (s >= T.CastAt && s < T.OpenAt)
            {
                float w = (s - T.CastAt) / T.Warm;
                Vector2 hand = T.HandAt(o);
                Sprite(hand, 0.35f + 0.6f * w, 0.35f + 0.6f * w, Fade(EyeBlue, 0.55f * w), glow, Overhead + 0.05f);
                GokuGraphics.Glint(hand, 0.08f + 0.2f * w, w, Ice);
                for (int i = 0; i < T.Gather; i++)
                {
                    float v = (w * 1.5f + i / (float)T.Gather) % 1f, r0 = 0.9f * (1f - v) + 0.1f, r1 = r0 + 0.25f * (1f - v);
                    Vector2 d = Turn(i * 45f + 20f);
                    Streak(hand + d * r0, hand + d * r1, 0.035f, Fade(Violet, 0.9f * Mathf.Sin(v * Mathf.PI)), whiteGlow, Overhead + 0.051f, 2);
                }
            }
        }

        /// <summary>
        /// The barrier from outside: a dark sphere of <paramref name="radius"/> over its ground point
        /// <paramref name="c"/>, four see-through fills of the dome's outline so the edge is soft, a thin
        /// light rim and a faint lit cap.
        /// </summary>
        internal static void DarkDome(Vector2 c, float radius, float alpha)
        {
            if (radius <= 0.05f || alpha <= 0f) return;
            for (int j = 0; j < FillScale.Length; j++)
                DrawMesh(DomeFan, c, Overhead + 0.02f + j * 0.001f, radius * FillScale[j], radius * FillScale[j], 0f,
                    Fade(VergilGraphics.Void, FillAlpha[j] * alpha), solid);
            float e = Mathf.Min(0.08f, radius * 0.2f) / radius;
            Sides(DomeSides + 1, out Vector2[] outer, out Vector2[] inner);
            for (int i = 0; i <= DomeSides; i++)
            {
                Vector2 q = DomeOutline[i % DomeSides] * radius;
                outer[i] = c + q;
                inner[i] = c + q * (1f - e);
            }
            Strip(outer, inner, Fade(Ice, 0.3f * alpha), whiteGlow, Overhead + 0.024f);
            Sprite(new Vector2(c.x - radius * 0.3f, c.y + radius * 0.72f), radius * 0.9f, radius * 0.45f, Fade(Blue, 0.16f * alpha), glow, Overhead + 0.025f, -20f);
        }

        /// <summary>
        /// The barrier shrunk to a ball (manga ch. 227-228): a black ball hanging <paramref name="height"/>
        /// cells over <paramref name="ground"/>, a thin light rim, a faint blue-violet halo, a slow glint,
        /// and its shadow.
        /// </summary>
        internal static void Ball(Vector2 ground, float height, float size, float s, float alpha, Vector2 sun, float shadow)
        {
            if (alpha <= 0f || size <= 0f) return;
            Vector2 c = PowerPoleGraphics.Raised(ground, height);
            float r = size / 2f, g = s * 1.3f;
            Sprite(ground + sun * height, size * 1.3f, size * 0.7f, Fade(Ink, Mathf.Min(0.8f, shadow * 1.6f) * alpha), soft, ShadowLayer);
            Sprite(c, size * 5f, size * 5f, Fade(Violet, 0.14f * alpha), glow, Overhead + 0.03f);
            Sprite(c, size * 2.6f, size * 2.6f, Fade(EyeBlue, 0.22f * alpha), glow, Overhead + 0.031f);
            DrawMesh(disc, c, Overhead + 0.032f, r, r, 0f, Fade(VergilGraphics.Void, alpha), solid);
            PaperBombGraphics.RingAt(c, r * 1.02f, Fade(Ice, 0.75f * alpha), Overhead + 0.033f, false, whiteGlow);
            Sprite(new Vector2(c.x + Mathf.Cos(g) * r * 0.55f, c.y + Mathf.Sin(g) * r * 0.55f), r * 0.5f, r * 0.5f, Fade(White, 0.3f * alpha), glow, Overhead + 0.034f);
            Sprite(new Vector2(c.x - r * 0.35f, c.y + r * 0.4f), r * 0.45f, r * 0.32f, Fade(Ice, 0.5f * alpha), glow, Overhead + 0.035f);
        }

        /// <summary>The ball breaking, <paramref name="age"/> s after: cracks of light across it, then a white flash out to the radius.</summary>
        internal static void BallBurst(Vector2 ground, float height, float size, float age, float radius)
        {
            if (age < 0f || age > 1.2f) return;
            Vector2 c = PowerPoleGraphics.Raised(ground, height);
            if (age < 0.12f)
                for (int i = 0; i < 4; i++)
                {
                    Vector2 d = Turn(i * 47f + 20f) * (size * 0.75f);
                    Streak(c - d * 0.25f, c + d, 0.035f, Fade(White, 1f - age / 0.12f), whiteGlow, Overhead + 0.04f, 3);
                }
            float f = Smooth((age - 0.08f) / 0.3f), fade = 1f - Mathf.Clamp01((age - 0.2f) / 0.6f);
            if (f <= 0f) return;
            Sprite(c, radius * 2f * f, radius * 2f * f, Fade(White, 0.5f * fade), glow, Overhead + 0.05f);
            PaperBombGraphics.RingAt(ground, radius * f, Fade(White, 0.6f * fade), Overhead + 0.051f, false, whiteGlow);
        }
    }
}
