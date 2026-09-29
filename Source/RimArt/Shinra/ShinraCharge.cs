using System;

namespace RimArt
{
    /// <summary>
    /// One Shinra Tensei on its clip. Both clocks advance only from game ticks; release freezes the charge, not the
    /// clip. A tap plays RimArt_ShinraTap from the start. A hold plays RimArt_ShinraCharge: it opens, then its charge
    /// segment is sought by power (the arms step up at a third and two thirds), and the release jumps to the segment
    /// for the size reached. Clip times are make_shinra_anim.py's; the rule's numbers are <see cref="ShinraTuning"/>.
    /// </summary>
    public sealed class ShinraCharge
    {
        public const float TapBurst = 0.22f, TapEnd = 0.70f;
        public const float Open = 0.27f, Span = 1.00f;
        /// <summary>Release segments: the quick version from a hold (arms down and out), then one per size.</summary>
        public static readonly float[] ReleaseAt = { 1.29f, 2.39f, 3.49f };
        public const float ReleaseBurst = 0.11f, ReleaseLength = 1.08f;

        /// <summary>Started as a tap: the quick version on its own clip.</summary>
        public bool tap;
        /// <summary>Game ticks charged (a hold only), up to the full charge.</summary>
        public int ticks;
        /// <summary>Clip seconds.</summary>
        public float time;
        public bool releasing, burst;
        /// <summary>Set at release: -1 the quick version, else an index into <see cref="ShinraTuning.sizes"/>.</summary>
        public int size = -1;

        public static ShinraCharge Tap() => new ShinraCharge { tap = true, releasing = true };
        public static ShinraCharge Hold() => new ShinraCharge();

        public float Seconds => ticks / 60f;
        public float Power => Math.Min(1f, ticks / (float)ShinraTuning.Get.FullChargeTicks);
        public bool Quick => size < 0;
        /// <summary>The size a release now would give.</summary>
        public int SizeNow => tap ? -1 : ShinraTuning.Get.SizeAfter(Seconds);
        public bool Held => !releasing && time >= Open;

        // The rule for this cast: the quick version, or the size reached and the power charged.
        private static ShinraTuning T => ShinraTuning.Get;
        public ShinraSize Size => size >= 0 ? T.sizes[size] : null;
        public float Radius => Size?.radius ?? T.tapRadius;
        public float PushCells => Quick ? T.tapPush : ByPower(T.pushLow, T.pushHigh);
        public float WallDamage => Quick ? T.tapWallDamage : ByPower(T.wallDamageLow, T.wallDamageHigh);
        public float ShotLimit => Quick ? T.tapShotLimit : ByPower(T.shotLimitLow, T.shotLimitHigh);
        /// <summary>Explosive shots are turned only by a full charge.</summary>
        public bool TurnsExplosives => !Quick && Power >= 1f;
        private float ByPower(float low, float high) => low + (high - low) * Power;

        public float BurstAt => tap ? TapBurst : Segment + ReleaseBurst;
        public float End => tap ? TapEnd : Segment + ReleaseLength;
        private float Segment => ReleaseAt[Math.Min(ReleaseAt.Length - 1, size + 1)];

        public void Release()
        {
            if (releasing) return;
            size = SizeNow;
            releasing = true;
            time = Segment;
        }

        /// <summary>One game tick. True on the tick the wave bursts.</summary>
        public bool Advance(float speed)
        {
            float step = Math.Max(0f, speed) / 60f;
            if (!releasing)
            {
                ticks = Math.Min(ShinraTuning.Get.FullChargeTicks, ticks + 1);
                time = time < Open ? Math.Min(Open, time + step) : Open + Power * Span;
                return false;
            }
            time += step;
            if (burst || time + 0.000001f < BurstAt) return false;
            burst = true;
            return true;
        }

        public float SecondsToBurst(float speed) =>
            (releasing ? Math.Max(0f, BurstAt - time) : ReleaseBurst) / Math.Max(0.001f, speed);
    }
}
