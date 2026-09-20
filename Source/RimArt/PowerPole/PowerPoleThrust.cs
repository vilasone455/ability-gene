using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// What one Extend Thrust did, in cells from the caster's feet toward the target. A real cast
    /// fills this from the map; the preview fills it from its script.
    /// </summary>
    public struct PowerPoleThrustShot
    {
        /// <summary>Where the tip stops first: at the body of the pawn it hits, or at the end of the line.</summary>
        public float Contact;
        /// <summary>How far the pawn is carried after that. 0 when nothing was hit.</summary>
        public float Room;
        /// <summary>A pawn was hit.</summary>
        public bool Hit;
        /// <summary>A wall stopped the carry early.</summary>
        public bool Blocked;

        public float Reach => Contact + Room;
        /// <summary>The near face of the wall that stopped the carry.</summary>
        public float WallFace => Reach + PowerPoleThrustTiming.BodyRadius + 0.3f;
    }

    /// <summary>
    /// Extend Thrust: when each part happens and where the pole's two ends are. Seconds in, cells
    /// out, no drawing and no map. The defaults of the lab's power-pole-extend-thrust.js. These are
    /// the picture's timings and shape. Range, push and damage are XML fields on the ability.
    /// </summary>
    public static class PowerPoleThrustTiming
    {
        public const float Windup = 0.3f, Extend = 0.15f, Push = 0.22f, Hold = 0.15f, Retract = 0.3f, Tail = 1.2f;
        public const float PullBack = 0.35f, Lunge = 0.25f, BodyRadius = 0.25f;
        public const float HitShake = 0.12f, WallShake = 0.08f;
        public const int SpeedLines = 6, HitPuffs = 8, WallPuffs = 6;
        // The preview's script.
        public const float ScriptDistance = 5f, ScriptPush = 3f, ScriptWallPast = 1.5f;

        public static float ThrustAt => Windup;
        public static float HitAt => ThrustAt + Extend;
        public static float PushedAt => HitAt + Push;
        public static float RetractAt => PushedAt + Hold;
        /// <summary>The pole is back to its carried length: the caster is free again.</summary>
        public static float HomeAt => RetractAt + Retract;
        public static float Duration => HomeAt + Tail;

        /// <summary>The preview's shot: a pawn 5 cells away carried 3 cells, or up to a wall 1.5 cells behind it.</summary>
        public static PowerPoleThrustShot Script(bool wall)
        {
            float room = wall ? Mathf.Min(ScriptPush, ScriptDistance + ScriptWallPast - 0.5f - 0.3f - ScriptDistance) : ScriptPush;
            return new PowerPoleThrustShot { Contact = ScriptDistance - BodyRadius, Room = room, Hit = true, Blocked = room < ScriptPush };
        }

        public static float TipAt(float time, in PowerPoleThrustShot shot)
        {
            if (time < ThrustAt) return PowerPoleGraphics.RestTip - PullBack * PowerPoleGraphics.Smooth01(time / Windup);
            if (time < HitAt) return Mathf.Lerp(PowerPoleGraphics.RestTip - PullBack, shot.Contact, PowerPoleGraphics.EaseOut((time - ThrustAt) / Extend));
            if (time < PushedAt) return shot.Contact + shot.Room * PowerPoleGraphics.EaseOut((time - HitAt) / Push);
            if (time < RetractAt) return shot.Reach;
            return Mathf.Lerp(shot.Reach, PowerPoleGraphics.RestTip, PowerPoleGraphics.Smooth01((time - RetractAt) / Retract));
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
        public static float FirstTime(float along, in PowerPoleThrustShot shot)
        {
            for (float time = ThrustAt; time < PushedAt; time += 0.01f)
                if (TipAt(time, shot) >= along) return time;
            return -1f;
        }
    }
}
