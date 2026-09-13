using System;

namespace RimArt
{
    // Both clocks advance only from game ticks. Release freezes power, not clip position.
    public sealed class ShinraCharge
    {
        public const float Hold = 0.27f, Burst = 0.38f, End = 1.35f;
        public const int FullTicks = 180, CooldownTicks = 1200, DefenseTicks = 45;
        public const float Radius = 4f;
        public int ticks;
        public float time;
        public bool releasing, burst;
        public float Power => Math.Min(1f, ticks / (float)FullTicks);
        public bool Held => !releasing && time >= Hold;
        public bool Advance(float speed)
        {
            if (!releasing) ticks = Math.Min(FullTicks, ticks + 1);
            time += Math.Max(0f, speed) / 60f;
            if (!releasing) time = Math.Min(Hold, time);
            if (!releasing || burst || time + 0.000001f < Burst) return false;
            burst = true;
            return true;
        }
        public float SecondsToBurst(float speed) => Math.Max(0f, Burst - time) / Math.Max(0.001f, speed);
        public float Push => 3f + 4f * Power;
        public float CollisionDamage => 8f + 12f * Power;
        public float ProjectileLimit => 12f + 48f * Power;
    }
}
