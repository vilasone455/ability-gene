using UnityEngine;
using Verse;
using static RimArt.PowerPoleGraphics;
using static RimArt.ThunderGodGraphics;
using T = RimArt.PowerPoleSweepTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Sweep: the floor outline of the true hit area with the notch a wall cuts into it, the
    /// pole turning from the caster's left to its right and shortening past the wall, the pale fan
    /// behind it, the dust under its far end, and a flash on each pawn the script says is hit. The
    /// pole lies flat at hand height, so there is no per-facing method. No pawn and no wall is drawn.
    /// </summary>
    public static class PowerPoleSweepGraphics
    {
        /// <summary><paramref name="centre"/> is the caster's cell and <paramref name="toward"/> the unit direction of the cast.</summary>
        public static void Draw(Vector3 centre, Vector2 toward, bool wall, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration) return;
            var caster = new Vector2(centre.x, centre.z);
            if (!Shown(caster, map)) return;
            Begin(caster);
            Sun(map, out Vector2 sun, out float shadow);
            float aim = ThunderGodTiming.Degrees(toward);
            Vector2 Polar(float range, float phi, float height = 0f) => Raised(caster + Turn(aim + phi) * range, height);

            // Floor outline of the true hit area. Two halves, because a strip holds 40 points at most.
            float shown = 1f - Smooth((seconds - T.SwungAt) / 0.4f);
            int half = T.AreaSteps / 2;
            for (int part = 0; part < 2; part++)
            {
                Sides(half + 1, out Vector2[] hub, out Vector2[] edge);
                for (int i = 0; i <= half; i++)
                {
                    float phi = Mathf.Lerp(T.Half, -T.Half, (part * half + i) / (float)T.AreaSteps);
                    hub[i] = Polar(0.35f, phi);
                    edge[i] = Polar(T.LengthAt(phi, aim, wall), phi);
                }
                Strip(hub, edge, Fade(Cream, 0.07f * shown), solid, Floor + part * 0.0002f);
                Sides(half + 1, out Vector2[] inner, out Vector2[] outer);
                for (int i = 0; i <= half; i++)
                {
                    float phi = Mathf.Lerp(T.Half, -T.Half, (part * half + i) / (float)T.AreaSteps), length = T.LengthAt(phi, aim, wall);
                    inner[i] = Polar(Mathf.Max(0f, length - 0.05f), phi);
                    outer[i] = Polar(length, phi);
                }
                Strip(inner, outer, Fade(Cream, 0.55f * shown), solid, Floor + 0.002f + part * 0.0002f);
            }

            // The preview's script: a flash on each pawn as the pole passes it. A hit pawn is shoved the way the pole was moving.
            for (int e = 0; e < T.EnemyRange.Length; e++)
            {
                float hit = T.HitAt(e, aim, wall), age = hit < 0f ? -1f : seconds - hit;
                if (age < 0f) continue;
                Vector2 at = Polar(T.EnemyRange[e], T.EnemyPhi[e]) + Turn(aim + T.EnemyPhi[e] - 90f) * (T.Shove * EaseOut(age / 0.2f));
                Sprite(Raised(at, HandHeight), 1.3f, 0.9f, Fade(Cream, Mathf.Max(0f, 1f - age / 0.12f) * 0.85f), glow, Overhead + 0.02f + e * 0.0002f);
            }

            // The swept fan behind the pole: three nested slices, so the newest part is the brightest.
            if (seconds >= T.SwingAt && seconds < T.SwungAt + T.FanLinger)
                for (int k = 0; k < T.FanSpans.Length; k++)
                {
                    float from = T.AngleAt(Mathf.Min(seconds, T.SwungAt)), to = T.AngleAt(Mathf.Max(T.SwingAt, seconds - T.FanSpans[k]));
                    float fade = seconds < T.SwungAt ? 1f : 1f - (seconds - T.SwungAt) / T.FanLinger;
                    Sides(T.FanSteps + 1, out Vector2[] inner, out Vector2[] outer);
                    for (int i = 0; i <= T.FanSteps; i++)
                    {
                        float phi = Mathf.Lerp(to, from, i / (float)T.FanSteps);
                        inner[i] = Polar(0.7f, phi, HandHeight);
                        outer[i] = Polar(T.LengthAt(phi, aim, wall), phi, HandHeight);
                    }
                    Strip(inner, outer, Fade(Cream, 0.13f * fade), solid, Overhead - 0.01f + k * 0.0002f);
                }

            // Dust along the ground under the far end as it passes.
            if (seconds >= T.SwingAt)
                for (int i = 0; i < T.DustPuffs; i++)
                {
                    float phi = Mathf.Lerp(T.Half, -T.Half, (i + 0.5f) / T.DustPuffs), born = T.Passes(phi);
                    float u = (seconds - born) / (0.4f + Rand(i) * 0.25f);
                    Puff(Polar(T.LengthAt(phi, aim, wall) - 0.15f + u * 0.5f, phi - u * 6f, u * 0.25f), 0.35f + u * 0.6f, 0.28f + u * 0.45f, u, 0.65f,
                        Overhead - 0.02f + i * 0.0002f);
                }

            float now = T.AngleAt(seconds), tip = T.TipLength(seconds, aim, wall);
            Vector2 a = caster + Turn(aim + now) * RestBack, b = caster + Turn(aim + now) * tip;
            Pole(Raised(a, HandHeight), Raised(b, HandHeight), a + sun * HandHeight, b + sun * HandHeight, tip - RestBack, Width, shadow, Overhead);
        }
    }
}
