using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.UbwGraphics;
using T = RimArt.UbwCommandTiming;

namespace RimArt
{
    /// <summary>
    /// What the commands' pictures are drawn with: the world's middle corner, its fixed low sun and shadow strength, the
    /// warm light the bake gives the field (<see cref="Tint"/>), and the weapon set.
    /// </summary>
    internal struct UbwCommandLook
    {
        public Vector2 O;
        public UbwXZ Sun;
        public float Strength;
        public Color Tint;
        public UbwWeaponSet Set;

        public static UbwCommandLook For(Vector2 o, Vector2 sun, float strength) => new UbwCommandLook
        {
            O = o, Sun = new UbwXZ(sun.x, sun.y), Strength = strength, Tint = Color.Lerp(White, UbwGraphics.Tint, (float)UbwField.Twilight), Set = UbwGraphics.Set,
        };

        /// <summary>A point in cells from the middle corner, on the map.</summary>
        public Vector2 Map(UbwXZ p) => new Vector2(O.x + (float)p.X, O.y + (float)p.Z);

        /// <summary>Where a 3D point is drawn on the map.</summary>
        public Vector2 Screen(UbwV3 p) => Map(UbwBlade.OnScreen(p));
    }

    /// <summary>
    /// The pictures of Unlimited Blade Works' commands, one sword at a time over the baked field (the port of the drawing in
    /// Tools/VfxLab/web/sketches/trace-ubw-world-commands.js): a sword pulled out of its hole and turning flat, hovering
    /// aimed with a glint at its point, flying point first, sticking in the ground leaning back in and quivering; Pin's
    /// swords driven in deep over the pawn; Draw's spinning flat with two fainter copies behind it; Arm's sliding up and
    /// arcing to the hand; Intercept's meeting the shot with a spark and breaking into light; the floor breaking where each
    /// leaves. Everything takes the world's warm light, as the bake does. Times and poses are <see cref="UbwCommandTiming"/>'s;
    /// the rules (Trace/Kit) say which sword, when and where.
    ///
    /// Heights: a sword in the air is drawn over everything on the ground (MoteOverhead), the south one over the north one; a
    /// sword stuck in the ground just over the baked field's rows; a pinning sword over the pawn it goes through.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwCommandGraphics
    {
        private static readonly Color DirtMid = new Color(0.36f, 0.27f, 0.18f), Dust = new Color(0.52f, 0.45f, 0.37f);
        private static readonly float StuckLayer = UbwLayers.Pocket.Blades + 0.045f, PinLayer = AltitudeLayer.Pawn.AltitudeFor() + 0.05f;

        private static float AirLayer(UbwPose b) => Overhead + 0.01f + Mathf.Clamp((40f - (float)UbwBlade.OnScreen(UbwBlade.Middle(b)).Z) * 0.0002f, 0f, 0.02f);

        // ---- one sword ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A blade (lib/trace.js blade): its shadow along the sun, and with <paramref name="upright"/> both edges, the face and
        /// the two dark bands low on it (as the bake draws a standing one), else the face alone (lying, flying or hovering).
        /// </summary>
        internal static void Blade(in UbwCommandLook k, string key, UbwPose b, float altitude, bool upright, float alpha = 1f)
        {
            if (b == null || alpha <= 0.002f) return;
            int row = b.W.Row;
            Builder shadows = Scratch(key + " shadow"), blades = Scratch(key + " blade");
            if (upright) UbwFieldBake.BladeInto(shadows, blades, b, k.Sun, row);
            else
            {
                List<UbwUV> above = UbwBlade.Clip(UbwBlade.Square, UbwBlade.HigherThan(b, 0));
                UbwFieldBake.PolyInto(shadows, above, b, k.Sun, new Cell(FaceCol[2], row));
                double lit = 0.8 + 0.2 * Math.Max(0, UbwV3.Dot(b.N, UbwV3.Unit(new UbwV3(-k.Sun.X, 1, -k.Sun.Z))));
                int face = 0;
                for (int i = 1; i < FaceLit.Length; i++)
                    if (Math.Abs(FaceLit[i] - lit) < Math.Abs(FaceLit[face] - lit)) face = i;
                UbwFieldBake.PolyInto(blades, above, b, null, new Cell(FaceCol[face], row));
            }
            Draw(shadows, k.O, UbwLayers.Pocket.FieldShadow, Fade(Black, 0.42f * k.Strength / 0.32f * alpha), k.Set.Atlas);
            Draw(blades, k.O, altitude, Fade(k.Tint, alpha), k.Set.Atlas);
        }

        /// <summary>The blade's outline in the trace colour (lib/trace.js blade with wireAlpha).</summary>
        internal static void Wire(in UbwCommandLook k, string key, UbwPose b, float alpha, float altitude)
        {
            if (alpha <= 0.002f) return;
            List<UbwUV> poly = UbwBlade.Clip(UbwBlade.Clip(UbwBlade.Square, UbwBlade.HigherThan(b, 0)), UbwBlade.LowerThan(b, 9));
            if (poly.Count < 3) return;
            Builder wire = Scratch(key + " wire");
            var pts = new List<Vector2>(poly.Count);
            var uvs = new List<Vector2>(poly.Count);
            foreach (UbwUV q in poly)
            {
                UbwXZ s = UbwBlade.OnScreen(UbwBlade.At3(b, q));
                pts.Add(new Vector2((float)s.X, (float)s.Z));
                uvs.Add(new Vector2((float)q.U, (float)q.V));
            }
            wire.Poly(pts, uvs);
            Draw(wire, k.O, altitude + 0.0018f, Fade(Trace, alpha), k.Set.Wire[b.W.Row]);
        }

        /// <summary>Where a blade goes into the floor: the contact shadow, cracks and slit, and both lips (the bake's marks for one sword).</summary>
        internal static void Plant(in UbwCommandLook k, string key, UbwCut cut, int seed, float altitude)
        {
            Builder marks = Scratch(key + " marks"), lips = Scratch(key + " lips");
            UbwFieldBake.MarksInto(marks, cut, seed, 5);
            UbwFieldBake.LipInto(lips, cut, -1, 0.026, seed, k.Sun);
            UbwFieldBake.LipInto(lips, cut, 1, 0.032, seed + 3, k.Sun);
            Draw(marks, k.O, UbwLayers.Pocket.Marks, k.Tint, k.Set.Atlas);
            Draw(lips, k.O, altitude + 0.0025f, k.Tint, k.Set.Atlas);
        }

        /// <summary>
        /// The floor breaking where a blade comes up or drives in (lib/trace.js breakOut): five crumbs thrown that land and
        /// stay, and a puff of dust. <paramref name="forward"/> throws them that way.
        /// </summary>
        internal static void BreakOut(in UbwCommandLook k, UbwCut cut, double age, int seed, UbwXZ? forward = null)
        {
            if (age < 0) return;
            for (int i = 0; i < 5; i++)
            {
                float u = (float)age - 0.02f - i * 0.025f, flight = 0.34f;
                if (u < 0f) continue;
                float ang = forward.HasValue ? Mathf.Atan2((float)forward.Value.Z, (float)forward.Value.X) + (VfxMath.Rand(seed * 5 + i) - 0.5f) * 1.8f
                    : VfxMath.Rand(seed * 5 + i) * Mathf.PI * 2f;
                float reach = 0.12f + VfxMath.Rand(seed * 9 + i) * 0.2f, kk = Mathf.Min(1f, u / flight), up = Mathf.Min(u, flight);
                float h = Mathf.Max(0f, 1.2f * up - 3.5f * up * up);
                var pos = new Vector2(k.O.x + (float)cut.X + Mathf.Cos(ang) * reach * kk, k.O.y + (float)cut.Z + Mathf.Sin(ang) * reach * kk * 0.8f + h * Lift);
                PaperBombGraphics.Rock(pos, 0.03f + VfxMath.Rand(seed + i * 3) * 0.025f, VfxMath.Rand(i + seed) * 360f + u * 400f * (1f - kk), 1f, 1 + 2 * (i % 3),
                    u < flight ? Overhead + 0.01f : VfxDraw.Floor + 0.006f);
            }
            float d = (float)(age / T.BreakOut);
            if (d < 1f)
                Sprite(new Vector2(k.O.x + (float)cut.X, k.O.y + (float)cut.Z + 0.08f + d * 0.12f), 0.35f + d * 0.7f, 0.26f + d * 0.45f,
                    Fade(Dust, 0.4f * (1f - d) * VfxMath.Smooth((float)age / 0.08f)), soft, Overhead + 0.005f);
        }

        /// <summary>A copy breaking into light (lib/trace.js shatter): its outline flashes and fades, sparks fly out from its middle. u 0..1.</summary>
        internal static void Shatter(in UbwCommandLook k, string key, UbwPose b, double u, int seed, float altitude)
        {
            if (u < 0 || u >= 1) return;
            Wire(k, key + " ghost", b, 0.9f * (1f - (float)u), altitude);
            Vector2 at = k.Screen(UbwBlade.Middle(b));
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + VfxMath.Rand(seed + i) * 0.6f, f = (float)u;
                Sprite(new Vector2(at.x + Mathf.Cos(a) * f * 0.7f, at.y + Mathf.Sin(a) * f * 0.5f + f * 0.2f), 0.08f, 0.08f, Fade(TraceHot, 1f - f), glow, Overhead + 0.02f);
            }
        }

