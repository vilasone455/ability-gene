using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>What Feed's replay says at one moment of the preview's script.</summary>
    public struct SamehadaFeedState
    {
        /// <summary>Charges on the blade, fractional while one is being drunk in.</summary>
        public float Charges;
        /// <summary>Drained stacks on the target.</summary>
        public int Stacks;
        /// <summary>The hit cycle now running (-1 before the first), and seconds into it.</summary>
        public int Current;
        public float Age;
    }

    /// <summary>
    /// Timing of Feed: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/samehada-feed.js; the constants are that sketch's defaults. The preview
    /// plays the sketch's script: three hits from no charges on a target 1.15 cells ahead.
    ///
    /// In game the swing is Core's; the picture starts at the bite, when a hit lands
    /// (<see cref="BiteLife"/>, <see cref="Drain"/>).
    /// </summary>
    public static class SamehadaFeedTiming
    {
        /// <summary>Blade angle at rest, behind the aim at the top of the swing, past it at the end (degrees from the aim).</summary>
        public const float Rest = -30f, Back = 70f, Through = 12f;
        /// <summary>Recovery time; how far the target rocks back at the bite; holder to target, cells.</summary>
        public const float Recover = 0.25f, Rock = 0.12f, Reach = 1.15f;
        public const float Windup = 0.22f, Swing = 0.14f, Drain = 0.45f, Hold = 1.0f;
        /// <summary>The preview's script: hits, and charges at the start.</summary>
        public const int ScriptHits = 3, ScriptStart = 0;
        /// <summary>A hit's picture in game: the scratches are gone this long after the bite; the grey puff of a full blade lasts this long.</summary>
        public const float BiteLife = 2.0f, FullPuff = 0.5f;
        /// <summary>The camera shake of each bite.</summary>
        public const float BiteShake = 0.015f;

        public static float Per => Windup + Swing + Drain + Recover;
        public static float First => SamehadaGraphics.Lead;
        public static float HitStart(int i) => First + i * Per;
        public static float Bite(int i) => HitStart(i) + Windup + Swing;
        public static float Result => First + ScriptHits * Per;
        public static float End => Result + Hold + SamehadaGraphics.Tail;

        /// <summary>Where the swing is <paramref name="a"/> seconds into one hit cycle: degrees relative to the aim.</summary>
        public static float SwingAngle(float a)
        {
            if (a < Windup) return Mathf.Lerp(Rest, -Back, Smooth(a / Windup));
            if (a < Windup + Swing)
                return Mathf.Lerp(-Back, Through, Mathf.Pow(ChainSickleGraphics.EaseOut((a - Windup) / Swing), 1.2f));
            float rest0 = Windup + Swing + Drain;
            if (a < rest0) return Through;
            return Mathf.Lerp(Through, Rest, Smooth((a - rest0) / Recover));
        }

        /// <summary>Replays the script's hits up to <paramref name="s"/>: charges, stacks, and the current hit's age.</summary>
        public static SamehadaFeedState Replay(float s, int most = SamehadaGraphics.MaxCharges)
        {
            int maxHits = Mathf.Min(ScriptHits, most - ScriptStart);
            var state = new SamehadaFeedState { Charges = ScriptStart, Current = -1 };
            for (int i = 0; i < ScriptHits; i++)
            {
                float b = HitStart(i), hit = Bite(i);
                if (s < b) break;
                state.Current = i;
                state.Age = s - b;
                if (s >= hit)
                {
                    state.Stacks = i + 1;
                    if (i < maxHits) state.Charges = ScriptStart + i + Mathf.Clamp01((s - hit) / Drain);
                }
            }
            return state;
        }

        public static int MaxHits(int most = SamehadaGraphics.MaxCharges) => Mathf.Min(ScriptHits, most - ScriptStart);
    }
}
