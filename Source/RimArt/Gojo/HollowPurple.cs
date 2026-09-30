using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>When each part of one Hollow Purple happens, in seconds from the start of the picture.</summary>
    public struct HollowPurpleTimes
    {
        /// <summary>Red leaves the finger, Red reaches Blue, the ignition, Purple starts to travel, Purple stops, Purple is gone, the picture ends.</summary>
        public float Fire, Contact, Ignite, Move, Stop, Gone, End;
    }

    /// <summary>
    /// Times and sizes of the Hollow Purple picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/gojo-purple.js). Seconds in, numbers out, no drawing.
    ///
    /// The picture's clock starts with Blue already open <see cref="BlueDist"/> cells in front of Gojo.
    /// Red charges at the finger from <see cref="Start"/> for the charge, flies at <see cref="RedSpeed"/>
    /// into Blue, the two merge, Purple ignites at Blue's centre, grows for <see cref="Grow"/> s, travels
    /// the run at its speed, breaks up over <see cref="Fade"/> s and the trench shading fades
    /// <see cref="Tail"/> s after that.
    /// </summary>
    public static class HollowPurple
    {
        // Balance. These defaults will come from XML when the combo is built; the ability passes its own
        // numbers in the shot: the sphere's radius and speed, the longest run (cells, or the map edge),
        // and the centre row, how far off the path a pawn's cell centre can be and still be erased.
        public const float Radius = 1.5f, Speed = 6f, MaxTravel = 30f, CentreRow = 0.5f;

        // The sketch's other sliders at their defaults: Blue's distance from Gojo and the run (cells), Red's
        // charge and the merge (s), and the world dim while it travels (0 to 1).
        public const float BlueDist = 6f, Travel = 14f, Charge = 0.5f, Merge = 0.35f, Dim = 0.35f;

        /// <summary>The arm rises over Raise s; Red starts to charge at Start; Red flies at RedSpeed cells/s from Tip cells out.</summary>
        public const float Raise = 0.15f, Start = 0.15f, RedSpeed = 20f, Tip = 0.8f;
        /// <summary>Purple grows over Grow s, breaks up over Fade s, and the picture runs Tail s after that.</summary>
        public const float Grow = 0.3f, Fade = 0.5f, Tail = 2f;
        /// <summary>Blue's sphere and Red's orb radius as they come in (cells).</summary>
        public const float BlueR = 0.9f, OrbR = 0.2f;
        public const int Bands = 12, Bolts = 5, Rays = 10, Specks = 24, Lifted = 18;
        /// <summary>A ripple ring leaves the sphere every RingEvery s while it travels.</summary>
        public const float RingEvery = 0.5f;
        /// <summary>
        /// The trench: its north wall TrenchDepth cells deep; a haze puff every BandStep cells living BandLife s;
        /// the cut edges in EdgeStep pieces cooling over EdgeCool s; dust every DustStep cells off both lips for DustLife s.
        /// </summary>
        public const float TrenchDepth = 0.6f, BandStep = 0.4f, BandLife = 1.1f, EdgeStep = 0.4f, EdgeCool = 1.5f, DustStep = 0.45f, DustLife = 2f;
        /// <summary>The trench shading holds TrenchHold s after the stop, then fades over TrenchFade; the dim comes in over DimIn and lifts over DimRelease.</summary>
        public const float TrenchHold = 0.4f, TrenchFade = 1.6f, DimIn = 0.3f, DimRelease = 0.6f;
        /// <summary>The cut on a side-row pawn glows for CutGlow s; the cut faces of a wall for WallCutGlow s.</summary>
        public const float CutGlow = 2.5f, WallCutGlow = 1.5f;
        /// <summary>A touched thing breaks into specks over Dissolve s.</summary>
        public const float Dissolve = 0.5f;

        /// <summary>Camera shakes from the sketch's events(): Red leaving, Red reaching Blue, the ignition, and a rumble every RumbleEvery s of travel.</summary>
        public const float FireShake = 0.03f, ContactShake = 0.05f, IgniteShake = 0.16f, RumbleShake = 0.035f, RumbleEvery = 0.5f;

        // The preview's script, the sketch's "wall and raiders" stand-ins: things on the path at (cells past Blue's
        // centre, cells across to the left of the path): a tree, a raider on the centre row (erased), an ally on a
        // side row (struck), a raider 2.4 cells off the line (only lit, not touched) and a crate; and a 7-cell wall
        // across the path at ScriptWallAt cells past Blue, cells ScriptWallCells across.
        public static readonly float[] ScriptAlong = { 2.5f, 4f, 6.5f, 11f, 12.5f }, ScriptAcross = { 0.5f, -0.3f, 0.8f, 2.4f, -0.6f };
        public static readonly HollowPurpleScriptThing[] ScriptKinds =
        {
            HollowPurpleScriptThing.Tree, HollowPurpleScriptThing.Raider, HollowPurpleScriptThing.Ally,
            HollowPurpleScriptThing.Raider, HollowPurpleScriptThing.Crate,
        };
        public const float ScriptWallAt = 9f;
        public static readonly int[] ScriptWallCells = { -3, -2, -1, 0, 1, 2, 3 };

        public static HollowPurpleTimes TimesFor(float blueDist, float travel, float charge, float merge, float speed)
        {
            var t = new HollowPurpleTimes { Fire = Start + charge };
            t.Contact = t.Fire + Mathf.Max(0f, blueDist - Tip) / RedSpeed;
            t.Ignite = t.Contact + merge;
            t.Move = t.Ignite + Grow;
            t.Stop = t.Move + travel / speed;
            t.Gone = t.Stop + Fade;
            t.End = t.Gone + Tail;
            return t;
        }

        /// <summary>The picture with the sketch's defaults.</summary>
        public static HollowPurpleTimes Default => TimesFor(BlueDist, Travel, Charge, Merge, Speed);

        /// <summary>Purple's centre now, in cells from Gojo along the aim.</summary>
        public static float Centre(in HollowPurpleTimes t, float seconds, float blueDist, float travel, float speed) =>
            blueDist + Mathf.Min(travel, Mathf.Max(0f, seconds - t.Move) * speed);

        /// <summary>How grown Purple is, 0 at the ignition to 1 when it starts to travel.</summary>
        public static float Grown(in HollowPurpleTimes t, float seconds) => Smooth((seconds - t.Ignite) / Grow);

        /// <summary>1 until Purple stops, then down to 0 over <see cref="Fade"/>.</summary>
        public static float Fading(in HollowPurpleTimes t, float seconds) => seconds >= t.Stop ? 1f - Smooth((seconds - t.Stop) / Fade) : 1f;

        /// <summary>Purple's radius as drawn: grown, and shrinking to 60 % while it breaks up.</summary>
        public static float Size(in HollowPurpleTimes t, float seconds, float radius) =>
            radius * Grown(t, seconds) * (seconds >= t.Stop ? 0.6f + 0.4f * Fading(t, seconds) : 1f);

        /// <summary>
        /// When the sphere reaches a thing <paramref name="along"/> cells from Gojo and <paramref name="across"/> off
        /// the path: when its centre comes within the radius of it. Infinity for a thing the sphere never covers.
        /// </summary>
        public static float HitAt(in HollowPurpleTimes t, float blueDist, float speed, float radius, float along, float across) =>
            Mathf.Abs(across) >= radius ? float.PositiveInfinity
                : t.Move + Mathf.Max(0f, along - blueDist - Mathf.Sqrt(radius * radius - across * across)) / speed;

        /// <summary>When the sphere's centre passed a point <paramref name="along"/> cells from Gojo on the path.</summary>
        public static float PassedAt(in HollowPurpleTimes t, float blueDist, float speed, float along) =>
            t.Move + Mathf.Max(0f, along - blueDist) / speed;

        /// <summary>How far Gojo's arm is out: up over <see cref="Raise"/>, down over 0.3 s from 0.3 s after Red reaches Blue.</summary>
        public static float ArmOut(in HollowPurpleTimes t, float seconds) =>
            Smooth(seconds / Raise) * (1f - Smooth((seconds - t.Contact - 0.3f) / 0.3f));

        /// <summary>The world dim now: in over <see cref="DimIn"/> from the ignition, out over <see cref="DimRelease"/> from the stop.</summary>
        public static float DimNow(in HollowPurpleTimes t, float seconds, float dim) =>
            dim * (seconds < t.Ignite ? 0f : seconds < t.Stop ? Smooth((seconds - t.Ignite) / DimIn) : 1f - Smooth((seconds - t.Stop) / DimRelease));

        /// <summary>The trench shading: full until <see cref="TrenchHold"/> after the stop, then out over <see cref="TrenchFade"/>.</summary>
        public static float TrenchNow(in HollowPurpleTimes t, float seconds) =>
            seconds < t.Stop + TrenchHold ? 1f : 1f - Smooth((seconds - t.Stop - TrenchHold) / TrenchFade);

        /// <summary>
        /// True once a cell centre <paramref name="along"/> cells from Gojo and <paramref name="across"/> off the path is
        /// in the lane behind the sphere: within the radius of the path run so far (Blue's centre to the sphere's).
        /// A centre exactly on the edge counts; the small allowance keeps float rounding from dropping it.
        /// </summary>
        public static bool InLane(float along, float across, float blueDist, float centre, float travel, float radius)
        {
            float on = Mathf.Min(Mathf.Max(along, blueDist), blueDist + travel);
            if (on > centre + 1e-4f) return false;
            float dx = along - on;
            return dx * dx + across * across <= radius * radius + 1e-3f;
        }
    }

    /// <summary>The preview's stand-ins on the path.</summary>
    public enum HollowPurpleScriptThing { Tree, Raider, Ally, Crate }
}
