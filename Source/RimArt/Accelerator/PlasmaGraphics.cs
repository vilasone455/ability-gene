using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.AcceleratorGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.Plasma;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>A round caught by the channel: where it crossed the pull ring (a ground point) and when, on the picture's clock.</summary>
    public struct PlasmaCatch
    {
        public Vector2 Entry;
        public float At;

        public PlasmaCatch(Vector2 entry, float at)
        {
            Entry = entry;
            At = at;
        }
    }

    /// <summary>What one Plasma looks like now. Points are ground points on the map.</summary>
    public struct PlasmaShot
    {
        /// <summary>The caster's position (the pawn's DrawPos in game) and the aim in degrees, 0 east, 90 north.</summary>
        public Vector2 Feet;
        public float Aim;
        /// <summary>Lane length and width, burst radius, pull radius (cells), ball across when compressed, channel and flight over the full lane (s).</summary>
        public float Length, Width, Radius, Pull, BallSize, Channel, Flight;
        /// <summary>Cells down the lane where the head stops and bursts: at the first pawn or wall it meets, or the lane's end.</summary>
        public float Stop;
        /// <summary>A wall stopped it at <see cref="WallAt"/> cells: the lane is drawn to half a cell short of it, the wall is scorched and no fire is drawn behind it.</summary>
        public bool Walled;
        public float WallAt;
        /// <summary>The picture's clock, 0 at the start of the channel.</summary>
        public float Seconds;
        /// <summary>When the channel was broken (stun, downing, move), on the picture's clock; negative if it was not.</summary>
        public float Cancelled;
        /// <summary>Rounds caught during the channel, or null. Each is drawn from its entry on.</summary>
        public List<PlasmaCatch> Caught;
        /// <summary>The game started real fires: the drawn fire cells only flare and are gone about 1 s after the burst.</summary>
        public bool RealFires;
        /// <summary>The caster's sleeve and skin colours, for the drawn arm.</summary>
        public Color Sleeve, Skin;
    }

    /// <summary>
    /// Draws Plasma, the port of the lab's accelerator-plasma.js. The channel: the lane on the floor at
    /// its true width with a pulse running down it once a second, the pull ring, 30 wind lines
    /// spiralling in to the hand, loose dust and rocks sliding in along the floor and lifting into the
    /// hand, the arm, the ball compressing into plasma, caught rounds spiralling into the hand with a
    /// flash as each is absorbed. The release: a ring off the hand, dust blown back, the head down the
    /// lane with its tail, core and black speed lines and the floor lit under it. The burst: a flash,
    /// four additive dome layers and a ring on the true radius, a ring spreading on the floor, 14
    /// sparks thrown and falling, smoke, fires on the cells inside the radius, and a scorched circle
    /// that stays; soot on a wall that stopped it.
    ///
    /// The ball, the lane and the burst are level circles and flat shapes at chest height, so they turn
    /// with the aim and there is no per-facing method.
    ///
    /// Not drawn (the sketch's stand-ins, or the game's): the pawns, their white tint and lying down,
    /// the caster's lunge and slide (the real pawn does not move), the wall, rounds before they enter
    /// the pull ring and rounds that hit, muzzle flashes, and the fire on the pawn that is hit. The loose
    /// things sliding in are picture only: real items do not move.
    /// </summary>
    public static class PlasmaGraphics
    {
        private const float Tau = 6.2831855f;

        public static void Draw(in PlasmaShot shot, Map map)
        {
            float s = shot.Seconds, channel = shot.Channel, len = shot.Length, W = shot.Width, R = shot.Radius, stop = shot.Stop;
            float hit = T.HitAt(channel, shot.Flight, stop, len);
            // A channel broken before release fades out over CancelFade and nothing after it is drawn.
            bool cancelled = shot.Cancelled >= 0f && shot.Cancelled < channel;
            if (s < 0f || s >= hit + T.Tail || (cancelled && s >= shot.Cancelled + T.CancelFade) || !Shown(shot.Feet, map)) return;
            Begin(shot.Feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            Vector2 feet = shot.Feet, toward = Turn(shot.Aim);
            float aim = shot.Aim, chest = GokuGraphics.ChestOn, lift = GokuGraphics.Lift;
            Vector2 Place(float along, float across = 0f) => GokuTiming.Place(feet, toward, along, across);
            Vector2 Up(Vector2 ground) => new Vector2(ground.x, ground.y + chest);
            // The sketch's clock (channel at Lead), for flicker and spin only, so the preview draws the sketch's frames.
            float wave = s + T.Lead;
            float fade = cancelled ? 1f - Mathf.Clamp01((s - shot.Cancelled) / T.CancelFade) : 1f;
            bool channelling = cancelled || s < channel;
            float fired = cancelled ? -1f : s - channel, age = cancelled ? -1f : s - hit;
            bool flying = fired >= 0f && s < hit;
            float charge = T.Charge(cancelled ? Mathf.Min(s, shot.Cancelled) : s, channel), grown = Smooth(charge * 2f), stage = Smooth(charge);
            float reach = fired >= 0f ? T.Reach(fired, stop, shot.Flight, len) : 0f;
            Vector2 end = Place(stop), hand0 = feet + toward * T.HandOut, hand = Up(hand0);

            // --- caught rounds: pale, spiralling into the hand over BendTime, then absorbed -------------------------
            float absorbing = 0f;
            List<PlasmaCatch> caught = shot.Caught;
            if (caught != null)
                for (int i = 0; i < caught.Count; i++)
                {
                    float since = s - caught[i].At;
                    if (since < 0f) continue;
                    if (since >= T.BendTime)
                    {
                        absorbing += Mathf.Max(0f, 1f - (since - T.BendTime) / T.Absorb);
                        continue;
                    }
                    // An inward spiral round the hand: the radius shrinks from the ring to nothing while the angle
                    // advances, slowly at first and faster near the centre, and the round rises to the hand.
                    Vector2 e = caught[i].Entry;
                    float th0 = Mathf.Atan2(e.y - hand0.y, e.x - hand0.x), r0 = Vector2.Distance(e, hand0), u = since / T.BendTime;
                    Vector2 at = Spiral(hand0, th0, r0, u, chest), step = at - Spiral(hand0, th0, r0, Mathf.Max(0f, u - 0.02f), chest);
                    float stepLen = step.magnitude;
                    if (stepLen == 0f) stepLen = 1f;
                    Streak(at - step / stepLen * 0.55f, at, 0.09f, Fade(Air, 0.95f * fade), whiteGlow, Overhead + 0.15f, 3);
                    Sprite(at, 0.16f, 0.16f, Fade(White, 0.9f * fade), glow, Overhead + 0.151f);
                }
            if (absorbing > 0f)
            {
                Sprite(hand, 0.6f + 0.5f * absorbing, 0.6f + 0.5f * absorbing, Fade(White, 0.8f * absorbing * fade), glow, Overhead + 0.125f);
                PaperBombGraphics.RingAt(hand, 0.3f + (1f - absorbing) * 0.5f, Fade(Air, 0.8f * absorbing * fade), Overhead + 0.124f, false, whiteGlow);
            }

            // --- floor: the lane, the light under the head, the scorch after ------------------------------------------
            if (s < hit + 0.4f)
            {
                float show = Smooth(s / 0.3f) * (1f - Mathf.Clamp01((s - hit) / 0.4f)) * fade, laneEnd = shot.Walled ? shot.WallAt - 0.5f : len;
                float pulse = channelling ? 0.3f + 0.35f * (1f - Mathf.Clamp01(s % T.LanePulse / 0.5f)) : 0.6f;
                for (int side = -1; side <= 1; side += 2)
                    Streak(Place(0.6f, side * W / 2f), Place(laneEnd, side * W / 2f), 0.06f, Fade(Air, pulse * show), solid, Floor + 0.02f, 2);
                Sprite(Place(laneEnd / 2f), laneEnd, W, Fade(AirDeep, 0.08f * show), glow, Floor + 0.006f, -aim);
                if (channelling)
                {
                    float run = s % T.LanePulse / 0.45f;
                    if (run < 1f)
                    {
                        float d = laneEnd * run, f = Mathf.Sin(run * Mathf.PI);
                        Streak(Place(d, -W / 2f), Place(d, W / 2f), 0.25f, Fade(Air, 0.8f * f * show), whiteGlow, Floor + 0.025f, 4);
                    }
                }
            }
            if (flying) Sprite(Place(Mathf.Max(0f, reach - 1.5f)), 4f, W * 2.5f, Fade(PlasmaDeep, 0.35f), glow, Floor + 0.012f, -aim);
            if (age >= 0f)
            {
                float burnt = Smooth(age / 0.3f) * (1f - 0.25f * Smooth(age / T.Tail));
                Sprite(end, R * 2.3f, R * 2.3f, Fade(Ink, 0.55f * burnt), soft, Floor + 0.011f);
                PaperBombGraphics.RingAt(end, R * 0.97f, Fade(Ink, 0.45f * burnt), Floor + 0.012f, true);
                if (shot.Walled) Sprite(Place(shot.WallAt), 1.3f, 1.3f, Fade(Ink, 0.6f * Smooth(age / 0.3f)), soft, Overhead + 0.001f);
            }

            // --- the arm: out while channelling, thrust on release, back in by 0.5 s after -----------------------------
            float armReach = cancelled ? T.ArmChannel * Smooth(s / 0.3f) : T.ArmReach(s, channel);
            Arm(feet, aim, armReach, chest, shot.Sleeve, shot.Skin, fade);

            // --- the channel: the pull ring, wind into the hand, loose things sliding in, the ball ---------------------
            if (channelling)
            {
                PaperBombGraphics.RingAt(feet, shot.Pull, Fade(Air, (0.25f + 0.3f * absorbing) * grown * (0.8f + 0.2f * Mathf.Sin(wave * 6f)) * fade), Floor + 0.02f);
                for (int i = 0; i < T.WindLines; i++)
                {
                    float v = (s * T.WindSpeed * (0.8f + 0.4f * Rand(i + 3)) + Rand(i)) % 1f, ang0 = Rand(i + 11) * Tau, turn = 1f + 0.6f * Rand(i + 17);
                    Vector2[] pts = GokuGraphics.Points(10);
                    for (int k = 0; k <= 9; k++)
                    {
                        float u = Mathf.Clamp01(v - T.SegLen + k / 9f * T.SegLen), r = shot.Pull * Mathf.Pow(1f - u, 1.15f) + 0.2f, ang = ang0 + u * turn;
                        pts[k] = new Vector2(hand0.x + Mathf.Cos(ang) * r, hand0.y + Mathf.Sin(ang) * r + chest * Smooth((u - 0.6f) / 0.4f));
                    }
                    float alpha = 0.55f * grown * (0.3f + 0.7f * v) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Min(1f, v) * Mathf.PI)), 0.5f);
                    GokuGraphics.Line(pts, 0.05f, Fade(Air, alpha * fade), whiteGlow, Overhead + 0.02f, Taper.Both);
                }
                // Loose things on the floor slide in and lift into the hand at the end: every third a rock, the rest dust.
                for (int i = 0; i < T.Loose; i++)
                {
                    float v = (s * 0.3f * (0.8f + 0.4f * Rand(i + 2)) + Rand(i)) % 1f, r = shot.Pull * (1f - v) + 0.3f, ang = Rand(i + 5) * Tau + v * 1.4f;
                    float h = chest * Smooth((v - 0.75f) / 0.25f), show = Mathf.Sin(v * Mathf.PI) * grown * fade;
                    var ground = new Vector2(hand0.x + Mathf.Cos(ang) * r, hand0.y + Mathf.Sin(ang) * r);
                    if (i % 3 == 0)
                    {
                        Sprite(ground + sun * h, 0.18f, 0.1f, Fade(Ink, 0.3f * show), soft, Floor + 0.05f);
                        PaperBombGraphics.Rock(new Vector2(ground.x, ground.y + h), 0.11f, i * 40f + s * 90f, show, i, Overhead + 0.004f);
                    }
                    else Sprite(new Vector2(ground.x, ground.y + h), 0.35f + 0.25f * (1f - v), 0.28f + 0.2f * (1f - v), Fade(GokuGraphics.Dust, 0.35f * show), soft, Floor + 0.04f);
                }
                PlasmaBall(hand, shot.BallSize * (1f + T.Swell * absorbing), wave, grown * fade, stage);
            }

            // --- release: the ring off the hand, dust blown back, the head down the lane -------------------------------
            if (fired >= 0f && fired < 0.22f)
                PaperBombGraphics.RingAt(hand, 0.25f + fired * 5f, Fade(White, 0.8f * (1f - fired / 0.22f)), Overhead + 0.09f, false, whiteGlow);
            if (fired >= 0f && fired < 0.8f)
                for (int i = 0; i < T.BackDust; i++)
                {
                    float u = Mathf.Clamp01((fired - Rand(i) * 0.1f) / 0.7f), d = 0.3f + u * (1.5f + 2f * Rand(i + 4)), across = (Rand(i + 8) - 0.5f) * 1.4f;
                    Sprite(Place(-d, across), 0.35f + u * 0.5f, 0.3f + u * 0.4f, Fade(GokuGraphics.Dust, 0.45f * Mathf.Sin(u * Mathf.PI)), soft, Floor + 0.04f);
                }
            if (flying)
            {
                Vector2 head = Up(Place(reach));
                Streak(Up(Place(Mathf.Max(0.4f, reach - 5f))), head, 0.55f, Fade(PlasmaDeep, 0.45f), whiteGlow, Overhead + 0.1f, 6);
                Streak(Up(Place(Mathf.Max(0.4f, reach - 2.5f))), head, 0.2f, Fade(PlasmaHot, 0.95f), whiteGlow, Overhead + 0.101f, 6);
                for (int i = 0; i < T.SpeedLines; i++)                                                        // black speed lines
                {
                    float side = (i % 2 == 1 ? 1f : -1f) * (0.35f + 0.5f * Rand(i + 30)), from = Mathf.Max(0.4f, reach - 3f - 2f * Rand(i)), to = Mathf.Max(0.4f, reach - 0.6f - Rand(i + 3));
                    Streak(Up(Place(from, side)), Up(Place(to, side)), 0.06f, Fade(Ink, 0.7f), solid, Overhead + 0.09f, 3);
                }
                PlasmaBall(head, shot.BallSize, wave, 1f, 1f);
            }

            // --- the burst ---------------------------------------------------------------------------------------------
            if (age < 0f) return;
            if (age < T.FlashTime)
            {
                float k = 1f - age / T.FlashTime;
                Sprite(Up(end), 5f * k + 1f, 5f * k + 1f, Fade(White, 0.9f * k), glow, Overhead + 0.13f);
            }
            float open = Smooth(age / T.DomeOpen), dome = 1f - Smooth((age - 0.35f) / 0.6f);
            if (dome > 0f)
            {
                // Four additive layers out to the true radius, each smaller and whiter: soft light, not a solid.
                for (int lvl = 0; lvl < 4; lvl++)
                {
                    float rr = R * open * (1.15f - lvl * 0.22f);
                    Sprite(end, rr * 2.4f, rr * 2.4f, Fade(Color.Lerp(PlasmaDeep, White, lvl / 3f), (0.25f + 0.15f * lvl) * dome), glow, Overhead + 0.05f + lvl * 0.001f);
                }
                PaperBombGraphics.RingAt(end, R * open, Fade(PlasmaHot, 0.7f * dome), Overhead + 0.06f, false, whiteGlow);
            }
            if (age < 0.6f) PaperBombGraphics.RingAt(end, R * (1f + age * 3f), Fade(Air, 0.6f * (1f - age / 0.6f)), Floor + 0.022f);
            for (int i = 0; i < T.Sparks; i++)
            {
                float ang = i * Tau / T.Sparks + Rand(i + 3) * 0.4f, speed = 2.5f + 3f * Rand(i + 7), rise = 2f + 2.5f * Rand(i + 9), h = rise * age - 4.9f * age * age;
                if (h < 0f) continue;
                float d = speed * age;
                var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 up = new Vector2(0f, h * lift);
                Streak(end + way * (d - 0.25f) + up, end + way * d + up, 0.08f, Fade(White, 0.9f), whiteGlow, Overhead + 0.12f, 3);
            }
            for (int i = 0; i < T.SmokePuffs; i++)
            {
                float v = Mathf.Clamp01((age - 0.3f - i * 0.08f) / 2f);
                if (v <= 0f || v >= 1f) continue;
                float ang = Rand(i + 50) * Tau, d = Rand(i + 60) * R * 0.8f, h = v * (1f + 0.6f * Rand(i + 70));
                Sprite(new Vector2(end.x + Mathf.Cos(ang) * d + Mathf.Sin(v * 5f + i) * 0.1f, end.y + Mathf.Sin(ang) * d + h * lift), 0.6f + v * 1.1f, 0.5f + v,
                    Fade(FlameGauntletGraphics.Smoke, 0.55f * Mathf.Sin(v * Mathf.PI)), soft, Overhead + 0.14f);
            }
            // Fires on the cells inside the radius, none behind a wall. With real fires in game these only flare
            // and go by about 1 s, over the cells the game set alight; the scorch under each stays.
            float amount = Smooth(age / 0.25f) * (0.6f + 0.4f * (1f - Smooth(age / T.Tail))), flare = 1f - Smooth(age / 0.6f);
            float fireAlpha = shot.RealFires ? 1f - Smooth((age - T.FireFadeFrom) / T.FireFadeTime) : 1f;
            Vector2 fireAt = shot.RealFires ? new Vector2(Mathf.Floor(end.x) + 0.5f, Mathf.Floor(end.y) + 0.5f) : end;
            int span = Mathf.CeilToInt(R);
            for (int dx = -span; dx <= span; dx++)
                for (int dz = -span; dz <= span; dz++)
                {
                    if (Mathf.Sqrt(dx * dx + dz * dz) > R) continue;
                    if (shot.Walled && dx * toward.x + dz * toward.y > 0.5f) continue;
                    FlameGauntletGraphics.FireCell(new Vector2(fireAt.x + dx, fireAt.y + dz), wave, amount * (0.7f + 0.3f * Rand(dx * 7 + dz)), dx * 5 + dz + 3,
                        Overhead + 0.03f, flare, true, fireAlpha);
                }
        }

        /// <summary>A caught round <paramref name="q"/> (0 to 1) of the way along its spiral into the hand, as drawn.</summary>
        private static Vector2 Spiral(Vector2 hand0, float th0, float r0, float q, float chest)
        {
            float r = r0 * Mathf.Pow(1f - q, 1.3f), th = th0 + T.Orbits * Tau * Mathf.Pow(q, 1.6f);
            return new Vector2(hand0.x + Mathf.Cos(th) * r, hand0.y + Mathf.Sin(th) * r + chest * Smooth(q));
        }
    }
}
