using System;

namespace RimArt
{
    // Pure tuning and state transitions, shared by gameplay and executable tests.
    public static class GravityRules
    {
        public const float Range = 20f, Radius = 8f, BulletRadius = 5f, Core = 1.5f, BurstRadius = 2f;
        public const int OpeningTicks = 30, DurationTicks = 360, CooldownTicks = 2400;
        public const float FullMass = 200f, BodyMass = 60f, CoreDamage = 4f;
        public static float Damage(float mass) => 15f + 30f * Clamp(mass / FullMass);
        public static float Pull(float distance, float resistance) =>
            6f * Clamp((Radius - distance) / (Radius - Core)) / Math.Max(1f, resistance);
        public static float BendDegrees(float distance, float travel) =>
            20f * travel * Clamp((BulletRadius - distance) / (BulletRadius - Core));
        public static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
    }

    public enum GravityPhase { Opening, Channel, Finished }

    public sealed class GravityClock
    {
        public GravityPhase phase;
        public int ticks;
        public bool activated, imploded;
        public bool Tick()
        {
            if (phase == GravityPhase.Finished) return false;
            ticks++;
            if (phase == GravityPhase.Opening && ticks >= GravityRules.OpeningTicks)
            { phase = GravityPhase.Channel; ticks = 0; activated = true; }
            return phase == GravityPhase.Channel && ticks >= GravityRules.DurationTicks;
        }
        public bool Finish(bool implode)
        {
            if (phase == GravityPhase.Finished) return false;
            imploded = implode && activated;
            phase = GravityPhase.Finished;
            return imploded;
        }
    }
}
