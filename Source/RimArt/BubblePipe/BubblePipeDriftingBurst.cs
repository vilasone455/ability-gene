using System;
using UnityEngine;

namespace RimArt
{
    /// <summary>One bubble of a Drifting Burst at one moment, in the caster's aim frame (cells).</summary>
    public struct DriftingBubble
    {
        public float Along, Across, Height, Radius, Age;
        public bool Forming;
    }

    /// <summary>A bubble the preview's walking pawn popped: when, and where the bubble was.</summary>
    public struct DriftingPop
    {
        public bool Popped;
        public float At, Along, Across, Height;
    }

    /// <summary>
    /// Timing and geometry of Drifting Burst: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/bubble-pipe-drifting-burst.js; the constants are that sketch's defaults.
    /// The count, fan, drift speed and life of a real cast are the ability's (XML); the Script values are
    /// what the preview plays.
    ///
    /// Positions are in the caster's aim frame: along the cast, across it to the left, height up. The
    /// first bubble leaves the tip at <see cref="BlowAt"/>, one more every <see cref="Gap"/>; each takes
    /// <see cref="Form"/> to grow on the tip, then drifts in its share of the fan and bobs at half a cell.
    /// </summary>
    public static class BubblePipeDriftingBurstTiming
    {
        public const float Form = 0.2f, Gap = 0.18f, Hover = 0.5f, Bob = 0.12f, Radius = 0.28f;
        /// <summary>The blows one cast takes out of the jar, in the preview.</summary>
        public const int ScriptCost = 2, ScriptCount = 6;
        public const float ScriptSpread = 50f, ScriptSpeed = 0.7f, ScriptLife = 8f, ScriptBlows = 10f;
        /// <summary>The preview's floor ring at the blast radius, and its pawn's stagger.</summary>
        public const float ScriptBlast = 0.8f, ScriptStagger = 1f;
        /// <summary>
        /// The preview's script: the caster stands 2.5 cells behind the chosen cell. A pawn starts
        /// walking 1.5 s after the blow, across the path 0.4 cells past the chosen cell, at 1.6 cells a
        /// second from 2.6 cells to the right. A bubble within its radius + 0.28 of the pawn pops; the
        /// pawn stops for 0.4 of the stagger and walks at 0.6 speed for the rest. Checked every 1/30 s.
        /// </summary>
        public const float ScriptCasterBack = 2.5f, ScriptWalkAt = 1.5f, ScriptCrossAlong = 0.4f, ScriptStartAcross = -2.6f,
            ScriptWalkSpeed = 1.6f, ScriptSlowed = 0.6f, ScriptReach = 0.28f;
        private const double ScriptStep = 1.0 / 30.0;

        public static float BlowAt => BubblePipeGraphics.Lead + BubblePipeGraphics.Raise;
        public static float LastBlowAt(int count) => BlowAt + (count - 1) * Gap;
        /// <summary>The last bubble has left the tip.</summary>
        public static float AllOutAt(int count) => LastBlowAt(count) + Form;
        public static float ExpireAt(float life) => BlowAt + life;

        /// <summary>The preview keeps the pipe up until the bubbles are gone, as the sketch does.</summary>
        public static float ScriptLowerAt(float life) => ExpireAt(life) + 0.35f;
        public static float ScriptDuration(float life) => ScriptLowerAt(life) + BubblePipeGraphics.Lower + BubblePipeGraphics.Tail;

        /// <summary>A real cast lowers the pipe 0.1 s after the last bubble leaves it, so the caster is free after that.</summary>
        public static float CastLowerAt(int count) => AllOutAt(count) + 0.1f;
        public static float CastDuration(int count) => CastLowerAt(count) + BubblePipeGraphics.Lower;

        /// <summary>The jar's soap at <paramref name="seconds"/>: it drops <paramref name="cost"/> blows over the blowing.</summary>
        public static float Blows(float before, int cost, int count, float seconds) =>
            before - cost * Mathf.Clamp01((seconds - BlowAt) / (count * Gap));

        /// <summary>The radius of whichever bubble is growing on the tip now, 0 when none is.</summary>
        public static float Forming(int count, float seconds)
        {
            float forming = 0f;
            for (int i = 0; i < count; i++)
            {
                float age = seconds - (BlowAt + i * Gap);
                if (age >= 0f && age < Form) forming = Mathf.Max(forming, Radius * SixPathsSlamTiming.Smooth(age / Form));
            }
            return forming;
        }

        /// <summary>Where bubble <paramref name="i"/> is at <paramref name="seconds"/>, before any pop. False before it is blown.</summary>
        public static bool At(int i, int count, float spread, float speed, float seconds, out DriftingBubble bubble)
        {
            bubble = default;
            float age = seconds - (BlowAt + i * Gap);
            if (age < 0f) return false;
            float share = count > 1 ? i / (float)(count - 1) - 0.5f : 0f;
            float fan = share * spread * Mathf.Deg2Rad, seed = i * 1.7f;
            bool forming = age < Form;
            float run = Mathf.Max(0f, age - Form), dist = run * speed;
            float along = BubblePipeGraphics.TipAlong + Mathf.Cos(fan) * dist, across = BubblePipeGraphics.TipAcross + Mathf.Sin(fan) * dist;
            float sway = forming ? 0f : 0.10f * Mathf.Sin(run * 2.1f + seed) * Mathf.Clamp01(run);
            float tip = BubblePipeGraphics.TipH;
            bubble.Along = along;
            bubble.Across = across + sway * Mathf.Cos(fan);
            bubble.Height = forming ? tip : Mathf.Lerp(tip, Hover + Bob * Mathf.Sin(run * 1.7f + seed), SixPathsSlamTiming.Smooth(run / 1.2f));
            bubble.Radius = forming ? Radius * SixPathsSlamTiming.Smooth(age / Form) : Radius;
            bubble.Forming = forming;
            bubble.Age = age;
            return true;
        }

        /// <summary>
        /// The preview's walking pawn, replayed from the blow to <paramref name="seconds"/>: which bubbles
        /// it popped, when, and where each was. The same replay the
        /// sketch runs each frame, so the preview scrubs like the sketch. The pawn itself is not drawn.
        /// </summary>
        public static DriftingPop[] ScriptPops(float seconds)
        {
            var pops = new DriftingPop[ScriptCount];
            double along = ScriptCrossAlong + ScriptCasterBack, across = ScriptStartAcross, time = BlowAt + ScriptWalkAt;
            double haltUntil = -1, slowUntil = -1;
            for (; time <= seconds; time += ScriptStep)
            {
                double speed = time < haltUntil ? 0 : time < slowUntil ? ScriptWalkSpeed * ScriptSlowed : ScriptWalkSpeed;
                across += speed * ScriptStep;
                for (int i = 0; i < ScriptCount; i++)
                {
                    if (pops[i].Popped || !At(i, ScriptCount, ScriptSpread, ScriptSpeed, (float)time, out DriftingBubble b) || b.Forming) continue;
                    if (time >= ExpireAt(ScriptLife)) continue;
                    if (Math.Sqrt((b.Along - along) * (b.Along - along) + (b.Across - across) * (b.Across - across)) >= b.Radius + ScriptReach) continue;
                    pops[i] = new DriftingPop { Popped = true, At = (float)time, Along = b.Along, Across = b.Across, Height = b.Height };
                    haltUntil = time + ScriptStagger * 0.4;
                    slowUntil = time + ScriptStagger;
                }
            }
            return pops;
        }
    }
}
