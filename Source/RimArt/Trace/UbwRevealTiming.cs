using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// When each part of Unlimited Blade Works' reveal shot happens, and where its camera is: seconds in, numbers
    /// out, no drawing. The shot plays at the take, with the game paused, before the world v4 stands; any key
    /// skips it. The port of Tools/VfxLab/web/sketches/trace-ubw-reveal.js with its defaults and the keyframes of
    /// lib/ubw-reveal.js (the plan agreed 2026-09-26):
    ///   0.00-0.50  the white of the take fades onto the sky: the camera at head height (1.7 cells, 3.6 south of
    ///              the caster) looking 30 degrees up, north, at the gears;
    ///   1.25-2.50  the camera tilts down to 6 degrees below the horizon; from 1.2 the fire runs out from the
    ///              caster to 130 cells in 1.6 s and the swords rise out of the ground behind it, ring by ring;
    ///   2.50-3.30  the camera cranes up to 13 cells, keeping the horizon in frame;
    ///   3.15-4.20  the picture blends from the 3D camera into the game view (36 cells tall, 7 north of the
    ///              caster); the camera goes on to 26 cells up, 38 degrees down;
    ///   4.20       hand-over: the map camera draws the world v4 from here;
    ///   4.15-4.55  the black bars leave; at 4.6 the game runs again and the world's timer starts.
    /// </summary>
    internal static class UbwRevealTiming
    {
        /// <summary>Keyframes [t, x, y, z, pitch, fov] from the caster, eased in and out between each pair.</summary>
        public static readonly float[][] Keys =
        {
            new[] { 0f, 0f, 1.7f, -3.6f, 30f, 64f }, new[] { 1.25f, 0f, 1.9f, -4.4f, 25f, 62f }, new[] { 2.5f, 0f, 4.2f, -9.5f, -6f, 56f },
            new[] { 3.3f, 0f, 13f, -14f, -19f, 50f }, new[] { 4.2f, 0f, 26f, -18f, -38f, 44f },
        };
        /// <summary>The camera's last keyframe and the hand-over; the end; the black bars (36 of 383 pixels, the plan's), leaving from BarsOff over 0.4 s.</summary>
        public const float HandOver = 4.2f, End = 4.6f, BarH = 36f / 383f, BarsOff = 4.15f, BarsFor = 0.4f;
        /// <summary>The sketch's sliders: the white fades in 0.5 s; the fire starts at 1.2, runs out to 130 cells in 1.6 s; the blend starts at 3.15.</summary>
        public const float White = 0.5f, FireFrom = 1.2f, FireRun = 1.6f, FireTo = 130f, BlendFrom = 3.15f;
        /// <summary>The game view it blends into: 36 cells tall, centred 7 north of the caster (the world v4's usual framing).</summary>
        public const float CellsTall = 36f, GameNorth = 7f;
        /// <summary>The swords stand in rings RingStep cells wide out from the caster; a ring rises out of the ground over Rise cells of the fire's run once it is past.</summary>
        public const float RingStep = 1.5f, Rise = 3f;

        public struct Pose
        {
            public float X, Y, Z, Pitch, Fov;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        /// <summary>The camera at t (seconds into the shot), from the caster.</summary>
        public static Pose Along(float t)
        {
            int i = 0;
            while (i < Keys.Length - 2 && t > Keys[i + 1][0]) i++;
            float[] a = Keys[i], b = Keys[i + 1];
            float u = Ease((t - a[0]) / (b[0] - a[0]));
            float L(int k) => a[k] + (b[k] - a[k]) * u;
            return new Pose { X = L(1), Y = L(2), Z = L(3), Pitch = L(4), Fov = L(5) };
        }

        /// <summary>How far the fire has run out at t (cells from the caster), or -1 before it starts: radius ~ time^1.7.</summary>
        public static float FireAt(float t) => t < FireFrom ? -1f : Mathf.Pow(Mathf.Clamp01((t - FireFrom) / FireRun), 1.7f) * FireTo;

        /// <summary>How far the picture has blended into the game view at t.</summary>
        public static float BlendAt(float t) => Smooth((t - BlendFrom) / (HandOver - BlendFrom));

        /// <summary>The white of the take over the frame at t.</summary>
        public static float WhiteAt(float t) => 1f - Smooth(t / White);

        /// <summary>The height of each black bar at t, a share of the screen.</summary>
        public static float BarsAt(float t) => BarH * (1f - Smooth((t - BarsOff) / BarsFor));

        /// <summary>The fire's flames fade out over the last 30 % of its run.</summary>
        public static float FlamesAt(float r) => 1f - Smooth((r - 0.7f * FireTo) / (0.3f * FireTo));

        public static int RingOf(float distance) => Mathf.FloorToInt(distance / RingStep);

        /// <summary>How far ring k has risen when the fire has run out to r (infinity: the fire is done); a ring past the fire's reach rises with the last ring it reaches.</summary>
        public static float RiseOf(int k, float r)
        {
            if (float.IsPositiveInfinity(r)) return 1f;
            int last = RingOf(FireTo - Rise) - 1;
            return Smooth((r - (Mathf.Min(k, last) + 1) * RingStep) / Rise);
        }

        public static float Smooth(float t) => UbwCastTiming.Smooth(t);
    }
}