        /// <summary>A white glint at a 3D point (a hit, a catch, a meeting), for <paramref name="length"/> seconds.</summary>
        internal static void Spark(in UbwCommandLook k, UbwV3 at, double age, double length, float size, Color colour)
        {
            if (age < 0 || age >= length) return;
            GokuGraphics.Glint(k.Screen(at), size, 1f - (float)(age / length), colour);
        }

        // ---- the commands' swords -----------------------------------------------------------------------------------------

        /// <summary>A sword about to leave, still standing (its hole already baked under it), drawn just over the field.</summary>
        internal static void Standing(in UbwCommandLook k, string key, UbwSword sw) => Blade(k, key, sw.Pose, StuckLayer, true);

        /// <summary>Full Open: a sword <paramref name="u"/> seconds after it began to lift, hovering aimed at <paramref name="foe"/>; a glint at its point as it is aimed.</summary>
        internal static void Gathering(in UbwCommandLook k, string key, UbwSword sw, double u, UbwXZ foe)
        {
            UbwPose b = T.Gathered(sw, u, foe, out bool up, out double aimed);
            Blade(k, key, b, AirLayer(b), up);
            if (aimed >= 0 && aimed < 0.2) GokuGraphics.Glint(k.Screen(b.Tip), 0.22f, Mathf.Sin((float)(aimed / 0.2) * Mathf.PI), TraceHot);
            BreakOut(k, sw.Cut, u, sw.Seed + 3);
        }

