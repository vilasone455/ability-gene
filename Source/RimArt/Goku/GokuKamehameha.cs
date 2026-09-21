using System.Collections.Generic;
using UnityEngine;
using G = RimArt.GokuTiming;

namespace RimArt
{
    /// <summary>When the parts of one Kamehameha happen, in seconds from the start of the preview.</summary>
    public struct KamehamehaPlan
    {
        /// <summary>
        /// The channel starts; the warp's vanish starts (Warp only); "HA"; the head has crossed the
        /// lane; the beam lets go; the end blast; the preview ends.
        /// </summary>
        public float Cast, Go, Fire, Out, Release, Blast, End;
        public float Channel, Hold;
        public bool Warp;
    }

    /// <summary>What a Kamehameha looks like now. Points are ground points on the map.</summary>
    public struct KamehamehaShot
    {
        /// <summary>The firing cell and the aim. For the warp, <see cref="Home"/> is where the caster channels; otherwise it is <see cref="From"/>.</summary>
        public Vector2 From, Toward, Home;
        public float Seconds;
        public KamehamehaPlan Plan;
        public float Length, Width, Blast, BallSize;
        /// <summary>Where the beam ends, in cells from <see cref="From"/>: its length, or half a cell short of a wall.</summary>
        public float Stop;
        /// <summary>A wall stopped the beam at <see cref="WallAt"/> cells: the three cells across the lane take soot from the blast.</summary>
        public bool Walled;
        public float WallAt;
        /// <summary>Pawns in the lane, where they are now, and when the head of the beam reached them.</summary>
        public Vector2[] Struck;
        public float[] StruckAt;
        public int StruckCount;
    }

    /// <summary>
    /// Timing of Kamehameha: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/goku-kamehameha.js; the constants are that sketch's defaults. There
    /// is no ability behind it yet. The rule (user's draft, placeholders): a 2.5 s channel in four
    /// beats, then a beam 3 cells wide and 30 long (or to the first wall) that holds 1.2 s and pushes
    /// the pawns it hits 1.5 cells, then an end blast of radius 2.5. The warp variant channels
    /// elsewhere and jumps to the firing cell at the end of the channel.
    /// </summary>
    public static class GokuKamehamehaTiming
    {
        public const float Lead = 0.3f, Tail = 2.4f, HeadTime = 0.25f, Depart = 0.3f, Push = 1.5f, Recoil = 0.3f;
        public const int Beats = 4, Threads = 12, Pebbles = 10, WindRings = 3;
        public const float Vanish = 0.12f, Gap = 0.06f, Step = 0.25f;
        public const int BurstLines = 14, FlowLines = 16, BeamRings = 5, SideDust = 26, SideRocks = 20, Smoke = 18;
        public const float LanePulse = 0.45f, FinalFlare = 0.35f;
        public const int BackDust = 14, HitSparks = 6, PressSparks = 12;
        public const float BlastOpen = 0.25f, BlastHold = 0.25f, BlastFade = 0.5f, Cool = 2f, WallAt = 20f;
        public const int BlastLevels = 4, BlastCracks = 8, BlastRocks = 12;
        /// <summary>Most points in a beam layer: 140 steps of <see cref="Step"/>.</summary>
        public const int MostSteps = 140;
        public const float FireShake = 0.1f, HoldShake = 0.04f, HoldShakeEvery = 0.2f, BlastShake = 0.14f, AfterShake = 0.06f;

        /// <summary>The script's enemies: cells along the lane from the caster, cells across it, and whether it walks at the caster.</summary>
        public static readonly float[] EnemyAlong = { 5f, 10f, 17f, 24f, 29f, 11f, 19f }, EnemyAcross = { 0.6f, -0.9f, 0.2f, -0.5f, 2.2f, 2.3f, -3.4f };
        public static readonly bool[] EnemyWalks = { false, false, true, false, false, false, false };
        public const float WalkIn = 1.1f;
        /// <summary>Where the warp's caster channels, (along, across) from the firing cell.</summary>
        public static readonly Vector2 WarpFrom = new Vector2(9f, 6f);

        // The preview's script: the sketch's sliders at their defaults.
        public const float ScriptLength = 30f, ScriptWidth = 3f, ScriptBlast = 2.5f, ScriptBallSize = 0.55f, ScriptChannel = 2.5f, ScriptHold = 1.2f;

        public static KamehamehaPlan Plan(bool warp, float channel = ScriptChannel, float hold = ScriptHold)
        {
            var plan = new KamehamehaPlan { Cast = Lead, Channel = channel, Hold = hold, Warp = warp };
            plan.Fire = plan.Cast + channel;
            plan.Go = plan.Fire - Vanish * 2f - Gap;
            plan.Out = plan.Fire + HeadTime;
            plan.Release = plan.Fire + hold;
            plan.Blast = plan.Release + Depart;
            plan.End = plan.Blast + Tail;
            return plan;
        }

        /// <summary>Where the beam ends: its length, or half a cell short of a wall that is nearer.</summary>
        public static float Stop(float length, bool wall) => wall && WallAt < length ? WallAt - 0.5f : length;

        /// <summary>When the head of the beam passes <paramref name="along"/> cells down the lane.</summary>
        public static float Passes(in KamehamehaPlan plan, float along, float length) => plan.Fire + HeadTime * along / length;

        /// <summary>
        /// Script enemy <paramref name="i"/> at <paramref name="s"/>: (along, across) from the firing
        /// cell, whether it is in the lane, and when the head reaches it. One in the lane is carried
        /// 1.5 cells from the moment it is hit; the one that walks at the caster stops when it is hit.
        /// </summary>
        public static Vector2 Enemy(int i, float s, in KamehamehaPlan plan, float length, float width, float stop, out bool inLane, out float hitAt)
        {
            float d = EnemyAlong[i], across = EnemyAcross[i];
            inLane = Mathf.Abs(across) <= width / 2f && d < stop;
            hitAt = Passes(plan, d, length);
            float since = inLane ? s - hitAt : -1f;
            float walked = EnemyWalks[i] ? WalkIn * Mathf.Min(s, hitAt) : 0f;
            float carried = inLane ? Mathf.Min(Push * G.Smooth(since / 0.5f), Mathf.Max(0f, stop - 0.6f - d)) : 0f;
            return new Vector2(d - walked + carried, across);
        }

        /// <summary>The camera shakes: "HA", a tremble every 0.2 s while the beam holds, the end blast and its echo.</summary>
        public static List<GokuShake> Shakes(in KamehamehaPlan plan)
        {
            var list = new List<GokuShake> { new GokuShake(plan.Fire, FireShake) };
            for (int k = 1; plan.Fire + k * HoldShakeEvery < plan.Release - 1e-4f; k++) list.Add(new GokuShake(plan.Fire + k * HoldShakeEvery, HoldShake));
            list.Add(new GokuShake(plan.Blast, BlastShake));
            list.Add(new GokuShake(plan.Blast + 0.2f, AfterShake));
            return list;
        }
    }
}
