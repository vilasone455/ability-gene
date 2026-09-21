using UnityEngine;

namespace RimArt
{
    /// <summary>One light that counts full, as a campfire or a lamp does: its level falls in a straight line to 0 at its radius.</summary>
    public struct ShadowPlexusFire
    {
        public Vector2 At;
        public float Radius, On;

        public ShadowPlexusFire(Vector2 at, float radius, float on)
        {
            At = at;
            Radius = radius;
            On = on;
        }
    }

    /// <summary>
    /// The numbers and pure helpers shared by every Shadow plexus effect: the light rule, the
    /// stepped walk, and the swaying path a shadow line takes. Seconds and points in, numbers out,
    /// no drawing and no map. The port of Tools/VfxLab/web/sketches/lib/shadow-plexus.js; its
    /// numbers are that file's.
    /// </summary>
    public static class ShadowPlexusTiming
    {
        /// <summary>
        /// The light rule. Sky light counts <see cref="SkyShare"/>, a fire counts full, the higher one
        /// wins; under <see cref="DarkBelow"/> a cell is dark and a shadow line cannot cross it.
        /// </summary>
        public const float SkyShare = 0.5f, DarkBelow = 0.3f, FireRadius = 10f;
        /// <summary>How long the two halves of a cut line take to run back.</summary>
        public const float SnapTime = 0.4f;

        public static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);

        /// <summary>The ease the sketches use for a line running out: fast, then slowing.</summary>
        public static float EaseOut(float t)
        {
            float x = 1f - Mathf.Clamp01(t);
            return 1f - x * x;
        }

        /// <summary>The light level at a point by day or by night, with no fire.</summary>
        public static float Level(bool night) => night ? 0f : SkyShare;

        /// <summary>The light level at <paramref name="at"/> by day or by night, with one fire.</summary>
        public static float Level(Vector2 at, bool night, in ShadowPlexusFire fire)
        {
            float lit = fire.Radius > 0f ? fire.On * Mathf.Clamp01(1f - Vector2.Distance(at, fire.At) / fire.Radius) : 0f;
            return Mathf.Max(Level(night), lit);
        }

        /// <summary>An ability's true range: its full range times the light level, and nothing at all in the dark.</summary>
        public static float Range(float fullRange, float level) => level < DarkBelow ? 0f : fullRange * level;

        /// <summary>
        /// Cells walked <paramref name="t"/> seconds into <paramref name="count"/> steps of
        /// <paramref name="stepTime"/> with <paramref name="pause"/> after each. <paramref name="snap"/>
        /// above 1 finishes each step early, which is how a dragged body lurches where the carrier walks.
        /// </summary>
        public static float Walked(float t, float stepTime, float pause, int count, float snap = 1f)
        {
            if (t <= 0f) return 0f;
            float per = stepTime + pause;
            int k = Mathf.FloorToInt(t / per);
            return k >= count ? count : k + Smooth((t - k * per) / stepTime * snap);
        }

        public static Vector2 PointOn(Vector2 from, Vector2 to, float u) => Vector2.Lerp(from, to, u);

        /// <summary>
        /// Fills <paramref name="into"/> with points along the straight line from..to between the shares
        /// <paramref name="u0"/> and <paramref name="u1"/> of it, with a slow sideways wave pinned at both
        /// ends of the whole line. <paramref name="sway"/> 0 is a taut straight line.
        /// </summary>
        public static void Path(Vector2[] into, Vector2 from, Vector2 to, float u0, float u1, float seconds, float sway)
        {
            Vector2 run = to - from;
            float length = run.magnitude;
            if (length < 1e-5f) length = 1f;
            var normal = new Vector2(-run.y / length, run.x / length);
            float waves = Mathf.Max(1f, length / 2.4f);
            int steps = into.Length - 1;
            for (int i = 0; i <= steps; i++)
            {
                float u = Mathf.Lerp(u0, u1, i / (float)steps);
                float off = sway * Mathf.Sin(u * waves * Mathf.PI * 2f - seconds * 3f) * Mathf.Sin(u * Mathf.PI);
                into[i] = from + run * u + normal * off;
            }
        }
    }
}
