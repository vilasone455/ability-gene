using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Timing of Fusion: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/samehada-fusion.js; the constants are that sketch's defaults. The
    /// preview plays its script: merge, a 3.5 s fused walk along the aim at 1.3 cells a second, revert.
    /// The sketch's water patch and its second pawn are stand-ins and are not ported.
    ///
    /// In game the warmup is the sketch's lead-in: the ability fires at <see cref="Merge0"/> and the job
    /// holds until <see cref="Fused0"/>. The fused time is the ability's (15 s), the revert its last
    /// <see cref="Revert"/> seconds; the weapon stays in the hand, so the blade does not go into it.
    /// </summary>
    public static class SamehadaFusionTiming
    {
        public const float Rest = -30f;
        /// <summary>A normal pawn's display speed in cells a second, and the fused multiplier.</summary>
        public const float Speed = 1.0f, Boost = 1.3f;
        public const float Merge = 0.5f, Walk = 3.5f, Revert = 0.5f, Hold = 1.0f;
        /// <summary>The holder starts this far behind the chosen cell.</summary>
        public const float ScriptBack = 1.5f;
        public const float FusedShake = 0.015f;

        public static float Merge0 => SamehadaGraphics.Lead;
        public static float Fused0 => Merge0 + Merge;
        public static float Revert0 => Fused0 + Walk;
        public static float Done => Revert0 + Revert;
        public static float End => Done + Hold + SamehadaGraphics.Tail;

        /// <summary>How much of the shark shows at <paramref name="s"/>, for a fusion that reverts at <paramref name="revert0"/>.</summary>
        public static float SharkAt(float s, float revert0) =>
            s < revert0 ? Smooth((s - Merge0) / Merge) : 1f - Smooth((s - revert0) / Revert);

        /// <summary>The share of the blade out of the hand: in during the merge, out again in the revert (the preview only).</summary>
        public static float BladeOut(float s) => s < Revert0 ? 1f - Smooth((s - Merge0) / Merge) : Smooth((s - Revert0) / Revert);

        /// <summary>Charges on the blade: all spent over the merge.</summary>
        public static float ChargesAt(float s, float start) => s < Revert0 ? start * (1f - Smooth((s - Merge0) / Merge)) : 0f;

        /// <summary>How far the preview's holder has walked, cells.</summary>
        public static float Walked(float s) => Mathf.Min(Walk, Mathf.Max(0f, s - Fused0)) * Speed * Boost;
    }
}
