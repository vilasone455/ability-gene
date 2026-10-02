using System;
using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Last Prism picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/last-prism.js). Seconds in, numbers out, no drawing and no map.
    ///
    /// The picture's clock runs while the prism is held. The caller says when a channel started: six
    /// beams leave the prism's tip in a level fan up to the fan angle either side of the aim and sweep
    /// across each other (<see cref="Sweep"/>); over the join time the fan narrows and the beams join
    /// into one beam a lane wide. When the beam stops it fades over
    /// <see cref="Fade"/> s, or flickers out over <see cref="Sputter"/> s when the charge ran out. The
    /// prism rolls about its long axis while firing (<see cref="Roll"/>).
    ///
    /// The balance numbers (range, store, the join time, the fan's angle, the beam's width, the hit reach, the
    /// turn rate and the burns) are XML fields on AG_LastPrism_Fire and AG_LastPrism (<see cref="CompProperties_LastPrismFire"/>,
    /// <see cref="CompProperties_LastPrism"/>): the rules, the picture (through <see cref="LastPrismShot"/>) and the
    /// previews all read them from there. Here is only the shape and timing of the picture.
    ///
    /// The beam geometry is in double, as the sketch's is, because <see cref="LastPrismScript"/> decides
    /// who is hit with it and must down the same pawns on the same frame as the sketch.
    /// </summary>
    public static class LastPrismTiming
    {
        public const int Beams = 6;
        /// <summary>After the beam stops: it fades over Fade s, or flickers out over Sputter s when the charge ran out. The join's white flash and ring last JoinFlash s.</summary>
        public const float Fade = 0.25f, Sputter = 0.35f, JoinFlash = 0.3f;
        /// <summary>Seconds per sweep of the fan: SweepSlow at first, speeding up over the second half of the narrowing to SweepFast (Terraria's spin rate going from 16 to 6 frames).</summary>
        public const float SweepSlow = 1.8f, SweepFast = 0.6f;
        /// <summary>The prism's roll in turns per second: SpinStart when the channel starts, rising linearly to SpinFull at the join (Terraria's sprite frames go from every 4 ticks to every 2); it winds down at rate SpinStop (1/s) after the beam stops.</summary>
        public const float SpinStart = 1f, SpinFull = 2f, SpinStop = 3f;
        /// <summary>Cells across the aim where the fan beams leave the tip: StartSide when the channel starts, EndSide at the join.</summary>
        public const float StartSide = 0.1f, EndSide = 0.03f;
        /// <summary>Half width of a fan beam's outer glow (cells).</summary>
        public const float FanHalf = 0.16f;
        /// <summary>
        /// The prism, a pyramid lying level at chest height: PrismLen long, its three base corners PrismRad from
        /// its long axis (about 0.6 across, 0.45 of a pawn's height, as Terraria's 20 x 21 px sprite is to its
        /// 45 px player), its base PrismGap in front of the wielder. PrismH is the chest's height above the floor
        /// in lab units (lib/pawn.js), for the prism's shadow.
        /// </summary>
        public const float PrismLen = 0.6f, PrismRad = 0.35f, PrismGap = 0.3f, PrismH = 0.98f;
        /// <summary>The prism bobs Bob cells on screen at BobRate radians a second.</summary>
        public const float Bob = 0.025f, BobRate = 2.4f;
        /// <summary>The beams come in over ChannelIn s; the joined beam over JoinIn s; the pulse runs down it in PulseTime s.</summary>
        public const float ChannelIn = 0.15f, JoinIn = 0.15f, PulseTime = 0.2f;

        private const double Tau = Math.PI * 2.0, Deg2Rad = Math.PI / 180.0;

        /// <summary>How far north of its rest the prism has bobbed at <paramref name="s"/> (cells on screen); the beams leave from where it is.</summary>
        public static float Bobbing(float s) => Bob * Mathf.Sin(s * BobRate);

        /// <summary>How far the fan has narrowed toward the join, 0 to 1.</summary>
        public static float Narrowed(float s, float channelAt, float join) => Mathf.Clamp01((s - channelAt) / join);

        /// <summary>The fan beams' opacity at narrowing <paramref name="u"/>: 0.2 to 0.45 over the first two thirds, 0.45 to 1 over the last third (Terraria's curve).</summary>
        public static float Opacity(float u) => u <= 0.66f ? Mathf.Lerp(0.2f, 0.45f, u / 0.66f) : Mathf.Lerp(0.45f, 1f, (u - 0.66f) / 0.34f);

        /// <summary>
        /// The fan's sweep phase in radians: one pass per <see cref="SweepSlow"/> s, speeding up over the second
        /// half of the narrowing (the speed rising with the square of the time) to one per <see cref="SweepFast"/> s.
        /// </summary>
        public static double Sweep(double s, double channelAt, double join)
        {
            double w0 = Tau / SweepSlow, w1 = Tau / SweepFast, a = channelAt + join * 0.5, half = join * 0.5, r = Clamp01((s - a) / half);
            double g = s <= a + half ? half / 3.0 * r * r * r : half / 3.0 + (s - a - half);
            return w0 * (s - channelAt) + (w1 - w0) * g;
        }

        /// <summary>
        /// Fan beam <paramref name="i"/> (0 to 5) at <paramref name="s"/>: <paramref name="turn"/> is its angle off
        /// the aim (radians, swept by cos(phase + i/6 turn) and narrowing to 0 at the join), <paramref name="shift"/>
        /// is how far across the aim it leaves the tip (cells, positive to the aim's left).
        /// </summary>
        public static void FanBeam(int i, double s, double channelAt, double join, double fanDegrees, out double turn, out double shift)
        {
            double u = Clamp01((s - channelAt) / join), c = Math.Cos(Sweep(s, channelAt, join) + i * Tau / Beams);
            turn = fanDegrees * Deg2Rad * (1.0 - u) * c;
            shift = (StartSide + ((double)EndSide - StartSide) * u) * c;
        }

        /// <summary>The prism's tip on the ground, where every beam starts: PrismGap + PrismLen along the aim from the wielder.</summary>
        public static Vector2 Tip(Vector2 wielder, Vector2 toward) => wielder + toward * (PrismGap + PrismLen);

        /// <summary>
        /// Turns the prism has rolled about its long axis: none before the channel; then <see cref="SpinStart"/> a second
        /// rising linearly to <see cref="SpinFull"/> as the beams narrow, SpinFull while joined, and after the beam stops
        /// at <paramref name="release"/> it winds down exponentially at <see cref="SpinStop"/>.
        /// </summary>
        public static float Roll(float s, float channelAt, float join, float release)
        {
            if (s < channelAt) return 0f;
            float joinAt = channelAt + join;
            float Until(float x)
            {
                float u = Mathf.Clamp01((x - channelAt) / join);
                return x <= joinAt ? SpinStart * (x - channelAt) + (SpinFull - SpinStart) * join * u * u / 2f
                    : SpinStart * join + (SpinFull - SpinStart) * join / 2f + SpinFull * (x - joinAt);
            }
            float turns = Until(Mathf.Min(s, release));
            if (s > release)
            {
                float v = SpinStart + (SpinFull - SpinStart) * Mathf.Clamp01((release - channelAt) / join);
                turns += v * (1f - Mathf.Exp(-SpinStop * (s - release))) / SpinStop;
            }
            return turns;
        }

        /// <summary>True when (qx, qz) is within <paramref name="reach"/> of the line from (ax, az) along <paramref name="angle"/>, between 0 and <paramref name="length"/> cells out.</summary>
        public static bool OnLine(double qx, double qz, double ax, double az, double angle, double length, double reach)
        {
            double dx = Math.Cos(angle), dz = Math.Sin(angle), rx = qx - ax, rz = qz - az, along = rx * dx + rz * dz;
            return along > 0 && along < length && Math.Abs(rz * dx - rx * dz) <= reach;
        }

        /// <summary>
        /// Cells from (ox, oz) along <paramref name="angle"/> to the first of <paramref name="count"/> wall cells (centres
        /// <paramref name="cellX"/>, <paramref name="cellZ"/>, each a 1 x 1 square), or <paramref name="max"/>. A slab test
        /// per cell; a wall the ray starts inside does not count.
        /// </summary>
        public static double RayWall(double ox, double oz, double angle, double[] cellX, double[] cellZ, int count, double max)
        {
            double dx = Math.Cos(angle), dz = Math.Sin(angle), best = max;
            for (int c = 0; c < count; c++)
            {
                double t0 = double.NegativeInfinity, t1 = double.PositiveInfinity;
                if (!Slab(ox, dx, cellX[c], ref t0, ref t1) || !Slab(oz, dz, cellZ[c], ref t0, ref t1)) continue;
                if (t0 <= t1 && t0 > 0 && t0 < best) best = t0;
            }
            return best;
        }

        // One axis of the slab test: narrows t0..t1 to where the ray is inside centre +- 0.5; false when it never is.
        private static bool Slab(double o, double d, double centre, ref double t0, ref double t1)
        {
            double lo = centre - 0.5, hi = centre + 0.5;
            if (Math.Abs(d) < 1e-9) return o >= lo && o <= hi;
            double ta = (lo - o) / d, tb = (hi - o) / d;
            if (ta > tb) (ta, tb) = (tb, ta);
            t0 = Math.Max(t0, ta);
            t1 = Math.Min(t1, tb);
            return true;
        }

        /// <summary>An angle brought into -pi..pi, as the sketch's wrap (JavaScript's Math.round rounds halves up).</summary>
        public static double Wrap(double x) => x - Tau * Math.Floor(x / Tau + 0.5);

        public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
