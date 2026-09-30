using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Reversal: Red picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/gojo-red-v2.js). Seconds in, numbers out, no drawing and no map.
    ///
    /// The picture's clock is 0 when the arm starts to come up. The charge runs from <see cref="Start"/>
    /// for the charge's length; Red leaves the finger at <see cref="Fire"/>, flies from <see cref="Tip"/>
    /// cells out at the speed, and bursts at <see cref="Arrive"/>. The thing hit is thrown at
    /// <see cref="ThrowSpeed"/> slowing evenly to rest; a wall stops it early. Pawns inside the burst
    /// radius are pushed over <see cref="PushTime"/>. The picture ends <see cref="Tail"/> s after the
    /// last of those.
    ///
    /// The balance numbers (<see cref="Speed"/>, <see cref="Charge"/>, <see cref="ThrowCells"/>,
    /// <see cref="PushCells"/>, <see cref="BurstRadius"/>) are the sketch's placeholders: the ability will
    /// read them from its XML and pass them in <see cref="GojoRedShot"/>. The rest is shape and timing.
    /// </summary>
    public static class GojoRed
    {
        // Balance, from the ability's XML later: Red's speed (cells/s), the charge at the finger (s), cells the
        // thing hit is thrown, cells the others are pushed, the burst radius, and the red wash's radius.
        public const float Speed = 20f, Charge = 0.5f, ThrowCells = 6f, PushCells = 2f, BurstRadius = 1.5f, WashRadius = 3.5f;

        /// <summary>The charge starts Start s in; the arm is up in Raise s; the fire flash lasts Flash s; the picture runs Tail s after the burst's last motion.</summary>
        public const float Start = 0.1f, Raise = 0.15f, Flash = 0.12f, Tail = 1.6f;
        /// <summary>The thrown body leaves at ThrowSpeed cells/s; pushed pawns take PushTime s.</summary>
        public const float ThrowSpeed = 26f, PushTime = 0.28f;
        /// <summary>The orb's radius at the finger; how far the hand and the orb are from Gojo's centre (cells).</summary>
        public const float OrbRadius = 0.17f, Tip = 0.8f;
        /// <summary>The swirl at the finger grows from SwirlFrom to SwirlTo cells, turning SwirlSpin degrees a second.</summary>
        public const float SwirlFrom = 0.3f, SwirlTo = 0.75f, SwirlSpin = 540f;
        /// <summary>The ribbon arcs round Gojo: radius (cells) and starting angle (degrees) each; they sweep ArcSweep degrees.</summary>
        public static readonly float[,] Arcs = { { 1.1f, 30f }, { 1.4f, 210f } };
        public const float ArcSweep = 200f;
        /// <summary>The red wash lasts WashTime s; the burst ring opens in RingTime s and fades over RingHold s.</summary>
        public const float WashTime = 0.45f, RingTime = 0.12f, RingHold = 0.5f;
        /// <summary>The thrown body's squash on the wall face. Not drawn (a real pawn cannot be squashed); the red edge waits for it.</summary>
        public const float Squash = 0.1f;
        public const int Sparks = 16, Chips = 12, Cracks = 5, Rocks = 6, Crescents = 2, InkStreaks = 8;
        /// <summary>Red in flight: its radius, how fast the crescents turn (degrees/s).</summary>
        public const float FlightRadius = 0.21f, CrescentSpin = 900f;
        /// <summary>The wake: a pair of dust puffs every WakeStep cells, each moving WakeOut cells out over WakeLife s.</summary>
        public const float WakeStep = 0.6f, WakeLife = 0.45f, WakeOut = 0.9f;
        /// <summary>The shock front: its speed (cells/s), half its width in degrees, cells across, fade after it stops.</summary>
        public const float FrontSpeed = 30f, FrontHalf = 55f, FrontWide = 1.6f, FrontFade = 0.12f;
        /// <summary>The thrown body's arc top and bounce, in cells up.</summary>
        public const float Peak = 0.6f, Bounce = 0.15f;
        /// <summary>A body thrown at a wall stops with its back this far short of the face.</summary>
        public const float StopShort = 0.15f;
        /// <summary>Camera shakes: the fire, the burst, the slam on a wall.</summary>
        public const float FireShake = 0.03f, BurstShake = 0.12f, SlamShake = 0.08f;

        // The preview's script, the sketch's defaults: the first pawn (or the target cell) Dist cells from Gojo, a
        // wall WallBehind cells behind him (its face half a cell nearer). Other pawns per scenario: cells past the
        // burst point along Red's line, cells across it (left of the line is positive).
        public const float ScriptDist = 9f, ScriptWallBehind = 4f;
        public static readonly float[,] WallOthers = { { 0.3f, 1.1f } };
        public static readonly float[,] OpenOthers = { { -0.5f, 1f }, { 0.6f, -0.95f } };
        public static readonly float[,] EmptyOthers = { { 0.4f, 1.15f }, { -1.2f, -2.2f } };

        /// <summary>When Red leaves the finger.</summary>
        public static float Fire(float charge) => Start + charge;

        /// <summary>When Red bursts, <paramref name="dist"/> cells from Gojo.</summary>
        public static float Arrive(float charge, float dist, float speed) => Fire(charge) + Mathf.Max(0f, dist - Tip) / speed;

        /// <summary>How long a free throw of <paramref name="throwCells"/> takes to come to rest.</summary>
        public static float ThrowTime(float throwCells) => 2f * throwCells / ThrowSpeed;

        /// <summary>Seconds from the burst until the body stops at <paramref name="stop"/> cells (a wall stops it early).</summary>
        public static float FlyTime(float throwCells, float stop) =>
            (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - stop / throwCells))) * ThrowTime(throwCells);

        /// <summary>The picture's length on its own clock; <paramref name="fly"/> is 0 when nothing was thrown.</summary>
        public static float End(float arrive, float fly) => arrive + Mathf.Max(fly, PushTime) + Tail;

        /// <summary>Where a body thrown at a wall face <paramref name="face"/> cells past the burst point stops.</summary>
        public static float WallStop(float throwCells, float face) => Mathf.Min(throwCells, face - StopShort);

        public static bool ReachesWall(float throwCells, float face) => throwCells >= face - StopShort;

        /// <summary>The charge's progress 0 to 1.</summary>
        public static float Charged(float s, float charge) => Mathf.Clamp01((s - Start) / charge);

        /// <summary>The arm: up over Raise s, down 0.4 to 0.7 s after the burst.</summary>
        public static float ArmOut(float s, float arrive) => Smooth(s / Raise) * (1f - Smooth((s - arrive - 0.4f) / 0.3f));

        /// <summary>How far, 0 to 1, a pawn inside the burst radius has been pushed <paramref name="age"/> s after the burst.</summary>
        public static float Pushed(float age)
        {
            if (age <= 0f) return 0f;
            float u = 1f - Mathf.Clamp01(age / PushTime);
            return 1f - u * u;
        }

        /// <summary>Seconds after the burst when a pushed pawn has covered <paramref name="at"/> (0 to 1) of its push.</summary>
        public static float PushedAt(float at) => PushTime * (1f - Mathf.Sqrt(1f - at));

        /// <summary>Cells the thrown body has travelled from the burst point <paramref name="age"/> s after it.</summary>
        public static float Thrown(float age, float throwCells, float stop)
        {
            if (age <= 0f) return 0f;
            float dur = ThrowTime(throwCells), u = Mathf.Min(age, FlyTime(throwCells, stop)) / dur;
            return Mathf.Min(stop, throwCells * (1f - (1f - u) * (1f - u)));
        }

        /// <summary>
        /// Height of the thrown body: one arc up to <see cref="Peak"/> over the first half of the throw's time
        /// (about 75 % of its distance), a small bounce, then on the floor.
        /// </summary>
        public static float Lift(float age, float throwCells)
        {
            float dur = ThrowTime(throwCells), air = dur * 0.5f, hop = dur * 0.2f;
            if (age <= 0f) return 0f;
            if (age < air) return Peak * Mathf.Max(0f, Mathf.Sin(Mathf.PI * age / air));
            if (age < air + hop) return Bounce * Mathf.Max(0f, Mathf.Sin(Mathf.PI * (age - air) / hop));
            return 0f;
        }

        /// <summary>
        /// The preview's script for the thrown body, the sketch's stand-in: cells from the burst point along Red's
        /// line, height in cells, and whether it lies (flying and skidding) or stands, <paramref name="age"/> s after
        /// the burst. On a wall it holds at the face through the squash, then slides 0.2 cells back and down to the
        /// floor over 0.15 s and stands. In the open it lands, bounces, skids and stands 0.15 s after it stops.
        /// </summary>
        public static void ScriptBody(float age, float throwCells, float stop, bool wall, out float along, out float height, out bool lying)
        {
            float fly = FlyTime(throwCells, stop);
            if (age < 0f)
            {
                along = 0f; height = 0f; lying = false;
                return;
            }
            if (wall && age >= fly)
            {
                float slam = age - fly, slamUp = Lift(fly, throwCells);
                float u = slam < Squash ? 0f : Smooth((slam - Squash) / 0.15f);
                along = stop - 0.2f * u;
                height = slamUp * (1f - u);
                lying = false;
                return;
            }
            along = Thrown(age, throwCells, stop);
            height = Lift(Mathf.Min(age, fly), throwCells);
            lying = wall || age <= fly + 0.15f;
        }
    }
}
