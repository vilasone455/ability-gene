using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>One camera shake of a Goku effect: when, in seconds from the cast, and how hard.</summary>
    public struct GokuShake
    {
        public float At, Size;
        public GokuShake(float at, float size) { At = at; Size = size; }
    }

    /// <summary>
    /// What the Goku timing classes share: the sketches' smooth, rand and Math.round, with no
    /// drawing and no map.
    /// </summary>
    public static class GokuTiming
    {
        public static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);
        public static float Rand(int index) => SixPathsBloomTiming.Rand(index);
        /// <summary>Math.round: halves go up, not to even.</summary>
        public static int Round(float v) => Mathf.FloorToInt(v + 0.5f);

        /// <summary>A point <paramref name="along"/> a direction and <paramref name="across"/> it (to its left) from <paramref name="origin"/>.</summary>
        public static Vector2 Place(Vector2 origin, Vector2 toward, float along, float across = 0f) =>
            origin + toward * along + new Vector2(-toward.y, toward.x) * across;

        /// <summary>A point <paramref name="distance"/> cells from <paramref name="origin"/> in direction <paramref name="degrees"/> (0 east, 90 north).</summary>
        public static Vector2 Polar(Vector2 origin, float degrees, float distance) =>
            origin + new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * distance;

        internal static List<GokuShake> Sorted(List<GokuShake> shakes)
        {
            shakes.Sort((a, b) => a.At.CompareTo(b.At));
            return shakes;
        }
    }
}
