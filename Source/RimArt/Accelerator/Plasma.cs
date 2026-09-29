using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Plasma picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/accelerator-plasma.js). Seconds in, numbers out, no drawing.
    ///
    /// The picture's clock starts with the channel (0), which the sketch starts <see cref="Lead"/> s
    /// into its clip; the preview adds that lead itself. The ball is released at the channel's end,
    /// the head reaches its stop <see cref="Flight"/> x stop / length later, and the picture runs
    /// <see cref="Tail"/> s after that. None of this is balance: damage, radius, strain, the pull and
    /// the channel length are the ability's, and the game passes them in the shot.
    /// </summary>
    public static class Plasma
    {
        /// <summary>The sketch's stand still before the channel; the picture after the burst.</summary>
        public const float Lead = 0.3f, Tail = 2.5f;
        // The sketch's sliders at their defaults: lane length, burst radius, pull radius, ball across
        // when compressed, channel and flight over the full lane (s). The lane is Width cells wide.
        public const float Length = 15f, Radius = 1.5f, Pull = 8f, BallSize = 0.5f, Channel = 3f, Flight = 0.3f, Width = 1f;

        // Wind lines spiralling in from the pull ring: how many, laps per second, the share of the path each line covers.
        public const int WindLines = 30, Loose = 16;
        public const float WindSpeed = 0.55f, SegLen = 0.18f;
        /// <summary>The lane's pulse runs once a second; the ball is plasma for the last 30 % of the channel.</summary>
        public const float LanePulse = 1f, HotAt = 0.7f;
        public const int Sparks = 14, SmokePuffs = 10, BackDust = 10, SpeedLines = 6;
        public const float FlashTime = 0.15f, DomeOpen = 0.2f;
        /// <summary>The caster's lunge and slide on release in the sketch; the real pawn does not move, so neither is drawn.</summary>
        public const float Lunge = 0.12f, Recoil = 0.25f;
        /// <summary>A caught round spirals into the hand over BendTime s, Orbits turns, then flashes for Absorb s and the ball swells 25 %.</summary>
        public const float BendTime = 1.3f, Orbits = 1.25f, Absorb = 0.2f, Swell = 0.25f;
        /// <summary>The ball sits this far along the aim from the caster, at chest height; the arm reaches 0.4 while channelling and 0.48 on release.</summary>
        public const float HandOut = 0.42f, ArmChannel = 0.4f, ArmThrust = 0.48f;

        /// <summary>A broken channel (stun, downing, move) fades out over this long, then nothing is drawn.</summary>
        public const float CancelFade = 0.2f;
        /// <summary>With real fires in game the drawn fire cells flare and are gone FireFadeFrom + FireFadeTime s after the burst.</summary>
        public const float FireFadeFrom = 0.4f, FireFadeTime = 0.6f;

        /// <summary>Camera shakes: on release, on the burst, and AfterShakeDelay s after the burst.</summary>
        public const float FireShake = 0.07f, HitShake = 0.14f, AfterShake = 0.05f, AfterShakeDelay = 0.2f;

        // The preview's script, the sketch's stand-ins: the first pawn in the lane at FirstAt cells, a wall
        // WallBefore cells before it; two shooters standing ShooterGap cells outside the pull circle at
        // (cells along the lane, side), firing every FireEvery s from FirstShot to LastShot (sketch clock)
        // with rounds at RoundSpeed cells/s.
        public const float FirstAt = 9f, WallBefore = 3f;
        public static readonly float[] ShooterAlong = { 3f, -2f }, ShooterSide = { 1f, -1f };
        public const float ShooterGap = 1.3f, RoundSpeed = 10f, FireEvery = 0.9f, FirstShot = 0.5f, LastShot = 4.2f;
        public const int MostShots = 8;

        /// <summary>When the head reaches its stop, <paramref name="stop"/> cells down a lane <paramref name="length"/> long.</summary>
        public static float HitAt(float channel, float flight, float stop, float length) => channel + flight * stop / length;

        /// <summary>The picture's length on its own clock.</summary>
        public static float Duration(float channel, float flight, float stop, float length) => HitAt(channel, flight, stop, length) + Tail;

        /// <summary>Cells the head has travelled <paramref name="fired"/> s after release.</summary>
        public static float Reach(float fired, float stop, float flight, float length) =>
            stop * Mathf.Clamp01(fired / Mathf.Max(1e-4f, flight * stop / length));

        /// <summary>The channel's progress 0 to 1.</summary>
        public static float Charge(float seconds, float channel) => Mathf.Clamp01(seconds / channel);

        /// <summary>How far the arm is out: 0.4 while channelling, 0.48 on release, back in by 0.5 s after.</summary>
        public static float ArmReach(float seconds, float channel)
        {
            if (seconds < 0f) return 0f;
            if (seconds < channel) return ArmChannel * Smooth(seconds / 0.3f);
            float fired = seconds - channel;
            return fired < 0.5f ? ArmThrust * (1f - Smooth((fired - 0.2f) / 0.3f)) : 0f;
        }

        /// <summary>
        /// The white tint on the caster in the sketch, 0 to 0.3: it grows in the last 30 % of the channel
        /// and goes in the 0.3 s after release. The game's to draw on the real pawn, if at all.
        /// </summary>
        public static float CasterWhite(float seconds, float channel)
        {
            if (seconds < 0f) return 0f;
            float hot = Mathf.Clamp01((Charge(seconds, channel) - HotAt) / (1f - HotAt));
            return 0.3f * hot * (seconds < channel ? 1f : 1f - Mathf.Clamp01((seconds - channel) / 0.3f));
        }

        /// <summary>The preview's wall, in cells from the caster.</summary>
        public static float ScriptWallAt(float firstAt) => Mathf.Max(2f, firstAt - WallBefore);

        /// <summary>The preview's stop: 0.4 cells short of the first pawn, or half a cell short of the wall.</summary>
        public static float ScriptStop(float length, float firstAt, bool wall) =>
            Mathf.Min(length, wall ? ScriptWallAt(firstAt) - 0.5f : firstAt - 0.4f);

        /// <summary>When shooter <paramref name="shooter"/> fires its shot <paramref name="n"/>, on the sketch's clock.</summary>
        public static float ShotAt(int shooter, int n) => FirstShot + n * FireEvery + shooter * FireEvery / 2f;
    }
}
