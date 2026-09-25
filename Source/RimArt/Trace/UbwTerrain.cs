using System;
using System.Collections.Generic;
using System.Linq;

namespace RimArt
{
    /// <summary>The rules of the world v2's ground (lib/ubw-terrain.js Ground), plus the hill and the field's reach from the world's look.</summary>
    public struct UbwGround
    {
        /// <summary>Plate spacing on the map, on the hill and past the map; the share of map seeds dropped (bigger plates); the crack width.</summary>
        public double Plate, HillPlate, Outer, Drop, Gap;
        /// <summary>Tiers past the map edge: their height, how many north, east and west, how many south, the jitter between neighbours; the far ridge's band and most height.</summary>
        public double TierStep, Jitter, RidgeBand, RidgeMax;
        public int Tiers, SouthTiers;
        /// <summary>On the map: the hill's terrace step, the random step between plates, the hill's terraces.</summary>
        public double HillStep, PlateStep;
        public int HillLevels;
        /// <summary>The hill of swords' radius and how far the field goes on past the map edge (the world's look).</summary>
        public double Hill, Beyond;

        /// <summary>The v2 sketch's defaults (2026-09-25, an experiment) with the world's hill and reach.</summary>
        public static readonly UbwGround Default = new UbwGround
        {
            Plate = 3.2, HillPlate = 2.2, Outer = 5.5, Drop = 0.12, Gap = 0.14,
            TierStep = 1.2, Tiers = 3, SouthTiers = 3, Jitter = 1, RidgeBand = 6, RidgeMax = 5,
            HillStep = 0.18, PlateStep = 0.08, HillLevels = 3,
            Hill = UbwField.Look.Hill, Beyond = UbwField.Look.Beyond,
        };
    }

    /// <summary>A corner of a plate: the seed whose bisector made the edge that starts here (-1 for the world's edge), and how far that edge moved inward for the crack.</summary>
    public sealed class UbwPlateVertex
    {
        public double X, Z, G;
        public int Nb;

        public UbwPlateVertex(double x, double z, int nb) { X = x; Z = z; Nb = nb; }
    }

    /// <summary>One plate of the ground: a convex polygon at one height, in one of four earth shades.</summary>
    public sealed class UbwPlate
    {
        public List<UbwPlateVertex> Poly;
        /// <summary>1 when the polygon runs counter-clockwise in map coordinates, -1 otherwise.</summary>
        public int Sgn;
        public double MinX, MaxX, MinZ, MaxZ, H;
        /// <summary>0 on the map; the tier past the edge, negative south.</summary>
        public int Tier, Shade;
    }

    public sealed class UbwSeed
    {
        public double X, Z, Sp;
    }

    /// <summary>
    /// The ground of Unlimited Blade Works with height: Voronoi plates of jittered grids, each shrunk by
    /// half the crack width, each at one height (level on the map but for the hill's terraces and a small
    /// step; tiers past the map edge rising north, east and west and dropping south; the far ridge along
    /// the north edge), and the ridge line the sky stands behind. The port of makeTerrain in
    /// Tools/VfxLab/web/sketches/lib/ubw-terrain.js and ridgeOf in lib/ubw-sky.js, in double precision with
    /// the lab's integer hash, so seed 1 with the same rules is the same ground in game and in the lab
    /// (Tests/Ubw checks it). System only.
    /// </summary>
    public sealed class UbwTerrain
    {
        public const double Lift = UbwBlade.Lift;
        /// <summary>The ground is drawn Margin cells past the sword field and cut off Edge cells past that: the world's edge.</summary>
        public const double Margin = 2, Edge = 2, Box = 2.5;
        /// <summary>The ridge line is sampled every Step cells across the drawn width plus 10 cells either side.</summary>
        public const double RidgeStep = 0.5;

