using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.NezukoBoxGraphics;

namespace RimArt
{
    /// <summary>How a pawn leaves the box: the leap with a strike, laid down (downed), or stepping out (plain).</summary>
    public enum NezukoExit { Strike, Downed, TimeUp }

    /// <summary>
    /// Go in's script (nezuko-box-go-in.js, default sliders). In game the approach is the ability's own
    /// walk to the pawn, so the picture starts at <see cref="Open0"/> with the warmup (the door opening)
    /// and the ability fires at <see cref="Enter0"/>; the pawn is taken off the map at <see cref="Gone"/>.
    /// </summary>
    public static class NezukoBoxGoInTiming
    {
        public const float Approach = 0.4f, Open = 0.25f, Enter = 0.3f, Shut = 0.2f, Settle = 0.45f, Hold = 2f;
        public const float Approach0 = 0.2f, Open0 = Approach0 + Approach, Enter0 = Open0 + Open, Gone = Enter0 + Enter * 0.5f;
        public const float Shut0 = Enter0 + Enter, Latch = Shut0 + Shut, Asleep = Latch + Settle, End = Latch + Hold;
    }

    /// <summary>
    /// Come out's script (nezuko-box-come-out.js). Strike: rumble, the lid bursts, the leap (the flyer's
    /// flight time), landing, the strike. Downed and time up: the door opens and the pawn is laid down
    /// or steps out on the cell outside the door. In game a strike starts at <see cref="Rumble0"/> and a
    /// calm exit at <see cref="Open0"/>; nothing waits before them.
    /// </summary>
    public static class NezukoBoxComeOutTiming
    {
        public const float Rumble = 0.25f, Burst = 0.1f, Emerge = 0.12f, StrikeGap = 0.05f, Dazed = 1.5f, Shut = 0.2f;
        public const float Flight = 0.45f, Peak = 1.1f, Distance = 3f, Hold = 1.2f;
        public const float Rumble0 = 0.2f, BurstAt = Rumble0 + Rumble, Leap0 = BurstAt + 0.05f;
        public const float Open0 = 0.2f, OpenFor = 0.3f, Out0 = Open0 + OpenFor;

        public static float Land(float flight) => Leap0 + flight;
        public static float StrikeAt(float flight) => Land(flight) + StrikeGap;
        public static float Out(NezukoExit kind) => kind == NezukoExit.TimeUp ? 0.45f : 0.3f;

        public static float Shut0(NezukoExit kind, float flight) =>
            kind == NezukoExit.Strike ? StrikeAt(flight) + 0.4f : Out0 + Out(kind) + 0.3f;

        public static float End(NezukoExit kind, float flight, float hold) =>
            kind == NezukoExit.Strike ? StrikeAt(flight) + Mathf.Max(Dazed + 0.3f, hold) : Shut0(kind, flight) + Shut + hold;
    }

    /// <summary>One Go in picture: the wearer's feet and facing, and how it is fitted.</summary>
    public struct NezukoGoInShot
    {
        public Vector2 Feet;
        public float Facing;
        /// <summary>The preview's downed pawn leaves blood where it lay; in game the real blood is there.</summary>
        public bool DownedBlood;
        /// <summary>1 in the previews, FitScale on a real wearer.</summary>
        public float Scale;
        public float? PawnLayer;
        /// <summary>The breathing clock.</summary>
        public float T;
    }

    /// <summary>One Come out picture.</summary>
    public struct NezukoComeOutShot
    {
        public Vector2 Feet, Land;
        public float Facing;
        public NezukoExit Kind;
        /// <summary>The enemy's feet the strike lands on, or null for no strike picture.</summary>
        public Vector2? Enemy;
        /// <summary>The preview's stand-in enemy is knocked back 0.12 cells; a real pawn is not moved.</summary>
        public bool Knock;
        public float Flight, Peak, Scale;
        public float? PawnLayer;
        public float T;
    }

    public static class NezukoBoxPictures
    {
        private static readonly Vector2[] Streak = new Vector2[7];
        private static readonly Vector2[] Slash = new Vector2[3];

