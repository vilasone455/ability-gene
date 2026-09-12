using System;

namespace RimArt
{
    /// <summary>Seconds on a preview clock, independent of game speed and combat state.</summary>
    public static class ShinraVfxTiming
    {
        public const float ChargeEnd = 0.38f;
        public const float ExpansionEnd = 1.28f;
        public const float PeakTime = 1.20f;
        public const float ShellEnd = 2.05f;
        public const float Duration = 3.4f;
        public const float Radius = 8f;
        // An illustrated projection, not the map's camera tilt or a gameplay radius.
        public const float GroundDepth = 0.68f;
        public const float HeightLift = 0.75f;

        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
        public static float Progress(float time, float start, float end) => Clamp01((time - start) / (end - start));
        public static float Smooth(float value)
        {
            float t = Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        public static float Expansion(float time)
        {
            float t = Progress(time, ChargeEnd, ExpansionEnd);
            return 1f - (1f - t) * (1f - t);
        }

        public static float ShellRadius(float time) => Radius * (0.045f + 0.955f * Expansion(time));
        public static float ShellAlpha(float time) =>
            Smooth(Progress(time, ChargeEnd, ChargeEnd + 0.13f)) *
            (1f - Smooth(Progress(time, 1.40f, ShellEnd)));

        public static float DustAlpha(float time, float delay = 0f) =>
            Smooth(Progress(time - delay, ChargeEnd, 0.72f)) *
            (1f - Smooth(Progress(time, 1.55f, Duration)));

        public static float DebrisProgress(float time, float delay) => Progress(time, ChargeEnd + delay, 2.85f + delay);
    }
}
