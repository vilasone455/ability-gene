using UnityEngine;
using Verse;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Paradise Lost pictures (WhiteNight's staff, docs/ego-weapons.md "Weapon 4"),
    /// the defaults of the lab sketch Tools/VfxLab/web/sketches/ego-paradise-lost-v2.js ("Paradise Lost v2
    /// (sketch)", with the v2.1 wings). Seconds in, numbers out, no drawing and no map.
    ///
    /// The room hit: the apple and the halo on the staff flash for <see cref="StaffFlash"/> s; every pawn hit
    /// gets maroon thorns <see cref="HitDelay"/> s after the shot, rising in <see cref="Snap"/> s, standing
    /// <see cref="ThornLife"/> s and sinking in <see cref="Sink"/> s, with floor marks and blood spatter that
    /// fade over <see cref="MarkFade"/> s after the thorns start to sink. The corroded look grows over
    /// <see cref="Enter"/> s and goes over <see cref="Exit"/> s. A ring: the cross of light for
    /// <see cref="CrossLife"/> s, the floor spikes from <see cref="SpikeStart"/> for <see cref="SpikeLife"/> s,
    /// the red disc from <see cref="RingDelay"/> spreading for <see cref="Spread"/> s, then the pink-white
    /// flash for <see cref="FlashLife"/> s.
    ///
    /// The balance numbers below are the sketch's placeholders. The rules PR reads them from the weapon's XML
    /// (CompProperties_EgoWeapon); the one a picture must match, the ring's radius, then reaches the picture
    /// through <see cref="EgoParadiseLostRingShot.Radius"/>. The rest is shape and timing of the picture.
    /// </summary>
    public static class EgoParadiseLostTiming
    {
        // ---- Balance placeholders (XML later). The room hit: range 30, 16 damage to one pawn, 12 each to 2-5,
        // 9 each to 6 or more, outdoors every hostile within 6 cells of the aimed one, a shot every 2 s.
        public const float Range = 30f, OutdoorRadius = 6f, ShotInterval = 2f;
        public const int DamageSingle = 16, DamageFew = 12, DamageMany = 9, FewMax = 5;
        /// <summary>Placeholder: a pawn hit moves at SlowFactor of its speed (60 % slower) for SlowSeconds s.</summary>
        public const float SlowFactor = 0.4f, SlowSeconds = 1f;
        /// <summary>Placeholder: +1 mood per hostile hit, up to SanityCap. Drawn as a gizmo in game, not here.</summary>
        public const int SanityCap = 10;
        /// <summary>Placeholder: the ring hits every pawn within RingRadius cells for RingDamage, one every RingInterval s while corroded.</summary>
        public const float RingRadius = 6f, RingInterval = 6f;
        public const int RingDamage = 12;
        /// <summary>Placeholder: Overclock fires OverclockRings rings, one every OverclockInterval s, hostiles only.</summary>
        public const float OverclockInterval = 2f;
        public const int OverclockRings = 3;

        // ---- The picture's timing (s).
        /// <summary>The thorns rise this long after the shot; rise in Snap; sink in Sink; marks fade over MarkFade.</summary>
        public const float HitDelay = 0.06f, Snap = 0.08f, Sink = 0.3f, MarkFade = 2.5f;
        /// <summary>The room hit's thorns stand ThornLife s; a ring's small thorns RingThornLife s.</summary>
        public const float ThornLife = 0.6f, RingThornLife = 0.3f;
        /// <summary>The apple and the halo flash for StaffFlash s on a shot and RingFlash s on a ring.</summary>
        public const float StaffFlash = 0.25f, RingFlash = 0.35f;
        /// <summary>The corroded look grows over Enter s and goes over Exit s.</summary>
        public const float Enter = 0.9f, Exit = 1f;
        public const float CrossLife = 0.45f, SpikeStart = 0.06f, SpikeLife = 0.45f, RingDelay = 0.12f, Spread = 0.5f, FlashLife = 0.3f;
        /// <summary>The ring's edge stays this long after it reaches the radius, fading.</summary>
        public const float RingLinger = 0.35f;
        /// <summary>The body flash at a pawn hit lasts BodyFlash s.</summary>
        public const float BodyFlash = 0.15f;

        // ---- Shape.
        /// <summary>A point h cells up from a pawn's ground contact (<see cref="PawnBody.Ground"/>) draws h x Lift further north.</summary>
        public const float Lift = SixPathsHeight.Lift;
        /// <summary>The pawn's height (head top .63 to feet -.54) in lab cells; the chest's (+.05) above the ground line.</summary>
        public const float PawnH = (PawnBody.HeadTop - PawnBody.Feet) / Lift, ChestH = (PawnBody.Chest - PawnBody.Ground) / Lift;
        /// <summary>The tallest room-hit thorn is ThornScale pawn heights; a ring's thorns RingThornScale.</summary>
        public const float ThornScale = 1.5f, RingThornScale = 0.55f;
        public const int Thorns = 8, RingThorns = 6, Drops = 10, RingDrops = 4;
        /// <summary>Blood drops fall at Gravity lab cells/s².</summary>
        public const float Gravity = 7f;
        /// <summary>The staff, gold tip to the top of the halo, in lab cells; the snake's loop width (cells) and turns.</summary>
        public const float StaffH = 2.2f, SnakeLoop = 0.075f, SnakeTurns = 3.5f;
        /// <summary>The staff's scale-feather fan: three rows at these distances from its root (cells).</summary>
        public static readonly float[] WingRows = { 0.13f, 0.23f, 0.33f };
        /// <summary>The corroded wings: each side reaches WingSpan x WingReach cells; three arms of Coverts + Flights feathers.</summary>
        public const float WingSpan = 1.3f, WingReach = 1.25f;
        public const int Coverts = 5, Flights = 6;
        /// <summary>The three arms a side: screen angle (degrees from east, for a wing spreading east) and share of the reach.</summary>
        public static readonly float[] ArmAngle = { 38f, 8f, -24f }, ArmLength = { 0.5f, 0.55f, 0.45f };
        /// <summary>The ring's cross reaches CrossUp cells up and CrossSide cells each side; Spikes floor spikes, EdgeSpikes on the edge.</summary>
        public const float CrossUp = 7f, CrossSide = 4.8f;
        public const int Spikes = 40, EdgeSpikes = 28;
        /// <summary>The floor spikes are 2.4 to 4 cells long for a ring of SpikeRadius cells and shrink with a smaller one.</summary>
        public const float SpikeRadius = 6f;
        /// <summary>The thorn star round a corroded wielder: its circle's radius (cells).</summary>
        public const float StarRadius = 0.78f;

        /// <summary>The sketch's easeOut: 1 - (1 - x)^3, x clamped to 0..1.</summary>
        public static float EaseOut(float x)
        {
            float u = 1f - Mathf.Clamp01(x);
            return 1f - u * u * u;
        }

        /// <summary>The inverse of <see cref="EaseOut"/>: the share of the time at which it reaches <paramref name="u"/>.</summary>
        public static float UnEase(float u) => 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 1f / 3f);

        /// <summary>How far a pawn's thorns are up, 0 to 1, <paramref name="age"/> s after the hit.</summary>
        public static float Rise(float age, float life) =>
            age < Snap ? EaseOut(age / Snap) : age < life ? 1f : 1f - Smooth((age - life) / Sink);

        /// <summary>The floor marks' and spatter's strength, 1 to 0: full until the thorns start to sink, gone MarkFade s later.</summary>
        public static float Mark(float age, float life) => 1f - Smooth((age - life) / MarkFade);

        /// <summary>The thorns of a hit draw nothing from this age on.</summary>
        public static float HitGone(float life) => life + MarkFade;

        /// <summary>The corroded look, 0 to 1: grows from 0 over Enter s, goes from <paramref name="exitAt"/> over Exit s.</summary>
        public static float Look(float s, float exitAt) => Smooth(s / Enter) * (1f - Smooth((s - exitAt) / Exit));

        /// <summary>The staff's flash, 1 to 0, <paramref name="age"/> s after a shot.</summary>
        public static float ShotFlash(float age) => age < 0f ? 0f : 1f - Mathf.Clamp01(age / StaffFlash);

        /// <summary>The staff's flash, 1 to 0, <paramref name="age"/> s after a ring.</summary>
        public static float RingFlashAt(float age) => age >= 0f && age < RingFlash ? 1f - age / RingFlash : 0f;

        /// <summary>The ring's edge, cells from the wielder, <paramref name="ra"/> s after it starts to spread (RingDelay after the firing).</summary>
        public static float Front(float ra, float radius, float spread = Spread) => Mathf.Lerp(0.4f, radius, EaseOut(ra / spread));

        /// <summary>Seconds after a ring fires when its edge passes a pawn <paramref name="dist"/> cells away: when that pawn is hit.</summary>
        public static float RingHitAge(float dist, float radius, float spread = Spread) =>
            RingDelay + spread * UnEase((dist - 0.4f) / (radius - 0.4f));

        /// <summary>How long a ring draws from its firing.</summary>
        public static float RingLength(float spread = Spread) => Mathf.Max(CrossLife, Mathf.Max(SpikeStart + SpikeLife, RingDelay + spread + Mathf.Max(RingLinger, FlashLife)));

        /// <summary>The facing toward <paramref name="degrees"/> (0 east, 90 north), the nearest of the four as the sketch rounds it (halves go up).</summary>
        public static Rot4 FacingOf(float degrees)
        {
            float d = ((degrees % 360f) + 360f) % 360f;
            int quarter = Mathf.FloorToInt(d / 90f + 0.5f) % 4;
            return quarter == 0 ? Rot4.East : quarter == 1 ? Rot4.North : quarter == 2 ? Rot4.West : Rot4.South;
        }
    }
}
