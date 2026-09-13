using System;

namespace RimArt
{
    /// <summary>Seconds on a preview clock, independent of game speed and combat state.</summary>
    public static class ShinraVfxTiming
    {
        public const float ChargeEnd = 0.38f;
        public const float ExpansionEnd = 1.08f;
        public const float FlashEnd = 0.56f;
        public const float PeakTime = 1.20f;
        public const float ShellEnd = 2.05f;
        public const float Duration = 3.4f;
        public const float Radius = ShinraCharge.Radius;
        // The ground ring and dust carry the outward force instead of the dome, so they travel
        // past it. The dome itself holds one size.
        public const float RingSpan = 1.25f;

        // The dome arrives at full size and punches once: 88% at the release, 106% a tenth of a
        // second later, then settled. It never swells outward from nothing.
        public const float PopIn = 0.10f;
        public const float PopScale = 0.88f;
        public const float Overshoot = 0.06f;
        public const float Rebound = 0.22f;

        // Impact waves inside the dome: each is born at the core and dies against the shell.
        public const int ImpactPulses = 3;
        public const float ImpactSpacing = 0.20f;
        public const float ImpactLife = 0.44f;

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

        /// <summary>Outward travel of the ground ring and the dust it drives, 0 to 1.</summary>
        public static float Expansion(float time)
        {
            float t = Progress(time, ChargeEnd, ExpansionEnd);
            return 1f - (1f - t) * (1f - t);
        }

        public static float RingRadius(float time) => Radius * RingSpan * Expansion(time);

        /// <summary>The dome's own size: nothing, then a punch that settles to exactly 1.</summary>
        public static float ShellScale(float time)
        {
            if (time < ChargeEnd) return 0f;
            float pop = Smooth(Progress(time, ChargeEnd, ChargeEnd + PopIn));
            float settle = 1f - Smooth(Progress(time, ChargeEnd + PopIn, ChargeEnd + PopIn + Rebound));
            return PopScale + (1f - PopScale) * pop + Overshoot * pop * settle;
        }

        public static float ShellRadius(float time) => Radius * ShellScale(time);
        public static float ShellAlpha(float time) =>
            Smooth(Progress(time, ChargeEnd, ChargeEnd + 0.06f)) *
            (1f - Smooth(Progress(time, 1.40f, ShellEnd)));

        /// <summary>A bright burst at the instant of release, gone well before the shell fades.</summary>
        public static float ReleaseFlash(float time)
        {
            if (time < ChargeEnd) return 0f;
            float t = Progress(time, ChargeEnd, FlashEnd);
            return (1f - t) * (1f - t);
        }

        /// <summary>The ground ring leads the dome: full at release, gone once travel ends.</summary>
        public static float GroundRingAlpha(float time) =>
            Smooth(Progress(time, ChargeEnd, ChargeEnd + 0.05f)) *
            (1f - Smooth(Progress(time, ChargeEnd + 0.10f, ExpansionEnd + 0.35f)));

        /// <summary>Screen warp over the dome. It tracks the shell, not the outward ring.</summary>
        public static float DistortionIntensity(float time) =>
            Smooth(Progress(time, ChargeEnd, ChargeEnd + 0.05f)) *
            (1f - Smooth(Progress(time, 0.90f, ShellEnd)));

        public static float ImpactProgress(int index, float time)
        {
            float start = ChargeEnd + index * ImpactSpacing;
            return Progress(time, start, start + ImpactLife);
        }

        /// <summary>Brightest as it leaves the core, gone by the time it reaches the shell.</summary>
        public static float ImpactAlpha(int index, float time)
        {
            float t = ImpactProgress(index, time);
            if (t <= 0f || t >= 1f) return 0f;
            return (1f - t) * (1f - t);
        }

        public static float ImpactRadius(int index, float time) =>
            Radius * (0.10f + 0.90f * Smooth(ImpactProgress(index, time)));

        public static float DustAlpha(float time, float delay = 0f) =>
            Smooth(Progress(time - delay, ChargeEnd, 0.72f)) *
            (1f - Smooth(Progress(time, 1.55f, Duration)));

    }
}
