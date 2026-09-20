using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by the Paper Bomb kit's Tag Throw, Tag Line and Paper Shroud: one
    /// explosive tag, the burst, the floor ring at a true radius, and a piece of debris. The port of
    /// Tools/VfxLab/web/sketches/lib/paper-bomb.js; its numbers are that file's. Everything is a quad,
    /// a level circle or a soft sprite, so nothing has a per-facing method. Every routine takes ages
    /// and amounts and keeps no state. Strips, sprites and the sun come from ThunderGodGraphics and
    /// PowerPoleGraphics.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class PaperBombGraphics
    {
        internal static readonly Color Paper = new Color(0.93f, 0.88f, 0.75f), PaperEdge = new Color(0.33f, 0.25f, 0.17f),
            InkRed = new Color(0.62f, 0.12f, 0.10f), Char = new Color(0.10f, 0.08f, 0.07f), Ember = new Color(1f, 0.5f, 0.12f),
            Hot = new Color(1f, 0.9f, 0.6f), Red = new Color(0.9f, 0.2f, 0.06f), Smoke = new Color(0.16f, 0.14f, 0.13f),
            Scorch = new Color(0.05f, 0.04f, 0.04f), Steel = new Color(0.2f, 0.2f, 0.24f), Shade = new Color(0.13f, 0.09f, 0.02f);
        private static readonly Color StoneDark = new Color(0.17f, 0.15f, 0.13f), StoneLit = new Color(0.47f, 0.43f, 0.38f),
            DirtDark = new Color(0.24f, 0.16f, 0.10f), DirtLit = new Color(0.5f, 0.37f, 0.25f);

        /// <summary>One tag, in cells. A hand seal lasts Seal before ignition; a tag curls for Burn before it bursts.</summary>
        internal const float TagLong = 0.7f, TagWide = 0.26f, Seal = 0.35f, Burn = 0.12f;

        private static readonly Mesh sealRing = SixPathsBurstGraphics.Band(0.78f, "Paper Bomb seal ring");
        // A ring mesh scales its thickness with its radius, so rings are picked from a set by the thickness wanted.
        private static readonly float[] BandInner = { 0.995f, 0.99f, 0.98f, 0.96f, 0.93f, 0.86f, 0.75f };
        private static readonly Mesh[] bands = MakeBands();
        private static readonly Mesh[] rocks = MakeRocks();

        private static Mesh[] MakeBands()
        {
            var made = new Mesh[BandInner.Length];
            for (int i = 0; i < made.Length; i++) made[i] = SixPathsBurstGraphics.Band(BandInner[i], "Paper Bomb band " + i);
            return made;
        }

        /// <summary>Six irregular outlines of 5 to 7 corners, about 1 cell across before scaling. Odd ones are dirt, even are stone.</summary>
        private static Mesh[] MakeRocks()
        {
            var made = new Mesh[6];
            for (int v = 0; v < made.Length; v++)
            {
                int n = 5 + v % 3;
                var vertices = new Vector3[n + 1];
                var indices = new int[n * 3];
                for (int i = 0; i < n; i++)
                {
                    float turn = (i + (Rand(v * 10 + i) - 0.5f) * 0.7f) / n * Mathf.PI * 2f, reach = 0.5f * (0.6f + 0.4f * Rand(v * 20 + i + 3));
                    vertices[1 + i] = new Vector3(Mathf.Cos(turn) * reach, 0f, Mathf.Sin(turn) * reach * 0.8f);
                    // Clockwise on screen, so the face survives backface culling.
                    indices[i * 3] = 0; indices[i * 3 + 1] = 1 + (i + 1) % n; indices[i * 3 + 2] = 1 + i;
                }
                made[v] = new Mesh { name = "Paper Bomb rock " + v, vertices = vertices, triangles = indices };
                made[v].RecalculateNormals();
                made[v].RecalculateBounds();
            }
            return made;
        }

        /// <summary>A quad whose long side points along <paramref name="degrees"/> (0 east, 90 north).</summary>
        internal static void Quad(Vector2 at, float along, float across, float degrees, Color colour, float altitude, Material material = null) =>
            Sprite(at, along, across, colour, material ?? solid, altitude, -degrees);

        internal static Vector2 Up(Vector2 ground, float height) => PowerPoleGraphics.Raised(ground, height);

        /// <summary>A ring round a point, about 0.07 cells thick, or up to 0.45 with <paramref name="wide"/>. Round on screen whatever the aim.</summary>
        internal static void RingAt(Vector2 at, float radius, Color colour, float altitude, bool wide = false, Material material = null)
        {
            if (radius <= 0f) return;
            float want = 1f - Mathf.Min(0.5f, (wide ? Mathf.Min(0.45f, radius * 0.09f + 0.05f) : 0.07f) / radius);
            int pick = 0;
            for (int i = 1; i < BandInner.Length; i++)
                if (Mathf.Abs(BandInner[i] - want) < Mathf.Abs(BandInner[pick] - want)) pick = i;
            DrawMesh(bands[pick], at, altitude, radius, radius, 0f, colour, material ?? solid);
        }

        /// <summary>A lump of debris: a dark outline with a smaller, lighter copy shifted toward the light.</summary>
        internal static void Rock(Vector2 at, float size, float degrees, float alpha, int variant, float altitude)
        {
            if (alpha <= 0f || size <= 0f) return;
            int v = Mathf.Abs(variant) % rocks.Length;
            bool dirt = v % 2 == 1;
            DrawMesh(rocks[v], at, altitude, size, size, degrees, Fade(dirt ? DirtDark : StoneDark, alpha), solid);
            DrawMesh(rocks[v], new Vector2(at.x - size * 0.07f, at.y + size * 0.09f), altitude + 0.0005f, size * 0.62f, size * 0.58f, degrees,
                Fade(dirt ? DirtLit : StoneLit, alpha), solid);
        }

        /// <summary>
        /// One explosive tag centred on <paramref name="mid"/>, its long side along <paramref name="degrees"/>.
        /// <paramref name="length"/> and <paramref name="wide"/> scale it (1 is TagLong by TagWide; a flipping
        /// sheet passes wide below 1). <paramref name="heat"/> lights the ink, <paramref name="curl"/> shrinks
        /// and chars it just before the burst, <paramref name="armed"/> is the idle pulse.
        /// </summary>
        internal static void Tag(Vector2 mid, float degrees, float altitude, float glowAltitude, float length = 1f, float wide = 1f,
            float heat = 0f, float curl = 0f, float armed = 0f)
        {
            float ls = length * (1f - 0.6f * curl), stroke = 0.02f * Mathf.Min(1f, length * 1.4f);
            Vector2 along = Turn(degrees);
            Color ink = Color.Lerp(Color.Lerp(InkRed, Ember, heat), Char, curl), sheet = Color.Lerp(Color.Lerp(Paper, Hot, heat * 0.6f), Char, curl * 0.8f);
            Quad(mid, TagLong * ls, TagWide * wide, degrees, ink, altitude);
            Quad(mid, (TagLong - 0.08f) * ls, (TagWide - 0.07f) * wide, degrees, sheet, altitude + 0.001f);
            DrawMesh(sealRing, mid, altitude + 0.002f, 0.085f * ls, 0.085f * wide, -degrees, ink, solid);
            Quad(mid, 0.12f * ls, stroke, degrees, ink, altitude + 0.003f);
            Quad(mid, stroke, 0.12f * wide, degrees, ink, altitude + 0.0031f);
            Quad(mid + along * (0.23f * ls), stroke, 0.13f * wide, degrees, ink, altitude + 0.0032f);
            Quad(mid - along * (0.23f * ls), stroke, 0.13f * wide, degrees, ink, altitude + 0.0033f);
            if (armed > 0f) Sprite(mid, 0.5f * length, 0.4f * length, Fade(Red, 0.16f * armed), glow, altitude + 0.004f);
            if (heat > 0f) Sprite(mid, 0.9f * length, 0.7f * length, Fade(Ember, 0.8f * heat * (1f - curl * 0.5f)), glow, glowAltitude);
        }

        /// <summary>
        /// One burst at floor point <paramref name="g"/>. The floor ring grows to <paramref name="radius"/>,
        /// the rule's true blast radius; <paramref name="power"/> scales the flash and fireball only. Soft
        /// additive layers rising from the floor, then dust, smoke, rocks and charred scraps; what lands stays
        /// for as long as the caller keeps drawing it.
        /// </summary>
        internal static void Burst(Vector2 g, float age, float radius, Vector2 sun, float shadow, int seed, float power = 1f)
        {
            if (age < 0f) return;
            float big = radius * power, start = Mathf.Clamp01(age / 0.06f);
            Sprite(g, big * 1.9f, big * 1.5f, Fade(Scorch, 0.72f * start * (1f - 0.25f * Smooth(age / 2f))), soft, Floor + 0.004f);
            if (power > 1f) Sprite(g, big * 1.1f, big * 0.9f, Fade(Scorch, Mathf.Min(0.7f, power - 1f) * start), soft, Floor + 0.005f);
            Sprite(Up(g, 0.1f), big * 3.4f, big * 2.8f, Fade(Hot, Mathf.Clamp01(1f - age / 0.12f)), glow, Overhead + 0.2f);
            float fb = Mathf.Clamp01(age / 0.45f);
            if (fb < 1f)
            {
                float rise = 0.1f + fb * 0.7f, size = big * (1f + 1.3f * Smooth(fb)), al = (1f - fb) * (1f - fb);
                Sprite(Up(g, rise), size * 1.5f, size * 1.3f, Fade(Red, 0.8f * al), glow, Overhead + 0.16f);
                Sprite(Up(g, rise), size, size * 0.9f, Fade(Ember, 0.9f * al), glow, Overhead + 0.17f);
                Sprite(Up(g, rise * 0.8f), size * 0.55f, size * 0.5f, Fade(Hot, al), glow, Overhead + 0.18f);
            }
            if (age < 0.4f) RingAt(g, radius * Smooth(age / 0.22f), Fade(Hot, 0.8f * (1f - age / 0.4f)), Floor + 0.03f, true, whiteGlow);

            float sparkAge = age / 0.22f;
            if (sparkAge < 1f)
                for (int k = 0; k < 8; k++)
                {
                    float turn = (k / 8f + Rand(seed + k) * 0.1f) * Mathf.PI * 2f, r1 = big * (0.2f + sparkAge * 0.9f), r2 = r1 + big * 0.5f * (1f - sparkAge);
                    var way = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn) * 0.8f);
                    Streak(g + way * r1, g + way * r2, 0.06f, Fade(Hot, 1f - sparkAge), whiteGlow, Overhead + 0.19f + k * 0.0002f, 3);
                }

            for (int k = 0; k < 6; k++)                                  // dust along the floor, then smoke rising
            {
                float turn = k * 1.05f + Rand(seed + k + 30), du = age / 0.7f;
                if (du < 1f)
                    Sprite(new Vector2(g.x + Mathf.Cos(turn) * radius * 1.2f * Smooth(du), g.y + Mathf.Sin(turn) * radius * 0.9f * Smooth(du) + du * 0.1f),
                        0.5f + du * 0.7f, 0.4f + du * 0.5f, Fade(PowerPoleGraphics.Dust, 0.45f * Mathf.Max(0f, Mathf.Sin(du * Mathf.PI))), PowerPoleGraphics.puff, Overhead + 0.08f + k * 0.0002f);
                float u = age / (0.9f + Rand(seed + k + 40) * 0.6f);
                if (u >= 1f) continue;
                float d = big * (0.15f + 0.45f * Rand(seed + k + 50)) * Smooth(u * 2f);
                Sprite(Up(new Vector2(g.x + Mathf.Cos(turn) * d, g.y + Mathf.Sin(turn) * d * 0.7f), 0.2f + u * 1.1f), (0.5f + u * 0.9f) * power, (0.45f + u * 0.75f) * power,
                    Fade(Smoke, 0.55f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), PowerPoleGraphics.puff, Overhead + 0.1f + k * 0.001f);
            }

            for (int k = 0; k < 5; k++)                                  // rocks: thrown, land, stay
            {
                float turn = Rand(seed + k + 60) * Mathf.PI * 2f, dist = radius * (0.5f + 0.9f * Rand(seed + k + 70)), u = Mathf.Clamp01(age / (0.3f + 0.25f * Rand(seed + k + 80)));
                float h = (0.5f + 0.6f * Rand(seed + k + 90)) * 4f * u * (1f - u), size = 0.09f + 0.1f * Rand(seed + k + 100);
                var ground = new Vector2(g.x + Mathf.Cos(turn) * dist * u, g.y + Mathf.Sin(turn) * dist * u * 0.8f);
                if (u < 1f) Sprite(ground + sun * h, size * 1.6f, size, Fade(Shade, shadow), soft, AltitudeLayer.Shadows.AltitudeFor());
                Rock(Up(ground, h), size, u < 1f ? age * 500f : Rand(seed + k) * 360f, 1f, seed + k, (u < 1f ? Overhead + 0.05f : Floor + 0.02f) + k * 0.001f);
            }

            for (int k = 0; k < 7; k++)                                  // paper scraps: thrown up, flutter down, stay charred
            {
                float turn = Rand(seed + k + 110) * Mathf.PI * 2f, dist = radius * (0.6f + Rand(seed + k + 120)), u = Mathf.Clamp01(age / (1f + 0.8f * Rand(seed + k + 130)));
                float d = dist * (1f - Mathf.Pow(1f - u, 2.5f)), h = (0.7f + 0.9f * Rand(seed + k + 140)) * (u < 0.2f ? Smooth(u / 0.2f) : 1f - Smooth((u - 0.2f) / 0.8f));
                float sway = Mathf.Sin(age * 9f + k) * 0.12f * (1f - u), flip = u < 1f ? Mathf.Abs(Mathf.Cos(age * 13f + k * 2f)) : 0.8f;
                var at = new Vector2(g.x + Mathf.Cos(turn) * d + sway, g.y + Mathf.Sin(turn) * d * 0.8f + h * SixPathsHeight.Lift);
                Color scrap = Color.Lerp(Paper, Char, Mathf.Clamp01(age * 1.6f + Rand(seed + k) * 0.5f));
                Quad(at, 0.11f, 0.065f * flip + 0.012f, turn * 57.3f + age * 220f * (1f - u), Fade(scrap, 0.9f), (u < 1f ? Overhead + 0.06f : Floor + 0.021f) + k * 0.0003f);
                if (u < 1f) Sprite(at, 0.16f, 0.16f, Fade(Ember, 0.8f * (1f - u) * (0.5f + 0.5f * Mathf.Sin(age * 30f + k))), glow, Overhead + 0.061f + k * 0.0003f);
            }
        }
    }
}