        /// <summary>Go in at <paramref name="s"/> seconds: the door swings open, the pink puff where the pawn goes in, the door shuts, the latch jolt, and the box falls asleep.</summary>
        public static void GoIn(in NezukoGoInShot shot, float s, Vector2 sun, float strength)
        {
            const float Open0 = NezukoBoxGoInTiming.Open0, Shut0 = NezukoBoxGoInTiming.Shut0, Latch = NezukoBoxGoInTiming.Latch, Enter0 = NezukoBoxGoInTiming.Enter0;
            float S = shot.Scale;
            float door = s < Open0 ? 0f : s < Shut0 ? Smooth((s - Open0) / NezukoBoxGoInTiming.Open) : 1f - Smooth((s - Shut0) / NezukoBoxGoInTiming.Shut);
            var look = new NezukoBoxLook
            {
                Door = door,
                Inner = door * (s < Shut0 ? 1f : 0.5f),
                Shake = 0.03f * Bump((s - Latch) / 0.18f) * Mathf.Sin((s - Latch) * 60f),
                Sleeping = Smooth((s - Latch) / NezukoBoxGoInTiming.Settle),
                T = shot.T,
            };
            NezukoBoxDrawn box = Box(shot.Feet, shot.Facing, look, sun, strength, S, shot.PawnLayer);

            if (shot.DownedBlood)
            {
                // Where the downed pawn lay: a cell away behind and to the left of the wearer.
                float a = shot.Facing * Mathf.Deg2Rad;
                var f = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var n = new Vector2(-f.y, f.x);
                Vector2 start = shot.Feet + ((Back - 0.95f) * f + 0.55f * n) * S;
                Sprite(new Vector2(start.x - 0.05f * S, start.y + 0.02f * S), 0.7f * S, 0.45f * S, Fade(Blood, 0.8f * Smooth(s / 0.1f)), puff, Floor + 0.02f);
            }
            // The puff that covers the pawn leaving the map, and the wood dust off the doorway.
            float puffAge = s - Enter0;
            if (puffAge >= 0f && puffAge < 0.6f)
            {
                float u = puffAge / 0.6f;
                Vector2 m = box.DoorScreen;
                Sprite(m, 0.9f * (0.4f + u) * S, 0.8f * (0.4f + u) * S, Fade(Pink, 0.6f * Mathf.Sin(u * Mathf.PI)), glow, Y + 0.07f);
                for (int i = 0; i < 5; i++)
                {
                    float th = i * 1.26f + 0.4f, far = u * 0.35f * S;
                    Sprite(new Vector2(m.x + Mathf.Cos(th) * far, m.y + Mathf.Sin(th) * far * 0.7f), 0.35f * (0.5f + u) * S, 0.3f * (0.5f + u) * S,
                        Fade(Pink, 0.35f * (1f - u)), puff, Y + 0.071f + i * 0.0005f);
                }
            }
            WoodDust(box.DoorScreen, s - Enter0, 0.7f, 0.5f, 10, S);
            WoodDust(box.DoorScreen, s - Latch, 0.5f, 0.4f, 30, S);
        }

