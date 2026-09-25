using System;

namespace RimArt
{
    /// <summary>
    /// The lab's easing and hash, shared by every ported picture so a port lands where its sketch
    /// did. System.Math only, so the rule tests can compile it without Unity.
    /// </summary>
    public static class VfxMath
    {
        /// <summary>The lab's smoothstep of t, clamped to 0..1 first.</summary>
        public static float Smooth(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        /// <summary>The lab's rand(i), the sine hash, in double as it is there.</summary>
        public static double Hash(int index)
        {
            double n = Math.Sin(index * 127.1 + 17) * 43758.5453;
            return n - Math.Floor(n);
        }

        /// <summary><see cref="Hash"/> as a float, 0 to 1, fixed per index. Visual scatter must not consume gameplay RNG.</summary>
        public static float Rand(int index) => (float)Hash(index);
    }
}
