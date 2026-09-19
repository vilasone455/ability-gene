using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// The numbers shared by every Flying Thunder God effect: the lab's lib/flying-thunder-god.js
    /// constants. Seconds and cells.
    /// </summary>
    public static class ThunderGodTiming
    {
        public const float SlashTime = 0.12f, SlashFade = 0.1f, SlashRadius = 0.85f, SlashHalfArc = 60f;
        public const float Afterimage = 0.3f, Scorch = 1.2f, SparkLife = 0.28f;
        /// <summary>Cells past the target, seen from where the caster came from, that the caster lands.</summary>
        public const float Behind = 1f;
        // Floor script. A strip starts StripBack cells before the kunai and runs into the landing cell.
        public const float StripBack = 0.3f, CrossStart = 0.15f, GlyphPitch = 0.13f, GlyphFlash = 0.05f;
        public const int StripGlyphs = 10, CrossGlyphs = 4;
        public const float Stroke = 0.03f, Bracket = 0.7f, BracketArm = 0.18f;
        public const float StarTurn = 12f, LeaveStar = 0.18f, LeaveStarScale = 0.7f;
        /// <summary>A touched barrier lights the glyphs within this many degrees for this long.</summary>
        public const float FlashArc = 40f, FlashTime = 0.25f;
        /// <summary>Glints, the ball and hit sparks sit this far north of the feet: chest height.</summary>
        public const float Chest = 0.3f;

        public static float Degrees(Vector2 toward) => Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Flying Thunder God, the single jump: when each part happens and where the two places are.
    /// The defaults of the lab's kunai-flying-thunder-god.js. The chosen cell is halfway between the
    /// two places, as in the sketch, so both are in view.
    /// </summary>
    public static class ThunderGodJumpTiming
    {
        public const float Lead = 0.4f, StrikeDelay = 0.04f;
        public const float Seal = 0.15f, Squeeze = 0.06f, Line = 0.08f, Flash = 0.24f, Linger = 1.35f;
        public const float FlashRadius = 1.2f, LineWidth = 0.12f, Distance = 7f, Shake = 0.04f;

        /// <summary>The seal lights and the floor script starts to be written.</summary>
        public static float CastAt => Lead;
        /// <summary>The caster narrows and the line joins the two places.</summary>
        public static float GoAt => CastAt + Seal;
        public static float ArriveAt => GoAt + Squeeze;
        public static float StrikeAt => ArriveAt + StrikeDelay;
        public static float HitAt => StrikeAt + ThunderGodTiming.SlashTime / 2f;
        /// <summary>The script starts to burn away.</summary>
        public static float SettleAt => ArriveAt + Flash;
        public static float Duration => SettleAt + Linger;

        /// <summary>The kunai's cell.</summary>
        public static Vector2 Kunai(Vector2 centre, Vector2 toward) => centre + toward * (Distance / 2f);
        public static Vector2 Home(Vector2 centre, Vector2 toward) => centre - toward * (Distance / 2f);
        public static Vector2 Landing(Vector2 centre, Vector2 toward, bool inEnemy) =>
            Kunai(centre, toward) + toward * (inEnemy ? ThunderGodTiming.Behind : 0f);
    }
}
