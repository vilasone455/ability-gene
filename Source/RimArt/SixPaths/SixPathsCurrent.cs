using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>A soft puff on the ground, in cells east and north of the orb's spot.</summary>
    public struct CurrentPuff
    {
        public Vector2 at;
        public float width, depth, alpha;
    }

    /// <summary>
    /// Pure clock for Devouring Current: one orb leaves the sage's ring and hovers in front of the
    /// sage, draws a funnel of wind in along the cast direction, holds what it pulled in, then
    /// fires it back out along the same line and goes back to its slot. Seconds in, geometry out;
    /// nothing here draws or touches the map.
    ///
    /// Everything is written along the cast direction, across it and up, and the orb hovers at
    /// one height, so the same code serves every direction.
    ///
    /// The numbers are the ones picked in the VFX lab's Devouring Current sketch
    /// (Tools/VfxLab/web/sketches/six-paths-devouring-current.js). There is no ability behind it
    /// yet, and what it pulls and fires is not drawn here.
    /// </summary>
    public static class SixPathsCurrentTiming
    {
        public const float Prepare = 0.40f, Pull = 1.5f, Hold = 0.45f, Release = 0.40f, Settle = 0.65f;
        /// <summary>The orb works from a spot this far in front of the sage, this high.</summary>
        public const float Behind = 0.9f, Hover = 1.05f;
        /// <summary>How far the funnel reaches and how wide its mouth is, in cells.</summary>
        public const float Reach = 6f, Width = 2f;
        public const float Wind = 0.8f, Shake = 0.065f;
        public const int Parcels = 62, ParcelPoints = 12, PullDust = 26, Compressions = 7, CompressionPoints = 12,
            Gusts = 9, GustPoints = 18, ReleaseDust = 16, CrescentPoints = 25;

        public static float PullAt => Prepare;
        public static float HoldAt => PullAt + Pull;
        public static float ReleaseAt => HoldAt + Hold;
        public static float StopAt => ReleaseAt + Release;
        public static float Duration => StopAt + Settle;

        private static float Arch(float u) => Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));

        /// <summary>Seconds since the release; negative before.</summary>
        public static float Age(float seconds) => seconds - ReleaseAt;

        /// <summary>
        /// A drawn point <paramref name="along"/> the cast direction, <paramref name="across"/> it
        /// and <paramref name="height"/> up, from the orb's spot.
        /// </summary>
        public static Vector2 At(Vector2 toward, float along, float across = 0f, float height = 0f) =>
            new Vector2(toward.x * along - toward.y * across, toward.y * along + toward.x * across + height * SixPathsHeight.Lift);

        /// <summary>How hard the orb is drawing, 0 to 1: up in 0.18 s as the pull starts, gone 0.1 s into the release.</summary>
        public static float Pressure(float seconds) => Smooth((seconds - Prepare) / 0.18f) * (1f - Smooth(Age(seconds) / 0.1f));

        public static float TailFade(float seconds) => 1f - Smooth((seconds - StopAt) / Settle);

        /// <summary>Where the orb hovers along the cast direction: it eases forward as it prepares and kicks back at the release.</summary>
        public static float OrbAlong(float seconds)
        {
            float age = Age(seconds), recoil = age >= 0f ? -0.19f * Arch(Mathf.Clamp01(age / 0.3f)) : 0f;
            return -0.3f * (1f - Smooth(seconds / Prepare)) + recoil;
        }

        /// <summary>How far the orb is from its ring slot (0) to its hover spot (1).</summary>
        public static float Deployed(float seconds) => Smooth(seconds / Prepare) * (1f - Smooth((seconds - StopAt) / Settle));

        /// <summary>Point <paramref name="j"/> of the pale crescent on the orb's inlet side, for an orb of <paramref name="radius"/>.</summary>
        public static Vector2 Crescent(Vector2 toward, int j, float radius, float seconds)
        {
            float angle = -1.1f + j / (float)(CrescentPoints - 1) * 2.2f, reach = radius - 0.03f;
            return At(toward, OrbAlong(seconds) + reach * Mathf.Cos(angle), reach * Mathf.Sin(angle), Hover);
        }

        /// <summary>
        /// Wind parcel <paramref name="index"/>: false when it is not showing. Parcels are emitted
        /// over and over while the orb pulls, each on its own lane of the funnel.
        /// </summary>
        public static bool Parcel(int index, float seconds, out float age, out float width, out bool pale, out float alpha)
        {
            age = width = alpha = 0f;
            pale = index % 3 == 0;
            float life = 0.5f + Rand(index + 21) * 0.28f, period = life + 0.08f, offset = Rand(index + 81) * period;
            float cycle = Mathf.Floor((seconds - Prepare - offset) / period);
            age = (seconds - (Prepare + offset + cycle * period)) / life;
            if (cycle < 0f || seconds >= ReleaseAt || age < 0f || age > 1f) return false;
            width = index % 4 == 0 ? 0.065f : 0.026f;
            alpha = Arch(age) * Wind * 0.48f;
            return true;
        }

        /// <summary>Point <paramref name="j"/> of parcel <paramref name="index"/>, which trails 0.19 of its life.</summary>
        public static Vector2 ParcelPoint(Vector2 toward, int index, int j, float age)
        {
            float v = Mathf.Max(0f, age - (1f - j / (float)(ParcelPoints - 1)) * 0.19f);
            float lane = (Rand(index + 140) - 0.5f) * 2f;
            float along = Mathf.Lerp(Reach, 0.38f, Mathf.Pow(Mathf.Clamp01(v), 1.65f)), funnel = 0.2f + 0.8f * Smooth(along / Reach);
            return At(toward, along, lane * Width * 0.5f * funnel + Mathf.Sin(v * 7f + index) * 0.065f * funnel,
                Hover + Mathf.Sin(index * 2.4f) * 0.16f);
        }

        /// <summary>Dust dragged along the ground toward the orb while it pulls; alpha 0 when it is not showing.</summary>
        public static CurrentPuff DraggedDust(Vector2 toward, int index, float seconds)
        {
            float u = (seconds - (Prepare + index / (float)PullDust * Pull)) / 0.65f;
            if (u < 0f || u > 1f || seconds >= ReleaseAt) return default;
            float along = Mathf.Lerp(1.8f + Rand(index + 280) * (Reach - 1.8f), 0.5f, u * u);
            return new CurrentPuff
            {
                at = At(toward, along, (Rand(index + 320) - 0.5f) * Width * (1f - u * 0.7f), 0.05f),
                width = 0.16f + u * 0.22f, depth = 0.12f + u * 0.14f, alpha = Arch(u) * Wind * 0.23f,
            };
        }

        /// <summary>True while the short compression lines run into the orb's inlet.</summary>
        public static bool Compressing(float seconds) => seconds >= Prepare && seconds < ReleaseAt;

        public static Vector2 CompressionPoint(Vector2 toward, int index, int j, float seconds)
        {
            float u = ((seconds - Prepare) * 2.2f + index / (float)Compressions) % 1f;
            float v = Mathf.Clamp01(u - (1f - j / (float)(CompressionPoints - 1)) * 0.2f);
            return At(toward, Mathf.Lerp(1.55f, 0.4f, v), (index % 2 == 1 ? 1f : -1f) * Mathf.Lerp(0.55f, 0.15f, v), Hover);
        }

        /// <summary>How far down the line the release has got, in cells.</summary>
        public static float Front(float sinceRelease)
        {
            float left = 1f - Mathf.Clamp01(sinceRelease / Release);
            return Mathf.Lerp(0.5f, Reach, 1f - left * left);
        }

        /// <summary>Point <paramref name="j"/> of outward gust <paramref name="index"/>; the gusts make a shallow arrowhead.</summary>
        public static Vector2 GustPoint(Vector2 toward, int index, int j, float seconds)
        {
            float v = j / (float)(GustPoints - 1), across = (index - 4) / 4f * Width * 0.5f;
            return At(toward, Front(Age(seconds)) - (1f - v) * 1.15f - Mathf.Abs(across) * 0.25f, across * (0.7f + 0.3f * v),
                Hover + Mathf.Sin(index * 2.1f) * 0.12f);
        }

        /// <summary>Dust left where the release passed, which drifts and thins; alpha 0 when it is not showing.</summary>
        public static CurrentPuff ReleasedDust(Vector2 toward, int index, float seconds)
        {
            float emitted = index / (float)ReleaseDust * Release, u = (Age(seconds) - emitted) / 0.5f;
            if (u < 0f || u > 1f) return default;
            return new CurrentPuff
            {
                at = At(toward, Front(emitted) + u * 0.3f, (Rand(index + 500) - 0.5f) * Width),
                width = 0.18f + u * 0.4f, depth = 0.14f + u * 0.25f, alpha = Arch(u) * Wind * 0.25f,
            };
        }
    }
}
