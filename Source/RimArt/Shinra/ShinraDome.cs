using System;
using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// What one Shinra Tensei draws with: its dome's radius, the charge power (0 to 1) and which version it is.
    /// The tap and a hold let go before the first size are both the quick version (no white-out, the short shots
    /// window and pour); only the tap is <see cref="tap"/> (its own clip, a palm flash instead of the chest glow).
    /// </summary>
    public sealed class ShinraDomeCast
    {
        public float radius = 4f, power = 1f;
        public bool tap, quick;
        /// <summary>How long the dome holds (the shots window) and how long its lines pour down, in seconds.</summary>
        public float Defense => quick ? ShinraDome.TapDefense : ShinraDome.Defense;
        public float Pour => quick ? ShinraDome.TapPour : ShinraDome.PourTime;
        /// <summary>Power the picture uses: the tap draws with the sketch's fixed 0.35.</summary>
        public float Look => tap ? ShinraDome.TapPower : power;
    }

    /// <summary>
    /// Shinra Tensei v2's picture: timing and geometry of the lab sketch
    /// Tools/VfxLab/web/sketches/pain-shinra-tensei.js, seconds in, geometry out, no drawing and no map. Its
    /// numbers are the sketch's defaults. <see cref="ShinraDomeGraphics"/> draws it; its header says what is where.
    /// A cast's picture runs on e, the seconds since the burst; the charge picture on the seconds held.
    /// </summary>
    public static class ShinraDome
    {
        // The sketch's sliders at their defaults, and its decided constants.
        public const float Wave = 0.16f, DustFade = 1.8f, DomeStrength = 1f, StreakAmount = 0.7f;
        public const int Debris = 18, Swirls = 12, Streaks = 20, Scrapes = 22, Wisps = 18;
        public const float FlashT = 0.12f, DustDrift = 0.6f, FlashLife = 0.26f, RippleLife = 0.4f, WhiteoutT = 0.12f;
        public static readonly float[] Ripples = { 0.02f, 0.24f };
        public const float Defense = 0.75f, PourTime = 0.3f, TapDefense = 0.45f, TapPour = 0.2f, TapPower = 0.35f;
        public const float FullCharge = 3f, SizeStep = 1f;
        public const float Lift = SixPathsHeight.Lift;
        /// <summary>The shell's north half is this tall: the half sphere's outline bulges north by sqrt(1 + Lift^2).</summary>
        public static readonly float DomeK = Mathf.Sqrt(1f + Lift * Lift);
        /// <summary>The ground marks (scoured circle, lip, scrapes, pressed circle, stones) stay, then fade out by this many seconds.</summary>
        public const float MarkSeconds = 30f;
        /// <summary>Seconds after the burst when the moving parts (dome, dust, streaks) are all gone.</summary>
        public static float Settled(ShinraDomeCast cast) => cast.tap ? Mathf.Max(2.6f, 0.6f + DustFade + 0.6f) : Mathf.Max(3.2f, 0.8f + DustFade + 0.9f);

        public static float Smooth(float t) => VfxMath.Smooth(t);
        public static float Rand(int i) => VfxMath.Rand(i);
        public static float EaseOut(float x) { float u = 1f - Mathf.Clamp01(x); return 1f - u * u * u; }
        public static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;

        /// <summary>The front's radius e seconds after the burst.</summary>
        public static float Front(float e, float radius) => e <= 0f ? 0f : radius * EaseOut(e / Wave);
        /// <summary>When the front reaches d cells.</summary>
        public static float Reaches(float d, float radius) => Wave * (1f - (float)Math.Pow(Math.Max(0.0, 1.0 - d / radius), 1.0 / 3.0));

        /// <summary>The ground marks' alpha e seconds after the burst: whole, then gone by <see cref="MarkSeconds"/>.</summary>
        public static float MarkAlpha(float e) => 1f - Smooth(e / MarkSeconds);

        /// <summary>
        /// A point on the half sphere of radius rs, az round from east, el up from the floor, drawn in the projection
        /// (height goes north by Lift), relative to the centre. It faces the viewer when its normal points against
        /// the view direction (0, 1, -Lift): sin el - Lift cos el sin az &gt; 0.
        /// </summary>
        public static Vector2 DomeAt(float rs, float az, float el, out bool front)
        {
            float ce = Mathf.Cos(el);
            front = Mathf.Sin(el) - Lift * ce * Mathf.Sin(az) > 0f;
            return new Vector2(rs * ce * Mathf.Cos(az), rs * ce * Mathf.Sin(az) + rs * Mathf.Sin(el) * Lift);
        }

        /// <summary>The ground's "wobble" 0..1 round a circle, fixed per seed (the pressed rim, the lip).</summary>
        public static float Wob(float a, int seed) =>
            0.5f + 0.2f * Mathf.Sin(a * 5f + Rand(seed) * Mathf.PI * 2f) + 0.17f * Mathf.Sin(a * 13f + Rand(seed + 1) * Mathf.PI * 2f)
            + 0.13f * Mathf.Sin(a * 29f + Rand(seed + 2) * Mathf.PI * 2f);

        // ---- a pushed pawn (the sketch's pushed()): the game moves it at the burst; the picture flies it there ----

        /// <summary>Seconds a pawn pushed <paramref name="travel"/> cells is in the air, from when the front reaches it.</summary>
        public static float FlightSeconds(float travel) => 0.1f + 0.05f * travel;
        /// <summary>Share of the way flown at u (0..1 of the flight): fast out, easing into the landing.</summary>
        public static float FlightAlong(float u) => 1f - (1f - u) * (1f - u);
        /// <summary>Cells up at u: a low hop, 0.3 at most (a 4-cell push or more).</summary>
        public static float FlightHeight(float travel, float u) => 0.3f * Mathf.Min(1f, travel / 4f) * Bump(u);
        /// <summary>Cells across the landing sways, <paramref name="since"/> seconds after landing, for 0.5 s.</summary>
        public static float LandingSway(float since) => since < 0f || since >= 0.5f ? 0f : Mathf.Sin(since * 30f) * 0.05f * (1f - since / 0.5f);
        /// <summary>A body this big slides upright instead of being thrown (the sketch's heavy animal, body 2.4).</summary>
        public const float SlideBodySize = 2f;
        /// <summary>Seconds after landing that the picture keeps a pushed pawn (sway, dust, the wall-hit flash).</summary>
        public const float AfterLanding = 0.5f;

        /// <summary>The charge picture's size ring: the radius a release now would give after <paramref name="held"/> s, and its pulse at each step.</summary>
        public static float SizeRing(float held, float[] sizes, out float pulse)
        {
            int step = Mathf.Min(sizes.Length - 1, Mathf.FloorToInt(held / SizeStep));
            float since = held - step * SizeStep;
            pulse = step > 0 && since < 0.35f ? 1f - since / 0.35f : 0f;
            return sizes[step];
        }

        // ---- the lab preview: the sketch's default scene on one cell, no pawn ----

        /// <summary>The preview's timeline, as the sketch's times(): Lead, then the charge, then the burst at R.</summary>
        public const float Lead = 0.2f, Hold = 0.27f, TapBurst = 0.22f, ReleaseBurst = 0.11f;
        /// <summary>The sketch's sizes by charge: under 1 s the quick version's 2.5 cells, then 3 and 4.</summary>
        public static readonly float[] PreviewSizes = { 2.5f, 3f, 4f };

        public static ShinraDomeCast PreviewCast(float charge)
        {
            bool tap = charge <= 0f;
            float power = tap ? TapPower : Mathf.Clamp01(charge / FullCharge);
            return new ShinraDomeCast
            {
                tap = tap, quick = tap || charge < SizeStep, power = power,
                radius = tap ? PreviewSizes[0] : PreviewSizes[Mathf.Min(PreviewSizes.Length - 1, Mathf.FloorToInt(charge / SizeStep))],
            };
        }

        /// <summary>Scene seconds of the burst for a preview charged <paramref name="charge"/> s (0 = the tap).</summary>
        public static float PreviewBurst(float charge) => charge <= 0f ? Lead + TapBurst : Lead + Mathf.Max(charge, Hold) + ReleaseBurst;
        public static float PreviewEnd(float charge) => PreviewBurst(charge) + Settled(PreviewCast(charge));

        /// <summary>The sketch's phase markers for the preview.</summary>
        public static IEnumerable<(string name, float seconds)> PreviewPhases(float charge)
        {
            float r = PreviewBurst(charge);
            ShinraDomeCast cast = PreviewCast(charge);
            yield return ("Rest", 0f);
            if (charge <= 0f) yield return ("Hand out", Lead);
            else
            {
                yield return ("Hands to chest", Lead);
                yield return ("Hold (charging)", Lead + Hold);
                for (int i = 1; i < PreviewSizes.Length && i * SizeStep <= charge; i++)
                    yield return ($"Size {PreviewSizes[i]:0.#} cells", Lead + i * SizeStep);
            }
            yield return ("Burst", r);
            yield return ("Dome full size", r + Wave);
            yield return ("Dome gone (shots turned until)", r + cast.Defense);
            yield return ("Dust gone", r + 0.5f + DustFade);
        }
    }
}
