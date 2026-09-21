using UnityEngine;
using Verse;
using static RimArt.GokuGraphics;
using static RimArt.ThunderGodGraphics;
using T = RimArt.GokuSolarFlareTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Solar Flare: the ring on the floor at the true radius, light gathering at the head in the
    /// warmup (a glint and short rays running in), then the flash (a whiteout, a ring front out to
    /// the radius, needle rays, the cross flare, halo rings, a yellow fill on the floor), the long
    /// shadows every pawn and the wall throw away from it while it is bright, and stars circling the
    /// heads of the stunned. All level circles and lines through one point, so no per-facing method.
    ///
    /// Not drawn (the sketch's stand-ins): the pawns and their shadows, the caster's hands to the
    /// face, the flash's tint on the pawns, the arm over the eyes, the dark bar over the eyes after
    /// the stun, and the wall itself.
    /// </summary>
    public static class GokuSolarFlareGraphics
    {
        private const int MostPawns = 8;
        private static readonly Vector2[] pawns = new Vector2[MostPawns];
        private static readonly int[] seeds = new int[MostPawns];

        /// <summary>The preview. <paramref name="centre"/> is the caster's cell, as in the lab's sketch; the scene does not turn.</summary>
        public static void DrawPreview(Vector3 centre, bool wall, float seconds, Map map)
        {
            SolarFlarePlan plan = T.Plan();
            if (seconds < 0f || seconds >= plan.End) return;
            var o = new Vector2(centre.x, centre.z);
            int n = 0;
            for (int i = 0; i < T.MeleeDegrees.Length; i++)
            {
                if (T.MeleeDistance[i] > T.ScriptRadius) continue;
                pawns[n] = o + T.Melee(i, seconds, plan);
                seeds[n++] = i;
            }
            int blinded = n;
            pawns[n] = o + T.Outside(seconds, plan, T.ScriptRadius);
            seeds[n++] = T.OutsideSeed;
            if (wall)
            {
                pawns[n] = o + T.Covered;
                seeds[n++] = T.CoveredSeed;
            }
            T.Wall(out Vector2 a, out Vector2 b);
            Draw(new SolarFlareShot
            {
                Centre = o, Seconds = seconds, Plan = plan, Radius = T.ScriptRadius, Fade = T.ScriptFade,
                Pawns = pawns, Seeds = seeds, Count = n, Blinded = blinded, Walled = wall, WallA = o + a, WallB = o + b,
            }, map);
        }

        public static void Draw(in SolarFlareShot shot, Map map)
        {
            SolarFlarePlan t = shot.Plan;
            float s = shot.Seconds, radius = shot.Radius;
            Vector2 o = shot.Centre;
            if (s < 0f || s >= t.End || !Shown(o, map)) return;
            Begin(o);
            float age = s - t.Flash, f = Mathf.Clamp01(age / shot.Fade), bright = age >= 0f ? (1f - f) * (1f - f) : 0f;
            var head = new Vector2(o.x, o.y + 0.6f);

            // --- the floor: the true radius, the fill, and the shadows the flash throws ---
            if (s >= t.Cast)
            {
                float warn = Smooth((s - t.Cast) / t.Warm), left = 1f - Smooth((s - t.Wake) / 0.5f);
                PaperBombGraphics.RingAt(o, radius, Fade(FlareWarm, 0.55f * warn * left), Floor + 0.02f);
            }
            if (bright > 0f)
            {
                DrawMesh(disc, o, Floor + 0.006f, radius, radius, 0f, Fade(FlareWarm, 0.3f * (1f - f)), whiteGlow);
                // Long shadows away from the light, on top of the fill. The wall's is a wedge out to the radius.
                for (int i = 0; i < shot.Count; i++)
                {
                    Vector2 p = shot.Pawns[i], d = p - o;
                    float dist = d.magnitude, far = T.ShadowReach * (0.6f + 0.4f * bright);
                    if (dist == 0f) dist = 1f;
                    Streak(p, p + d / dist * far, 0.5f, Fade(Ink, 0.5f * bright), solid, Floor + 0.03f, 6);
                }
                if (shot.Walled)
                {
                    Sides(2, out Vector2[] a, out Vector2[] b);
                    a[0] = shot.WallA;
                    a[1] = o + (shot.WallA - o).normalized * radius;
                    b[0] = shot.WallB;
                    b[1] = o + (shot.WallB - o).normalized * radius;
                    Strip(a, b, Fade(Ink, 0.5f * (1f - f)), solid, Floor + 0.03f);
                }
            }

            // --- stars round the heads of the stunned ---
            for (int i = 0; i < shot.Blinded; i++)
            {
                bool stunned = age >= 0f && s < t.Wake;
                StunStars(shot.Pawns[i], s + shot.Seeds[i], stunned ? Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((t.Wake - s) / 0.2f) : 0f);
            }

            // --- warmup: light gathers at the head ---
            if (s >= t.Cast && age < 0f)
            {
                float w = (s - t.Cast) / t.Warm;
                Sprite(head, 0.5f + w * 0.9f, 0.5f + w * 0.9f, Fade(GokuGraphics.Flare, 0.7f * w), glow, Overhead + 0.05f);
                Glint(head, 0.1f + 0.25f * w, w);
                for (int i = 0; i < T.Gather; i++)
                {
                    float v = (w * 1.6f + Rand(i)) % 1f, r0 = 1.1f * (1f - v) + 0.12f, r1 = r0 + 0.3f * (1f - v);
                    Vector2 way = Turn(i * 36f + Rand(i + 5) * 25f);
                    Streak(head + way * r0, head + way * r1, 0.04f, Fade(GokuGraphics.Flare, 0.9f * Mathf.Sin(v * Mathf.PI)), whiteGlow, Overhead + 0.04f, 2);
                }
            }

            // --- the flash ---
            if (bright > 0f)
            {
                float open = Smooth(age / T.Front);
                Sprite(head, radius * 5f, radius * 5f, Fade(GokuGraphics.Flare, 0.8f * bright), glow, Overhead + 0.1f);            // whiteout, past the radius
                Sprite(head, radius * 2.3f, radius * 2.3f, Fade(White, 0.95f * bright), glow, Overhead + 0.101f);
                PaperBombGraphics.RingAt(o, radius * open, Fade(White, 0.9f * (1f - f)), Overhead + 0.102f, true, whiteGlow);   // the front, on the true radius
                PaperBombGraphics.RingAt(head, radius * 0.34f * (0.7f + 0.3f * open), Fade(FlareWarm, 0.5f * bright), Overhead + 0.103f, false, whiteGlow);
                PaperBombGraphics.RingAt(head, radius * 0.58f * (0.7f + 0.3f * open), Fade(FlareWarm, 0.5f * bright), Overhead + 0.104f, false, whiteGlow);
                for (int i = 0; i < T.Rays; i++)
                {
                    bool longRay = i % 4 == 0;
                    Vector2 way = Turn(i * 360f / T.Rays + Rand(i + 11) * 8f + 10f * f);
                    float reach = radius * (longRay ? 1f + 0.15f * Rand(i + 30) : 0.4f + 0.45f * Rand(i + 30)) * open * (1f - 0.25f * f), w = (longRay ? 0.24f : 0.13f) * (1f - 0.5f * f);
                    Vector2 tip = head + way * reach, root = head + way * 0.15f;
                    Streak(root, tip, w * 2.6f, Fade(FlareWarm, 0.5f * bright), whiteGlow, Overhead + 0.104f, 6);
                    Streak(root, tip, w, Fade(White, Mathf.Min(1f, bright * 1.5f)), whiteGlow, Overhead + 0.105f, 6);
                }
                // The cross flare of the anime: one long horizontal ray, one shorter vertical one.
                Vector2 across = new Vector2(radius * 1.35f * open, 0f), up = new Vector2(0f, radius * 0.9f * open);
                Streak(head - across, head + across, 0.5f * (1f - 0.5f * f), Fade(White, Mathf.Min(1f, bright * 1.6f)), whiteGlow, Overhead + 0.106f, 10);
                Streak(head - up, head + up, 0.38f * (1f - 0.5f * f), Fade(White, Mathf.Min(1f, bright * 1.6f)), whiteGlow, Overhead + 0.106f, 10);
                float core = 0.5f + 1.2f * open * (1f - f);
                DrawMesh(disc, head, Overhead + 0.107f, core, core, 0f, Fade(White, Mathf.Min(1f, bright * 2f)), solid);
            }
        }
    }
}