        /// <summary>
        /// A sword on a <see cref="UbwSwordShot"/> (Full Open's volley, Pin): turning onto its line from <paramref name="from"/>,
        /// flying, then stuck in the ground with its marks, quivering. <paramref name="pinned"/> draws it over the pawn.
        /// </summary>
        internal static void Shot(in UbwCommandLook k, string key, UbwSword sw, UbwPose from, in UbwSwordShot shot, double s, double wobble, bool pinned)
        {
            UbwPose b = T.Flight(shot, from, sw.Size, s, wobble, out bool air, out double age);
            if (air) Blade(k, key, b, AirLayer(b), false);
            else
            {
                float layer = pinned ? PinLayer : StuckLayer;
                UbwCut cut = UbwBlade.CutOf(b, shot.Buried);
                Plant(k, key, cut, sw.Seed + 500, layer);
                BreakOut(k, cut, age, sw.Seed, shot.Dir);
                Blade(k, key, b, layer, true);
            }
            Spark(k, shot.Hit, age, 0.15, pinned ? 0.22f : 0.32f, White);
        }

        /// <summary>Full Open cancelled: a hovering sword dropping back into its own hole, u 0..1.</summary>
        internal static void DropBack(in UbwCommandLook k, string key, UbwSword sw, UbwPose from, double u)
        {
            UbwPose b = UbwBlade.Blend(from, sw.Pose, T.Smooth(u));
            Blade(k, key, b, u < 0.5 ? AirLayer(b) : StuckLayer, u >= 0.5);
        }

        /// <summary>
        /// Draw: <paramref name="t"/> seconds after the sword began to tear out, <paramref name="share"/> of the way from
        /// <paramref name="start"/> to <paramref name="end"/>; while it spins, two fainter copies a moment behind it.
        /// <paramref name="shareAt"/> gives the share some seconds earlier, for those copies.
        /// </summary>
        internal static void Drawing(in UbwCommandLook k, string key, UbwSword sw, double t, UbwV3 start, UbwV3 end, double share, Func<double, double> shareAt)
        {
            UbwPose b = T.Drawn(sw, t, start, end, share, out bool up);
            float layer = AirLayer(b);
            if (!up && t > T.TearTime + T.TurnFlat)
                for (int i = 0; i < T.Blur.GetLength(0); i++)
                {
                    double lag = T.Blur[i, 0];
                    Blade(k, key + " blur " + i, T.Spinning(sw, start, end, shareAt(lag), t - T.TearTime - lag), layer - 0.0004f * (i + 1), false, (float)T.Blur[i, 1]);
                }
            Blade(k, key, b, layer, up);
            UbwXZ to = T.Toward(start.X, start.Z, end.X, end.Z, out _);
            BreakOut(k, sw.Cut, t, sw.Seed + 3, to);
        }

