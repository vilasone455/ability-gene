using UnityEngine;
using P = RimArt.ShadowPlexusTiming;

namespace RimArt
{
    /// <summary>How a Shadow double ends: its time runs out and it sinks and slides home, or its cell goes dark and it bursts.</summary>
    public enum DoubleEnd { TimeRunsOut, FireGoesOut }

    /// <summary>What a Shadow double looks like now. Points are ground points on the map.</summary>
    public struct DoubleShot
    {
        /// <summary>The carrier, the double's cell, and the pawn it holds with an Imitation cast from that cell.</summary>
        public Vector2 Carrier, Spot, Enemy;
        /// <summary>The way the flat shadow points while it slides, in degrees.</summary>
        public float Aim;
        /// <summary>
        /// Seconds since the cast began; the cast (the flat shadow slides out); when the Imitation from the
        /// double starts and when it holds; when that hold ends; when the double itself goes.
        /// </summary>
        public float Seconds, Cast, CastStart, HeldAt, Release, Gone;
        public DoubleEnd End;
        /// <summary>The Imitation range ring round the double now, from the double's own light; 0 draws none.</summary>
        public float Range;
    }

    /// <summary>
    /// Timing of Shadow double: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/shadow-plexus-double.js; the constants are that sketch's defaults.
    /// There is no ability behind it yet. The rule (user's draft, placeholders): a cell within 24.9
    /// cells that is at least 30 % lit, 1 s cast, 20 s; the carrier's shadow stands there, every other
    /// ability is cast from it and uses its light, and it copies the carrier's steps.
    /// </summary>
    public static class ShadowDoubleTiming
    {
        public const float ImitationRange = 19.9f, Rise = 0.35f, StepPause = 0.12f, LineOut = 0.6f, HoldFor = 1.4f, LineBack = 0.45f, Home = 0.6f;
        public const float Die = 1.2f, Burst = 0.25f, Tail = 0.6f, TieWidth = 0.07f, LineWidth = 0.17f, PoolRadius = 0.42f;

        // The preview's script: the sketch's sliders at their defaults. Night, one campfire, the carrier 9 cells away in the dark.
        public const float ScriptDistance = 9f, ScriptCast = 1f, ScriptStep = 0.45f;
        public const int ScriptSteps = 2;

        /// <summary>How long after <see cref="DoubleShot.Gone"/> the effect is over.</summary>
        public static float After(DoubleEnd end) => end == DoubleEnd.TimeRunsOut ? Rise + Home : Burst + LineBack;

        public static DoublePlan Plan(DoubleEnd end)
        {
            float per = ScriptStep + StepPause, stood = ScriptCast + Rise, walkStart = stood + 0.4f, castStart = walkStart + ScriptSteps * per + 0.3f;
            var plan = new DoublePlan
            {
                End = end, WalkStart = walkStart, CastStart = castStart, HeldAt = castStart + LineOut,
                Hearth = new Vector2(0.6f, -1.6f), Enemy = new Vector2(4f, 1f),
            };
            plan.DieStart = plan.HeldAt + 0.6f;
            if (end == DoubleEnd.TimeRunsOut)
            {
                plan.Release = plan.HeldAt + HoldFor;
                plan.Gone = plan.Release + LineBack;
            }
            else
            {
                // The raider's cell goes under 30 % first and the line dies; then the double's cell does.
                plan.Gone = plan.DarkAt(new Vector2(0f, ScriptSteps));
                plan.Release = Mathf.Min(plan.DarkAt(plan.Enemy), plan.Gone);
            }
            plan.Duration = plan.Gone + After(end) + Tail;
            return plan;
        }
    }

    /// <summary>The preview's script. Points are (along the aim, to its left) from the lit cell.</summary>
    public struct DoublePlan
    {
        public DoubleEnd End;
        public Vector2 Hearth, Enemy;
        public float WalkStart, CastStart, HeldAt, DieStart, Release, Gone, Duration;

        /// <summary>How far the campfire is still burning, 0 to 1. In the fire scene it dies over Die seconds.</summary>
        public float On(float seconds) => End == DoubleEnd.FireGoesOut ? Mathf.Clamp01(1f - (seconds - DieStart) / ShadowDoubleTiming.Die) : 1f;

        public float Level(Vector2 local, float seconds) => P.Level(local, true, new ShadowPlexusFire(Hearth, P.FireRadius, On(seconds)));

        /// <summary>When <paramref name="local"/> first goes under 30 % as the fire dies.</summary>
        public float DarkAt(Vector2 local)
        {
            for (int k = 0; DieStart + k * 0.01f < DieStart + ShadowDoubleTiming.Die; k++)
                if (Level(local, DieStart + k * 0.01f) < P.DarkBelow) return DieStart + k * 0.01f;
            return DieStart + ShadowDoubleTiming.Die;
        }

        /// <summary>Cells the carrier, and with it the double, has stepped to the left of the aim.</summary>
        public float Moved(float seconds) => P.Walked(seconds - WalkStart, ShadowDoubleTiming.ScriptStep, ShadowDoubleTiming.StepPause, ShadowDoubleTiming.ScriptSteps);
    }
}
