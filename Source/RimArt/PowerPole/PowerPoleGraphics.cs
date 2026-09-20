using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by the Power Pole's Extend Thrust, Sweep and Vault Strike: the pole
    /// itself, the dust puff and the scene's sun. The pole is a straight strip between two drawn
    /// points, its width measured across its own direction on screen, so one routine serves a pole
    /// lying flat at hand height and one leaning up from the ground. Only the shaft stretches: the
    /// two ferrules keep a fixed length. Strips, sprites and rings come from ThunderGodGraphics.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class PowerPoleGraphics
    {
        internal static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);

        internal static readonly Color Red = new Color(0.80f, 0.13f, 0.10f), RedLit = new Color(0.98f, 0.46f, 0.36f),
            RedDark = new Color(0.25f, 0.04f, 0.04f), Ferrule = new Color(0.46f, 0.07f, 0.06f),
            Cream = new Color(1f, 0.95f, 0.8f), Dust = new Color(0.80f, 0.74f, 0.63f), Shade = new Color(0.035f, 0.028f, 0.050f);

        /// <summary>Cells of pole at each end that do not stretch, the dark outline round the pole, the carried staff.</summary>
        internal const float FerruleLength = 0.14f, Outline = 0.02f, Width = 0.12f;
        internal const float RestBack = -0.45f, RestTip = 0.75f, HandHeight = 0.5f;
        /// <summary>The lab's shadow strength at full daylight.</summary>
        private const float ShadowStrength = 0.32f;

        /// <summary>Where the shadow of a point one cell up falls, and how dark shadows are now.</summary>
        internal static void Sun(Map map, out Vector2 sun, out float shadow)
        {
            sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
            shadow = ShadowStrength * GenCelestial.CurShadowStrength(map);
        }

        /// <summary>A ground point raised <paramref name="height"/> cells, as it is drawn.</summary>
        internal static Vector2 Raised(Vector2 ground, float height) => new Vector2(ground.x, ground.y + height * SixPathsHeight.Lift);

        /// <summary>
        /// The pole from <paramref name="a"/> to <paramref name="b"/>, both already drawn points, with
        /// its shadow between the two shadow points. <paramref name="length"/> is the pole's true
        /// length in cells, which sets how much of the drawn line each ferrule takes.
        /// </summary>
        internal static void Pole(Vector2 a, Vector2 b, Vector2 aShadow, Vector2 bShadow, float length, float width,
            float shadow, float altitude)
        {
            Vector2 run = b - a;
            float drawn = run.magnitude;
            if (drawn < 0.0001f) return;
            // The normal points north on screen, so the lit strip is the north side.
            var normal = new Vector2(-run.y / drawn, run.x / drawn);
            if (normal.y < 0f || (normal.y == 0f && normal.x > 0f)) normal = -normal;

            Vector2 cast = bShadow - aShadow;
            float castLength = cast.magnitude;
            if (castLength > 0.0001f)
            {
                Vector2 side = new Vector2(-cast.y, cast.x) / castLength * (width / 2f);
                Sides(2, out Vector2[] p, out Vector2[] q);
                p[0] = aShadow - side; p[1] = bShadow - side; q[0] = aShadow + side; q[1] = bShadow + side;
                Strip(p, q, Fade(Shade, shadow), solid, AltitudeLayer.Shadows.AltitudeFor());
            }

            float cap = Mathf.Min(0.45f, FerruleLength / Mathf.Max(length, 0.0001f)), grow = Outline / drawn, half = width / 2f;
            Part(a, run, normal, -grow, 1f + grow, -half - Outline, half + Outline, RedDark, altitude);
            Part(a, run, normal, 0f, 1f, -half, half, Red, altitude + 0.002f);
            Part(a, run, normal, 0f, 1f, width * 0.12f, width * 0.40f, RedLit, altitude + 0.004f);
            Part(a, run, normal, 0f, cap, -half, half, Ferrule, altitude + 0.006f);
            Part(a, run, normal, 1f - cap, 1f, -half, half, Ferrule, altitude + 0.0062f);
        }

        /// <summary>A strip over part of the line from <paramref name="a"/> along <paramref name="run"/>: shares of its length, and cells to either side.</summary>
        internal static void Part(Vector2 a, Vector2 run, Vector2 normal, float from, float to, float low, float high,
            Color colour, float altitude, Material material = null)
        {
            Sides(2, out Vector2[] p, out Vector2[] q);
            p[0] = a + run * from + normal * low; p[1] = a + run * to + normal * low;
            q[0] = a + run * from + normal * high; q[1] = a + run * to + normal * high;
            Strip(p, q, colour, material ?? solid, altitude);
        }

        /// <summary>One dust puff. <paramref name="u"/> is its age as a share of its life.</summary>
        internal static void Puff(Vector2 at, float width, float depth, float u, float strength, float altitude)
        {
            if (u < 0f || u > 1f) return;
            Sprite(at, width, depth, Fade(Dust, Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * strength), puff, altitude);
        }

        /// <summary>A line through <paramref name="points"/> that tapers to nothing at both ends. The points are overwritten.</summary>
        internal static void Tapered(Vector2[] points, float width, Color colour, float altitude)
        {
            int n = points.Length;
            Sides(n, out Vector2[] left, out Vector2[] right);
            for (int i = 0; i < n; i++)
            {
                Vector2 along = points[Mathf.Min(n - 1, i + 1)] - points[Mathf.Max(0, i - 1)];
                float length = along.magnitude;
                if (length < 1e-5f) length = 1f;
                Vector2 side = new Vector2(-along.y, along.x) / length * (Mathf.Max(0f, Mathf.Sin(i / (float)(n - 1) * Mathf.PI)) * width / 2f);
                left[i] = points[i] + side;
                right[i] = points[i] - side;
            }
            Strip(left, right, colour, solid, altitude);
        }

        /// <summary>A ring of dust puffs thrown out from a point on the ground. <paramref name="key"/> seeds it.</summary>
        internal static void Burst(int key, Vector2 centre, float age, int count, float reach, float strength, float altitude, float size = 1f)
        {
            if (age < 0f || age > 0.7f) return;
            for (int i = 0; i < count; i++)
            {
                float u = age / (0.4f + Rand(i + key) * 0.3f);
                if (u > 1f) continue;
                float turn = i * 2.399f + key, far = reach * (0.25f + u * (0.6f + Rand(i + key + 9) * 0.6f));
                var at = new Vector2(centre.x + Mathf.Cos(turn) * far, centre.y + Mathf.Sin(turn) * far * 0.7f + Mathf.Sin(u * Mathf.PI) * 0.2f);
                Puff(at, (0.3f + u * 0.5f) * size, (0.24f + u * 0.4f) * size, u, strength, altitude + i * 0.0002f);
            }
        }

        /// <summary>The clamped ease the sketches call smooth.</summary>
        internal static float Smooth01(float t) => SixPathsSlamTiming.Smooth(t);

        internal static float EaseOut(float t)
        {
            float left = 1f - Mathf.Clamp01(t);
            return 1f - left * left * left;
        }
    }
}
