using System.Collections.Generic;
using UnityEngine;
using G = RimArt.GokuTiming;

namespace RimArt
{
    /// <summary>
    /// When the parts of one Spirit Bomb happen, in seconds from the start of the preview, and the
    /// power behind it. Every size follows the power; the weight of the impact follows
    /// <see cref="Charge"/>, the power at the throw over 30.
    /// </summary>
    public struct SpiritBombPlan
    {
        /// <summary>The channel starts; the throw; the flight; the grind; the detonation; the dome is fully open; it starts to lift; it is gone; the preview ends.</summary>
        public float Cast, Release, Fly, Hit, Dome, Open, Fade, Gone, End;
        public float Charge, FlyTime;
        public int Lenders;
        public float Lend, BlastPer, SizePer;

        /// <summary>Between a small bomb's value and a full one's, by the charge.</summary>
        public float By(float small, float full) => Mathf.Lerp(small, full, Charge);
        public int Count(float small, float full) => G.Round(By(small, full));

        /// <summary>When lender <paramref name="i"/> joins.</summary>
        public float Joins(int i) => Cast + GokuSpiritBombTiming.FirstLender + i * GokuSpiritBombTiming.LenderEvery;

        /// <summary>Power at <paramref name="s"/>: 1 per second from the caster plus <see cref="Lend"/> per second from each lender since it joined.</summary>
        public float PowerAt(float s)
        {
            float now = Mathf.Min(s, Release), power = Mathf.Max(0f, now - Cast);
            for (int i = 0; i < Lenders; i++) power += Mathf.Max(0f, now - Joins(i)) * Lend;
            return power;
        }

        public float BlastAt(float s) => GokuSpiritBombTiming.BaseRadius + BlastPer * PowerAt(s);
        public float RadiusAt(float s) => GokuSpiritBombTiming.StartSize + SizePer * PowerAt(s);
    }

    /// <summary>What a Spirit Bomb looks like now. Points are ground points on the map.</summary>
    public struct SpiritBombShot
    {
        /// <summary>The caster's cell and the centre of the target area.</summary>
        public Vector2 Caster, Target;
        public float Seconds;
        public SpiritBombPlan Plan;
        /// <summary>The lenders, <see cref="SpiritBombPlan.Lenders"/> of them, in the order they join.</summary>
        public Vector2[] Lenders;
        /// <summary>Hostile pawns round the target. Those inside the blast break into flecks as the dome passes them.</summary>
        public Vector2[] Foes;
        public int FoeCount;
        /// <summary>Pawns round the target it spares (colonists, animals): they glow blue as the dome passes them.</summary>
        public Vector2[] Spared;
        public int SparedCount;
        /// <summary>Structure cells inside the blast: they glow blue while the dome stands.</summary>
        public Vector2[] Walls;
        public int WallCount;
    }

    /// <summary>
    /// Timing of Spirit Bomb: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/goku-spirit-bomb.js; the constants are that sketch's defaults. There
    /// is no ability behind it yet. The rule (user's draft, placeholders): a channel with no upper
    /// limit (minimum 3 s); 1 power per second from the caster and 1 from each colonist that lends;
    /// blast radius 2 + 0.25 x power; damage only to hostile pawns; the bomb flies 1.4 s.
    /// </summary>
    public static class GokuSpiritBombTiming
    {
        public const float Lead = 0.3f, Tail = 2f, Swing = 0.25f, Open = 0.4f, Fade = 1f, FullPower = 30f;
        public const float FirstLender = 0.8f, LenderEvery = 0.7f, BaseRadius = 2f, StartSize = 0.25f, Hang = 2.2f, Climb = 1.6f;
        public const int Wisps = 16, WispsPerLender = 8, Levels = 7, Sparkles = 60;
        public const float WispReach = 10f;
        public const float SurgeTime = 0.45f, SurgeSwell = 0.14f, Stretch = 0.16f, FlightSpin = 3f, TailSpan = 0.3f;
        public const int FallSparks = 16, GrindSparks = 16, Chunks = 10, Flecks = 9, MaxPebbles = 14;
        public const float ColumnExtra = 5f;
        // What follows the charge, as small bomb and full bomb.
        public static readonly Vector2 GrindTime = new Vector2(0.3f, 0.75f), HoldTime = new Vector2(0.35f, 1f), WhiteTime = new Vector2(0.03f, 0.09f),
            Shake = new Vector2(0.05f, 0.2f), CrackCount = new Vector2(5f, 14f), VeinCount = new Vector2(2f, 8f), PillarCount = new Vector2(0f, 10f),
            RockCount = new Vector2(5f, 32f), RockHeight = new Vector2(0.5f, 1.2f), StreakCount = new Vector2(8f, 26f), PathRingCount = new Vector2(2f, 7f);
        /// <summary>The ball's orbit lines: tilt speed, turn speed (degrees per second), starting angle.</summary>
        public static readonly float[] OrbitTilt = { 1.1f, -1.5f, 0.8f }, OrbitSpeed = { 25f, -35f, 45f }, OrbitStart = { 0f, 60f, 120f };

