using System;

namespace RimArt
{
    // Speeds are cells per tick; distances are cells. Shared by driving and its numerical checks.
    public static class ToyCarMotion
    {
        public const float ArrivalDistance = 0.001f;
        public const float MaxSpeed = 1f / ToyCarDefaults.TicksPerCell;
        public const float Acceleration = MaxSpeed / ToyCarDefaults.AccelerationTicks;
        public const float Braking = MaxSpeed / ToyCarDefaults.BrakingTicks;
        public const float BrakingDistance = MaxSpeed * MaxSpeed / (2f * Braking);

        public static float CornerSpeed(float angle)
        {
            return MaxSpeed * (1f - 0.75f * Math.Min(Math.Abs(angle) / 90f, 1f));
        }

        public static float ApproachSpeed(float exitSpeed, float distance)
        {
            // Account for this tick's travel as well as the continuous stopping distance.
            return Math.Min(MaxSpeed, Math.Max(0f,
                (float)Math.Sqrt(exitSpeed * exitSpeed + 2f * Braking * Math.Max(0f, distance)
                                 + Braking * Braking) - Braking));
        }

        public static float ChangeSpeed(float current, float target)
        {
            return current < target
                ? Math.Min(target, current + Acceleration)
                : Math.Max(target, current - Braking);
        }
    }
}
