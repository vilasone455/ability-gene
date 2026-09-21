using UnityEngine;
using P = RimArt.ShadowPlexusTiming;

namespace RimArt
{
    /// <summary>How a Shadow imitation hold ends: the time runs out, a pawn crosses the line, or a cell under it goes dark.</summary>
    public enum ImitationEnd { Released, Cut, Dark }

    /// <summary>What a Shadow imitation looks like now. Points are ground points on the map.</summary>
    public struct ImitationShot
    {
        public Vector2 Carrier, Target;
        /// <summary>Seconds since the cast began, and how long the line takes to run out.</summary>
        public float Seconds, Cast;
        /// <summary>When the hold ends, in the same seconds; positive infinity while it still holds.</summary>
        public float Release;
        public ImitationEnd End;
        /// <summary>Where a cut line was cut, as a share of carrier to target.</summary>
        public float CutShare;
        /// <summary>The range ring's radius now (<see cref="ShadowPlexusTiming.Range"/>); 0 draws none.</summary>
        public float Range;
        public float Width, Sway;
    }

    /// <summary>
    /// Timing of Shadow imitation: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/shadow-plexus-imitation.js; the constants are that sketch's
    /// defaults. There is no ability behind it yet. The rule (user's draft, placeholders): range
    /// 19.9 cells times the light level, 1 s cast, the target cannot act for 15 s and is dragged one
    /// cell with every step the carrier takes.
    /// </summary>
    public static class ShadowImitationTiming
    {
        public const float FullRange = 19.9f, Hold = 0.6f, StepPause = 0.12f, Retract = 0.5f, Tail = 0.9f, DragLag = 0.06f, DragSnap = 2.4f;
        public const float PoolRadius = 0.42f, KneeThreads = 0.35f, WalkerSpeed = 3f;
        public const int DarkSteps = 6;

        // The preview's script: the sketch's sliders at their defaults.
        public const float ScriptCast = 1f, ScriptStep = 0.45f, ScriptDistance = 7f, Width = 0.17f, Sway = 0.1f;
        public const int ScriptSteps = 3;

        /// <summary>How long the line takes to go once the hold ends.</summary>
        public static float After(ImitationEnd end) => end == ImitationEnd.Released ? Retract : P.SnapTime;

        /// <summary>The preview's script for one ending, laid out along the aim from the midpoint of carrier and target.</summary>
        public static ImitationPlan Plan(ImitationEnd end)
        {
            float half = ScriptDistance / 2f, per = ScriptStep + StepPause, walkStart = ScriptCast + Hold;
            var plan = new ImitationPlan { End = end, Half = half, WalkStart = walkStart, BreakAt = float.PositiveInfinity };
            switch (end)
            {
                case ImitationEnd.Released:
                    plan.Steps = ScriptSteps;
                    break;
                case ImitationEnd.Cut:
                    // Night, a campfire to one side; a pawn walks over the line 1 s after it holds.
                    plan.Night = true;
                    plan.Fire = new ShadowPlexusFire(new Vector2(0f, -2.2f), P.FireRadius, 1f);
                    plan.BreakAt = ScriptCast + 1f;
                    break;
                default:
                    // Night, a campfire behind the carrier: the target is dragged out of its light.
                    plan.Night = true;
                    plan.Fire = new ShadowPlexusFire(new Vector2(-half + 2f, -1.5f), P.FireRadius, 1f);
                    for (int k = 0; walkStart + k * 0.02f < walkStart + per * DarkSteps; k++)
                    {
                        float t = walkStart + k * 0.02f;
                        var target = new Vector2(half, P.Walked(t - walkStart - DragLag, ScriptStep, StepPause, DarkSteps, DragSnap));
                        if (P.Level(target, true, plan.Fire) < P.DarkBelow) { plan.BreakAt = t; break; }
                    }
                    if (float.IsPositiveInfinity(plan.BreakAt)) plan.BreakAt = walkStart + per * DarkSteps;
                    // The carrier finishes the step it is on.
                    plan.Steps = Mathf.CeilToInt((plan.BreakAt - walkStart) / per);
                    break;
            }
            plan.WalkEnd = walkStart + plan.Steps * per;
            plan.Release = end == ImitationEnd.Released ? plan.WalkEnd + 0.8f : plan.BreakAt;
            plan.Duration = plan.Release + After(end) + Tail;
            return plan;
        }
    }

    /// <summary>The preview's script for one ending. Points are (along the aim, to its left) from the midpoint.</summary>
    public struct ImitationPlan
    {
        public ImitationEnd End;
        public bool Night;
        public ShadowPlexusFire Fire;
        public int Steps;
        public float Half, WalkStart, WalkEnd, BreakAt, Release, Duration;

        public float Level(Vector2 local) => P.Level(local, Night, Fire);

        /// <summary>Where the carrier and the target stand, <paramref name="seconds"/> in.</summary>
        public void Places(float seconds, out Vector2 carrier, out Vector2 target)
        {
            const float step = ShadowImitationTiming.ScriptStep, pause = ShadowImitationTiming.StepPause;
            float moved = P.Walked(seconds - WalkStart, step, pause, Steps);
            float pulled = P.Walked(Mathf.Min(seconds, BreakAt) - WalkStart - ShadowImitationTiming.DragLag, step, pause, Steps, ShadowImitationTiming.DragSnap);
            float cast = ShadowImitationTiming.ScriptCast;
            bool held = seconds >= cast && seconds < Release;
            float shudder = held ? 0.04f * Mathf.Sin((seconds - cast) * 60f) * Mathf.Exp(-(seconds - cast) * 8f) : 0f;
            carrier = new Vector2(-Half, moved);
            target = new Vector2(Half + shudder, pulled);
        }
    }
}
