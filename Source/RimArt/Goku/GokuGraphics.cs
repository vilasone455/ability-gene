using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by Solar Flare, Instant Transmission, Kamehameha and Spirit Bomb:
    /// the ki colours, a line through points, the vanish slices, the blink, the glint, stun stars,
    /// the ki ball and the aura. The port of Tools/VfxLab/web/sketches/lib/goku.js; its numbers are
    /// that file's. Everything is a level circle, a quad or a strip, so nothing has a per-facing
    /// method. Every routine takes ages and amounts and keeps no state.
    ///
    /// Taken from elsewhere rather than copied: the lib's ringAt is PaperBombGraphics.RingAt (the
    /// same band set), its rock is PaperBombGraphics.Rock, and strip, streak, sprites, the disc and
    /// Ink are ThunderGodGraphics'. PowerPoleGraphics.Tapered is not the lib's line: that one has
    /// three tapers, a material and a minimum width, so it is <see cref="Line"/> here.
    ///
    /// Not ported (the lab's stand-ins): pawn, its shadow, arms, tints and white outline, and
    /// wallCell. <see cref="Sliced"/> keeps the stand-in's outline only as the shape the slices
    /// are cut from, and draws them in ki colours over the pawn, which the game would hide, as
    /// ThunderGodGraphics.Sliver does.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class GokuGraphics
    {
        internal static readonly Color Ki = new Color(0.3f, 0.66f, 1f), KiDeep = new Color(0.08f, 0.3f, 0.95f),
            KiSky = new Color(0.55f, 0.82f, 1f), KiIce = new Color(0.8f, 0.93f, 1f);
        internal static readonly Color White = new Color(1f, 1f, 1f), Flare = new Color(1f, 0.96f, 0.72f), FlareWarm = new Color(1f, 0.82f, 0.35f);
        internal static readonly Color Dust = new Color(0.52f, 0.45f, 0.37f);
        /// <summary>Chest height as drawn, and cells north per cell up (SixPathsHeight.Lift).</summary>
        internal const float Chest = 0.3f, Lift = SixPathsHeight.Lift;
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();

        private static readonly Mesh thinRing = SixPathsBurstGraphics.Band(0.93f, "Goku thin ring");
        private static readonly Vector2[][] scratch = new Vector2[MostPoints + 1][];

        // The stand-in pawn's parts as ellipses: centre x, centre z, radius x, radius z. The slices are cut from them.
        private static readonly float[,] Standing = { { 0f, 0.18f, 0.22f, 0.32f }, { 0f, 0.58f, 0.16f, 0.17f }, { 0f, 0.69f, 0.19f, 0.1f } };
        private static readonly float[,] Lying = { { 0f, 0.12f, 0.32f, 0.2f }, { 0.42f, 0.14f, 0.16f, 0.17f } };
        // The slices' ki colours, one per part: body, head, hair.
        private static readonly Color[] PartKi = { Ki, KiSky, KiDeep };

        internal enum Taper { End, Both, None }

        /// <summary>A scratch array of <paramref name="points"/> points for <see cref="Line"/>. One per length, so fill it and draw it before asking for another of the same length.</summary>
        internal static Vector2[] Points(int points) => scratch[points] ?? (scratch[points] = new Vector2[points]);

        /// <summary>
        /// A line through points. <see cref="Taper.Both"/> thins it to nothing at both ends,
        /// <see cref="Taper.End"/> only at the far end, <see cref="Taper.None"/> keeps the width.
        /// </summary>
        internal static void Line(Vector2[] pts, float width, Color colour, Material material, float altitude, Taper taper = Taper.End)
        {
            int n = pts.Length, last = n - 1;
            if (n < 2 || colour.a <= 0.001f) return;
            Sides(n, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = pts[Mathf.Min(last, i + 1)] - pts[Mathf.Max(0, i - 1)];
                float len = d.magnitude, u = i / (float)last;
                if (len == 0f) len = 1f;
                float w = width / 2f * (taper == Taper.Both ? Mathf.Sin(u * Mathf.PI) : taper == Taper.End ? Mathf.Pow(1f - u, 0.6f) : 1f) + 0.004f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, material ?? solid, altitude);
        }

        /// <summary>
        /// Instant Transmission's vanish: the body breaks into horizontal slices that slide left and
        /// right alternately, stretch into lines, go white and thin away. <paramref name="u"/> is 0
        /// (whole) to 1 (gone); run it backwards for the arrival. Drawn over the pawn in ki colours.
        /// </summary>
        internal static void Sliced(Vector2 pos, float u, bool hair = false, bool lie = false)
        {
            if (u <= 0f || u >= 1f) return;
            float[,] parts = lie ? Lying : Standing;
            int count0 = lie || !hair ? 2 : 3;
            float left = 1f - u, low = float.MaxValue, high = float.MinValue;
            for (int k = 0; k < count0; k++)
            {
                low = Mathf.Min(low, parts[k, 1] - parts[k, 3]);
                high = Mathf.Max(high, parts[k, 1] + parts[k, 3]);
            }
            int count = Mathf.Max(4, GokuTiming.Round((high - low) / 0.085f));
            float step = (high - low) / count;
            for (int i = 0; i < count; i++)
            {
                float zc = low + (i + 0.5f) * step, side = i % 2 == 1 ? 1f : -1f;
                float shift = side * (u * u * 0.85f + Mathf.Sin(u * 50f + i * 2.1f) * 0.035f * Mathf.Sin(u * Mathf.PI));
                for (int k = 0; k < count0; k++)
                {
                    float cz = parts[k, 1], rz = parts[k, 3], q = 1f - ((zc - cz) / rz) * ((zc - cz) / rz);
                    if (q <= 0f) continue;
                    float half = parts[k, 2] * Mathf.Sqrt(q) * (1f + 2.2f * u);
                    Sprite(new Vector2(pos.x + parts[k, 0] + shift, pos.y + zc), half * 2f, step * (1.02f - 0.82f * u),
                        Fade(Color.Lerp(PartKi[k], KiIce, Mathf.Min(1f, u * 1.3f)), left), solid, Overhead + k * 0.002f);
                }
            }
            Sprite(new Vector2(pos.x, pos.y + 0.3f), 1.2f + u, 1.1f, Fade(KiSky, 0.5f * Mathf.Sin(u * Mathf.PI)), glow, Overhead + 0.01f);
        }

        /// <summary>
        /// What is left where a pawn vanished or appeared: horizontal speed lines at body height that
        /// shoot sideways, a thin ring on the floor, a little dust.
        /// </summary>
        internal static void Blink(Vector2 pos, float age, float life = 0.28f)
        {
            if (age < 0f || age >= life) return;
            float u = age / life, f = (1f - u) * (1f - u);
            for (int i = 0; i < 7; i++)
            {
                float side = i % 2 == 1 ? 1f : -1f, z = pos.y - 0.05f + i * 0.115f, near = 0.1f + u * 1.2f, far = near + 0.5f + Rand(i + 3) * 0.9f * (1f - u * 0.5f);
                Streak(new Vector2(pos.x + side * near, z), new Vector2(pos.x + side * far, z), 0.045f, Fade(KiIce, f), whiteGlow, Overhead + 0.02f, 3);
            }
            PaperBombGraphics.RingAt(pos, 0.25f + Smooth(u) * 0.9f, Fade(KiIce, 0.6f * (1f - u)), Floor + 0.02f);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 1.05f + Rand(i), d = 0.2f + u * (0.5f + Rand(i + 8) * 0.5f);
                Sprite(new Vector2(pos.x + Mathf.Cos(ang) * d, pos.y + Mathf.Sin(ang) * d * 0.7f + u * 0.15f), 0.3f + u * 0.4f, 0.24f + u * 0.3f,
                    Fade(Dust, 0.4f * Mathf.Sin(u * Mathf.PI)), soft, Overhead + 0.005f);
            }
        }

        /// <summary>A four-point glint: a core and two crossed rays. Used at the forehead and on sparkles.</summary>
        internal static void Glint(Vector2 at, float size, float alpha, Color colour, float turn = 0f)
        {
            if (alpha <= 0f || size <= 0f) return;
            Sprite(at, size * 0.9f, size * 0.9f, Fade(colour, alpha), glow, Overhead + 0.06f);
            for (int i = 0; i < 2; i++)
            {
                Vector2 d = Turn(i * 90f + turn) * (size * (i == 1 ? 0.7f : 1f));
                Streak(at - d, at + d, size * 0.16f, Fade(colour, alpha), whiteGlow, Overhead + 0.061f, 4);
            }
        }

        internal static void Glint(Vector2 at, float size, float alpha) => Glint(at, size, alpha, White);

        /// <summary>Three small stars circling a pawn's head (<paramref name="pos"/> is its feet): it is stunned.</summary>
        internal static void StunStars(Vector2 pos, float seconds, float alpha)
        {
            if (alpha <= 0f) return;
            for (int i = 0; i < 3; i++)
            {
                float ang = seconds * 5f + i * 2.094f;
                Glint(new Vector2(pos.x + Mathf.Cos(ang) * 0.27f, pos.y + 0.84f + Mathf.Sin(ang) * 0.1f), 0.1f, alpha * (0.6f + 0.4f * Mathf.Sin(ang)), Flare, 45f);
            }
        }

        /// <summary>
        /// A ball of ki: glow, blue shell with a dark rim, pale inside, a white core that beats, and
        /// rays of light that leak out and turn. <paramref name="rays"/> scales how far they reach.
        /// </summary>
        internal static void KiBall(Vector2 at, float size, float seconds, float alpha, float rays = 1f)
        {
            if (size <= 0.01f || alpha <= 0f) return;
            float r = size / 2f, beat = 1f + 0.12f * Mathf.Sin(seconds * 38f);
            Sprite(at, size * 3.6f, size * 3.6f, Fade(Ki, 0.6f * alpha), glow, Overhead + 0.1f);
            DrawMesh(disc, at, Overhead + 0.11f, r, r, 0f, Fade(Ki, 0.9f * alpha), solid);
            DrawMesh(disc, at, Overhead + 0.111f, r * 0.8f, r * 0.8f, 0f, Fade(KiSky, 0.85f * alpha), solid);
            DrawMesh(thinRing, at, Overhead + 0.112f, r, r, 0f, Fade(KiDeep, alpha), solid);
            if (rays > 0f)
                for (int i = 0; i < 8; i++)
                {
                    Vector2 way = Turn(i * 45f + seconds * (i % 2 == 1 ? 60f : -45f) + Rand(i + 20) * 20f);
                    float flick = 0.55f + 0.45f * Mathf.Sin(seconds * 23f + i * 1.9f), reach = r * (1.5f + 2.4f * Rand(i + 40)) * rays * flick;
                    Streak(at + way * (r * 0.3f), at + way * (r + reach), r * 0.34f, Fade(KiIce, 0.8f * alpha * flick), whiteGlow, Overhead + 0.113f, 4);
                }
            DrawMesh(disc, at, Overhead + 0.12f, r * 0.42f * beat, r * 0.42f * beat, 0f, Fade(White, alpha), solid);
            Sprite(at, size * 0.9f * beat, size * 0.9f * beat, Fade(White, 0.9f * alpha), glow, Overhead + 0.121f);
        }

        /// <summary>
        /// A flame-shaped aura standing round a pawn (<paramref name="pos"/> is its feet), drawn behind it.
        /// <paramref name="power"/> 0 to 1. <paramref name="colour"/> defaults to ki blue; the Vergil kit
        /// passes its own blue, as the lab's aura() takes a colour.
        /// </summary>
        internal static void Aura(Vector2 pos, float seconds, float power, Color? colour = null)
        {
            if (power <= 0f) return;
            Color tint = colour ?? KiSky;
            const int steps = 12;
            Sides(steps + 1, out Vector2[] left, out Vector2[] right);
            for (int j = 0; j <= steps; j++)
            {
                float v = j / (float)steps, z = pos.y - 0.12f + v * (1.25f + 0.35f * power);
                float half = (0.46f * Mathf.Sin(Mathf.PI * Mathf.Pow(v, 0.62f)) * (1f - v * 0.45f) + 0.03f) * (0.8f + 0.2f * power), sway = Mathf.Sin(seconds * 27f + j * 1.2f) * 0.045f * v;
                left[j] = new Vector2(pos.x - half + sway + Mathf.Sin(seconds * 41f + j * 2.3f) * 0.025f, z);
                right[j] = new Vector2(pos.x + half + sway + Mathf.Sin(seconds * 37f + j * 1.7f) * 0.025f, z);
            }
            Strip(left, right, Fade(tint, 0.34f * power), whiteGlow, PawnLayer - 0.02f);
            Sprite(new Vector2(pos.x, pos.y + 0.35f), 1.5f, 1.9f, Fade(tint, 0.3f * power), glow, PawnLayer - 0.021f);
        }
    }
}
