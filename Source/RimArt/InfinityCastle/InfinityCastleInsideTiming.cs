using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// When each part of the castle's own timeline happens: the carrier and the enemies arriving through
    /// floor doors, the hold, Release, the castle going dark. Seconds in, numbers out, no drawing and no
    /// map. The port of Tools/VfxLab/web/sketches/infinity-castle-castle.js with its defaults (seed 1,
    /// 38 rooms, 6 enemies, 3.2 s before Release; the real castle holds 60 s). The enemies' walk toward
    /// the biwa room is the preview's script: it only decides where the Release doors open.
    /// </summary>
    internal static class InfinityCastleInsideTiming
    {
        public const int Seed = 1, Rooms = CastleLayout.DefaultRooms, Enemies = 6;
        public const float CasterLands = 0.3f, FirstEnemy = 0.9f, EnemyGap = 0.14f, Rise = 0.55f, Sink = 0.4f, Walk = 3f;
        public const float ReleaseDoors = 0.35f, CasterLast = 0.8f, FadeAfter = 1.6f, FadeFor = 0.8f, HoldFor = 3.2f;
        /// <summary>The Release strum's rings reach 14 cells and last 0.9 s.</summary>
        public const float StrumReach = 14f, StrumLife = 0.9f;

        public static float Landed => FirstEnemy + (Enemies - 1) * EnemyGap + Rise;
        public static float Release => Landed + HoldFor;
        public static float FadeAt => Release + FadeAfter;
        public static float Duration => FadeAt + FadeFor + 0.2f;

        public static float EnemyLands(int i) => FirstEnemy + i * EnemyGap;
        public static float EnemyOut(int i) => Release + ReleaseDoors + i * 0.05f;
        public static float CasterOut => Release + CasterLast;

        /// <summary>
        /// A moment in the hold with nothing on screen but the castle: every arrival door has gone and
        /// Release has not begun. The live castle map waits here until it is released.
        /// </summary>
        public static float Quiet => EnemyLands(Enemies - 1) + CastleEffectGraphics.DoorEnd(Rise * 0.75f) + 0.25f;

        /// <summary>How far enemy <paramref name="i"/> has walked at <paramref name="s"/>: 3 cells a second from 0.1 s after it is up, stopping 0.15 s before its Release door.</summary>
        public static float Walked(float s, int i) =>
            Mathf.Max(0f, Mathf.Min(s, Release + ReleaseDoors - 0.15f) - EnemyLands(i) - Rise - 0.1f) * Walk;

        /// <summary>The point <paramref name="distance"/> cells along a path of points.</summary>
        public static Vector2 Along(List<Vector2> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = path[i - 1], b = path[i];
                float length = (b - a).magnitude;
                if (distance <= length) return length > 0f ? Vector2.Lerp(a, b, distance / length) : a;
                distance -= length;
            }
            return path[path.Count - 1];
        }
    }
}
