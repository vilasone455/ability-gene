using UnityEngine;
using G = RimArt.GokuTiming;

namespace RimArt
{
    /// <summary>The three scenes of the Instant Transmission preview.</summary>
    public enum TransmissionScene { Alone, Kidnap, Rescue }

    /// <summary>When the parts of one Instant Transmission happen, in seconds from the start of the preview.</summary>
    public struct TransmissionPlan
    {
        /// <summary>Fingers to the forehead; the body starts to slice away; it is gone; it starts to close up at the destination; it is whole; the preview ends.</summary>
        public float Cast, Go, Gone, Arrive, Landed, End;
        public float Warm, Vanish;
    }

    /// <summary>What an Instant Transmission looks like now. Points are ground points on the map.</summary>
    public struct TransmissionShot
    {
        /// <summary>The caster's cell before and after the jump, and the jump's direction, which sets the passenger's side.</summary>
        public Vector2 Home, Dest, Toward;
        public float Seconds;
        public TransmissionPlan Plan;
        /// <summary>A passenger comes along; it is hostile (stunned on arrival); it is downed (it lies).</summary>
        public bool Carries, Hostile, Lying;
        /// <summary>Pawns standing at the destination. They glow faintly while the caster feels for it.</summary>
        public Vector2[] Waiting;
        public int WaitingCount;
    }

    /// <summary>
    /// Timing of Instant Transmission: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/goku-instant-transmission.js; the constants are that sketch's
    /// defaults. There is no ability behind it yet. The rule (user's draft, placeholders): optionally
    /// touch one adjacent pawn, pick any standable cell, 0.5 s warmup, the caster appears there and
    /// the passenger on the cell beside it, on the same side as before; a hostile passenger arrives
    /// stunned 1.5 s.
    /// </summary>
    public static class GokuInstantTransmissionTiming
    {
        public const float Lead = 0.3f, Tail = 1.6f, Flicker = 0.15f, BlinkLife = 0.28f, HostileStun = 1.5f;
        /// <summary>The passenger's cell, across the jump direction.</summary>
        public const float Side = -1f;
        /// <summary>Who waits at the destination, as (along, across) from it: the kidnap's three melee colonists, the rescue's doctor.</summary>
        public static readonly Vector2[] KidnapWaiting = { new Vector2(1f, -1f), new Vector2(-1f, -1f), new Vector2(0f, -2f) }, RescueWaiting = { new Vector2(-1f, -2f) };

        // The preview's script: the sketch's sliders at their defaults.
        public const float ScriptDistance = 10f, ScriptWarm = 0.5f, ScriptVanish = 0.12f, ScriptGap = 0.06f;

        public static TransmissionPlan Plan(float warm = ScriptWarm, float vanish = ScriptVanish, float gap = ScriptGap)
        {
            var plan = new TransmissionPlan { Cast = Lead, Warm = warm, Vanish = vanish };
            plan.Go = plan.Cast + warm;
            plan.Gone = plan.Go + vanish;
            plan.Arrive = plan.Gone + gap;
            plan.Landed = plan.Arrive + vanish;
            plan.End = plan.Landed + Tail;
            return plan;
        }

        public static Vector2[] Waiting(TransmissionScene scene) =>
            scene == TransmissionScene.Kidnap ? KidnapWaiting : scene == TransmissionScene.Rescue ? RescueWaiting : System.Array.Empty<Vector2>();

        /// <summary>How far the caster has felt for the destination: up over 0.15 s from the cast, gone 0.1 s after the vanish starts.</summary>
        public static float Sensing(float s, in TransmissionPlan plan) => G.Smooth((s - plan.Cast) / 0.15f) * (1f - G.Smooth((s - plan.Go) / 0.1f));
    }
}
