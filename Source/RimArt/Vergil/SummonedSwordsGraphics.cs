using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.VergilGraphics;
using T = RimArt.SummonedSwordsTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Summoned Swords: the range ring on the floor and the ring's mark under the carrier, a thin
    /// aura during the warm-up, eight blades of light rising out of the floor one after another and
    /// circling at chest height, each with its shadow; in "fires" a blade leaving its slot, turning
    /// point-first, flying with a light trail, sticking in the target's chest and breaking like glass,
    /// and the empty slot regrowing; in the second mode each blade dragging an arc of light; and at the
    /// end every blade breaking and one thin ring front.
    ///
    /// The port of Tools/VfxLab/web/sketches/vergil-summoned-swords.js. The ring is a level circle and
    /// each blade a flat shape lying at one height, so no part needs a per-facing method; the ring only
    /// shifts north by its height. Blades on the north half of the ring draw under the pawn layer and
    /// the south half over it, so the real carrier stands inside the ring.
    ///
    /// The sketch's stand-in pawns are not ported, and with them go the flash, the flinch, the blue tint
    /// per stuck blade, the red slits, and the second mode's cut across each chest. Those are the
    /// ability's, not the drawing's. The blades that stick in a target are drawn: they are the ability's
    /// own projectile.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SummonedSwordsGraphics
    {
        private const int ArcSteps = 9;
        private static readonly Vector2[] arcPoints = new Vector2[ArcSteps];

        /// <summary>The preview. <paramref name="centre"/> is the chosen cell, where the carrier starts.</summary>
        public static void DrawPreview(Vector3 centre, SwordsScene scene, float seconds, Map map) =>
            Draw(new Vector2(centre.x, centre.z), scene, seconds, map);

        public static void Draw(Vector2 origin, SwordsScene scene, float s, Map map)
        {
            if (s < 0f || s >= T.Duration) return;
            Vector2 c = origin + T.CasterAt(scene, s);
            if (!Shown(c, map)) return;
            Begin(c);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float shadow);

            int n = T.Slots;
            List<SwordShot> shots = T.Shots(scene);
            bool spinning = T.Spins(scene);
            float sinceStop = s - T.StopAt, left = 1f - Smooth(sinceStop / T.RingFade), R = T.RingRadius(scene, s);
            float spun = spinning ? Smooth((s - T.FormedAt) / T.Ramp) : 0f;

            // --- the floor: the range, and the ring's mark under the carrier ------------------------------
            PaperBombGraphics.RingAt(c, spinning ? T.CutRadius : T.Range, Fade(Blue, (spinning ? 0.45f : 0.22f) * (s < T.CastAt ? 1f : left)), Floor + 0.015f);
            if (s >= T.CastAt)
                PaperBombGraphics.RingAt(c, R * Smooth((s - T.CastAt) / (T.Form * 0.6f)), Fade(Blue, (s < T.FormedAt ? 0.7f : 0.3f) * left), Floor + 0.02f);

            // --- the warm-up aura round the carrier -----------------------------------------------------
            if (s >= T.CastAt && s < T.FormedAt + 0.3f)
                GokuGraphics.Aura(c, s, 0.5f * Mathf.Clamp01((s - T.CastAt) / T.Form) * (1f - Mathf.Clamp01((s - T.FormedAt) / 0.3f)), Blue);

            // --- the ring: blades rise out of the floor, circle, regrow after a shot, break at the end ----
            for (int i = 0; i < n && s >= T.CastAt; i++)
            {
                SwordShot fired = default;
                bool hasFired = false;
                foreach (SwordShot h in shots)
                    if (h.Slot == i && h.FireAt <= s) { fired = h; hasFired = true; }
                float angle = T.SlotAngle(i, n, scene, s);
                bool north = Mathf.Sin(angle * Mathf.Deg2Rad) > 0f;
                float layer = north ? PawnLayer - 0.02f : Overhead + 0.03f;

                // The second mode: each blade drags a thin arc of light.
                if (spun > 0f && sinceStop < 0f)
                {
                    for (int j = 0; j < ArcSteps; j++)
                        arcPoints[j] = T.RingPoint(c, angle - T.ArcBehind * spun * j / (ArcSteps - 1f), R, T.Height);
                    GokuGraphics.Line(arcPoints, 0.1f, Fade(Blue, 0.5f * spun), whiteGlow, layer - 0.001f);
                }

                if (sinceStop >= 0f)
                {
                    if (!hasFired || T.StopAt >= fired.RegrowAt + T.Grow * 0.5f)
                    {
                        Vector2 at = origin + T.CasterAt(scene, T.StopAt);
                        float stopAngle = T.SlotAngle(i, n, scene, T.StopAt);
                        Shatter(i + 1, T.RingPoint(at, stopAngle, T.RingRadius(scene, T.StopAt), T.Height), stopAngle, sinceStop);
                    }
                    continue;
                }

                if (hasFired)
                {
                    if (s < fired.RegrowAt) continue;
                    float u = Mathf.Clamp01((s - fired.RegrowAt) / T.Grow);
                    Vector2 mid = T.RingPoint(c, angle, R, T.Height);
                    if (u < 1f) Glint(mid, 0.22f * (1f - u), 1f - u, Snow, 30f);
                    Blade(mid, angle, u, layer, hot: 1f - u, shown: Smooth(u));
                    BladeShadow(mid, T.Height, angle, sun, shadow * 0.6f * u);
                    continue;
                }

                float appear = T.CastAt + i / (float)n * Mathf.Max(0f, T.Form - T.Rise), rise = Mathf.Clamp01((s - appear) / T.Rise);
                if (s < appear) continue;
                float height = T.Height * Smooth(rise);
                Vector2 point = T.RingPoint(c, angle, R, height);
                if (rise < 1f) Glint(T.RingPoint(c, angle, R, 0f), 0.3f * (1f - rise), 1f - rise, Snow, 30f);
                Blade(point, angle, rise, layer, hot: 1f - rise);
                BladeShadow(point, height, angle, sun, shadow * 0.6f * rise);
            }

            // A thin line of light joins the blades, so the ring reads as one thing.
            if (s >= T.FormedAt - 0.1f && sinceStop < T.RingFade)
                PaperBombGraphics.RingAt(new Vector2(c.x, c.y + T.Height * SixPathsHeight.Lift), R,
                    Fade(Blue, 0.16f * Mathf.Clamp01((s - T.FormedAt + 0.1f) / 0.2f) * left), Overhead + 0.02f, false, whiteGlow);

            // --- the shots: turn, fly, stick, break -------------------------------------------------------
            foreach (SwordShot h in shots)
            {
                float age = s - h.FireAt;
                if (age < 0f) continue;
                Vector2 d = Turn(h.Deg), from = origin + h.From, chest = origin + h.Chest;
                if (age < 0.12f) Glint(from, 0.3f * (1f - age / 0.12f), 1f - age / 0.12f, Snow, 30f);

                if (s < h.HitAt)
                {
                    float u = Mathf.Clamp01(age / T.TurnTime), flown = Mathf.Max(0f, age - T.TurnTime) * T.Speed;
                    Vector2 outward = Turn(h.StartAngle) * (0.2f * Smooth(u) * (1f - Mathf.Clamp01(flown)));
                    Vector2 mid = from + outward + d * flown;
                    if (flown > 0f)
                    {
                        float back = Mathf.Min(flown, 2.4f);
                        Vector2 tail = mid - d * back;
                        Sprite((mid + tail) / 2f, back * 1.2f, 0.5f, Fade(Blue, 0.4f), glow, Overhead + 0.024f, -h.Deg);
                        Streak(tail, mid, 0.07f, Fade(Ice, 0.6f), whiteGlow, Overhead + 0.025f, 6);
                    }
                    Blade(mid, T.TurnTo(h.StartAngle, h.Deg, Smooth(u)), 1f, Overhead + 0.03f, hot: u);
                    continue;
                }

                float breakAt = Mathf.Min(h.HitAt + T.Stuck, T.StopAt), lean = h.Deg + (Rand(h.K + 3) - 0.5f) * 24f;
                var tip = new Vector2(chest.x + (Rand(h.K + 9) - 0.5f) * 0.14f, chest.y + (Rand(h.K + 15) - 0.5f) * 0.14f);
                Vector2 stuckMid = tip - Turn(lean) * (T.StuckOut * 0.6f);
                if (s < breakAt) Blade(stuckMid, lean, 1f, Overhead + 0.03f, hot: Mathf.Clamp01(1f - (s - h.HitAt) / 0.15f), length: T.StuckOut);
                else Shatter(h.K + 20, stuckMid, lean, s - breakAt);
                float hit = (s - h.HitAt) / 0.16f;
                if (hit < 1f)
                {
                    Sprite(tip, 0.7f * (1f - hit) + 0.2f, 0.7f * (1f - hit) + 0.2f, Fade(Ice, 0.6f * (1f - hit)), glow, Overhead + 0.04f);
                    Glint(tip, 0.4f * (1f - hit), 1f - hit, Snow, h.Deg);
                }
            }

            // --- it ends: one thin ring front round the carrier -------------------------------------------
            if (sinceStop >= 0f && sinceStop < 0.3f)
                PaperBombGraphics.RingAt(new Vector2(c.x, c.y + T.Height * SixPathsHeight.Lift), R * (1f + 0.6f * Smooth(sinceStop / 0.3f)),
                    Fade(Ice, 0.6f * (1f - sinceStop / 0.3f)), Overhead + 0.03f, false, whiteGlow);
        }

        /// <summary>
        /// One blade, from the middle of its length: a dark under-line so it reads on pale ground, a soft
        /// halo, a blue-white body and a white core that taper to the point, a cross guard and a hilt.
        /// <paramref name="shown"/> 0 to 1 grows it from the guard to the tip.
        /// </summary>
        private static void Blade(Vector2 mid, float deg, float alpha, float layer, float hot = 0f, float length = T.BladeLength, float shown = 1f)
        {
            if (alpha <= 0f || shown <= 0f) return;
            Vector2 d = Turn(deg), root = mid - d * (length * 0.4f), side = new Vector2(d.y, -d.x) * 0.14f;
            float l = length * shown;
            Tapered(root, d, l, T.BladeWide * 1.5f, Fade(Deep, 0.55f * alpha * (1f - hot)), solid, layer);
            // The halo is a soft texture stretched along the blade, not a flat wedge, or it reads as solid.
            Sprite(root + d * (l * 0.5f), l * 1.3f, T.BladeWide * (4.5f + 3f * hot), Fade(Blue, 0.42f * alpha), glow, layer + 0.001f, -deg);
            Tapered(root, d, l, T.BladeWide, Fade(Color.Lerp(Blue, Ice, 0.45f + 0.55f * hot), 0.85f * alpha), whiteGlow, layer + 0.002f);
            Tapered(root, d, l, T.BladeWide * 0.3f, Fade(Snow, alpha), whiteGlow, layer + 0.003f);
            Streak(root + side, root - side, 0.08f, Fade(Ice, alpha), whiteGlow, layer + 0.003f, 4);
            Streak(root, root - d * (length * 0.24f), 0.06f, Fade(Ice, 0.8f * alpha), whiteGlow, layer + 0.002f, 4);
        }

        /// <summary>A blade's shadow on the floor: from under it, along the sun.</summary>
        private static void BladeShadow(Vector2 drawn, float height, float deg, Vector2 sun, float alpha)
        {
            if (alpha <= 0f) return;
            var ground = new Vector2(drawn.x + sun.x * height, drawn.y - height * SixPathsHeight.Lift + sun.y * height);
            Sprite(ground, 0.75f, 0.14f, Fade(Ink, alpha), soft, ShadowLayer, -deg);
        }

        /// <summary>A blade breaking like glass: short shards that fly out from along its length and drop, and a glint.</summary>
        private static void Shatter(int seed, Vector2 mid, float deg, float age)
        {
            if (age < 0f || age >= T.Break) return;
            float u = age / T.Break;
            Vector2 along = Turn(deg);
            for (int i = 0; i < T.Shards; i++)
            {
                float offset = (Rand(seed * 13 + i) - 0.5f) * T.BladeLength * 0.8f;
                float outward = Rand(seed * 17 + i + 40) * Mathf.PI * 2f, far = (0.2f + 0.45f * Rand(seed * 19 + i + 80)) * Smooth(u);
                Vector2 at = mid + along * offset + new Vector2(Mathf.Cos(outward), Mathf.Sin(outward)) * far - new Vector2(0f, 0.3f * u * u);
                float tilt = Rand(seed * 23 + i) * Mathf.PI + u * 3f, half = 0.05f + 0.06f * Rand(seed * 29 + i);
                var ray = new Vector2(Mathf.Cos(tilt), Mathf.Sin(tilt)) * half;
                Streak(at - ray, at + ray, 0.07f, Fade(Ice, 0.9f * (1f - u)), whiteGlow, Overhead + 0.035f, 3);
            }
            Glint(mid, 0.28f * (1f - u) + 0.06f, 1f - u, Snow, 30f);
        }
    }
}
