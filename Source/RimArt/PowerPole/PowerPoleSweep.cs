using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Sweep: when each part happens, the pole's angle and length, and when each pawn is hit.
    /// Seconds and degrees in, cells out, no drawing and no map. The defaults of the lab's
    /// power-pole-sweep.js. Angles named "phi" are degrees from the aim, positive to the caster's left.
    ///
    /// Reach, Arc, the wall and the enemies are the preview's script. The ability takes reach, arc and
    /// damage from XML and the pole's length per angle from the map, one table per cast.
    /// </summary>
    public static class PowerPoleSweepTiming
    {
        public const float Windup = 0.3f, Swing = 0.4f, Hold = 0.1f, Retract = 0.25f, Tail = 1f;
        public const float Shove = 0.25f, HitShake = 0.06f, FanLinger = 0.12f, BodyRadius = 0.25f, WallGap = 0.08f;
        public const int DustPuffs = 16, FanSteps = 10, AreaSteps = 72;
        // The preview's script: cells from the caster and degrees from the aim.
        public const float Reach = 4f, Arc = 180f, WallRange = 2.2f, WallPhi = -45f;
        public static readonly float[] EnemyRange = { 2f, 3.5f, 3.4f, 5f, 2.5f }, EnemyPhi = { 60f, 10f, -45f, -20f, 170f };
        public static readonly float[] FanSpans = { 0.12f, 0.07f, 0.035f };

        public static float SwingAt => Windup;
        public static float SwungAt => SwingAt + Swing;
        public static float RetractAt => SwungAt + Hold;
        public static float HomeAt => RetractAt + Retract;
        public static float Duration => HomeAt + Tail;
        public static float Half => Arc / 2f;

        /// <summary>
        /// How long the pole is at <paramref name="phi"/>: the reach, or up to the near face of the
        /// script's wall cell. <paramref name="aim"/> is the cast direction in degrees.
        /// </summary>
        public static float LengthAt(float phi, float aim, bool wall)
        {
            if (!wall) return Reach;
            float turn = (aim + WallPhi) * Mathf.Deg2Rad, line = (aim + phi) * Mathf.Deg2Rad;
            float enter = float.NegativeInfinity, exit = float.PositiveInfinity;
            for (int axis = 0; axis < 2; axis++)
            {
                float d = axis == 0 ? Mathf.Cos(line) : Mathf.Sin(line), c = WallRange * (axis == 0 ? Mathf.Cos(turn) : Mathf.Sin(turn));
                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (Mathf.Abs(c) > 0.5f) return Reach;
                    continue;
                }
                float one = (c - 0.5f) / d, two = (c + 0.5f) / d;
                enter = Mathf.Max(enter, Mathf.Min(one, two));
                exit = Mathf.Min(exit, Mathf.Max(one, two));
            }
            return enter <= exit && enter > 0f ? Mathf.Min(Reach, enter - WallGap) : Reach;
        }

        public static float AngleAt(float time)
        {
            if (time < SwingAt) return Half * PowerPoleGraphics.Smooth01(time / Windup);
            if (time < SwungAt) return Half - Arc * PowerPoleGraphics.Smooth01((time - SwingAt) / Swing);
            if (time < RetractAt) return -Half;
            return -Half * (1f - PowerPoleGraphics.Smooth01((time - RetractAt) / Retract));
        }

        /// <summary>When the swing passes <paramref name="phi"/>, or -1 outside the arc.</summary>
        public static float Passes(float phi)
        {
            if (Mathf.Abs(phi) > Half) return -1f;
            for (float time = SwingAt; time <= SwungAt; time += 0.004f)
                if (AngleAt(time) <= phi) return time;
            return SwungAt;
        }

        /// <summary>When the script's enemy is hit, or -1 if it is out of reach, behind the caster or behind the wall.</summary>
        public static float HitAt(int enemy, float aim, bool wall) =>
            EnemyRange[enemy] <= LengthAt(EnemyPhi[enemy], aim, wall) + BodyRadius ? Passes(EnemyPhi[enemy]) : -1f;

        public static float TipLength(float time, float aim, bool wall)
        {
            if (time < SwingAt) return Mathf.Lerp(PowerPoleGraphics.RestTip, LengthAt(Half, aim, wall), PowerPoleGraphics.Smooth01(time / Windup));
            if (time < RetractAt) return LengthAt(AngleAt(time), aim, wall);
            return Mathf.Lerp(LengthAt(-Half, aim, wall), PowerPoleGraphics.RestTip, PowerPoleGraphics.Smooth01((time - RetractAt) / Retract));
        }
    }
}
