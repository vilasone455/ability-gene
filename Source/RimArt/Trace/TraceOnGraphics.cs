using System;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.TraceHandGraphics;
using T = RimArt.TraceOnTiming;

namespace RimArt
{
    /// <summary>
    /// Trace On's picture (Tools/VfxLab/web/sketches/trace-on.js): a circuit line runs from the chest to the hand,
    /// a wire outline of the weapon draws out of the hand from the grip to the point with its centre line and four
    /// cross lines, steel fills it from the grip behind a bright line, a glint at the point, and the wire fades.
    /// A real weapon slides to the hip first; a held copy breaks into light; a copy that leaves the hand turns in
    /// the air and breaks before it lands.
    ///
    /// The preview plays the sketch's four scenarios with its stand-in carry pose and draws the finished copy
    /// itself. In game (Trace/Kit/TraceHands.cs) the poses are the planes the game draws the weapons on and the
    /// finished copy is the game's own weapon.
    /// </summary>
    internal static class TraceOnGraphics
    {
        private static readonly Color Dust = new Color(0.52f, 0.45f, 0.37f);

        /// <summary>
        /// A copy tracing into the hand: the arm line, the flash at the hand, the wire and its structure lines, the
        /// steel, the glint at the point. <paramref name="plain"/>: draw the finished weapon too (the lab, or a game
        /// frame drawn before the fire tick put the real copy in the hand). <paramref name="altitude"/> is the
        /// weapon's.
        /// </summary>
        internal static void TraceIn(string key, TraceShape shape, UbwPose b, float s, TraceTimes times, TraceBody body, Vector2 hand,
            float altitude, bool plain, bool shadow)
        {
            if (s < times.At) return;
            float fade = 1f - Smooth((s - times.Lit) / T.WireFade);
            ArmLine(key, body, Mathf.Clamp01((s - times.At) / (times.Wire - times.At)), fade);
            if (s < times.Wire) return;
            float flash = (s - times.Wire) / T.Flash;
            if (flash < 1f) Sprite(hand, 0.4f, 0.4f, Fade(Trace, 0.55f * (1f - flash)), glow, altitude + 0.007f);
            if (fade > 0f) Wire(key, shape, b, Mathf.Clamp01((s - times.Wire) / (times.Wired - times.Wire)), T.Bright * fade, altitude);
            float ff = Mathf.Clamp01((s - times.Fill) / (times.Lit - times.Fill));
            if (ff >= 1f)
            {
                if (plain) Plain(key + " steel", shape, b, altitude, shadow);
            }
            else if (ff > 0f) Fill(key, shape, b, ff, altitude, shadow);
            float g = s - times.Lit;
            if (g >= 0f && g < T.Glint) GokuGraphics.Glint(Tip(b), 0.3f, 1f - g / T.Glint, TraceHot);
        }

        /// <summary>A real weapon going to the inventory: it slides from the hand to the hip and is gone (u 0..1).</summary>
        internal static void Stowing(string key, TraceShape shape, UbwPose held, UbwPose hip, float u, float altitude, bool shadow)
        {
            if (u >= 1f) return;
            UbwPose b = u > 0f ? UbwBlade.Blend(held, hip, u) : held;
            Plain(key, shape, b, altitude, shadow, (1f - u) * (1f - u));
        }

        /// <summary>
        /// Where a stowed weapon ends: its point at <paramref name="hip"/> (a 3D point), turned to point the other
        /// way from <paramref name="angle"/>, flat, at the held pose's size.
        /// </summary>
        internal static UbwPose Hip(UbwPose held, UbwV3 hip, double angle)
        {
            double d = (angle + 180) * UbwBlade.D2R;
            return UbwBlade.Flying(held.W, held.Scale / held.W.Image, hip, new UbwXZ(Math.Cos(d), Math.Sin(d)), 0);
        }

        /// <summary>The direction the point of a pose shows, in degrees (0 east, 90 north).</summary>
        internal static double Angle(UbwPose b) => Math.Atan2(-b.A.Z, -b.A.X) / UbwBlade.D2R;

