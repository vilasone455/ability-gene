using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// A weapon picture the trace look is drawn on: its shape as read from the texture (tip, pommel, width table),
    /// the texture itself (<see cref="Face"/>, drawn in <see cref="Colour"/>), its outline (the wire) and its
    /// silhouette (the scan line), both as added light.
    /// </summary>
    internal sealed class TraceShape
    {
        public UbwWeapon W;
        public Material Face, Wire, Mask;
        /// <summary>The face's own colour: a stuff-coloured weapon in game, white in the lab.</summary>
        public Color Colour = Color.white;
    }

    /// <summary>
    /// Where Shirou's circuit goes this frame: the point the pawn is drawn at, the side its weapon hand is on, how
    /// wide the table is drawn, run 0 (to the real grip in game), and the altitude of the lines.
    /// </summary>
    internal struct TraceBody
    {
        public Vector2 Me;
        public float Side, Width, Altitude;
        public TraceCircuitRun Arm;

        /// <summary>The lab's stand-in: the table as it is, mirrored for the west hand, over the pawn layer.</summary>
        public static TraceBody Lab(Vector2 me, float side) =>
            new TraceBody { Me = me, Side = side, Width = 1f, Altitude = TraceHandGraphics.PawnLayer, Arm = TraceCircuit.Runs[0] };

        /// <summary>A table point on this body, in map coordinates (fitted to a real pawn while <see cref="PawnFit.On"/>).</summary>
        public Vector2 Point(Vector2 q) => PawnFit.At(Me, q.x * Side * Width, q.y);

        /// <summary>The table point that <see cref="Point"/> takes to <paramref name="world"/>.</summary>
        public Vector2 Table(Vector2 world)
        {
            float body = PawnFit.Body, x = (world.x - Me.x) / (Side * Width * body), dy = world.y - Me.y;
            return new Vector2(x, PawnFit.On ? (dy + PawnFit.Drop) / PawnFit.Scale : dy);
        }
    }

    /// <summary>
    /// The pieces Trace On and Reinforcement share (lib/trace.js): parts of a weapon's texture on its plane (the
    /// wire, the structure lines, the steel filling in behind a bright line, the glow on the edge), the circuit on
    /// the body, and a copy breaking into light. A held weapon is a <see cref="UbwPose"/>: in the lab the sketch's
    /// stand-in pose at hand height, in game the plane the game draws the weapon on (height 0, see
    /// Trace/Kit/TraceHands.cs). Every piece is one mesh built this frame under its own key, so the key must name
    /// the pawn as well as the piece.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class TraceHandGraphics
    {
        internal static readonly Color Trace = UbwGraphics.Trace, TraceHot = UbwGraphics.TraceHot, White = Color.white, Black = Color.black;
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        /// <summary>The sketches' shadow vector per cell of height; the stand-in weapon's shadow and light use it.</summary>
        internal static readonly UbwXZ Sun = new UbwXZ(-0.45, -0.32);
        private static readonly Vector2 Uv = new Vector2(0.5f, 0.5f);

        // ---- shapes ------------------------------------------------------------------------------------------

        /// <summary>
        /// Set by the game (Trace/Kit/TraceWeaponShapes.cs): a lab weapon's name to the shape of the game weapon that
        /// stands in for it in the previews. Without it, the lab's reference textures (make_trace_trial_textures.py,
        /// git-excluded) with the numbers in UbwWeapons.Lab.
        /// </summary>
        internal static Func<string, TraceShape> Provider;
        private static readonly Dictionary<string, TraceShape> lab = new Dictionary<string, TraceShape>();

        internal static TraceShape Shape(string name)
        {
            TraceShape shape = Provider?.Invoke(name);
            if (shape != null) return shape;
            if (lab.TryGetValue(name, out shape)) return shape;
            UbwWeapon w = Array.Find(UbwWeapons.Lab, x => x.Name == name) ?? UbwWeapons.Lab[1];
            shape = new TraceShape
            {
                W = w,
                Face = MaterialPool.MatFrom("RimArt/TraceTrial/" + w.Name, ShaderDatabase.Transparent),
                Wire = MaterialPool.MatFrom("RimArt/TraceTrial/" + w.Name + "Outline", ShaderDatabase.MoteGlow),
                Mask = MaterialPool.MatFrom("RimArt/TraceTrial/" + w.Name + "Mask", ShaderDatabase.MoteGlow),
            };
            lab[name] = shape;
            return shape;
        }

        // ---- geometry ----------------------------------------------------------------------------------------

        internal static Vector2 Screen(UbwV3 p)
        {
            UbwXZ s = UbwBlade.OnScreen(p);
            return new Vector2((float)s.X, (float)s.Z);
        }

        internal static Vector2 Tip(UbwPose b) => Screen(b.Tip);

        internal static Vector2 Middle(UbwPose b) => Screen(UbwV3.Plus(b.Tip, b.A, b.L / 2));

        /// <summary>The texture from the pommel down to <paramref name="edge"/> from the point (along the axis, in uv).</summary>
        private static List<UbwUV> From(UbwWeapon w, double edge) => UbwBlade.Clip(UbwBlade.Square, q => UbwBlade.Along(w, q) - edge);

        /// <summary>The texture between lo and hi along the axis.</summary>
        private static List<UbwUV> Band(UbwWeapon w, double lo, double hi) =>
            UbwBlade.Clip(UbwBlade.Clip(UbwBlade.Square, q => UbwBlade.Along(w, q) - lo), q => hi - UbwBlade.Along(w, q));

        /// <summary>How much the sun lights the blade's face, 0..1: the sketch's shade is 0.8 + 0.2 of it.</summary>
        private static float Lit(UbwPose b) =>
            (float)Math.Max(0.0, UbwV3.Dot(b.N, UbwV3.Unit(new UbwV3(-Sun.X, 1, -Sun.Z))));

        // ---- drawing -----------------------------------------------------------------------------------------

        /// <summary>Part of a blade's texture (a polygon in uv), drawn where it is or, with <paramref name="shadow"/>, cast along the sun.</summary>
        internal static void TexPoly(string key, List<UbwUV> poly, UbwPose b, bool shadow, Material material, Color colour, float altitude)
        {
            if (poly.Count < 3 || colour.a <= 0.002f) return;
            UbwGraphics.Builder builder = UbwGraphics.Scratch(key);
            var points = new Vector2[poly.Count];
            var uvs = new Vector2[poly.Count];
            for (int i = 0; i < poly.Count; i++)
            {
                UbwV3 p = UbwBlade.At3(b, poly[i]);
                UbwXZ s = shadow ? UbwBlade.AlongSun(p, Sun) : UbwBlade.OnScreen(p);
                points[i] = new Vector2((float)s.X, (float)s.Z);
                uvs[i] = new Vector2((float)poly[i].U, (float)poly[i].V);
            }
            builder.Poly(points, uvs);
            UbwGraphics.Draw(builder, Vector2.zero, altitude, colour, material);
        }

        /// <summary>The lab's line(): taper 0 keeps the width, 2 thins it to nothing at both ends.</summary>
        internal static void Line(string key, IList<Vector2> points, float width, Color colour, float altitude, int taper = 0, Material material = null)
        {
            if (points.Count < 2 || colour.a <= 0.002f) return;
            UbwGraphics.Builder builder = UbwGraphics.Scratch(key);
            builder.Line(points, width, Uv, taper);
            UbwGraphics.Draw(builder, Vector2.zero, altitude, colour, material ?? whiteGlow);
        }

        /// <summary>
        /// The plain weapon, as lib/trace.js blade() draws one lying in the hand: its shadow along the sun and its face,
        /// lit. The lab's stand-in for the weapon the game draws; in game only the steel filling in uses it (no shadow:
        /// the game draws none under a held weapon).
        /// </summary>
        internal static void Steel(string key, TraceShape shape, UbwPose b, List<UbwUV> poly, float altitude, bool shadow, float alpha = 1f)
        {
            if (shadow) TexPoly(key + " shadow", poly, b, true, shape.Face, Fade(Black, 0.42f * alpha), ShadowLayer + 0.002f);
            float g = 0.8f + 0.2f * Lit(b);
            Color c = shape.Colour;
            TexPoly(key + " face", poly, b, false, shape.Face, new Color(c.r * g, c.g * g, c.b * g, c.a * alpha), altitude + 0.001f);
        }

        internal static void Plain(string key, TraceShape shape, UbwPose b, float altitude, bool shadow, float alpha = 1f) =>
            Steel(key, shape, b, UbwBlade.Clip(UbwBlade.Square, q => UbwBlade.At3(b, q).Y), altitude, shadow, alpha);

        /// <summary>The wire from the grip up to share <paramref name="f"/> of the length, and the structure lines inside it.</summary>
        internal static void Wire(string key, TraceShape shape, UbwPose b, float f, float alpha, float altitude)
        {
            TexPoly(key + " wire", From(b.W, b.W.Length * (1 - f)), b, false, shape.Wire, Fade(Trace, 0.95f * alpha), altitude + 0.004f);
            Structure(key, b, f, Fade(Trace, 0.7f * alpha), altitude + 0.005f);
        }

        /// <summary>
        /// The copy's structure lines: along the middle of its width table from the grip up to share f of its length,
        /// and across it at the Crosses steps once the wire has passed them. They fit each weapon, curved ones too.
        /// </summary>
        private static void Structure(string key, UbwPose b, float f, Color colour, float altitude)
        {
            UbwWeapon w = b.W;
            Vector2 Point(int i, double x)
            {
                double s = w.Length * (i + 0.5) / 16;
                return Screen(UbwBlade.At3(b, new UbwUV(w.TipU + w.AxisU * s + w.AcrossU * x, w.TipV + w.AxisV * s + w.AcrossV * x)));
            }
            var spine = new List<Vector2>(16);
            for (int i = 15; i >= 0; i--)
                if ((i + 0.5) / 16 >= 1 - f) spine.Add(Point(i, (w.Width[i, 0] + w.Width[i, 1]) / 2));
            Line(key + " spine", spine, 0.014f, colour, altitude);
            foreach (int i in TraceOnTiming.Crosses)
                if ((i + 0.5) / 16 >= 1 - f)
                    Line(key + " cross " + i, new[] { Point(i, w.Width[i, 0] * 0.85), Point(i, w.Width[i, 1] * 0.85) }, 0.012f, colour, altitude);
        }

        /// <summary>The steel so far (share <paramref name="ff"/> from the grip) and the bright line at its front.</summary>
        internal static void Fill(string key, TraceShape shape, UbwPose b, float ff, float altitude, bool shadow)
        {
            double front = b.W.Length * (1 - ff);
            Steel(key + " fill", shape, b, From(b.W, front), altitude, shadow);
            TexPoly(key + " scan", Band(b.W, front - 0.012, front + 0.02), b, false, shape.Mask, Fade(TraceHot, 0.85f), altitude + 0.006f);
        }

        /// <summary>
        /// Reinforcement on the weapon: its outline lit from the grip up to share <paramref name="climb"/>, a soft light
        /// along the lit part so it still reads at game zoom, and the bright line while it climbs.
        /// </summary>
        internal static void Glow(string key, TraceShape shape, UbwPose b, float climb, float level, float altitude)
        {
            if (climb <= 0f || level <= 0f) return;
            double edge = b.W.Length * (1 - climb);
            float bright = TraceReinforcementTiming.Bright;
            TexPoly(key + " glow", From(b.W, edge), b, false, shape.Wire, Fade(Trace, 0.95f * bright * level), altitude + 0.002f);
            Vector2 point = Screen(b.Tip), pommel = Screen(UbwV3.Plus(b.Tip, b.A, b.L));
            Vector2 lit = Vector2.Lerp(pommel, point, climb / 2f);
            float length = (point - pommel).magnitude * climb + 0.2f;
            Sprite(lit, length, 0.24f, Fade(Trace, 0.3f * bright * level), glow, altitude + 0.0015f,
                -Mathf.Atan2(point.y - pommel.y, point.x - pommel.x) * Mathf.Rad2Deg);
            if (climb < 1f)
                TexPoly(key + " glow scan", Band(b.W, edge - 0.012, edge + 0.02), b, false, shape.Mask, Fade(TraceHot, 0.85f), altitude + 0.003f);
        }

        /// <summary>A copy breaking into light (u 0..1): its outline flashes and fades, sparks fly out from its middle.</summary>
        internal static void Shatter(string key, TraceShape shape, UbwPose b, float u, int seed, float altitude)
        {
            if (u < 0f || u >= 1f) return;
            TexPoly(key + " ghost", new List<UbwUV>(UbwBlade.Square), b, false, shape.Wire, Fade(Trace, 0.9f * (1 - u)), altitude + 0.0018f);
            Vector2 at = Middle(b);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + Rand(seed + i) * 0.6f;
                Sprite(new Vector2(at.x + Mathf.Cos(a) * u * 0.7f, at.y + Mathf.Sin(a) * u * 0.5f + u * 0.2f), 0.08f, 0.08f,
                    Fade(TraceHot, 1 - u), glow, Overhead + 0.02f);
            }
        }

        /// <summary>
        /// The circuit on the body, lit to <paramref name="reach"/> cells of travel (Reinforcement): each run as a halo
        /// and a bright line, with a node at every corner it has passed and a white head where it is still running.
        /// </summary>
        internal static void Circuit(string key, TraceBody body, float reach, float level, float width = TraceReinforcementTiming.Width)
        {
            if (level <= 0f) return;
            for (int k = 0; k < TraceCircuit.Runs.Length; k++)
            {
                TraceCircuitRun run = k == 0 ? body.Arm : TraceCircuit.Runs[k];
                float shown = Mathf.Min(run.Length, reach - run.From);
                if (shown <= 0.004f) continue;
                List<Vector2> pts = TraceCircuit.Partial(run.Points, shown);
                for (int i = 0; i < pts.Count; i++) pts[i] = body.Point(pts[i]);
                Line(key + " circuit halo " + k, pts, width * 3.5f, Fade(Trace, 0.2f * level), body.Altitude + 0.006f);
                Line(key + " circuit " + k, pts, width, Fade(TraceHot, 0.9f * level), body.Altitude + 0.007f);
                bool running = shown < run.Length;
                for (int i = 1; i < (running ? pts.Count - 1 : pts.Count); i++)
                    Sprite(pts[i], 0.05f, 0.05f, Fade(TraceHot, 0.8f * level), glow, body.Altitude + 0.008f);
                if (running) Sprite(pts[pts.Count - 1], 0.09f, 0.09f, Fade(White, 0.9f * level), glow, body.Altitude + 0.008f);
            }
        }

        /// <summary>
        /// Trace On's arm line: run 0 lit over share <paramref name="f"/>, a halo and a bright line fading with
        /// <paramref name="fade"/>, and a white head while it runs.
        /// </summary>
        internal static void ArmLine(string key, TraceBody body, float f, float fade)
        {
            float bright = TraceOnTiming.Bright;
            List<Vector2> pts = TraceCircuit.Partial(body.Arm.Points, body.Arm.Length * f);
            for (int i = 0; i < pts.Count; i++) pts[i] = body.Point(pts[i]);
            if (fade <= 0f || pts.Count < 2) return;
            Line(key + " arm halo", pts, 0.08f, Fade(Trace, 0.2f * bright * fade), body.Altitude + 0.006f);
            Line(key + " arm", pts, 0.022f, Fade(TraceHot, 0.9f * bright * fade), body.Altitude + 0.007f);
            if (f < 1f) Sprite(pts[pts.Count - 1], 0.09f, 0.09f, Fade(White, 0.9f * bright), glow, body.Altitude + 0.008f);
        }
    }
}
