using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Yamato Dash's timing and path: when the hand goes to the hilt, when the dash starts and stops,
    /// when the blade clicks home, and where a point along the dash lies. Seconds in, geometry out, no
    /// drawing and no map. The port of Tools/VfxLab/web/sketches/vergil-yamato-dash.js; the constants
    /// are that sketch's default values.
    ///
    /// The ability behind it is proposed and not agreed, and every number is a placeholder that will
    /// become an XML field: a walkable cell up to 8 cells away along an unobstructed straight path,
    /// prepare 0.35 s, dash 0.15 s, sheathe 0.4 s. Each hostile within the 1-cell-wide path is marked as
    /// the carrier passes and every mark resolves on the click: 24 Cut at 40 % armour penetration, once
    /// each. Allies are never marked. Cooldown 8 s.
    /// </summary>
    public static class YamatoDashTiming
    {
        /// <summary>Decided values, the sketch's constants.</summary>
        public const float Lead = 0.3f, Tail = 1f, Width = 1f, TrailLife = 0.34f, LineLife = 0.3f, Flash = 0.12f;
        public const int Lines = 6;
        /// <summary>How long the click glint and the dust puffs last.</summary>
        public const float ClickGlint = 0.16f, EndDust = 0.3f, PathDust = 0.45f;
        /// <summary>A pawn's chest is drawn this far north of its feet.</summary>
        public const float Chest = 0.3f;

        /// <summary>The sketch's panel defaults, the ones the preview plays.</summary>
        public const float Aim = 0f, Distance = 6f, Warm = 0.35f, Dash = 0.15f, Sheathe = 0.4f;

        public static float CastAt => Lead;
        public static float LaunchAt => Lead + Warm;
        public static float ArriveAt => LaunchAt + Dash;
        public static float ClickAt => ArriveAt + Sheathe;
        public static float Duration => ClickAt + Tail;

        /// <summary>How far along the dash the carrier is at <paramref name="s"/>, 0 to 1.</summary>
        public static float Travel(float s) => Mathf.Clamp01((s - LaunchAt) / Dash);

        /// <summary>
        /// A point <paramref name="along"/> cells down the path, <paramref name="across"/> cells to its
        /// left and <paramref name="lift"/> cells north. The path is centred on <paramref name="centre"/>,
        /// as the sketch centres it on the chosen cell.
        /// </summary>
        public static Vector2 At(Vector2 centre, Vector2 d, float distance, float along, float across = 0f, float lift = 0f) =>
            new Vector2(centre.x + d.x * (along - distance / 2f) - d.y * across,
                centre.y + d.y * (along - distance / 2f) + d.x * across + lift);
    }
}
