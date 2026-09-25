using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>One cross-section of a jaw rim, in cells east and north of the trap tile.</summary>
    public struct JawRow
    {
        /// <summary>The drawn point, height included, and the point on the ground under it.</summary>
        public Vector2 at, ground;
        /// <summary>Along the rim, and across it toward the other jaw, on screen.</summary>
        public Vector2 along, inward;
        /// <summary>0 at the hinge, up to 1 at the tip. Cells above the ground. Rim width here.</summary>
        public float u, height, width;
    }

    /// <summary>
    /// Pure clock for Twin Maw: two orbs leave the sage's ring, sink into the ground left and right
    /// of a tile and leave a toothed seam. When the trap is sprung two jaws rise out of the floor,
    /// shut, bite three times and hold; then they open, sink, and the orbs go back to their slots.
    /// Seconds in, geometry out; nothing here draws or touches the map.
    ///
    /// The jaws always close east-west on screen whatever the cast direction, so height and span
    /// never share the screen axis. Each jaw is a tall back rim and a short front rim.
    ///
    /// The numbers are the ones picked in the VFX lab's Twin Maw sketch
    /// (Tools/VfxLab/web/sketches/six-paths-twin-maw.js). There is no ability behind it yet.
    /// </summary>
    public static class SixPathsTwinMawTiming
    {
        public const float Arm = 0.50f, Rise = 0.10f, Snap = 0.12f, Hold = 4f, Open = 0.20f, Sink = 0.25f,
            Recall = 0.65f, Settle = 0.30f;
        /// <summary>
        /// The preview's script: seconds the armed trap waits before something steps on it. In the
        /// game that is whenever a hostile pawn arrives, up to 20 s.
        /// </summary>
        public const float PreviewWait = 1.6f;
        /// <summary>Back jaw height, hinge half-spacing and jaw thickness, in cells.</summary>
        public const float Size = 1.55f, Reach = 0.95f, Plate = 0.30f;
        public const float OpenAngle = 70f * Mathf.Deg2Rad, Sweep = 88f * Mathf.Deg2Rad;
        public const int Teeth = 6, Bites = 3, Rows = 29, TrailPoints = 16, SeamPoints = 15;
        public const float BiteGap = 0.3f, BiteFlash = 0.16f, Shake = 0.1f;
        /// <summary>The share of <see cref="Arm"/> an orb spends flying; the rest it spends going under.</summary>
        public const float FlightShare = 0.75f;
        /// <summary>Cells an orb's arc rises above the straight line, and seconds of path its trail covers.</summary>
        public const float Arc = 0.9f, TrailSeconds = 0.14f;
        /// <summary>The back rims stand this far north of the tile's middle and the front rims as far south.</summary>
        public const float RimDepth = 0.2f;
        /// <summary>Front rim height as a share of the back rim's.</summary>
        public const float FrontTall = 0.6f;

        public static float ArmedAt => Arm;
        public static float TriggerAt => ArmedAt + PreviewWait;
        public static float SnapAt => TriggerAt + Rise;
        public static float ShutAt => SnapAt + Snap;
        public static float ReleaseAt => ShutAt + Hold;
        public static float SinkAt => ReleaseAt + Open;
        public static float GoneAt => SinkAt + Sink;
        public static float Duration => GoneAt + Recall + Settle;

        /// <summary>The jaws: 0 open, 1 shut. Two re-clenches after the first bite, then a slow breathing grip.</summary>
        public static float Shutness(float seconds)
        {
            float snap = Mathf.Clamp01((seconds - SnapAt) / Snap), shut = snap * snap;
            for (int k = 1; k < Bites; k++)
                shut -= 0.13f * Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01((seconds - ShutAt - k * BiteGap) / 0.16f) * Mathf.PI));
            if (seconds > ShutAt && seconds < ReleaseAt) shut -= 0.012f * (1f + Mathf.Sin(seconds * 9f));
            return shut * (1f - Smooth((seconds - ReleaseAt) / Open));
        }

        /// <summary>How much of each rim is out of the floor, 0 to 1.</summary>
        public static float Grow(float seconds) =>
            Smooth((seconds - TriggerAt) / Rise) * (1f - Smooth((seconds - SinkAt) / Sink));

        /// <summary>0 to 1 while the orbs are in the ground.</summary>
        public static float Planted(float seconds) =>
            Smooth((seconds - Arm * FlightShare) / (Arm * (1f - FlightShare))) * (1f - Smooth((seconds - GoneAt) / 0.15f));

        /// <summary>The armed marker's slow pulse, 0 to 1.</summary>
        public static float Pulse(float seconds) => 0.5f + 0.5f * Mathf.Sin(seconds * 5f);

        /// <summary>The ground socket an orb sinks into; <paramref name="side"/> is -1 west, 1 east.</summary>
        public static Vector2 Socket(int side) => new Vector2(side * Reach, 0f);

        /// <summary>Point <paramref name="j"/> of the toothed seam between the sockets.</summary>
        public static Vector2 Seam(int j) => new Vector2((j / (float)(SeamPoints - 1) * 2f - 1f) * Reach,
            j > 0 && j < SeamPoints - 1 ? (j % 2 == 1 ? 0.075f : -0.075f) : 0f);

        /// <summary>
        /// Fills <paramref name="rows"/> with one rim of one jaw. <paramref name="depth"/> is how far
        /// north of the tile's middle it stands and <paramref name="tall"/> scales its height.
        /// </summary>
        public static void Rim(int side, float depth, float tall, float seconds, JawRow[] rows)
        {
            float grow = Grow(seconds), theta = (1f - Shutness(seconds)) * OpenAngle;
            float cos = Mathf.Cos(theta), sin = Mathf.Sin(theta);
            int last = rows.Length - 1;
            for (int j = 0; j <= last; j++)
            {
                float u = j / (float)last * grow, a = u * Sweep;
                float rx = Reach * Mathf.Cos(a) - Reach, ry = Size * tall * Mathf.Sin(a);
                float height = Mathf.Max(0f, -rx * sin + ry * cos);
                var ground = new Vector2(side * (Reach + rx * cos + ry * sin), depth);
                rows[j] = new JawRow
                {
                    u = u, height = height, ground = ground,
                    at = ground + new Vector2(0f, height * SixPathsHeight.Lift),
                    width = (Plate * Mathf.Pow(Mathf.Max(0f, 1f - u), 0.7f) + 0.02f) * (tall < 1f ? 0.85f : 1f),
                };
            }
            for (int j = 0; j <= last; j++)
            {
                Vector2 along = rows[Mathf.Min(last, j + 1)].at - rows[Mathf.Max(0, j - 1)].at;
                float length = along.magnitude;
                along = length > 1e-5f ? along / length : Vector2.zero;
                rows[j].along = along;
                rows[j].inward = new Vector2(-along.y, along.x) * side;
            }
        }

        /// <summary>
        /// The row tooth <paramref name="index"/> stands on, or -1 while that part of the rim is
        /// still under the floor. East and west teeth are staggered half a step so they mesh when shut.
        /// </summary>
        public static int ToothRow(int index, int side, float seconds, out float length)
        {
            float u = 0.2f + (index + (side > 0 ? 0.5f : 0f)) / Teeth * 0.78f, grow = Grow(seconds);
            length = 0.26f * (1f - 0.3f * u);
            if (u > grow) return -1;
            return Mathf.Min(Rows - 1, Mathf.FloorToInt(u / grow * (Rows - 1) + 0.5f));
        }

        /// <summary>
        /// An orb's flight, <paramref name="flown"/> 0 at its ring slot and 1 at its socket. Both
        /// ends are drawn points relative to the trap tile.
        /// </summary>
        public static Vector2 Path(Vector2 slot, Vector2 socket, float flown) =>
            Vector2.LerpUnclamped(slot, socket, flown) + new Vector2(0f, Mathf.Max(0f, Mathf.Sin(flown * Mathf.PI)) * Arc * SixPathsHeight.Lift);

        /// <summary>How far along <see cref="Path"/> an orb is: out at the start, back at the end.</summary>
        public static float Flown(float seconds) => seconds < GoneAt
            ? Smooth(seconds / (Arm * FlightShare))
            : 1f - Smooth((seconds - GoneAt) / Recall);

        /// <summary>0 to 1 as an outbound orb goes under the floor.</summary>
        public static float Sinking(float seconds) => Smooth((seconds - Arm * FlightShare) / (Arm * (1f - FlightShare)));

        /// <summary>True while the orbs are in flight or back in their slots, false while they are in the ground.</summary>
        public static bool OrbsShown(float seconds) => seconds < Arm || seconds >= GoneAt;

        /// <summary>Bite <paramref name="index"/>'s flash, 1 at the bite and 0 by <see cref="BiteFlash"/> later.</summary>
        public static float Bite(int index, float seconds)
        {
            float age = seconds - ShutAt - index * BiteGap;
            return age < 0f || age > BiteFlash ? 0f : 1f - age / BiteFlash;
        }
    }
}