        /// <summary>
        /// A copy that left the hand, <paramref name="age"/> s ago: it turns in the air, drifting to the weapon's
        /// side, and breaks into light before it reaches the ground; a puff of dust at <paramref name="dust"/>.
        /// </summary>
        internal static void Falling(string key, TraceShape shape, UbwPose start, float age, float side, Vector2 dust, float altitude, bool shadow)
        {
            UbwWeapon w = start.W;
            float fly = Mathf.Min(age, T.Fall);
            Vector3 arc = T.FallArc(fly);
            UbwV3 m0 = UbwBlade.At3(start, new UbwUV((w.TipU + w.PommelU) / 2, (w.TipV + w.PommelV) / 2));
            double a = (Angle(start) + side * arc.z) * UbwBlade.D2R;
            UbwPose b = UbwBlade.FlatAt(w, start.Scale / w.Image, new UbwV3(m0.X + side * arc.x, m0.Y + arc.y, m0.Z), new UbwXZ(Math.Cos(a), Math.Sin(a)));
            if (age < T.Fall) Plain(key + " falling", shape, b, altitude, shadow);
            else Shatter(key, shape, b, (age - T.Fall) / T.Break, 11, altitude);
            float u = age / T.Dust;
            if (u < 1f)
                Sprite(new Vector2(dust.x + 0.2f, dust.y + 0.12f + u * 0.1f), 0.5f + u * 0.5f, 0.3f + u * 0.3f, Fade(Dust, 0.4f * (1f - u)), soft, Overhead + 0.005f);
        }

        // ---- the preview ---------------------------------------------------------------------------------------

        /// <summary>
        /// The sketch at <paramref name="s"/> seconds, its stand-in pawn drawn at 2 cells north of <paramref name="cell"/>
        /// facing east, tracing a longsword; the second weapon (the swap's and the real one) is a spear. Stand-ins are
        /// not drawn.
        /// </summary>
        internal static void DrawPreview(Vector3 cell, float s, TraceOnScenario scenario)
        {
            if (s < 0f || s >= T.Duration(scenario)) return;
            var me = new Vector2(cell.x, cell.z + 2f);
            const float side = 1f;
            const double rest = T.RestEast;
            float altitude = PawnLayer + 0.012f, y = Overhead + 0.01f;
            TraceShape w = Shape("LongSword"), other = Shape("Spear");
            var pos = new UbwXZ(me.x, me.y);
            UbwPose first = UbwBlade.HeldCopy(w.W, T.Size, pos, rest, side);
            TraceBody body = TraceBody.Lab(me, side);
            Vector2 hand = Screen(new UbwV3(me.x + 0.24f * side, 0.3, me.y + 0.02f));

            if (scenario == TraceOnScenario.RealWeapon && s < T.CastAt + T.StowTime)
            {
                UbwPose held = UbwBlade.HeldCopy(other.W, T.Size, pos, rest, side);
                float u = Smooth(Mathf.Clamp01((s - T.CastAt) / T.StowTime));
                Stowing("trace on real", other, held, Hip(held, new UbwV3(me.x + 0.1f * side, 0.12, me.y - 0.04f), rest), u, altitude, true);
            }

            if (scenario != TraceOnScenario.Downed || s < T.SwapAt)
            {
                if (scenario != TraceOnScenario.SwapCopy || s < T.SwapAt)
                    TraceIn("trace on a", w, first, s, T.First(scenario), body, hand, altitude, true, true);
                else
                {
                    Shatter("trace on a", w, first, (s - T.SwapAt) / T.Break, 11, altitude);
                    TraceIn("trace on b", other, UbwBlade.HeldCopy(other.W, T.Size, pos, rest, side), s, T.Second, body, hand, altitude, true, true);
                }
            }
            else Falling("trace on a", w, first, s - T.SwapAt, side, me, y, true);
        }
    }
}
