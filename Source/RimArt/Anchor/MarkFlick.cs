using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Mark card flick, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/anchor-mark-flick.js). Seconds in, numbers out, no drawing.
    ///
    /// The clock starts with the warmup. The mark is placed or lifted at the warmup's end, which the
    /// caller reads from the ability's warmupTime; <see cref="Place"/> is the number the clips were
    /// authored against and what the previews use. make_mark_anim.py carries the clip times.
    ///
    /// None of this is balance. Range, cooldown, marks held and mark life are on the AbilityDef and
    /// AnchorGeneExtension.
    /// </summary>
    public static class MarkFlick
    {
        /// <summary>RimArt_MarkFlick: the card leaves the hand at <see cref="Release"/> and lands at <see cref="Place"/>.</summary>
        public const float Release = 0.38f, Place = 0.5f, FlickLength = 0.8f;
        /// <summary>RimArt_MarkCatch: a lifted mark leaves its target at the warmup's end and is in the fingers this much later.</summary>
        public const float CatchFlight = 0.12f, CatchLength = 0.95f;
        /// <summary>A placed mark card stands up out of the flat flying card over this long.</summary>
        public const float Settle = 0.12f, SettlePop = 0.3f, SparkleLife = 0.3f;
        /// <summary>Degrees per second the flying card turns on the screen; the streak is this share of the path.</summary>
        public const float Spin = 1440f, Streak = 0.3f, StreakWidth = 0.07f;

        // Where the clips have the hand, in the aim's frame: forward reach (with the body's step),
        // shoulder-side offset, and lift up the screen. Melee Animation's own hand is not read.
        private const float ReleaseReach = 0.37f, ReleaseSide = -0.08f, ReleaseLift = 0.22f;
        private const float CatchReach = 0.48f, CatchSide = -0.14f, CatchLift = 0.30f;
        /// <summary>Cells above the ground the hand is taken to be, for the card's shadow.</summary>
        public const float HandHeight = 0.4f;

        /// <summary>
        /// The screen point of the carrier's hand when the card leaves it, or when it is caught,
        /// for a carrier standing at <paramref name="stands"/> aiming at <paramref name="target"/>.
        /// A westward aim plays the east clip mirrored, so the hand is worked out for the mirrored
        /// aim and flipped back.
        /// </summary>
        public static Vector2 Hand(Vector2 stands, Vector2 target, bool catching)
        {
            Vector2 aim = target - stands;
            aim = aim.sqrMagnitude < 1e-6f ? Vector2.right : aim.normalized;
            bool mirrored = Mathf.Abs(aim.x) >= Mathf.Abs(aim.y) && aim.x < 0f;
            if (mirrored) aim.x = -aim.x;
            float reach = catching ? CatchReach : ReleaseReach, side = catching ? CatchSide : ReleaseSide;
            float east = reach * aim.x - side * aim.y, north = reach * aim.y + side * aim.x + (catching ? CatchLift : ReleaseLift);
            return new Vector2(stands.x + (mirrored ? -east : east), stands.y + north);
        }

        /// <summary>The screen point of a mark card: over a pawn's head, or on the tile.</summary>
        public static Vector2 MarkPoint(Vector2 ground, ClapMark kind) =>
            kind == ClapMark.Pawn ? ClapTeleport.Above(ground, 0f, 0f, ClapTeleport.MarkHeight) : ground;

        public static float MarkHeight(ClapMark kind) => kind == ClapMark.Pawn ? ClapTeleport.MarkHeight : 0f;
    }
}
