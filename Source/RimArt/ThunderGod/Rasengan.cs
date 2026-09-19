using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Rasengan: when the ball forms, the teleport, the thrust, the grind, the release and the
    /// throw happen, and where the thrown pawn is. The defaults of the lab's kunai-rasengan.js.
    /// <c>teleports</c> is the cast on a marked enemy from range; without it the caster is already
    /// next to the enemy. <c>wall</c> stops the throw one cell short at full speed. The thrown
    /// pawn's path is the preview's script: nobody is thrown by this.
    /// </summary>
    public static class RasenganTiming
    {
        public const float Lead = 0.4f, Hand = 0.32f, Lean = 0.2f, Reach = 0.06f, Press = 0.3f, Swell = 1.2f, Unsteady = 0.16f;
        public const float Form = 0.6f, Squeeze = 0.05f, Fly = 0.35f, BallSize = 0.4f, Swirl = 1.2f, ThrowDistance = 3f, Distance = 7f;
        public const int Threads = 10, WindRings = 3, Puffs = 8, GrindSparks = 12, BurstLines = 14, VortexRings = 5, GrooveDust = 7, StopDust = 10;
        public const float WindEvery = 0.45f, BurstTime = 0.14f, BurstRadius = 1.6f, ShockTime = 0.3f, ShockRadius = 1.5f, ShockTurns = 1.2f;
        public const float TrailLength = 1.8f, TrailWave = 0.2f, VortexTime = 0.4f;
        public const float Tail = 1.3f, LineTime = 0.08f, FlashTime = 0.22f, StarSize = 1.2f, LineWidth = 0.12f;
        public const float HitShake = 0.03f, ReleaseShake = 0.07f, WallShake = 0.04f;

        public static float CastAt => Lead;
        public static float FormedAt => CastAt + Form;
        public static float ArriveAt(bool teleports) => teleports ? FormedAt + Squeeze : FormedAt;
        public static float ThrustAt(bool teleports) => ArriveAt(teleports) + (teleports ? 0.04f : 0.06f);
        /// <summary>The ball reaches the target's body and the grind starts.</summary>
        public static float HitAt(bool teleports) => ThrustAt(teleports) + Reach;
        public static float ReleaseAt(bool teleports) => HitAt(teleports) + Press;
        public static float Thrown(bool wall) => wall ? ThrowDistance - 1f : ThrowDistance;
        public static float LandAt(bool teleports, bool wall) => ReleaseAt(teleports) + Fly * Thrown(wall) / ThrowDistance;
        public static float Duration(bool teleports, bool wall) => LandAt(teleports, wall) + Tail;

        /// <summary>The thrust and the throw go back along the aim after a teleport, because the caster landed behind the target.</summary>
        public static float Direction(bool teleports) => teleports ? -1f : 1f;

        // The chosen cell is the middle of what happens, as in the sketch, so all of it is in view.
        public static Vector2 Enemy(Vector2 centre, Vector2 toward, bool teleports) =>
            centre - toward * (teleports ? (1f - Distance) / 2f : (ThrowDistance - 1f) / 2f);
        public static Vector2 Home(Vector2 centre, Vector2 toward, bool teleports) =>
            Enemy(centre, toward, teleports) - toward * (teleports ? Distance : 1f);
        /// <summary>Where the caster stands to thrust.</summary>
        public static Vector2 Spot(Vector2 centre, Vector2 toward, bool teleports) =>
            teleports ? Enemy(centre, toward, teleports) + toward * ThunderGodTiming.Behind : Home(centre, toward, teleports);

        /// <summary>0 to 1 through the throw, not clamped.</summary>
        public static float Flight(float seconds, bool teleports, bool wall) =>
            (seconds - ReleaseAt(teleports)) / (LandAt(teleports, wall) - ReleaseAt(teleports));

        /// <summary>Cells the thrown pawn has gone. It slows to a stop in the open and meets a wall at full speed.</summary>
        public static float Gone(float seconds, bool teleports, bool wall)
        {
            float u = Mathf.Clamp01(Flight(seconds, teleports, wall));
            return Thrown(wall) * (wall ? u : 1f - (1f - u) * (1f - u));
        }

        /// <summary>When the thrown pawn passes <paramref name="where"/> cells along its path.</summary>
        public static float Passes(float where, bool teleports, bool wall)
        {
            float share = where / Thrown(wall);
            return ReleaseAt(teleports) + (LandAt(teleports, wall) - ReleaseAt(teleports))
                * (wall ? share : 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - share)));
        }
    }
}
