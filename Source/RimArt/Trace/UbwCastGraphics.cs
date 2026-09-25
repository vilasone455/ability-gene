using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.UbwGraphics;
using T = RimArt.UbwCastTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Unlimited Blade Works' home-map side round the caster's cell: the chant (cyan lines running
    /// out along the floor, a ring at their front, square circuit traces lighting round the caster's feet,
    /// a ring off the caster as each verse starts), the release (the lines catch fire from the caster
    /// outward, the fire runs along them and their branches, an ember line stays under it), the ring
    /// closing along the radius, the white that takes everyone, the low ring burning while they are away,
    /// the flare and the fire running back in, the white that brings them back.
    ///
    /// The port of Tools/VfxLab/web/sketches/trace-ubw-cast.js and the chant of lib/ubw-pocket.js at the
    /// home map's own layers. Its stand-ins are not ported, and with them go everything drawn on them: the
    /// caster's raised arm, the ally, the raiders, the downed one, the corpse. Those are the ability's.
    /// Floor lines, level rings and flames only, so it looks the same from every side.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwCastGraphics
    {

        // ---- the chant's lines and squares, worked out once -----------------------------------------------------

        /// <summary>Line i of the chant from the caster's feet out to reach: the chant draws it, and the fire runs along it.</summary>
        internal static List<Vector2> CircuitPath(int i, float reach)
        {
            float a0 = i / (float)T.Lines * Mathf.PI * 2f + Rand(i * 3 + 7) * 0.3f;
            var pts = new List<Vector2>();
            for (float r = 0.35f; ; r += 0.5f)
            {
                float rr = Mathf.Min(r, reach), wob = Mathf.Sin(rr * 1.3f + i) * 0.08f;
                pts.Add(new Vector2(Mathf.Cos(a0 + wob) * rr, Mathf.Sin(a0 + wob) * rr));
                if (r >= reach) break;
            }
            return pts;
        }

        private sealed class Square
        {
            public readonly List<Vector2> Pts = new List<Vector2>();
            public float Length, Delay;
        }

        /// <summary>Square traces round the caster's feet: each runs out from a small ring in straight steps that turn at right angles, and ends in a node.</summary>
        private static readonly Square[] Squares = MakeSquares();

        private static Square[] MakeSquares()
        {
            var squares = new Square[8];
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) / 8f * Mathf.PI * 2f + (Rand(i * 5 + 1) - 0.5f) * 0.3f;
                float sx = Mathf.Cos(a) < 0f ? -1f : 1f, sz = Mathf.Sin(a) < 0f ? -1f : 1f;
                float x = Mathf.Cos(a) * 0.42f, z = Mathf.Sin(a) * 0.42f;
                bool across = Mathf.Abs(Mathf.Cos(a)) > Mathf.Abs(Mathf.Sin(a));
                var sq = new Square();
                sq.Pts.Add(new Vector2(x, z));
                for (int k = 0; k < 4; k++)
                {
                    float len = 0.16f + Rand(i * 17 + k) * 0.28f;
                    if (across) x += sx * len; else z += sz * len;
                    sq.Pts.Add(new Vector2(x, z));
                    across = !across;
                }
                for (int k = 1; k < sq.Pts.Count; k++) sq.Length += (sq.Pts[k] - sq.Pts[k - 1]).magnitude;
                sq.Delay = Rand(i * 23 + 2) * 0.3f;
                squares[i] = sq;
            }
            return squares;
        }

        /// <summary>The first <paramref name="length"/> cells of a polyline.</summary>
        private static List<Vector2> Partial(List<Vector2> pts, float length)
        {
            var result = new List<Vector2> { pts[0] };
            float left = length;
            for (int i = 1; i < pts.Count && left > 0f; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float d = (b - a).magnitude;
                if (d <= left) { result.Add(b); left -= d; continue; }
                result.Add(a + (b - a) * (left / d));
                left = 0f;
            }
            return result;
        }

        // ---- walking a line ---------------------------------------------------------------------------------------

        private static float[] Lengths(List<Vector2> pts)
        {
            var L = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) L[i] = L[i - 1] + (pts[i] - pts[i - 1]).magnitude;
            return L;
        }

        internal static float LengthOf(List<Vector2> pts) => Lengths(pts)[pts.Count - 1];

        /// <summary>The point d cells along the line, and the line's direction there.</summary>
        internal static void PointAt(List<Vector2> pts, float d, out Vector2 at, out Vector2 dir)
        {
            float[] L = Lengths(pts);
            for (int i = 1; i < pts.Count; i++)
            {
                if (d > L[i] && i < pts.Count - 1) continue;
                float len = L[i] - L[i - 1];
                if (len == 0f) len = 1f;
                float f = Mathf.Clamp01((d - L[i - 1]) / len);
                Vector2 a = pts[i - 1], b = pts[i];
                at = a + (b - a) * f;
                dir = (b - a) / len;
                return;
            }
            at = pts[0];
            dir = Vector2.right;
        }

        /// <summary>The part of the line from d0 to d1 cells along it.</summary>
        internal static List<Vector2> Between(List<Vector2> pts, float d0, float d1)
        {
            var result = new List<Vector2>();
            if (d1 <= d0) return result;
            float[] L = Lengths(pts);
            PointAt(pts, d0, out Vector2 a, out _);
            result.Add(a);
            for (int i = 1; i < pts.Count - 1; i++) if (L[i] > d0 && L[i] < d1) result.Add(pts[i]);
            PointAt(pts, d1, out Vector2 b, out _);
            result.Add(b);
            return result;
        }

        // ---- the fire's lines: the chant's 10 at the full radius, each with its branches ------------------------------

        private sealed class Branch
        {
            public float Fork, L;
            public List<Vector2> Pts;
        }

        private sealed class FireLine
        {
            public List<Vector2> Main;
            public float L, Angle;
            public List<Branch> Branches = new List<Branch>();
        }

        private static readonly Dictionary<(float, int), FireLine[]> networks = new Dictionary<(float, int), FireLine[]>();

        private static FireLine[] Network(float R, int count)
        {
            if (networks.TryGetValue((R, count), out FireLine[] lines)) return lines;
            lines = new FireLine[T.Lines];
            for (int i = 0; i < T.Lines; i++)
            {
                var ln = new FireLine { Main = CircuitPath(i, R) };
                ln.L = LengthOf(ln.Main);
                Vector2 end = ln.Main[ln.Main.Count - 1];
                ln.Angle = Mathf.Atan2(end.y, end.x);
                for (int k = 0; k < count; k++)
                {
                    float fork = T.BranchAt[k] * ln.L;
                    PointAt(ln.Main, fork, out Vector2 q, out Vector2 dq);
                    float side = Rand(i * 7 + k * 3 + 1) > 0.5f ? 1f : -1f;
                    float a = Mathf.Atan2(dq.y, dq.x) + side * (T.BranchTurn[k] + (Rand(i * 11 + k) - 0.5f) * 0.3f);
                    var pts = new List<Vector2> { q };
                    for (int j = 1; j <= 6; j++)
                    {
                        float d = T.BranchLength[k] * R * j / 6f, wob = Mathf.Sin(d * 1.7f + i + k) * 0.12f;
                        pts.Add(new Vector2(q.x + Mathf.Cos(a + wob) * d, q.y + Mathf.Sin(a + wob) * d));
                    }
                    ln.Branches.Add(new Branch { Fork = fork, Pts = pts, L = LengthOf(pts) });
                }
                lines[i] = ln;
            }
            networks[(R, count)] = lines;
            return lines;
        }

        /// <summary>Flames every FlameEvery cells along the line from d0 to d1, taller within half a cell of <paramref name="front"/>.</summary>
        private static void Along(List<Flame> list, List<Vector2> pts, float d0, float d1, float front, float s, float h, int seed, Vector2 o)
        {
            int k = 0;
            for (float d = Mathf.CeilToInt(d0 / T.FlameEvery) * T.FlameEvery; d <= d1; d += T.FlameEvery, k++)
            {
                PointAt(pts, d, out Vector2 q, out _);
                float near = Mathf.Abs(d - front) < 0.5f ? 1.35f : 1f;
                list.Add(new Flame { X = o.x + q.x, Z = o.y + q.y, W = 0.26f, H = Flick(seed + k, s, h) * near });
            }
        }

        /// <summary>The glowing line the fire leaves on the ground.</summary>
        private static void EmberLine(string key, List<Vector2> pts, Vector2 o, float alpha)
        {
            if (pts.Count < 2 || alpha <= 0f) return;
            Builder line = Scratch(key + " ember");
            line.Line(pts, 0.08f, new Vector2(0.5f, 0.5f), 0);
            UbwGraphics.Draw(line, o, Floor + 0.006f, Fade(FireOuter, 0.55f * alpha), whiteGlow);
            Builder halo = Scratch(key + " ember glow");
            halo.Line(pts, 0.32f, new Vector2(0.5f, 0.5f), 0);
            UbwGraphics.Draw(halo, o, Floor + 0.0055f, Fade(FireOuter, 0.14f * alpha), whiteGlow);
        }

        private static void FloorLine(string key, List<Vector2> pts, float width, Color colour, float altitude, int taper, Vector2 o)
        {
            if (pts.Count < 2 || colour.a <= 0.002f) return;
            Builder b = Scratch(key);
            b.Line(pts, width, new Vector2(0.5f, 0.5f), taper);
            UbwGraphics.Draw(b, o, altitude, colour, whiteGlow);
        }

        // ---- the chant ----------------------------------------------------------------------------------------------

        /// <summary>The lines, their halo, the ring at their front, the rings of verses already said, a ring off the caster as each verse starts. alpha fades them all.</summary>
        internal static void Chant(Vector2 o, float s, int verses, float alpha)
        {
            if (alpha <= 0f) return;
            float reach = T.ChantRadius(s, verses);
            for (int i = 0; i < T.Lines; i++)
            {
                List<Vector2> pts = CircuitPath(i, reach);
                if (pts.Count < 2) continue;
                FloorLine("ubw circuit " + i, pts, 0.045f, Fade(Trace, 0.75f * alpha), Floor + 0.007f, 1, o);
                FloorLine("ubw circuit halo " + i, pts, 0.16f, Fade(Trace, 0.16f * alpha), Floor + 0.0065f, 1, o);
            }
            if (reach > 0.4f) PaperBombGraphics.RingAt(o, reach, Fade(Trace, 0.45f * alpha), Floor + 0.0068f);
            for (int k = 1; k < verses; k++)
                if (s >= k * T.VerseTime - 0.2f) PaperBombGraphics.RingAt(o, T.Radius[k - 1], Fade(Trace, 0.22f * alpha), Floor + 0.0067f);
            for (int k = 1; k <= verses; k++)
            {
                float u = (s - (k - 1) * T.VerseTime) / 0.45f;
                if (u >= 0f && u < 1f) PaperBombGraphics.RingAt(o, 0.3f + 1.2f * u, Fade(Trace, 0.7f * (1f - u) * alpha), Overhead + 0.01f, false, whiteGlow);
            }
        }

        /// <summary>The square traces: they light at 2.4 cells a second and pulse while they hold. s is seconds since the chant began.</summary>
        internal static void SquareTraces(Vector2 o, float s, float alpha)
        {
            if (alpha <= 0f || s < 0f) return;
            float pulse = 0.8f + 0.2f * Mathf.Sin(s * 6f);
            for (int i = 0; i < Squares.Length; i++)
            {
                Square sq = Squares[i];
                float lit = (s - sq.Delay) * 2.4f;
                if (lit <= 0f) continue;
                List<Vector2> pts = Partial(sq.Pts, Mathf.Min(lit, sq.Length));
                if (pts.Count < 2) continue;
                FloorLine("ubw square " + i, pts, 0.035f, Fade(Trace, 0.85f * alpha * pulse), Floor + 0.0071f, 0, o);
                FloorLine("ubw square halo " + i, pts, 0.12f, Fade(Trace, 0.14f * alpha), Floor + 0.007f, 0, o);
                if (lit >= sq.Length) Sprite(o + pts[pts.Count - 1], 0.12f, 0.12f, Fade(TraceHot, 0.9f * alpha * pulse), glow, Floor + 0.0073f);
            }
        }

        // ---- the cast ---------------------------------------------------------------------------------------------------

        /// <summary>The preview: the cast centred 2 cells north of the chosen cell, as the sketch is for the lab camera (<see cref="UbwWorldGraphics.SceneNorth"/>).</summary>
        public static void DrawPreview(Vector3 centre, float seconds, Map map) =>
            Draw(new Vector2(centre.x, centre.z + UbwWorldGraphics.SceneNorth), T.Verse, seconds, map);

        /// <summary>The whole home-map side round <paramref name="o"/>, released after <paramref name="verse"/>, at <paramref name="s"/> seconds.</summary>
        public static void Draw(Vector2 o, int verse, float s, Map map)
        {
            T.Plan t = T.For(verse);
            if (s < 0f || s >= t.End || !Shown(o, map)) return;
            float R = t.R;
            FireLine[] net = Network(R, T.Branches);

            // The chant, fading as the fire takes its lines.
            float chantFade = s < t.Open ? 1f : 1f - T.Smooth((s - t.Open) / (T.Run * 0.8f));
            if (s < t.Taken)
            {
                Chant(o, s, t.V, chantFade);
                SquareTraces(o, s, s < t.Open ? 1f : 1f - T.Smooth((s - t.Open) / 0.5f));
                if (s < t.Open) Sprite(new Vector2(o.x, o.y + 0.3f), 1.6f, 1.6f, Fade(Trace, 0.22f + 0.1f * Mathf.Sin(s * 9f)), glow, Overhead + 0.005f);
            }

            // The fire on the lines: out from the caster after the release, back in from the ring when the
            // world ends. Hidden by the white each time, so drawn only until the white is full.
            var fire = new List<Flame>();
            if (s >= t.Open && s < t.Taken + T.FlashHold)
            {
                float u = Mathf.Clamp01((s - t.Open) / T.Run), ease = 1f - (1f - u) * (1f - u);
                for (int i = 0; i < net.Length; i++)
                {
                    FireLine ln = net[i];
                    float front = ln.L * ease;
                    Along(fire, ln.Main, 0f, front, front, s, T.Flame, i * 100, o);
                    EmberLine("ubw cast " + i, Between(ln.Main, 0f, front), o, 1f);
                    for (int b = 0; b < ln.Branches.Count; b++)
                    {
                        Branch br = ln.Branches[b];
                        float d = Mathf.Clamp01((front - br.Fork) / br.L) * br.L;
                        if (d <= 0f) continue;
                        Along(fire, br.Pts, 0f, d, d, s, T.Flame * 0.8f, i * 100 + 50 + b * 20, o);
                        EmberLine("ubw cast " + i + " branch " + b, Between(br.Pts, 0f, d), o, 1f);
                    }
                }
            }
            if (s >= t.Inward && s < t.Home + T.FlashHold)
            {
                float u = Mathf.Clamp01((s - t.Inward) / T.Back), front = u * u;
                for (int i = 0; i < net.Length; i++)
                {
                    FireLine ln = net[i];
                    float d0 = ln.L * (1f - front);
                    Along(fire, ln.Main, d0, ln.L, d0, s, T.Flame, i * 100, o);
                    EmberLine("ubw back " + i, Between(ln.Main, d0, ln.L), o, 1f);
                    for (int b = 0; b < ln.Branches.Count; b++)
                    {
                        Branch br = ln.Branches[b];
                        if (d0 >= br.Fork) continue;
                        Along(fire, br.Pts, 0f, br.L, -1f, s, T.Flame * 0.8f, i * 100 + 50 + b * 20, o);
                        EmberLine("ubw back " + i + " branch " + b, br.Pts, o, 1f);
                    }
                }
            }

            // The ring: it closes from the ends of the lines, burns low while everyone is away, flares at the end.
            if (s >= t.Lit && s < t.Home + T.FlashHold)
            {
                float spread = Mathf.PI / T.Lines * Mathf.Clamp01((s - t.Lit) / T.RingClose);
                float height = s < t.Taken ? T.Flame : s < t.Ends ? T.Flame * T.Low : T.Flame * (T.Low + (1f - T.Low) * T.Smooth((s - t.Ends) / T.Flare));
                bool Reached(float a)
                {
                    for (int i = 0; i < net.Length; i++)
                    {
                        float d = a - net[i].Angle;
                        if (Mathf.Abs(Mathf.Atan2(Mathf.Sin(d), Mathf.Cos(d))) <= spread) return true;
                    }
                    return false;
                }
                RingFlames(fire, o, R, s, height, Reached);
                PaperBombGraphics.RingAt(o, R, Fade(FireOuter, 0.35f * (spread * T.Lines / Mathf.PI)), Floor + 0.008f, true, whiteGlow);
            }
            Flames("ubw cast fire", fire, 1f, o, Overhead + 0.02f);

            // The two flashes.
            WhiteDisc(o, R * 1.04f, T.FlashAlpha(s, t.Closed), Overhead + 0.06f);
            WhiteDisc(o, R * 1.04f, T.FlashAlpha(s, t.Back), Overhead + 0.06f);
        }
    }
}
