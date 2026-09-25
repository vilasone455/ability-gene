using UnityEngine;

namespace RimArt
{
    /// <summary>One Snag as the picture needs it. Ground points are x east, y north.</summary>
    public struct ChainSnagShot
    {
        /// <summary>The holder's feet and the target's ground point when the weight was thrown.</summary>
        public Vector2 Caster0, Start;
        /// <summary>Degrees from the holder to the target, 0 east, 90 north.</summary>
        public float Aim;
        /// <summary>Cells the target is pulled toward the holder, and cells the holder is dragged toward the target.</summary>
        public float Pull, Back;
        /// <summary>Seconds the reel (or the holder's drag) takes: the weight rule's reel.</summary>
        public float Reel;
        public bool Dragged;
        /// <summary>The target's body size: 1 a human; the coil grows with its square root.</summary>
        public float Size;
        /// <summary>The range ring's radius, cells.</summary>
        public float Range;
        /// <summary>Seconds the result shows after the reel.</summary>
        public float Hold;
        /// <summary>Where the holder and the target are now. Null: where the reel script puts them.</summary>
        public Vector2? CasterAt, TargetAt;
    }

    /// <summary>
    /// Timing of Snag: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/chain-sickle-snag.js; the constants are that sketch's defaults.
    ///   0.00 rest, 0.20 spin (2 turns overhead), 0.70 throw, 1.05 the weight hits and wraps,
    ///   1.40 reel (1 s / ratio, capped 2 s; the holder is dragged instead below ratio 0.4), then the result.
    /// </summary>
    public static class ChainSickleSnagTiming
    {
        public const float Spin = 0.5f, Flight = 0.35f, Wrap = 0.2f, Hold = 1.0f, Turns = 2f;
        /// <summary>Pause between the wrap and the reel.</summary>
        public const float Settle = 0.15f;
        /// <summary>How far the holder leans back while reeling, cells.</summary>
        public const float Lean = 0.10f;
        /// <summary>The overhead circle: radius and height, cells.</summary>
        public const float SpinR = 0.75f, SpinH = 0.85f;
        public const float HitShake = 0.02f, ReelShake = 0.015f;

        // The preview's script: the sketch's defaults.
        public const float ScriptDistance = 7f;

        public static float Spin0 => ChainSickleGraphics.Lead;
        public static float Throw0 => Spin0 + Spin;
        public static float Hit => Throw0 + Flight;
        public static float Wrapped => Hit + Wrap;
        public static float Reel0 => Wrapped + Settle;
        public static float ReelEnd(float reel) => Reel0 + reel;
        public static float End(float reel, float hold) => ReelEnd(reel) + hold + ChainSickleGraphics.Tail;

        /// <summary>The sketch's pull and drag at a distance: the target stops 1.5 cells short of the holder.</summary>
        public static void ScriptMoves(in ChainSickleWeight rule, float distance, out float pull, out float back)
        {
            pull = Mathf.Min(rule.Pull, distance - 1.5f);
            back = rule.Dragged ? Mathf.Min(ChainSickleNumbers.Sketch.DragBack, distance - 1.5f) : 0f;
        }

        /// <summary>Share of the reel done, eased out as the sketch's easeOut.</summary>
        public static float ReelShare(float seconds, float reel) =>
            seconds < Reel0 ? 0f : ChainSickleGraphics.EaseOut((seconds - Reel0) / Mathf.Max(0.001f, reel));
    }
}
