using UnityEngine;

namespace RimArt
{
    /// <summary>One point down the middle of the ribbon, in cells east and north of the target.</summary>
    public struct SerpentPoint
    {
        public Vector2 at;
        /// <summary>On the south half of the coil, which draws over the target; the rest draws under it.</summary>
        public bool front;
    }

    /// <summary>
    /// Pure clock for Black Serpent: one orb leaves the sage's ring and lands on the floor in front
    /// of the sage, feeds a ribbon along the ground to the target, the ribbon coils round the
    /// target's legs and tightens, holds, then unwinds back into the orb, which goes back to its
    /// slot. Seconds in, geometry out; nothing here draws or touches the map.
    ///
    /// The tether lies flat, so it turns freely with the cast direction. The coil is level, so it is
    /// the same picture for every direction and only its starting angle turns.
    ///
    /// The numbers are the ones picked in the VFX lab's Black Serpent sketch
    /// (Tools/VfxLab/web/sketches/six-paths-serpent.js). There is no ability behind it yet.
    /// </summary>
    public static class SixPathsSerpentTiming
    {
        public const float Wake = 0.40f, Travel = 0.70f, Wrap = 0.55f, Return = 0.65f, Settle = 0.35f;
        /// <summary>The preview's script: seconds the target is held. In the game that is the restraint's own length.</summary>
        public const float PreviewHold = 1.25f;
        /// <summary>The orb works from a spot on the floor this far in front of the sage.</summary>
        public const float Behind = 0.9f;
        /// <summary>Coil radius, turns and drawn rise; ribbon width, its pale edge, and how far the travelling tether swings.</summary>
        public const float Radius = 0.72f, Turns = 2.25f, Rise = 0.55f, Width = 0.16f, Edge = 0.018f, Wave = 0.45f;
        /// <summary>The coil is a level circle seen from above, so it is this much shallower than it is wide.</summary>
        public const float Squash = 0.60f;
        public const float CatchPulse = 0.5f, CatchSeconds = 0.35f;
        /// <summary>Points down the ribbon, and how many of them to the sketch's unit of length, which sets the taper at its tip.</summary>
        public const int Points = 161, PointsPerUnit = 80, TaperPoints = 7;

        public static float SeekAt => Wake;
        public static float ReachAt => SeekAt + Travel;
        public static float CatchAt => ReachAt + Wrap;
        public static float ReleaseAt => CatchAt + PreviewHold;
        public static float ReformAt => ReleaseAt + Return;
        public static float Duration => ReformAt + Settle;

        private static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);

        /// <summary>The ribbon fades in over the first 0.15 s and out over the settle.</summary>
        public static float Stage(float seconds) => Smooth(seconds / 0.15f) * (1f - Smooth((seconds - ReformAt) / Settle));

        /// <summary>How much ribbon is out: 0 none, 1 the tether has reached the target, 2 the coil is closed.</summary>
        public static float Extent(float seconds)
        {
            if (seconds >= ReleaseAt) return 2f * (1f - Smooth(Mathf.Clamp01((seconds - ReleaseAt) / Return)));
            return seconds < ReachAt ? Smooth((seconds - SeekAt) / Travel) : 1f + Smooth((seconds - ReachAt) / Wrap);
        }

        public static bool Bound(float seconds) => seconds >= CatchAt && seconds < ReleaseAt;

        /// <summary>How far the orb is from its ring slot (0) to its spot on the floor (1).</summary>
        public static float Deployed(float seconds) => Smooth(seconds / Wake) * (1f - Smooth((seconds - ReformAt) / Settle));

        /// <summary>The orb shrinks as it feeds the ribbon, and gains its mass back during recall.</summary>
        public static float Fed(float seconds) => Mathf.Sqrt(Mathf.Max(0.08f, 1f - Extent(seconds) / 2f));

        /// <summary>
        /// The ribbon's middle at <paramref name="u"/>: 0 to 1 is the tether from the orb's spot,
        /// <paramref name="range"/> cells back along <paramref name="toward"/>, and 1 to 2 is the coil.
        /// </summary>
        public static SerpentPoint Point(float u, float seconds, Vector2 toward, float range)
        {
            float tightened = Smooth((seconds - ReachAt) / Wrap), radius = Radius * (1f - 0.22f * tightened);
            if (u <= 1f)
            {
                float arch = Mathf.Max(0f, Mathf.Sin(Mathf.PI * u));
                float wave = Mathf.Sin(u * Mathf.PI * 2f - (seconds - Wake) * 5f) * arch * arch * Wave * (1f - 0.92f * tightened);
                // From the orb's spot to the coil's first point; the wave runs across that line.
                Vector2 start = -toward * range, end = new Vector2(-toward.x * radius, -toward.y * radius * Squash);
                Vector2 run = end - start;
                float length = run.magnitude;
                Vector2 side = length > 1e-5f ? new Vector2(-run.y, run.x) / length : Vector2.zero;
                return new SerpentPoint { at = start + run * u + side * wave };
            }
            float v = u - 1f, angle = Mathf.Atan2(toward.y, toward.x) + Mathf.PI + v * Mathf.PI * 2f * Turns;
            return new SerpentPoint
            {
                at = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * Squash + v * Rise),
                front = Mathf.Sin(angle) < 0f,
            };
        }

        /// <summary>Half the ribbon's width at <paramref name="u"/>; it tapers over its last few points to the tip at <paramref name="extent"/>.</summary>
        public static float Half(float u, float extent) =>
            Width * (0.30f + 0.70f * Smooth((extent - u) * PointsPerUnit / TaperPoints)) / 2f;

        /// <summary>The glint at the ribbon's tip: steady while it moves, breathing while the target is held.</summary>
        public static float Glint(float seconds) =>
            (Bound(seconds) ? 0.20f + 0.07f * Mathf.Sin((seconds - CatchAt) * 5f) : 0.14f) * Stage(seconds);

        /// <summary>The orb's flight between its ring slot and its spot; both are drawn points.</summary>
        public static Vector2 Path(Vector2 slot, Vector2 spot, float deployed) =>
            Vector2.LerpUnclamped(slot, spot, deployed)
                + new Vector2(0f, Mathf.Max(0f, Mathf.Sin(deployed * Mathf.PI)) * 0.3f * SixPathsHeight.Lift);
    }
}
