using UnityEngine;

namespace RimArt
{
    /// <summary>One rod this frame, in cells east and north of the middle of the wall.</summary>
    public struct RodPose
    {
        /// <summary>Where it leaves the ground, and the ground point under its tip.</summary>
        public Vector2 root, tip;
        /// <summary>Across the rod on screen, so a leaning rod keeps its thickness.</summary>
        public Vector2 across;
        /// <summary>Cells tall now; 0 while it is under the floor.</summary>
        public float height;
        /// <summary>Seconds since it broke the surface; negative before.</summary>
        public float age;
    }

    /// <summary>One cross-section of a rod: its drawn point, the ground under it, its height there and its half width.</summary>
    public struct RodRow
    {
        public Vector2 at, ground;
        public float height, half;
    }

    /// <summary>
    /// Pure clock for Black rods v2, the palisade: all six orbs leave the sage's ring and sink
    /// along a line across the cast direction, a crack runs along the line, twelve rods stab up
    /// from the middle outward and stand as a wall, then sink from the ends inward and the orbs
    /// go back to their slots. Seconds in, geometry out; nothing here draws or touches the map.
    ///
    /// A wall running north-south puts height and the line on the same screen axis, so its rods
    /// cross alternately east and west like a spiked barricade; an east-west wall stands upright.
    /// The amount of crossing follows the cast direction.
    ///
    /// The numbers are the ones picked in the VFX lab's Black rods v2 sketch
    /// (Tools/VfxLab/web/sketches/six-paths-rods-v2.js). There is no ability behind it yet.
    /// </summary>
    public static class SixPathsRodsTiming
    {
        public const float Sink = 0.50f, Crack = 0.25f, Stagger = 0.30f, Punch = 0.12f, Retract = 0.55f,
            Close = 0.25f, Return = 0.60f, Settle = 0.30f;
        /// <summary>The preview's script: seconds the wall stands. In the game that is 10 s.</summary>
        public const float PreviewStand = 4f;
        /// <summary>Wall length, rod height and rod base and tip width, in cells.</summary>
        public const float Length = 6f, Tall = 2.2f, Width = 0.30f, TipWidth = 0.07f;
        /// <summary>Overshoot as a rod punches up; how far the tips cross for a north-south wall; the lean every rod has.</summary>
        public const float Overshoot = 0.12f, Cross = 0.75f, Lean = 0.1f;
        public const int Rods = 12, Rows = 11, CrackPoints = 49, TrailPoints = 12, Chunks = 3;
        public const float Shake = 0.07f;
        /// <summary>The share of <see cref="Sink"/> an orb spends flying; the rest it spends going under.</summary>
        public const float FlightShare = 0.75f;
        public const float TrailSeconds = 0.12f, DirtSeconds = 0.55f;

        public static float CrackAt => Sink;
        public static float RiseAt => CrackAt + Crack;
        public static float StandAt => RiseAt + Stagger + Punch;
        public static float RetractAt => StandAt + PreviewStand;
        public static float GoneAt => RetractAt + Retract + Close;
        public static float Duration => GoneAt + Return + Settle;

        private static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);
        private static float Rand(int index) => SixPathsBloomTiming.Rand(index);

        /// <summary>
        /// A ground point <paramref name="along"/> the cast direction and <paramref name="across"/>
        /// it, from the middle of the wall. The wall runs across.
        /// </summary>
        public static Vector2 Floor(Vector2 toward, float along, float across) =>
            new Vector2(toward.x * along - toward.y * across, toward.y * along + toward.x * across);

        /// <summary>0 for the middle rods, 1 for the end ones.</summary>
        public static float Spread(int index)
        {
            float middle = (Rods - 1) / 2f;
            return Mathf.Abs(index - middle) / Mathf.Max(1f, middle);
        }

        public static float StartOf(int index) => RiseAt + Spread(index) * Stagger;

        /// <summary>1 until the wall is back down, 0 once the crack has closed.</summary>
        public static float Closing(float seconds) => 1f - Smooth((seconds - GoneAt + Close) / 0.5f);

        /// <summary>How far from the middle the crack has run, in cells.</summary>
        public static float CrackReach(float seconds) => Smooth((seconds - CrackAt) / Crack) * Length / 2f + 0.001f;

        /// <summary>Point <paramref name="j"/> of the crack, a zigzag along the line.</summary>
        public static Vector2 CrackPoint(Vector2 toward, int j, float seconds)
        {
            int k = j - (CrackPoints - 1) / 2, ends = (CrackPoints - 1) / 2;
            float jag = Mathf.Abs(k) < ends ? (k % 2 != 0 ? 0.05f : -0.05f) : 0f;
            return Floor(toward, jag, k / (float)ends * CrackReach(seconds));
        }