        /// <summary>
        /// Come out at <paramref name="s"/> seconds. The leaping pawn itself is not drawn (in game it is
        /// the real pawn in a flyer); its pink streak, the landing dust, the strike's flash and slashes
        /// and the enemy's daze marks are.
        /// </summary>
        public static void ComeOut(in NezukoComeOutShot shot, float s, Vector2 sun, float strength)
        {
            float S = shot.Scale;
            bool strike = shot.Kind == NezukoExit.Strike;
            float flight = shot.Flight;
            float shut0 = NezukoBoxComeOutTiming.Shut0(shot.Kind, flight);
            var look = new NezukoBoxLook { T = shot.T };
            float sleeping;
            if (strike)
            {
                const float Rumble0 = NezukoBoxComeOutTiming.Rumble0, BurstAt = NezukoBoxComeOutTiming.BurstAt, Rumble = NezukoBoxComeOutTiming.Rumble;
                // The burst goes out through the top lid; during the rumble the lid rattles on its hinge.
                look.Lid = s < BurstAt ? (s >= Rumble0 ? 0.07f * Mathf.Abs(Mathf.Sin((s - Rumble0) * 70f)) * Smooth((s - Rumble0) / Rumble) : 0f)
                    : s < shut0 ? Smooth((s - BurstAt) / NezukoBoxComeOutTiming.Burst) : 1f - Smooth((s - shut0) / NezukoBoxComeOutTiming.Shut);
                look.Inner = look.Lid * (1f - Smooth((s - NezukoBoxComeOutTiming.Leap0 - 0.2f) / 0.4f)) + 0.3f * look.Lid;
                sleeping = s < BurstAt ? 1f + 0.6f * Smooth((s - Rumble0) / Rumble) : 0f;
                if (s >= Rumble0 && s < BurstAt) look.Shake = 0.025f * Smooth((s - Rumble0) / Rumble) * Mathf.Sin((s - Rumble0) * 150f);
            }
            else
            {
                const float Open0 = NezukoBoxComeOutTiming.Open0;
                look.Door = s < Open0 ? 0f : s < shut0 ? Smooth((s - Open0) / NezukoBoxComeOutTiming.OpenFor) : 1f - Smooth((s - shut0) / NezukoBoxComeOutTiming.Shut);
                look.Inner = look.Door * 0.6f;
                sleeping = 1f - Smooth((s - Open0) / 0.2f);
            }
            look.Sleeping = Mathf.Clamp01(sleeping);
            NezukoBoxDrawn box = Box(shot.Feet, shot.Facing, look, sun, strength, S, shot.PawnLayer);
            if (sleeping > 1f) Sprite(box.TopScreen, 0.55f * S, 0.4f * S, Fade(Pink, 0.5f * (sleeping - 1f)), glow, Y + 0.05f);

            Vector2 land = shot.Land;
            float landAt = NezukoBoxComeOutTiming.Land(flight), strikeAt = NezukoBoxComeOutTiming.StrikeAt(flight);
            if (strike)
            {
                // The landing cell, marked from the start until just after the landing.
                float ringA = Smooth(s / 0.15f) * (1f - Smooth((s - landAt) / 0.3f));
                if (ringA > 0f) Circle(land, 0.45f * S, 0.6f * ringA, Floor + 0.01f, Pale);
                Vector2 dir = land - box.TopGround;
                if (shot.Enemy.HasValue && (shot.Enemy.Value - land).sqrMagnitude > 1e-4f) dir = shot.Enemy.Value - land;
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
                float aim = Mathf.Atan2(dir.y, dir.x);

                if (shot.Enemy.HasValue)
                {
                    Vector2 e0 = shot.Enemy.Value;
                    float knock = shot.Knock && s >= strikeAt ? 0.12f * Smooth((s - strikeAt) / 0.15f) : 0f;
                    DazeMarks(e0 + dir * knock, s, s - strikeAt, NezukoBoxComeOutTiming.Dazed, S);
                }

                // A short pink streak along the leap, just behind the pawn: up out of the top and on an arc to the cell.
                float leap0 = NezukoBoxComeOutTiming.Leap0;
                if (s >= leap0)
                {
                    float u = Mathf.Clamp01((s - leap0) / flight);
                    if (u > 0.02f && u < 1f)
                    {
                        for (int k = 0; k <= 6; k++)
                        {
                            float v = Mathf.Max(0f, u - 0.18f * (1f - k / 6f)), ev = Smooth(v);
                            Vector2 gv = Vector2.Lerp(box.TopGround, land, ev);
                            float hv = Mathf.Lerp(box.TopH, 0f, v) + Mathf.Sin(v * Mathf.PI) * shot.Peak * S;
                            Streak[k] = new Vector2(gv.x, gv.y + (hv + 0.35f * S) * Lift);
                        }
                        Trail(Streak, 7, 0.22f * S, Fade(Pink, 0.55f), Y + 0.05f);
                    }
                }
                // Landing dust: a ring and puffs.
                float la = s - landAt;
                if (la >= 0f && la < 0.5f)
                {
                    Circle(land, (0.2f + la * 1.6f) * S, (1f - la / 0.5f) * 0.55f, Floor + 0.015f, Dust);
                    for (int i = 0; i < 7; i++)
                    {
                        float u = la / (0.3f + Rand(i + 70) * 0.2f);
                        if (u > 1f) continue;
                        float th = Rand(i + 71) * Mathf.PI * 2f, far = u * (0.35f + Rand(i + 72) * 0.4f) * S;
                        Sprite(new Vector2(land.x + Mathf.Cos(th) * far, land.y + Mathf.Sin(th) * far * 0.7f + Mathf.Sin(u * Mathf.PI) * 0.1f * S), (0.22f + u * 0.3f) * S, (0.18f + u * 0.25f) * S,
                            Fade(Dust, Mathf.Sin(u * Mathf.PI) * 0.55f), puff, Y + 0.01f + i * 0.0005f);
                    }
                }
                // The strike: a flash and three slash streaks across the enemy's body.
                float sa = s - strikeAt;
                if (shot.Enemy.HasValue && sa >= 0f && sa < 0.35f)
                {
                    Vector2 e0 = shot.Enemy.Value;
                    var c = new Vector2(e0.x, e0.y + 0.35f * S);
                    Sprite(c, 0.9f * S, 0.7f * S, Fade(Pale, Mathf.Max(0f, 1f - sa / 0.1f) * 0.9f), glow, Y + 0.08f);
                    for (int i = 0; i < 3; i++)
                    {
                        float v = Mathf.Clamp01(sa / 0.08f - i * 0.25f), fade = 1f - Mathf.Clamp01((sa - 0.1f) / 0.25f);
                        if (v <= 0f) continue;
                        float off = (i - 1) * 0.1f * S, ang = aim + Mathf.PI / 2f + 0.5f;
                        var p0 = new Vector2(c.x - Mathf.Cos(ang) * 0.35f * S + dir.x * off, c.y - Mathf.Sin(ang) * 0.35f * S + dir.y * off);
                        var p1 = new Vector2(c.x + Mathf.Cos(ang) * 0.35f * S * (2f * v - 1f) + dir.x * off, c.y + Mathf.Sin(ang) * 0.35f * S * (2f * v - 1f) + dir.y * off);
                        Slash[0] = p0; Slash[1] = (p0 + p1) / 2f; Slash[2] = p1;
                        Trail(Slash, 3, 0.09f * S, Fade(Pale, 0.95f * fade), Y + 0.085f + i * 0.001f);
                    }
                }
                WoodDust(box.TopScreen, s - NezukoBoxComeOutTiming.BurstAt, 1.1f, 0.55f, 90, S);
            }
            else
            {
                // Downed: laid on the cell under a soft puff. Time up: steps out of the doorway to the cell.
                float oa = s - NezukoBoxComeOutTiming.Out0, out1 = NezukoBoxComeOutTiming.Out(shot.Kind);
                if (shot.Kind == NezukoExit.Downed)
                {
                    if (oa >= 0f && oa < 0.6f)
                    {
                        float u = oa / 0.6f;
                        Sprite(new Vector2(land.x, land.y + 0.15f * S), 1.0f * (0.5f + u) * S, 0.7f * (0.5f + u) * S, Fade(Pink, 0.45f * Mathf.Sin(u * Mathf.PI)), puff, Y + 0.07f);
                    }
                }
                else if (oa > out1 && oa < out1 + 0.8f)
                {
                    float v = (oa - out1) / 0.8f;
                    Sprite(new Vector2(land.x + 0.12f * S, land.y + (0.75f + v * 0.25f) * S), (0.18f + v * 0.12f) * S, (0.14f + v * 0.1f) * S,
                        Fade(Pale, 0.6f * Mathf.Sin(v * Mathf.PI)), puff, Y + 0.03f);
                }
                WoodDust(box.DoorScreen, s - NezukoBoxComeOutTiming.Open0, 0.5f, 0.5f, 120, S);
            }
        }

        /// <summary>The cell a calm exit lands on in the previews: just outside the door.</summary>
        public static Vector2 OutsideDoor(Vector2 feet, float facing, float scale = 1f)
        {
            float a = facing * Mathf.Deg2Rad;
            return feet + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ((Back - HalfDepth - 0.9f) * scale);
        }
    }
}
