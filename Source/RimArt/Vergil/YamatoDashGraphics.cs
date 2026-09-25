using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.VergilGraphics;
using T = RimArt.YamatoDashTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Yamato Dash: a thin aura and a hilt glint while the carrier prepares, the straight cut at
    /// chest height that grows behind it along the path, six speed lines flicking past on either side,
    /// dust at the start, along the path and at the stop, and on the click a glint at the hilt and one
    /// white line down the whole path for 0.12 s.
    ///
    /// The port of Tools/VfxLab/web/sketches/vergil-yamato-dash.js. Every part lies flat and turns with
    /// the aim, with height shown only as the northward chest offset, so no part needs a per-facing
    /// method.
    ///
    /// The sketch's stand-ins are not ported: the carrier, its held katana, the three afterimages, the
    /// hostiles and the ally, and with them the seams, the flinch and the wounds on the click. The
    /// carrier's travel is the ability's; the preview's hilt glints follow where the carrier would be.
    /// The sketch's path guides are a lab aid and are not drawn.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class YamatoDashGraphics
    {
        /// <summary>The hilt of the scabbard on the left hip, from the carrier's feet.</summary>
        private static readonly Vector2 Hilt = new Vector2(-0.02f, 0.46f);

        /// <summary>The preview. <paramref name="centre"/> is the chosen cell, the middle of the path.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, float seconds, Map map) =>
            Draw(new Vector2(centre.x, centre.z), aimDegrees, T.Distance, seconds, map);

        public static void Draw(Vector2 centre, float aimDegrees, float distance, float s, Map map)
        {
            if (s < 0f || s >= T.Duration) return;
            if (!Shown(centre, map)) return;
            Begin(centre);

            Vector2 d = Turn(aimDegrees);
            float travel = T.Travel(s), warm = Smooth((s - T.Lead) / T.Warm);
            Vector2 pos = T.At(centre, d, distance, distance * travel), hilt = pos + Hilt;
            Vector2 start = T.At(centre, d, distance, 0f, 0f, T.Chest), end = T.At(centre, d, distance, distance, 0f, T.Chest);

            // --- the prepare: a thin aura and a glint at the hilt -------------------------------------------
            if (s >= T.Lead && s < T.LaunchAt)
            {
                GokuGraphics.Aura(pos, s, 0.5f * warm, Blue);
                Glint(hilt, 0.06f + 0.12f * warm, warm, Blue, 20f);
            }

            if (s >= T.LaunchAt)
            {
                // The cut left in the air: one straight line at chest height, growing behind the carrier.
                // White while the blade is still on it, cooling to blue over 0.12 s once it stops.
                float fade = 1f - Smooth((s - T.ArriveAt) / T.TrailLife);
                Cut(start, end, travel, fade, 0.022f, 0.5f * (1f - Smooth((s - T.ArriveAt) / 0.12f)));

                // Speed lines: short streaks on either side, each thrown as the carrier passes it.
                for (int i = 0; i < T.Lines; i++)
                {
                    float u = (i + 0.5f) / T.Lines, age = s - (T.LaunchAt + u * T.Dash);
                    if (age < 0f || age >= T.LineLife) continue;
                    float f = 1f - age / T.LineLife, side = (i % 2 == 1 ? 1f : -1f) * (0.2f + 0.34f * Rand(i));
                    float back = u * distance - 0.35f - 1.1f * (1f - f);
                    Streak(T.At(centre, d, distance, back, side, T.Chest), T.At(centre, d, distance, back + 0.5f + 1.1f * f, side, T.Chest),
                        0.045f, Fade(Ice, 0.5f * f * f), whiteGlow, Overhead + 0.028f, 3);
                }

                // Small dust at the departure and at the braking foot, and under each footfall along the path.
                for (int e = 0; e < 2; e++)
                {
                    float u = (s - (e == 1 ? T.ArriveAt : T.LaunchAt)) / T.EndDust;
                    if (u < 0f || u > 1f) continue;
                    for (int side = -1; side <= 1; side += 2)
                        Sprite(T.At(centre, d, distance, e * distance - 0.25f * u, side * (0.16f + 0.25f * u)), 0.24f + 0.35f * u, 0.16f + 0.2f * u,
                            Fade(Grit, 0.35f * Mathf.Max(0f, Mathf.Sin(Mathf.PI * u))), soft, Floor + 0.025f + e * 0.001f);
                }
                for (int i = 0; i < T.Lines; i++)
                {
                    float u = (i + 0.5f) / T.Lines, v = (s - (T.LaunchAt + u * T.Dash)) / T.PathDust;
                    if (v < 0f || v > 1f) continue;
                    Sprite(T.At(centre, d, distance, u * distance - 0.3f * v, (Rand(i + 20) - 0.5f) * 0.5f), 0.3f + 0.5f * v, 0.2f + 0.32f * v,
                        Fade(Grit, 0.26f * Mathf.Max(0f, Mathf.Sin(Mathf.PI * v))), soft, Floor + 0.022f);
                }
            }

            // --- the click: a glint at the hilt, and the whole path lights for 0.12 s ----------------------
            float land = s - T.ClickAt;
            if (land >= 0f && land < T.ClickGlint)
            {
                float u = land / T.ClickGlint;
                Glint(hilt, 0.3f * (1f - u) + 0.06f, 1f - u, Snow, 20f);
            }
            if (land >= 0f && land < T.Flash) Cut(start, end, 1f, 1f - land / T.Flash, 0.02f, 1f);
        }
    }
}
