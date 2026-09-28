using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Chibaku Tensei's ground plates (proposed replacement for Gravity Well, sketch
    /// Tools/VfxLab/web/sketches/pain-chibaku-tensei.js): the disc round a cell cut into irregular plates.
    /// A Voronoi tiling of a jittered hex grid of seeds, clipped to the circle, each straight edge bent in the
    /// middle by an amount worked out from its two corners in a fixed order, so the two plates that share an
    /// edge bend it the same way and the cracks line up. The seeds, hash and numbers are the sketch's
    /// (doubles, as JavaScript computes them), so seed 0 gives the lab's layout. Pure geometry: points are
    /// x, z cells from the centre, counter-clockwise.
    /// </summary>
    public static class ChibakuCut
    {
        private struct P
        {
            public readonly double x, z;
            public P(double x, double z) { this.x = x; this.z = z; }
        }

        /// <summary>The sketch's repeatable 0..1 hash (rand() in lib/six-paths-impact.js).</summary>
        public static double Rand(double i)
        {
            double n = Math.Sin(i * 127.1 + 17) * 43758.5453;
            return n - Math.Floor(n);
        }

        /// <summary>
        /// The plates of a disc of <paramref name="radius"/> cells, about <paramref name="cell"/> cells across.
        /// Seed 0 is the sketch's layout; another seed moves every seed point.
        /// </summary>
        public static List<Vector2[]> Plates(double radius, double cell, int seed)
        {
            var seeds = new List<P>();
            double dz = cell * .866;
            int rows = (int)Math.Ceiling(radius / dz) + 1, cols = (int)Math.Ceiling(radius / cell) + 1;
            for (int row = -rows; row <= rows; row++)
                for (int col = -cols; col <= cols; col++)
                {
                    double h = (row + 97) * 331 + (col + 89) * 17 + seed * 7919;
                    var q = new P((col + (row & 1) * .5 + (Rand(h + .1) - .5) * .7) * cell, (row + (Rand(h + .6) - .5) * .7) * dz);
                    if (Math.Sqrt(q.x * q.x + q.z * q.z) < radius + cell * .6) seeds.Add(q);
                }
            var rim = new List<P>();
            for (int k = 0; k < 72; k++)
            {
                double a = k / 72.0 * Math.PI * 2;
                rim.Add(new P(Math.Cos(a) * radius, Math.Sin(a) * radius));
            }
            var plates = new List<Vector2[]>();
            double B = radius + 2;
            for (int i = 0; i < seeds.Count; i++)
            {
                P sd = seeds[i];
                var poly = new List<P> { new P(-B, -B), new P(B, -B), new P(B, B), new P(-B, B) };
                for (int j = 0; j < seeds.Count; j++)
                {
                    P o = seeds[j];
                    if (j == i || Math.Sqrt((o.x - sd.x) * (o.x - sd.x) + (o.z - sd.z) * (o.z - sd.z)) > cell * 2.6) continue;
                    poly = ClipHalf(poly, new P((o.x + sd.x) / 2, (o.z + sd.z) / 2), new P(o.x - sd.x, o.z - sd.z));
                }
                for (int k = 0; k < rim.Count && poly.Count > 2; k++)
                {
                    P a = rim[k], b = rim[(k + 1) % rim.Count];
                    poly = ClipHalf(poly, a, new P(b.z - a.z, a.x - b.x));     // the outward normal of a counter-clockwise edge
                }
                if (poly.Count > 2 && Math.Abs(Area(poly)) / 2 > .012)
                    plates.Add(Bend(poly, cell).Select(q => new Vector2((float)q.x, (float)q.z)).ToArray());
            }
            return plates;
        }

        /// <summary>Twice the signed area: positive for a counter-clockwise outline (x east, z north).</summary>
        public static float SignedArea(IList<Vector2> poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area;
        }

        /// <summary>Whether <paramref name="q"/> lies inside the outline (even-odd rule).</summary>
        public static bool Contains(IList<Vector2> poly, Vector2 q)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static double Area(List<P> poly)
        {
            double area = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                P a = poly[i], b = poly[(i + 1) % poly.Count];
                area += a.x * b.z - b.x * a.z;
            }
            return area;
        }

        /// <summary>The part of a convex outline on the side of the line through m where (q - m).n &lt;= 0.</summary>
        private static List<P> ClipHalf(List<P> poly, P m, P n)
        {
            var kept = new List<P>();
            for (int i = 0; i < poly.Count; i++)
            {
                P a = poly[i], b = poly[(i + 1) % poly.Count];
                double sa = (a.x - m.x) * n.x + (a.z - m.z) * n.z, sb = (b.x - m.x) * n.x + (b.z - m.z) * n.z;
                if (sa <= 0) kept.Add(a);
                if ((sa < 0) != (sb < 0) && sa != sb)
                {
                    double k = sa / (sa - sb);
                    kept.Add(new P(a.x + (b.x - a.x) * k, a.z + (b.z - a.z) * k));
                }
            }
            return kept;
        }

        private static double Id(P q) => Math.Floor(q.x * 500 + .5) * 7 + Math.Floor(q.z * 500 + .5) * 13;

        private static List<P> Bend(List<P> poly, double cell)
        {
            var bent = new List<P>();
            for (int i = 0; i < poly.Count; i++)
            {
                P a = poly[i], b = poly[(i + 1) % poly.Count];
                double len = Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
                bent.Add(a);
                if (len < cell * .3) continue;
                P p = a, q = b;
                if (Id(b) < Id(a)) { p = b; q = a; }
                double k = (Rand(Id(p) * .37 + Id(q) * .11) - .5) * .22 * len;
                bent.Add(new P((a.x + b.x) / 2 - (q.z - p.z) / len * k, (a.z + b.z) / 2 + (q.x - p.x) / len * k));
            }
            return bent;
        }
    }
}
