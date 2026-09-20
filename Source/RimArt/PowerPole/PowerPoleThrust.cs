using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Extend Thrust: when each part happens and where the pole's two ends are. Seconds in, cells
    /// out, no drawing and no map. The defaults of the lab's power-pole-extend-thrust.js. Distances
    /// named "along" are cells from the caster's feet toward the target.
    ///
    /// Distance, PushCells and the wall are the preview's script. The ability takes them from the
    /// cast: the range, push and damage are XML fields, not these constants.
    /// </summary>
    public static class PowerPoleThrustTiming
    {
        public const float Windup = 0.3f, Extend = 0.15f, Push = 0.22f, Hold = 0.15f, Retract = 0.3f, Tail = 1.2f;
        public const float PullBack = 0.35f, Lunge = 0.25f, BodyRadius = 0.25f;
        public const float HitShake = 0.12f, WallShake = 0.08f;
        // The preview's script.
        public const float Distance = 5f, PushCells = 3f, WallPast = 1.5f;
        public const int SpeedLines = 6, HitPuffs = 8, WallPuffs = 6;

        public static float ThrustAt => Windup;
        public static float HitAt => ThrustAt + Extend;
        public static float PushedAt => HitAt + Push;
        public static float RetractAt => PushedAt + Hold;
        public static float HomeAt => RetractAt + Retract;
        public static float Duration => HomeAt + Tail;

        /// <summary>The middle of the wall cell behind the target, from the caster's feet.</summary>
        public static float WallAlong => Distance + WallPast;
        /// <summary>How far the target is carried: the push, or less when a wall is in the way.</summary>
        public static float Room(bool wall) => wall ? Mathf.Min(PushCells, WallAlong - 0.5f - 0.3f - Distance) : PushCells;
        /// <summary>Where the tip meets the target's body.</summary>
        public static float Contact => Distance - BodyRadius;
        public static float Reach(bool wall) => Contact + Room(wall);

        public static float TipAt(float time, bool wall)
        {
            if (time < ThrustAt) return PowerPoleGraphics.RestTip - PullBack * PowerPoleGraphics.Smooth01(time / Windup);
            if (time < HitAt) return Mathf.Lerp(PowerPoleGraphics.RestTip - PullBack, Contact, PowerPoleGraphics.EaseOut((time - ThrustAt) / Extend));
            if (time < PushedAt) return Contact + Room(wall) * PowerPoleGraphics.EaseOut((time - HitAt) / Push);
            if (time < RetractAt) return Reach(wall);
            return Mathf.Lerp(Reach(wall), PowerPoleGraphics.RestTip, PowerPoleGraphics.Smooth01((time - RetractAt) / Retract));
        }

        public static float BackAt(float time)
        {
            float rest = PowerPoleGraphics.RestBack;
            if (time < ThrustAt) return rest - PullBack * PowerPoleGraphics.Smooth01(time / Windup);
            if (time < HitAt) return Mathf.Lerp(rest - PullBack, rest + Lunge, PowerPoleGraphics.EaseOut((time - ThrustAt) / Extend));
            if (time < RetractAt) return rest + Lunge;
            return Mathf.Lerp(rest + Lunge, rest, PowerPoleGraphics.Smooth01((time - RetractAt) / Retract));
        }

        /// <summary>When the tip first passes a place on the line, or -1 if it never does.</summary>
        public static float FirstTime(float along, bool wall)
        {
            for (float time = ThrustAt; time < PushedAt; time += 0.01f)
                if (TipAt(time, wall) >= along) return time;
            return -1f;
        }
    }
}
