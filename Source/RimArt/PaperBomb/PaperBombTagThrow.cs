using UnityEngine;

namespace RimArt
{
    /// <summary>What a thrown tag has stuck in. The picture differs a little for each.</summary>
    public enum TagThrowTarget { Floor, Pawn, Wall }

    /// <summary>
    /// Timing and geometry of Tag Throw: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/paper-bomb-tag-throw.js; the constants are that sketch's defaults.
    /// The fuse and the radius are the ability's (XML); the values here are what the preview plays.
    /// </summary>
    public static class PaperBombTagThrowTiming
    {
        /// <summary>The tag leaves the hand, as in the RimArt_TagThrow clip.</summary>
        public const float Release = 0.3f;
        public const float Fly = 0.45f, Aftermath = 2.2f;
        public const float HandOut = 0.35f, HandHeight = 0.45f, Arc = 0.8f, Power = 1.2f;
        /// <summary>Tag scale in the air and stuck on a body.</summary>
        public const float FlyLong = 0.62f, CarriedLong = 0.5f;
        /// <summary>A wall is hit on the near edge of its top, this far short of the cell's middle, one cell up.</summary>
        public const float WallShort = 0.35f, WallHeight = 1f, PawnHeight = 0.5f;
        public const float BurstShake = 0.16f;

        // The preview's script: what the lab's sketch plays with its defaults.
        public const float ScriptDistance = 6f, ScriptFuse = 2f, ScriptRadius = 1.5f, ScriptCarrierWalks = 2.6f;

        public static float LandAt => Release + Fly;
        public static float BurstAt(float fuse) => LandAt + fuse;
        public static float Duration(float fuse) => BurstAt(fuse) + Aftermath;

        public static float StuckHeight(TagThrowTarget target) =>
            target == TagThrowTarget.Pawn ? PawnHeight : target == TagThrowTarget.Wall ? WallHeight : 0f;

        /// <summary>The preview's carrier: walks up to the target cell until the tag lands, then on toward its group.</summary>
        public static float ScriptCarrierAlong(float seconds)
        {
            if (seconds < LandAt) return Mathf.Lerp(-0.6f, 0f, seconds / LandAt);
            return ScriptCarrierWalks * VfxMath.Smooth((seconds - LandAt) / (ScriptFuse - 0.1f));
        }
    }
}
