using UnityEngine;

namespace RimArt
{
    /// <summary>What the Trace On preview plays (the sketch's Scenario).</summary>
    public enum TraceOnScenario { EmptyHand, SwapCopy, RealWeapon, Downed }

    /// <summary>
    /// The times of one trace into the hand, from the moment it starts: the arm line in the first quarter of the
    /// cast, the wire from 25 % to 65 %, the steel from 55 % to the end, the glint at the end (Lit); the wire
    /// fades over the next <see cref="TraceOnTiming.WireFade"/> s. lib: trace-on.js times().
    /// </summary>
    public readonly struct TraceTimes
    {
        public readonly float At, Wire, Wired, Fill, Lit;

        public TraceTimes(float at, float cast)
        {
            At = at;
            Wire = at + 0.25f * cast;
            Wired = at + 0.65f * cast;
            Fill = at + 0.55f * cast;
            Lit = at + cast;
        }
    }

    /// <summary>
    /// Trace On's picture timing and sizes (Tools/VfxLab/web/sketches/trace-on.js). In game the cast is the
    /// ability's warmup, as long as the pawn's aiming delay makes it, and Lit is the tick the copy is put in the
    /// hand; before the warmup the job holds the pawn <see cref="StowTime"/> while a real weapon goes to the
    /// inventory, or <see cref="SwapGap"/> after a held copy breaks. The scenario times (CastAt, SwapAt) are the
    /// preview's script.
    /// </summary>
    public static class TraceOnTiming
    {
        /// <summary>The sketch's defaults: the cast, the copy's size in the lab (x its picture), the wire's brightness.</summary>
        public const float Cast = 0.6f, Size = 1.1f, Bright = 0.9f;
        /// <summary>The preview's script: when the first trace starts and when the swap or the downing happens.</summary>
        public const float CastAt = 0.2f, SwapAt = 1.6f;
        /// <summary>A real weapon slides to the hip for this long before the trace; a new copy starts this long after the old one breaks.</summary>
        public const float StowTime = 0.25f, SwapGap = 0.1f;
        /// <summary>A copy breaking into light; a dropped copy falls this long first.</summary>
        public const float Break = 0.35f, Fall = 0.28f;
        public const float WireFade = 0.3f, Glint = 0.25f, Flash = 0.2f, Dust = 0.5f;
        /// <summary>The width-table steps (of 16, point to pommel) the four cross lines sit on.</summary>
        public static readonly int[] Crosses = { 2, 5, 8, 11 };
        /// <summary>The stand-in's carry angle in the lab by facing (degrees, 0 east, 90 north).</summary>
        public const float RestEast = 55f, RestWest = 125f;
        /// <summary>How long the preview holds after the last copy is lit.</summary>
        public const float Tail = 1.2f;

        /// <summary>The preview's first trace.</summary>
        public static TraceTimes First(TraceOnScenario scenario) =>
            new TraceTimes(scenario == TraceOnScenario.RealWeapon ? CastAt + StowTime : CastAt, Cast);

        /// <summary>The preview's second trace (swap copy only).</summary>
        public static TraceTimes Second => new TraceTimes(SwapAt + SwapGap, Cast);

        public static float Duration(TraceOnScenario scenario)
        {
            switch (scenario)
            {
                case TraceOnScenario.SwapCopy: return Second.Lit + Tail;
                case TraceOnScenario.Downed: return SwapAt + Tail;
                default: return First(scenario).Lit + Tail;
            }
        }

        /// <summary>A dropped copy's arc: sideways drift, height and turn (degrees, times side) <paramref name="fly"/> s after it left the hand.</summary>
        public static Vector3 FallArc(float fly) => new Vector3(0.6f * fly / Fall, 1.4f * fly - 4f * fly * fly, 500f * fly);
    }
}