        /// <summary>The crack's edge: steady while the rods come and go, pulsing while the wall stands.</summary>
        public static float CrackGlow(float seconds) =>
            (seconds >= StandAt && seconds < RetractAt ? 0.4f + 0.3f * (0.5f + 0.5f * Mathf.Sin(seconds * 4f)) : 0.7f) * Closing(seconds);

        public static RodPose Rod(Vector2 toward, int index, float seconds)
        {
            float across = (index / (float)(Rods - 1) - 0.5f) * Length;
            float up = Mathf.Clamp01((seconds - StartOf(index)) / Punch);
            float back = Mathf.Clamp01((seconds - RetractAt - (1f - Spread(index)) * 0.25f) / Retract);
            float height = up <= 0f || back >= 1f ? 0f
                : Tall * (1f - 0.12f * Rand(index + 2)) * Smooth(up)
                    * (1f + Overshoot * Mathf.Max(0f, Mathf.Sin(up * Mathf.PI))) * (1f - Smooth(back));
            Vector2 root = Floor(toward, 0f, across);
            float amount = (index % 2 == 1 ? 1f : -1f) * (Lean + Cross * Mathf.Abs(toward.x));
            Vector2 tip = root + toward * (amount * height / Tall);
            Vector2 run = tip - root + new Vector2(0f, height * SixPathsHeight.Lift);
            float length = run.magnitude;
            return new RodPose
            {
                root = root, tip = tip, height = height, age = seconds - StartOf(index),
                across = length > 1e-5f ? new Vector2(run.y, -run.x) / length : Vector2.right,
            };
        }

        /// <summary>Row <paramref name="row"/> of <see cref="Rows"/>, from the rod's root to its tip.</summary>
        public static RodRow Row(in RodPose rod, int row)
        {
            float u = row / (float)(Rows - 1);
            Vector2 ground = Vector2.LerpUnclamped(rod.root, rod.tip, u);
            return new RodRow
            {
                ground = ground, height = rod.height * u,
                at = ground + new Vector2(0f, rod.height * u * SixPathsHeight.Lift),
                half = Mathf.Lerp(Width, TipWidth, u) / 2f * (row == Rows - 1 ? 0.15f : 1f),
            };
        }

        /// <summary>0 to 1 as the dirt round a rod's hole shows, and back to 0 as the crack closes.</summary>
        public static float Holed(in RodPose rod, float seconds) => Smooth(rod.age / 0.1f) * Closing(seconds);

        /// <summary>The tip's glow: bright while the rod punches up, faint while it stands.</summary>
        public static float TipGlow(in RodPose rod) => 0.6f * (rod.age < Punch + 0.25f ? 1f : 0.25f);

        /// <summary>
        /// One chunk of dirt thrown as rod <paramref name="index"/> breaks the surface, relative to
        /// the rod's root; alpha 0 when it is not showing.
        /// </summary>
        public static ImpactParticle Chunk(int index, int chunk, float age)
        {
            if (age < 0f || age >= DirtSeconds) return default;
            float u = age / DirtSeconds, direction = Rand(index * 7 + chunk) * Mathf.PI * 2f;
            float far = (0.35f + Rand(index + chunk * 3) * 0.5f) * u;
            return new ImpactParticle
            {
                x = Mathf.Cos(direction) * far, z = Mathf.Sin(direction) * far * 0.6f,
                height = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * (0.4f + Rand(index + chunk) * 0.4f),
                size = 0.05f, alpha = 1f - u,
            };
        }

        /// <summary>The ground socket orb <paramref name="index"/> sinks into.</summary>
        public static Vector2 Socket(Vector2 toward, int index) =>
            Floor(toward, 0f, ((index + 0.5f) / SixPathsTiming.Orbs - 0.5f) * Length);

        /// <summary>
        /// Orb <paramref name="index"/>'s flight, <paramref name="flown"/> 0 at its ring slot and 1
        /// at its socket. Both ends are drawn points relative to the middle of the wall.
        /// </summary>
        public static Vector2 Path(Vector2 slot, Vector2 socket, int index, float flown) =>
            Vector2.LerpUnclamped(slot, socket, flown)
                + new Vector2(0f, Mathf.Max(0f, Mathf.Sin(flown * Mathf.PI)) * (0.7f + index * 0.08f) * SixPathsHeight.Lift);

        /// <summary>How far along <see cref="Path"/> an orb is: out at the start, back at the end.</summary>
        public static float Flown(float seconds) => seconds < GoneAt
            ? Smooth(seconds / (Sink * FlightShare))
            : 1f - Smooth((seconds - GoneAt) / Return);

        /// <summary>0 to 1 as an outbound orb goes under the floor.</summary>
        public static float Drop(float seconds) => Smooth((seconds - Sink * FlightShare) / (Sink * (1f - FlightShare)));

        /// <summary>True while the orbs are in flight or back in their slots, false while they are in the ground.</summary>
        public static bool OrbsShown(float seconds) => seconds < Sink || seconds >= GoneAt;
    }
}