        /// <summary>The script's lenders: cells behind the caster, cells to its left.</summary>
        public static readonly Vector2[] LenderAt = { new Vector2(2.5f, 2f), new Vector2(3f, -1.6f), new Vector2(1f, -3.3f), new Vector2(1.2f, 3.5f), new Vector2(4.6f, 0.4f), new Vector2(4.2f, 3.1f) };
        /// <summary>At the target, cells east and north of its centre: five enemies, a colonist in melee with them, an animal, and where three cells of colony wall start.</summary>
        public static readonly Vector2[] FoeAt = { new Vector2(-0.8f, 0.6f), new Vector2(1.1f, -0.4f), new Vector2(0.3f, 1.9f), new Vector2(-1.9f, -1.2f), new Vector2(2.4f, 1.3f) };
        public static readonly Vector2 FriendAt = new Vector2(-0.1f, -0.5f), BeastAt = new Vector2(1.6f, -1.9f), WallFrom = new Vector2(-2.6f, 1.6f);
        /// <summary>One more enemy stands this far past the final blast radius, at this bearing (radians).</summary>
        public const float OutsideBy = 2.5f, OutsideBearing = -0.6f;

        // The preview's script: the sketch's sliders at their defaults.
        public const int ScriptLenders = 4;
        public const float ScriptDistance = 16f, ScriptChannel = 6f, ScriptFly = 1.4f, ScriptLend = 1f, ScriptBlastPer = 0.25f, ScriptSizePer = 0.12f;

        public static SpiritBombPlan Plan(int lenders, float channel = ScriptChannel, float fly = ScriptFly, float lend = ScriptLend,
            float blastPer = ScriptBlastPer, float sizePer = ScriptSizePer)
        {
            var plan = new SpiritBombPlan { Cast = Lead, Lenders = lenders, Lend = lend, BlastPer = blastPer, SizePer = sizePer, FlyTime = fly };
            plan.Release = plan.Cast + channel;
            plan.Charge = Mathf.Clamp01(plan.PowerAt(plan.Release) / FullPower);
            plan.Fly = plan.Release + Swing;
            plan.Hit = plan.Fly + fly;
            plan.Dome = plan.Hit + plan.By(GrindTime.x, GrindTime.y);
            plan.Open = plan.Dome + Open;
            plan.Fade = plan.Open + plan.By(HoldTime.x, HoldTime.y);
            plan.Gone = plan.Fade + Fade;
            plan.End = plan.Gone + Tail;
            return plan;
        }

        /// <summary>
        /// The camera shakes: a heavy bomb trembles every 0.5 s while it is still over the caster's
        /// head, the grind shakes every 0.1 s, then the detonation and two echoes.
        /// </summary>
        public static List<GokuShake> Shakes(in SpiritBombPlan plan)
        {
            var list = new List<GokuShake>();
            float big = plan.By(Shake.x, Shake.y);
            for (int k = 1; plan.Cast + k * 0.5f < plan.Release - 1e-4f; k++)
            {
                float at = plan.Cast + k * 0.5f, c = plan.PowerAt(at) / FullPower;
                if (c > 0.5f) list.Add(new GokuShake(at, 0.012f + 0.02f * Mathf.Min(1f, c)));
            }
            for (int k = 0; plan.Hit + k * 0.1f < plan.Dome - 0.01f; k++)
            {
                float at = plan.Hit + k * 0.1f;
                list.Add(new GokuShake(at, big * (0.15f + 0.2f * (at - plan.Hit) / (plan.Dome - plan.Hit))));
            }
            list.Add(new GokuShake(plan.Dome, big));
            list.Add(new GokuShake(plan.Dome + 0.25f, big * 0.55f));
            list.Add(new GokuShake(plan.Dome + 0.5f, big * 0.3f));
            return list;
        }
    }
}