        public readonly int Seed;
        public readonly UbwGround Rules;
        public readonly List<UbwSeed> Seeds = new List<UbwSeed>();
        /// <summary>One per seed; null where the cell was too small to keep.</summary>
        public UbwPlate[] Plates;
        public double Bottom, Reach, EdgeAt;
        private double bucketCell, bucketOrigin;
        private Dictionary<long, List<int>> buckets;
        /// <summary>Single precision, as the sketch keeps its ridge samples (a Float32Array): the drawing uses these values.</summary>
        private float[] ridge;
        private double ridgeX0;

        private UbwTerrain(UbwGround rules, int seed)
        {
            Rules = rules;
            Seed = seed;
        }

        /// <summary>The lab's hash(x, y, seed) from Tools/VfxLab/web/js/standins.js, 0..1 (the same as KamuiLayout.LabHash).</summary>
        public static double Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = (int)((long)x * 374761393L + (long)y * 668265263L + (long)seed * 144665L);
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967295.0;
            }
        }

        private static double Hypot(double x, double z) => Math.Sqrt(x * x + z * z);
        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        public static UbwTerrain Make(UbwGround o, int seed = 1)
        {
            var T = new UbwTerrain(o, seed);
            double reach = UbwField.MapHalf + o.Beyond + Margin, edgeAt = reach + Edge, inner = UbwField.MapHalf + o.Plate, hillR = o.Hill + 1;
            List<UbwSeed> seeds = T.Seeds;
            void Put(double gx, double gz, double sp, int k)
            {
                int i = seeds.Count;
                seeds.Add(new UbwSeed { X = gx + (Hash(i, 1, seed + k) - .5) * sp * .8, Z = gz + (Hash(i, 2, seed + k) - .5) * sp * .8, Sp = sp });
            }
            int gi = 0;
            for (double gz = -inner; gz <= inner + 1e-6; gz += o.Plate)
                for (double gx = -inner; gx <= inner + 1e-6; gx += o.Plate)
                {
                    gi++;
                    if (Hypot(gx, gz) < hillR || Hash(gi, 8, seed) < o.Drop) continue;
                    Put(gx, gz, o.Plate, 0);
                }
            if (o.Hill > 0)
                for (double gz = -hillR; gz <= hillR + 1e-6; gz += o.HillPlate)
                    for (double gx = -hillR; gx <= hillR + 1e-6; gx += o.HillPlate)
                        if (Hypot(gx, gz) < hillR) Put(gx, gz, o.HillPlate, 3);
            for (double gz = -reach; gz <= reach + 1e-6; gz += o.Outer)
                for (double gx = -reach; gx <= reach + 1e-6; gx += o.Outer)
                    if (Math.Max(Math.Abs(gx), Math.Abs(gz)) > inner + o.Outer * .6) Put(gx, gz, o.Outer, 7);

            T.Plates = new UbwPlate[seeds.Count];
            for (int i = 0; i < seeds.Count; i++) T.Plates[i] = MakePlate(seeds, i, o, seed, edgeAt);

            // Heights. On the map: level, the hill's terraces, a small random step. Past it: the tiers, and
            // the far ridge along the north edge of the drawn ground.
            double tierW = o.Beyond / o.Tiers;
            for (int i = 0; i < seeds.Count; i++)
            {
                UbwPlate p = T.Plates[i];
                if (p == null) continue;
                UbwSeed s = seeds[i];
                bool onMap = p.MinX < UbwField.MapHalf && p.MaxX > -UbwField.MapHalf && p.MinZ < UbwField.MapHalf && p.MaxZ > -UbwField.MapHalf;
                if (onMap)
                {
                    double d = Hypot(s.X, s.Z);
                    int level = d < o.Hill ? (int)Math.Floor(o.HillLevels * (1 - d / o.Hill) + .5) : 0;
                    p.H = level * o.HillStep + Hash(i, 3, seed) * o.PlateStep;
                    continue;
                }
                double ang = Math.Atan2(s.Z, s.X), beyond = Math.Max(Math.Abs(s.X), Math.Abs(s.Z)) - UbwField.MapHalf;
                double wob = 2.2 * Math.Sin(3 * ang + seed * .7) + 1.3 * Math.Sin(7 * ang + 2.1);
                int tier = Math.Max(1, Math.Min(o.Tiers, (int)Math.Ceiling((beyond - 1 + wob) / tierW)));
                bool south = p.MaxZ < -UbwField.MapHalf && p.MinX < UbwField.MapHalf && p.MaxX > -UbwField.MapHalf;
                if (south)
                {
                    if (o.SouthTiers <= 0) continue;
                    tier = Math.Min(tier, o.SouthTiers);
                }
                double mag = tier * o.TierStep + (Hash(i, 4, seed) - .5) * o.TierStep * o.Jitter;
                if (!south && s.Z > edgeAt - o.RidgeBand)
                {
                    double profile = .5 + .5 * Math.Sin(s.X * .13 + seed) + .35 * Math.Sin(s.X * .29 + 4.2) + .3 * Hash(i, 9, seed);
                    mag += Clamp01(profile / 1.65) * o.RidgeMax * (o.TierStep / 1.2);
                }
                p.H = south ? -mag : mag;
                p.Tier = south ? -tier : tier;
            }
            T.Bottom = -(o.SouthTiers * o.TierStep * (1 + o.Jitter / 2) + 1);
            T.Reach = reach;
            T.EdgeAt = edgeAt;

            // The plate under a point: the nearest seed, found through buckets one outer spacing wide.
            T.bucketCell = o.Outer;
            T.bucketOrigin = reach;
            T.buckets = new Dictionary<long, List<int>>();
            for (int i = 0; i < seeds.Count; i++)
            {
                long key = T.BucketKey((int)Math.Floor((seeds[i].X + reach) / o.Outer), (int)Math.Floor((seeds[i].Z + reach) / o.Outer));
                if (!T.buckets.TryGetValue(key, out List<int> list)) T.buckets[key] = list = new List<int>();
                list.Add(i);
            }
            return T;
        }

        private long BucketKey(int bx, int bz) => ((long)bx << 32) ^ (uint)bz;

        /// <summary>The ground's height under a point, in cells: the nearest seed's plate, 0 where that plate was not kept.</summary>
        public double HeightAt(double x, double z)
        {
            int bx = (int)Math.Floor((x + bucketOrigin) / bucketCell), bz = (int)Math.Floor((z + bucketOrigin) / bucketCell);
            int best = -1;
            double bd = double.PositiveInfinity;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    if (!buckets.TryGetValue(BucketKey(bx + i, bz + j), out List<int> list)) continue;
                    foreach (int k in list)
                    {
                        UbwSeed s = Seeds[k];
                        double d = (s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z);
                        if (d < bd) { bd = d; best = k; }
                    }
                }
            return best >= 0 && Plates[best] != null ? Plates[best].H : 0;
        }

        /// <summary>The plates in the order to draw them: north first, by their seeds.</summary>
        public List<int> PaintOrder()
        {
            var order = new List<int>();
            for (int i = 0; i < Plates.Length; i++) if (Plates[i] != null) order.Add(i);
            return order.OrderByDescending(i => Seeds[i].Z).ToList();
        }

        // ---- one plate --------------------------------------------------------------------------------------

        private static UbwPlate MakePlate(List<UbwSeed> seeds, int i, UbwGround o, int seed, double edgeAt)
        {
            UbwSeed s = seeds[i];
            double R = s.Sp * Box;
            var poly = new List<UbwPlateVertex>
            {
                new UbwPlateVertex(s.X - R, s.Z - R, -1), new UbwPlateVertex(s.X - R, s.Z + R, -1),
                new UbwPlateVertex(s.X + R, s.Z + R, -1), new UbwPlateVertex(s.X + R, s.Z - R, -1),
            };
            var near = new List<(int j, double d)>();
            for (int j = 0; j < seeds.Count; j++)
            {
                if (j == i) continue;
                double d = Hypot(seeds[j].X - s.X, seeds[j].Z - s.Z);
                if (d < 2 * R) near.Add((j, d));
            }
            near = near.OrderBy(q => q.d).ToList();
            foreach (var (j, d) in near)
            {
                UbwSeed n = seeds[j];
                poly = ClipBy(poly, s.X, s.Z, (n.X - s.X) / d, (n.Z - s.Z) / d, d / 2, j);
                if (poly.Count < 3) return null;
            }
            // The outermost plates end at the world's edge instead of running on to their search box.
            double[] nx = { 1, -1, 0, 0 }, nz = { 0, 0, 1, -1 };
            for (int k = 0; k < 4; k++)
            {
                poly = ClipBy(poly, 0, 0, nx[k], nz[k], edgeAt, -1);
                if (poly.Count < 3) return null;
            }
            // Each edge's crack: wider past the map, and varying edge to edge, the same width seen from both plates.
            bool far = Math.Max(Math.Abs(s.X), Math.Abs(s.Z)) > UbwField.MapHalf;
            double half = o.Gap * (far ? .8 : .5);
            foreach (UbwPlateVertex q in poly) q.G = q.Nb < 0 ? half : half * (.4 + 1.2 * Hash(Math.Min(i, q.Nb), Math.Max(i, q.Nb), seed + 5));
            List<UbwPlateVertex> shrunk = Inset(poly, 1);
            if (shrunk == null) return null;
            var p = new UbwPlate
            {
                Poly = shrunk, Sgn = Area2(shrunk) > 0 ? 1 : -1,
                MinX = shrunk.Min(q => q.X), MaxX = shrunk.Max(q => q.X), MinZ = shrunk.Min(q => q.Z), MaxZ = shrunk.Max(q => q.Z),
                H = 0, Tier = 0, Shade = (int)Math.Floor(Hash(i, 7, seed) * 4),
            };
            return p;
        }

        /// <summary>Keep the part of the polygon nearer the seed at (sx, sz) than the seed 2 x half cells away along (nx, nz).</summary>
        private static List<UbwPlateVertex> ClipBy(List<UbwPlateVertex> poly, double sx, double sz, double nx, double nz, double half, int nb)
        {
            var result = new List<UbwPlateVertex>(poly.Count + 2);
            for (int i = 0; i < poly.Count; i++)
            {
                UbwPlateVertex a = poly[i], b = poly[(i + 1) % poly.Count];
                double fa = half - ((a.X - sx) * nx + (a.Z - sz) * nz), fb = half - ((b.X - sx) * nx + (b.Z - sz) * nz);
                if (fa >= 0) result.Add(a);
                if ((fa >= 0) != (fb >= 0))
                {
                    double t = fa / (fa - fb);
                    result.Add(new UbwPlateVertex(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t, fa >= 0 ? nb : a.Nb));
                }
            }
            return result;
        }

        public static double Area2(List<UbwPlateVertex> pts)
        {
            double a = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                UbwPlateVertex p = pts[i], n = pts[(i + 1) % pts.Count];
                a += p.X * n.Z - n.X * p.Z;
            }
            return a;
        }

        private struct Line
        {
            public double Px, Pz, Dx, Dz, G;
            public int Nb;
        }

        /// <summary>One try at moving every edge its g cells inward (scaled by k): the polygon, or the index of the vertex that stops it (bad).</summary>
        private static List<UbwPlateVertex> InsetOnce(List<UbwPlateVertex> poly, double k, out int bad)
        {
            int n = poly.Count, sgn = Area2(poly) > 0 ? 1 : -1;
            var lines = new Line[n];
            for (int i = 0; i < n; i++)
            {
                UbwPlateVertex a = poly[i], b = poly[(i + 1) % n];
                double dx = b.X - a.X, dz = b.Z - a.Z, L = Hypot(dx, dz), g = a.G * k;
                if (L == 0) L = 1;
                lines[i] = new Line { Px = a.X - dz / L * sgn * g, Pz = a.Z + dx / L * sgn * g, Dx = dx / L, Dz = dz / L, Nb = a.Nb, G = a.G };
            }
            var result = new List<UbwPlateVertex>(n);
            for (int i = 0; i < n; i++)
            {
                Line p = lines[(i - 1 + n) % n], q = lines[i];
                double det = p.Dx * q.Dz - p.Dz * q.Dx;
                if (Math.Abs(det) < 1e-6) { bad = i; return null; }
                double t = ((q.Px - p.Px) * q.Dz - (q.Pz - p.Pz) * q.Dx) / det;
                result.Add(new UbwPlateVertex(p.Px + p.Dx * t, p.Pz + p.Dz * t, q.Nb) { G = q.G });
            }
            for (int i = 0; i < n; i++)
            {
                UbwPlateVertex a = result[i], b = result[(i + 1) % n];
                if ((b.X - a.X) * lines[i].Dx + (b.Z - a.Z) * lines[i].Dz <= .03) { bad = (i + 1) % n; return null; }
            }
            bad = -1;
            return result;
        }

        /// <summary>
        /// The polygon with every edge moved inward by its g. An edge shorter than the shrink is merged away
        /// (its far vertex dropped) and the shrink tried again; a cell too small for its gaps gets half of
        /// them, and so on down; only a cell of under three corners is given up (null).
        /// </summary>
        private static List<UbwPlateVertex> Inset(List<UbwPlateVertex> poly, double k)
        {
            var pts = new List<UbwPlateVertex>(poly);
            for (int pass = 0; pass < 10 && pts.Count >= 3; pass++)
            {
                List<UbwPlateVertex> result = InsetOnce(pts, k, out int bad);
                if (result != null) return result;
                pts.RemoveAt(bad);
            }
            return k > .05 ? Inset(poly, k / 2) : null;
        }

        // ---- the ridge --------------------------------------------------------------------------------------

        /// <summary>The first sample's x of the ridge line, and its count.</summary>
        public double RidgeX0 => ridgeX0;
        public int RidgeCount => RidgeSamples().Length;

        /// <summary>
        /// The highest lifted plate top at each x across the drawn width, for the plates along the north
        /// edge of the drawn ground; the world's edge where there is none. The port of ridgeOf.
        /// </summary>
        public float[] RidgeSamples()
        {
            if (ridge != null) return ridge;
            ridgeX0 = -EdgeAt - 10;
            int n = (int)Math.Round((2 * EdgeAt + 20) / RidgeStep) + 1;
            var zs = new float[n];
            for (int i = 0; i < n; i++) zs[i] = (float)EdgeAt;
            foreach (UbwPlate p in Plates)
            {
                if (p == null || p.MaxZ < EdgeAt - 8 || p.MaxZ + p.H * Lift <= EdgeAt) continue;
                double lift = p.H * Lift;
                int m = p.Poly.Count;
                int i0 = Math.Max(0, (int)Math.Ceiling((p.MinX - ridgeX0) / RidgeStep)), i1 = Math.Min(n - 1, (int)Math.Floor((p.MaxX - ridgeX0) / RidgeStep));
                for (int i = i0; i <= i1; i++)
                {
                    double x = ridgeX0 + i * RidgeStep, top = double.NegativeInfinity;
                    for (int k = 0; k < m; k++)
                    {
                        UbwPlateVertex a = p.Poly[k], b = p.Poly[(k + 1) % m];
                        if ((a.X < x) == (b.X < x)) continue;
                        double z = a.Z + (b.Z - a.Z) * (x - a.X) / (b.X - a.X);
                        if (z > top) top = z;
                    }
                    if (!double.IsNegativeInfinity(top) && top + lift > zs[i]) zs[i] = (float)(top + lift);
                }
            }
            ridge = zs;
            return zs;
        }

        /// <summary>The ridge's height on screen at x (cells north of the caster).</summary>
        public double RidgeAt(double x)
        {
            float[] zs = RidgeSamples();
            int i = Math.Max(0, Math.Min(zs.Length - 1, (int)Math.Round((x - ridgeX0) / RidgeStep)));
            return zs[i];
        }
    }
}
