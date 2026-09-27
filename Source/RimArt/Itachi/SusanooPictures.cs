using UnityEngine;
using static RimArt.SusanooDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The sketch's beats (itachi-susanoo.js), in seconds. Constants are its default values; the
    /// preview's scripted attackers use the block and seal beats, the game uses the seal beats from
    /// the click and the end beats from the hediff's removal.
    /// </summary>
    public static class SusanooTiming
    {
        // raise: Itachi alone, then the warm-up (growth as shares of it), then idle.
        public const float Lead = .25f, WarmUp = 1f, Idle = 1.8f;
        public const float EdgeRadius = 1.6f;
        // block (preview script): a shooter aims at Aim and the mirror turns over Face; three rounds
        // at 30 cells/s from 9 cells; a sword raider runs in and its blow lands at Blow.
        public const float Aim = .35f, Face = .2f, ShooterDist = 9f, RoundSpeed = 30f;
        public static readonly float[] Shots = { .5f, .7f, .9f };
        public const float Run = 1.2f, RunTime = .8f, MeleeFrom = 6f, MeleeStop = 1.95f, WindUp = .12f, Strike = .08f;
        public const float Cross0 = 1.88f, Cross1 = 2.1f, Stagger = .35f, Blow = Run + RunTime + WindUp + Strike, Settle0 = 2.7f, Settle1 = 3.0f, BlockLength = 3.6f;
        // In game the mirror holds its guard this long after the last hit, then goes back to rest over Settle.
        public const float GuardHold = .6f, Settle = .3f;
        // seal: the swing starts at SwingAt; Stab (swing and stab) and Pull (pull-in) as the sketch's params.
        public const float SwingAt = .5f, Stab = .45f, Pull = .6f, PierceHold = .15f, ReturnTime = .4f, RegrowTime = .4f, SealRange = 4f;
        // a hit (target not weak): the blade pulls back, the raider is knocked back and aims again.
        public const float RetractTime = .25f, KnockBack = .25f, Recoil = 40f;
        // end: idle, dim, then the break-apart (its length is BreakUp), then the tail.
        public const float EndIdle = .5f, DimTime = .3f, BreakAt = EndIdle + DimTime, BreakUp = 1f, EndTail = 1f;

        public struct Seal
        {
            public float windEnd, strikeEnd, pierce, pullFrom, retracted, sealedAt, armFrom, end;
        }

        /// <summary>The seal beats from the swing's start; a hit has retracted instead of sealedAt.</summary>
        public static Seal SealTimes(bool weak, float stab = Stab, float pull = Pull)
        {
            var T = new Seal { windEnd = SwingAt + stab * .35f, strikeEnd = SwingAt + stab * .65f, pierce = SwingAt + stab };
            T.pullFrom = T.pierce + PierceHold;
            if (!weak)
            {
                T.retracted = T.pullFrom + RetractTime;
                T.armFrom = T.retracted;
                T.end = T.retracted + ReturnTime + 1.2f;
                return T;
            }
            T.sealedAt = T.pullFrom + pull;
            T.armFrom = T.sealedAt + .1f;
            T.end = T.sealedAt + 1.3f;
            return T;
        }

        public struct End
        {
            public float hunch, cough0, cough1, end;
        }

        public static End EndTimes(float breakUp = BreakUp) => new End
        {
            hunch = BreakAt + .6f * breakUp, cough0 = BreakAt + .7f * breakUp, cough1 = BreakAt + .95f * breakUp, end = BreakAt + breakUp + EndTail,
        };

        public static float RaiseLength(float warmUp = WarmUp) => Lead + warmUp + Idle;

        /// <summary>The seal beat's stab, in ticks from the click: when the blade reaches the target.</summary>
        public static int PierceTicks => Mathf.RoundToInt(Stab * 60f);
    }

    /// <summary>
    /// The Yata Mirror's guard: it goes from rest to the point GuardR from the chest toward an
    /// attacker over Face, holds for GuardHold after the last hit, and goes back over Settle.
    /// Between two attackers it crosses through the lower half (in front of the chest), never
    /// over Itachi's head. Positions in design units.
    /// </summary>
    public sealed class SusanooMirrorGuard
    {
        private Vector2 from = SusanooGraphics.RestMirror, to = SusanooGraphics.RestMirror;
        private float fromDeg = float.NaN, toDeg = float.NaN;
        private float moveStart = -1f, moveTime = SusanooTiming.Face, holdUntil = -1f;
        private bool returning;

        public bool AtRest => holdUntil < 0f && !returning;

        /// <summary>Guard toward screen angle deg (from the chest, 0 = east) from time t.</summary>
        public void Aim(float deg, float t)
        {
            Vector2 now = At(t, out float nowDeg);
            from = now;
            fromDeg = nowDeg;
            toDeg = float.IsNaN(nowDeg) ? deg : Cross(nowDeg, deg);
            to = SusanooGraphics.MirrorAt(toDeg);
            moveStart = t;
            moveTime = SusanooTiming.Face;
            holdUntil = t + SusanooTiming.GuardHold;
            returning = false;
        }

        /// <summary>The mirror's position at t, and the guard angle it is at (NaN while at or going to rest).</summary>
        public Vector2 At(float t, out float deg)
        {
            deg = float.NaN;
            if (moveStart < 0f) return SusanooGraphics.RestMirror;
            if (!returning && holdUntil >= 0f && t >= holdUntil)
            {
                from = Position(holdUntil, out _);
                fromDeg = float.NaN;
                to = SusanooGraphics.RestMirror;
                toDeg = float.NaN;
                moveStart = holdUntil;
                moveTime = SusanooTiming.Settle;
                holdUntil = -1f;
                returning = true;
            }
            Vector2 p = Position(t, out deg);
            if (returning && t >= moveStart + moveTime) { returning = false; moveStart = -1f; deg = float.NaN; return SusanooGraphics.RestMirror; }
            return p;
        }

        private Vector2 Position(float t, out float deg)
        {
            float f = Smooth((t - moveStart) / moveTime);
            if (!float.IsNaN(fromDeg) && !float.IsNaN(toDeg))
            {
                deg = Mathf.Lerp(fromDeg, toDeg, f);
                return SusanooGraphics.MirrorAt(deg);
            }
            deg = f >= 1f ? toDeg : float.NaN;
            return Vector2.Lerp(from, to, f);
        }

        /// <summary>The angle equal to b (mod 360) that is reached from a through the lower half.</summary>
        public static float Cross(float a, float b)
        {
            float best = b, bestDist = float.MaxValue;
            for (int k = -1; k <= 1; k++)
            {
                float v = b + k * 360f, dist = Mathf.Abs(v - a);
                if (Mathf.Sin((a + v) / 2f * Mathf.Deg2Rad) < 0f && dist < bestDist) { best = v; bestDist = dist; }
            }
            if (bestDist == float.MaxValue)
            {
                for (int k = -1; k <= 1; k++)
                {
                    float v = b + k * 360f, dist = Mathf.Abs(v - a);
                    if (dist < bestDist) { best = v; bestDist = dist; }
                }
            }
            return best;
        }
    }

    /// <summary>
    /// The Totsuka's stab at one target, from the click: wind-up, swing, the blade shot out to the
    /// target's chest, the pierce, then either the pull into the gourd and the seal (weak) or a
    /// hit that pulls back out. Moves the sword arm in a pose and draws the effects on the target.
    /// Positions in map cells; the target is where it stood at the click.
    /// </summary>
    public sealed class SusanooSword
    {
        public readonly float start;
        public readonly Vector2 origin, pos, chest, outDir;
        public readonly bool weak;
        public readonly SusanooTiming.Seal T;

        public SusanooSword(float start, Vector2 origin, Vector2 targetPos, bool weak)
        {
            this.start = start;
            this.origin = origin;
            pos = targetPos;
            chest = new Vector2(pos.x, pos.y + .06f);
            outDir = Unit(origin, pos);
            this.weak = weak;
            T = SusanooTiming.SealTimes(weak);
        }

        public bool Done(float t) => t - start >= T.end;

        /// <summary>Moves the sword arm and the blade in <paramref name="pose"/> for time t, then draws the target's effects.</summary>
        public void Apply(float t, float s, in SusanooFrame F, in SusanooLook look, SusanooPose pose, bool drawSpots)
        {
            float lt = t - start;
            Vector2 reachHand = SusanooGraphics.HandToward(F, chest);
            var away = new Vector2(SusanooGraphics.RestHand.x - (reachHand.x - SusanooGraphics.RestHand.x) * .25f, SusanooGraphics.RestHand.y + .45f);
            float strikeDeg = Deg(F.At(reachHand), chest);
            float windDeg = SusanooGraphics.RestBladeDeg + (strikeDeg > 90f || strikeDeg < -90f ? -25f : 25f);
            Vector2 hand = SusanooGraphics.RestHand;
            float bladeDeg = SusanooGraphics.RestBladeDeg, elbowDown = 1f;
            if (lt >= SusanooTiming.SwingAt && lt < T.windEnd)
            {
                float f = Smooth((lt - SusanooTiming.SwingAt) / (T.windEnd - SusanooTiming.SwingAt));
                hand = Vector2.Lerp(SusanooGraphics.RestHand, away, f);
                bladeDeg = Mathf.Lerp(SusanooGraphics.RestBladeDeg, windDeg, f);
                elbowDown = 1f - f * .5f;
            }
            else if (lt >= T.windEnd && lt < T.armFrom)
            {
                float f = Smooth((lt - T.windEnd) / (T.strikeEnd - T.windEnd));
                hand = Vector2.Lerp(away, reachHand, f);
                bladeDeg = lt < T.strikeEnd ? Mathf.Lerp(windDeg, strikeDeg, f) : strikeDeg;
                elbowDown = .5f * (1f - f);
            }
            else if (lt >= T.armFrom)
            {
                float f = Smooth((lt - T.armFrom) / SusanooTiming.ReturnTime);
                hand = Vector2.Lerp(reachHand, SusanooGraphics.RestHand, f);
                bladeDeg = Mathf.Lerp(strikeDeg, SusanooGraphics.RestBladeDeg, f);
                elbowDown = f;
            }
            pose.hand = hand;
            pose.bladeDeg = bladeDeg;
            pose.elbowDown = elbowDown;
            Vector2 mouth = SusanooGraphics.GourdMouth(F, s, pose);

            // The blade: idle, tilted in the wind-up, then shot out to the chest.
            float sd = strikeDeg * Mathf.Deg2Rad;
            if (lt >= T.strikeEnd && lt < T.pullFrom)
            {
                float f = Smooth((lt - T.strikeEnd) / (T.pierce - T.strikeEnd));
                var idleTip = new Vector2(mouth.x + Mathf.Cos(sd) * look.bladeLen * .5f, mouth.y + Mathf.Sin(sd) * look.bladeLen * .5f);
                pose.hasBladeTip = true;
                pose.bladeTip = Vector2.Lerp(idleTip, chest, f);
                pose.stab = f;
            }
            if (!weak)
            {
                Hit(lt, s, F, look, pose, mouth, strikeDeg, drawSpots);
                return;
            }
            // Holding the target while it is pulled in, then gone into the gourd and pouring out again.
            float pullU = Clamp((lt - T.pullFrom) / SusanooTiming.Pull), pullE = pullU * pullU;
            Vector2 pulledAt = Vector2.Lerp(chest, mouth, pullE);
            if (lt >= T.pullFrom && lt < T.sealedAt)
            {
                pose.hasBladeTip = true;
                pose.bladeTip = pulledAt;
                pose.stab = 1f;
            }
            if (lt >= T.sealedAt) pose.bladeLen = look.bladeLen * Clamp((lt - T.sealedAt - .2f) / SusanooTiming.RegrowTime);
            if (lt >= T.sealedAt && lt < T.sealedAt + .2f) pose.bladeLen = 0f;

            // The target: burns at the pierce, then is pulled in; the seal closes at the gourd's mouth.
            if (lt >= T.pierce && lt < T.pullFrom + .08f)
                SusanooGraphics.FlameWrap(pos, s, Mathf.Min(1f, (lt - T.pierce) / .08f) * (1f - Clamp((lt - T.pullFrom) / .08f)));
            if (lt >= T.pierce && lt < T.pierce + .15f) SusanooGraphics.Glint(chest, .7f, 1f - (lt - T.pierce) / .15f, EyeHot);
            if (lt >= T.pullFrom && lt < T.sealedAt) SusanooGraphics.Pulled(pulledAt, Deg(chest, mouth), pullU, 1f - pullU * .4f, s);
            if (lt >= T.sealedAt) SusanooGraphics.SealFlash(mouth, lt - T.sealedAt);
        }

        // The stab on a target that is not weak: the blade pulls back out to half length and pours
        // out again; the target is knocked back and bleeds out of its back.
        private void Hit(float lt, float s, in SusanooFrame F, in SusanooLook look, SusanooPose pose, Vector2 mouth, float strikeDeg, bool drawSpots)
        {
            float sd = strikeDeg * Mathf.Deg2Rad;
            var half = new Vector2(mouth.x + Mathf.Cos(sd) * look.bladeLen * .5f * F.kx, mouth.y + Mathf.Sin(sd) * look.bladeLen * .5f * F.kz);
            float knock = Smooth((lt - T.pierce) / .2f) * SusanooTiming.KnockBack;
            Vector2 at = pos + outDir * knock, hitChest = chest + outDir * knock;
            if (lt >= T.pullFrom && lt < T.retracted)
            {
                float f = Smooth((lt - T.pullFrom) / SusanooTiming.RetractTime);
                pose.hasBladeTip = true;
                pose.bladeTip = Vector2.Lerp(hitChest, half, f);
                pose.stab = 1f - f;
            }
            else if (lt >= T.pierce && lt < T.pullFrom)
            {
                pose.hasBladeTip = true;
                pose.bladeTip = hitChest;
            }
            if (lt >= T.retracted) pose.bladeLen = look.bladeLen * Mathf.Lerp(.5f, 1f, Smooth((lt - T.retracted) / SusanooTiming.RegrowTime));
            if (lt >= T.pierce && lt < T.pierce + .12f) SusanooGraphics.Glint(hitChest, .45f, 1f - (lt - T.pierce) / .12f, EyeHot);
            SusanooBlood.Spurt(lt - T.pierce, hitChest, at, outDir, 11, 40, drawSpots);
        }
    }

    /// <summary>
    /// Blood as the sketch draws it: droplets in flight that leave a spot where they land. In game
    /// the spots are real filth (MapComponent_Susanoo), so drawSpots is false there.
    /// </summary>
    public static class SusanooBlood
    {
        /// <summary>Blood out of a pawn's back: n droplets leave the chest, arc and land 0.3-0.9 cells out along dir from its feet.</summary>
        public static void Spurt(float age, Vector2 from, Vector2 pos, Vector2 dir, int n, int seed, bool drawSpots)
        {
            if (age < 0f) return;
            var feet = new Vector2(pos.x, pos.y - .33f);
            for (int i = 0; i < n; i++)
            {
                int sd = seed * 50 + i * 7;
                float d = .3f + .6f * Rand(sd + 1), side = (Rand(sd + 2) - .5f) * .5f;
                var land = new Vector2(feet.x + dir.x * d - dir.y * side, feet.y + dir.y * d * .8f + dir.x * side * .8f);
                float fly = .16f + .14f * Rand(sd + 3), u = Mathf.Min(1f, age / fly), arc = .22f * Mathf.Sin(u * Mathf.PI);
                float size = .025f + .015f * Rand(sd + 4), grow = Mathf.Min(1f, (age - fly) / .1f + .5f);
                if (u < 1f) Blob(new Vector2(Mathf.Lerp(from.x, land.x, u), Mathf.Lerp(from.y, land.y, u) + arc), size, size, Blood, Front + .071f);
                else if (drawSpots) Blob(land, (.035f + .035f * Rand(sd + 5)) * grow, (.025f + .025f * Rand(sd + 5)) * grow, A(Blood, .9f), Floor + .004f + i * .0001f);
            }
        }

        /// <summary>Where the spurt's droplet i lands, for the game's filth.</summary>
        public static Vector2 SpurtLanding(Vector2 pos, Vector2 dir, int i, int seed)
        {
            int sd = seed * 50 + i * 7;
            float d = .3f + .6f * Rand(sd + 1), side = (Rand(sd + 2) - .5f) * .5f;
            var feet = new Vector2(pos.x, pos.y - .33f);
            return new Vector2(feet.x + dir.x * d - dir.y * side, feet.y + dir.y * d * .8f + dir.x * side * .8f);
        }

        /// <summary>
        /// Blood from one of Itachi's coughs: droplets leave the mouth in an arc and land in front of
        /// him (south); a dark puff at the mouth. age since the cough, j the cough's index.
        /// </summary>
        public static void Cough(Vector2 origin, Vector2 head, float age, int j, bool drawSpots)
        {
            if (age < 0f) return;
            var mouth = new Vector2(head.x, head.y - .1f);
            if (age < .2f) Sprite(mouth, .2f + age * .8f, .16f + age * .5f, A(Blood, .6f * (1f - age / .2f)), glow, Front + .07f);
            for (int i = 0; i < 9; i++)
            {
                int sd = j * 100 + i * 7 + 900;
                var land = new Vector2(origin.x + (Rand(sd + 1) - .5f) * .5f, origin.y - .38f - Rand(sd + 2) * .45f);
                float fly = .22f + .14f * Rand(sd + 3), u = Mathf.Min(1f, age / fly), arc = .25f * Mathf.Sin(u * Mathf.PI);
                var p = new Vector2(Mathf.Lerp(mouth.x, land.x, u), Mathf.Lerp(mouth.y, land.y, u) + arc);
                float size = .025f + .015f * Rand(sd + 4), grow = Mathf.Min(1f, (age - fly) / .1f + .5f);
                if (u < 1f) Blob(p, size, size, Blood, Front + .071f);
                else if (drawSpots) Blob(land, (.035f + .035f * Rand(sd + 5)) * grow, (.025f + .025f * Rand(sd + 5)) * grow, A(Blood, .9f), Floor + .004f + i * .0001f);
            }
        }
    }
}
