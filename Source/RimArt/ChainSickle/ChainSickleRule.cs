using System;

namespace RimArt
{
    /// <summary>
    /// The numbers of the weight rule. In game they are the XML fields of CompProperties_ChainSickle;
    /// <see cref="Sketch"/> is the lab sketch's values, which the previews use.
    /// </summary>
    public struct ChainSickleNumbers
    {
        /// <summary>Cells Snag pulls at ratio 1 and at most.</summary>
        public float FullPull;
        /// <summary>Reel seconds at ratio 1: the reel takes this / ratio.</summary>
        public float ReelSeconds;
        /// <summary>The reel never takes longer than this.</summary>
        public float MaxReel;
        /// <summary>Below this ratio the target stays, the holder is dragged, and Stake is not allowed.</summary>
        public float DragRatio;
        /// <summary>Cells the holder is dragged toward a too-heavy target, and over how many seconds.</summary>
        public float DragBack, DragTime;
        /// <summary>Stake's pin at ratio 1 and at most, seconds.</summary>
        public float MaxPin;

        public static ChainSickleNumbers Sketch => new ChainSickleNumbers
        {
            FullPull = 5f, ReelSeconds = 1f, MaxReel = 2f, DragRatio = 0.4f, DragBack = 2f, DragTime = 1f, MaxPin = 6f,
        };
    }

    /// <summary>What the weight rule says for one holder and one target.</summary>
    public readonly struct ChainSickleWeight
    {
        /// <summary>Holder carrying capacity over target mass plus gear.</summary>
        public readonly float Ratio;
        /// <summary>Too heavy: the target stays, the holder is dragged, Stake is not allowed.</summary>
        public readonly bool Dragged;
        /// <summary>Cells the target is pulled (0 when dragged).</summary>
        public readonly float Pull;
        /// <summary>Seconds the reel, or the holder's drag, takes.</summary>
        public readonly float Reel;
        /// <summary>Stake's pin in seconds; 0 means Stake is not allowed.</summary>
        public readonly float Pin;

        public ChainSickleWeight(float ratio, bool dragged, float pull, float reel, float pin)
        {
            Ratio = ratio;
            Dragged = dragged;
            Pull = pull;
            Reel = reel;
            Pin = pin;
        }
    }

    /// <summary>One of the sketch's stand-in targets: mass in kg (body plus gear) and body size for the drawing.</summary>
    public readonly struct ChainSickleTarget
    {
        public readonly string Label;
        public readonly float Mass, Size;

        public ChainSickleTarget(string label, float mass, float size)
        {
            Label = label;
            Mass = mass;
            Size = size;
        }
    }

    /// <summary>
    /// The Chain Sickle's weight rule, shared by Snag and Stake. The port of rule() in
    /// Tools/VfxLab/web/sketches/lib/chain-sickle.js:
    ///   ratio = holder carrying capacity / (target mass + carried gear), both in kg;
    ///   pull  = full pull x ratio, capped at the full pull;
    ///   reel  = reel seconds / ratio, capped at the max reel;
    ///   pin   = max pin x ratio, capped at the max pin;
    ///   ratio below the drag ratio: no pull, the reel is the drag time, and no pin (Stake refused).
    /// Plain arithmetic only, so a test can link this file without the game.
    /// </summary>
    public static class ChainSickleRule
    {
        /// <summary>The sketch's holder: a human's 75 kg carrying capacity.</summary>
        public const float ScriptCarry = 75f;

        /// <summary>The sketch's Targets, in its order.</summary>
        public static readonly ChainSickleTarget[] ScriptTargets =
        {
            new ChainSickleTarget("tribal 65 kg", 65f, 1f),
            new ChainSickleTarget("raider 95 kg", 95f, 1f),
            new ChainSickleTarget("muffalo 140 kg", 140f, 2.4f),
            new ChainSickleTarget("thrumbo 240 kg", 240f, 4f),
        };

        public static ChainSickleWeight Of(float carry, float mass, in ChainSickleNumbers n)
        {
            float ratio = mass > 0.001f ? carry / mass : 1000f;
            bool dragged = ratio < n.DragRatio;
            float pull = dragged ? 0f : Math.Min(n.FullPull, n.FullPull * ratio);
            float reel = dragged ? n.DragTime : Math.Min(n.MaxReel, n.ReelSeconds / Math.Max(0.001f, ratio));
            float pin = dragged ? 0f : Math.Min(n.MaxPin, n.MaxPin * ratio);
            return new ChainSickleWeight(ratio, dragged, pull, reel, pin);
        }

        public static ChainSickleWeight Script(int target) =>
            Of(ScriptCarry, ScriptTargets[target].Mass, ChainSickleNumbers.Sketch);
    }
}