        /// <summary>Draw's lane: the strip the spinning blade covers, reach either side of the line, faint.</summary>
        internal static void Lane(in UbwCommandLook k, UbwXZ a, UbwXZ b, double reach, float alpha)
        {
            if (alpha <= 0.002f) return;
            UbwXZ to = T.Toward(a.X, a.Z, b.X, b.Z, out _);
            var n = new Vector2((float)(-to.Z * reach), (float)(to.X * reach));
            Vector2 pa = k.Map(a), pb = k.Map(b);
            Sides(2, out Vector2[] left, out Vector2[] right);
            left[0] = pa + n; left[1] = pb + n;
            right[0] = pa - n; right[1] = pb - n;
            Strip(left, right, Fade(TraceHot, 0.07f * alpha), whiteGlow, VfxDraw.Floor + 0.0085f);
            Streak(pa + n, pb + n, 0.03f, Fade(TraceHot, 0.35f * alpha), whiteGlow, VfxDraw.Floor + 0.0088f, 2);
            Streak(pa - n, pb - n, 0.03f, Fade(TraceHot, 0.35f * alpha), whiteGlow, VfxDraw.Floor + 0.0088f, 2);
        }

        /// <summary>Arm: <paramref name="age"/> seconds after the sword began to move toward the pawn whose middle is <paramref name="pawn"/>; nothing once it is held (the game draws the copy).</summary>
        internal static void Arming(in UbwCommandLook k, string key, UbwSword sw, double age, UbwXZ pawn)
        {
            UbwPose b = T.Armed(sw, age, pawn, out bool held);
            if (held) return;
            Blade(k, key, b, AirLayer(b), age < T.PullTime);
        }

        /// <summary>The traced glint on a copy that just reached a hand (Draw's catch, Arm), <paramref name="since"/> seconds after.</summary>
        internal static void Caught(in UbwCommandLook k, UbwXZ hand, double since) =>
            Spark(k, new UbwV3(hand.X, T.HandHeight, hand.Z), since, 0.3, 0.4f, TraceHot);

        /// <summary>Intercept: the sword rising and flying to the meeting point, then a spark there and the sword breaking into light.</summary>
        internal static void Meeting(in UbwCommandLook k, string key, UbwSword sw, in UbwSwordShot shot, double s)
        {
            UbwPose b = T.Flight(shot, sw.Pose, sw.Size, s, 0, out _, out double age);
            BreakOut(k, sw.Cut, s - shot.Launch, sw.Seed + 3);
            if (age < 0)
            {
                Blade(k, key, b, AirLayer(b), s < shot.Launch + shot.Lift * 0.5);
                return;
            }
            Spark(k, shot.Hit, age, T.Spark, 0.38f, White);
            Shatter(k, key, b, age / T.Shatter, sw.Seed, Overhead + 0.01f);
        }

        // ---- marks on the ground ----------------------------------------------------------------------------------------

        /// <summary>A ring under a target (Full Open's mark, Pin's), pulsing slightly.</summary>
        internal static void TargetRing(in UbwCommandLook k, UbwXZ at, double radius, float alpha) =>
            PaperBombGraphics.RingAt(k.Map(at), (float)radius, Fade(TraceHot, alpha), VfxDraw.Floor + 0.009f, false, whiteGlow);

        /// <summary>An order: a white line from the pawn to the sword it calls and a ring at the sword.</summary>
        internal static void Order(in UbwCommandLook k, UbwXZ from, UbwXZ sword, double ring, float alpha)
        {
            if (alpha <= 0.002f) return;
            Streak(k.Map(new UbwXZ(from.X, from.Z + 0.25)), k.Map(sword), 0.025f, Fade(White, 0.55f * alpha), whiteGlow, Overhead + 0.005f, 2);
            TargetRing(k, sword, ring, 0.7f * alpha);
        }
    }
}
