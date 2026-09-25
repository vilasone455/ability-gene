using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>The preview's three scenes: the sketch's two "fires" scenarios and its second mode.</summary>
    public enum SwordsScene { WalksEast, Stands, Spins }

    /// <summary>One blade that left the ring. Points are relative to where the carrier started.</summary>
    public struct SwordShot
    {
        public int K, Slot, Target;
        public float FireAt, HitAt, RegrowAt, Deg, StartAngle;
        public Vector2 From, Chest;
    }

    /// <summary>
    /// Summoned Swords' timing and the preview's script: when the blades rise, where each slot is on the
    /// turning ring, and which blade flies at which target when. Seconds in, geometry out, no drawing and
    /// no map. The port of Tools/VfxLab/web/sketches/vergil-summoned-swords.js; the constants are that
    /// sketch's default values.
    ///
    /// The ability behind it is proposed and not agreed, and every number is a placeholder that will
    /// become an XML field: self cast, warm-up 0.5 s, 20 s, cooldown 45 s. 8 blades circle the carrier;
    /// every 0.6 s one fires by itself at the nearest hostile within 12 cells holding fewer than 4 blades,
    /// 9 Stab at 30 % armour penetration and -15 % move speed per blade stuck in it. A fired slot regrows
    /// after 2.4 s. The second mode widens and speeds up the ring and cuts every hostile within 1.6 cells
    /// for 6 Cut every 0.5 s.
    ///
    /// The preview's script: the hostiles are fixed points the sketch's stand-in raiders stood on, and
    /// the carrier walks east at 0.8 cells a second in <see cref="SwordsScene.WalksEast"/>. They are
    /// targets for the blades, not pawns; nothing here draws them.
    /// </summary>
    public static class SummonedSwordsTiming
    {
        /// <summary>Decided values, the sketch's constants.</summary>
        public const float Lead = 0.3f, Tail = 1f, Rise = 0.22f, Grow = 0.22f, TurnTime = 0.14f, Speed = 45f, Break = 0.35f,
            RingFade = 0.4f, FirstShot = 0.2f;
        public const float Height = 0.5f, Chest = 0.3f, BladeLength = 0.62f, BladeWide = 0.12f, StuckOut = 0.5f, Reach = 0.15f, WalkSpeed = 0.8f;
        public const int MaxPins = 4, Shards = 7;
        /// <summary>The second mode: the wider, faster ring, its cut radius, and the trail each blade drags.</summary>
        public const float SpinRing = 1.2f, SpinRate = 420f, CutRadius = 1.6f, Ramp = 0.3f, ArcBehind = 40f;

        /// <summary>The sketch's panel defaults, the ones the preview plays.</summary>
        public const float Range = 12f, Every = 0.6f, Regrow = 2.4f, Stuck = 3f, Ring = 0.95f, Spin = 160f, Form = 0.5f, Lasts = 6f;
        public const int Slots = 8;

        /// <summary>Where the sketch's raiders stood, east and north of the carrier's start. The last starts outside the range.</summary>
        private static readonly Vector2[] Hostiles = { new Vector2(6f, 2.5f), new Vector2(-4.5f, 4f), new Vector2(3.5f, -5.5f), new Vector2(14.5f, -3f) };

        public static float CastAt => Lead;
        public static float FormedAt => Lead + Form;
        public static float FireAt => FormedAt + FirstShot;
        public static float StopAt => FormedAt + Lasts;
        public static float Duration => StopAt + Tail;

        public static bool Spins(SwordsScene scene) => scene == SwordsScene.Spins;

        /// <summary>Where the carrier is at <paramref name="s"/>, relative to where it started.</summary>
        public static Vector2 CasterAt(SwordsScene scene, float s) =>
            new Vector2(scene == SwordsScene.WalksEast ? WalkSpeed * Mathf.Max(0f, Mathf.Min(s, StopAt) - FormedAt) : 0f, 0f);

        /// <summary>
        /// Slot <paramref name="i"/>'s angle in degrees at <paramref name="s"/>. The second mode speeds up
        /// over <see cref="Ramp"/>, so its angle is the integral of the rate.
        /// </summary>
        public static float SlotAngle(int i, int n, SwordsScene scene, float s)
        {
            float e = s - FormedAt, extra = 0f;
            if (Spins(scene) && e > 0f) extra = (SpinRate - Spin) * (e < Ramp ? e * e / (2f * Ramp) : e - Ramp / 2f);
            return i / (float)n * 360f + Spin * s + extra;
        }

        public static float RingRadius(SwordsScene scene, float s) =>
            Spins(scene) ? Mathf.Lerp(Ring, SpinRing, VfxMath.Smooth((s - FormedAt) / Ramp)) : Ring;

        /// <summary>A point on the ring round <paramref name="c"/>, <paramref name="height"/> cells up, as it is drawn.</summary>
        public static Vector2 RingPoint(Vector2 c, float deg, float radius, float height) =>
            new Vector2(c.x + Mathf.Cos(deg * Mathf.Deg2Rad) * radius, c.y + height * SixPathsHeight.Lift + Mathf.Sin(deg * Mathf.Deg2Rad) * radius);

        /// <summary>From <paramref name="a"/> toward <paramref name="b"/> degrees the short way round, by the share <paramref name="u"/>.</summary>
        public static float TurnTo(float a, float b, float u) => a + ((((b - a) % 360f) + 540f) % 360f - 180f) * u;

        private static readonly Dictionary<SwordsScene, List<SwordShot>> planned = new Dictionary<SwordsScene, List<SwordShot>>();

        /// <summary>
        /// Every shot of the clip. Worked out from zero once per scene, the way the sketch replays it each
        /// frame, so the timeline can be scrubbed. The second mode fires nothing.
        /// </summary>
        public static List<SwordShot> Shots(SwordsScene scene)
        {
            if (planned.TryGetValue(scene, out List<SwordShot> known)) return known;
            var shots = new List<SwordShot>();
            planned[scene] = shots;
            if (Spins(scene)) return shots;

            var free = new float[Slots];
            for (int i = 0; i < Slots; i++) free[i] = FormedAt;
            for (int k = 0; FireAt + k * Every <= StopAt - 0.4f; k++)
            {
                float T = FireAt + k * Every;
                Vector2 c = CasterAt(scene, T);
                int target = -1;
                float near = Range;
                for (int j = 0; j < Hostiles.Length; j++)
                {
                    float d = Vector2.Distance(Hostiles[j], c);
                    int held = 0;
                    foreach (SwordShot h in shots) if (h.Target == j && h.FireAt + Stuck > T) held++;
                    if (d <= near && held < MaxPins) { target = j; near = d; }
                }
                if (target < 0) continue;

                var chest = new Vector2(Hostiles[target].x, Hostiles[target].y + Chest);
                float aim = Mathf.Atan2(chest.y - (c.y + Height * SixPathsHeight.Lift), chest.x - c.x) * Mathf.Rad2Deg;
                int slot = -1;
                float best = 1e9f;
                for (int i = 0; i < Slots; i++)
                {
                    float off = Mathf.Abs(TurnTo(0f, SlotAngle(i, Slots, scene, T) - aim, 1f));
                    if (free[i] <= T && off < best) { slot = i; best = off; }
                }
                if (slot < 0) continue;

                free[slot] = T + Regrow + Grow;
                float angle = SlotAngle(slot, Slots, scene, T);
                Vector2 from = RingPoint(c, angle, Ring, Height);   // only "fires" gets here, so the ring is never the wider spinning one
                float dist = Vector2.Distance(chest, from);
                shots.Add(new SwordShot
                {
                    K = k, Slot = slot, Target = target, FireAt = T, From = from, Chest = chest,
                    Deg = Mathf.Atan2(chest.y - from.y, chest.x - from.x) * Mathf.Rad2Deg, StartAngle = angle,
                    HitAt = T + TurnTime + Mathf.Max(0f, dist - Reach) / Speed, RegrowAt = T + Regrow,
                });
            }
            return shots;
        }
    }
}
