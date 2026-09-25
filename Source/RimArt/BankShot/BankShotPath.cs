using System;
using System.Collections.Generic;

namespace RimArt
{
    public enum BankShotEnd { Hit, Embed, Range }

    /// <summary>A corner of the flight: a place and the distance flown to it.</summary>
    public struct BankShotPoint
    {
        public double X, Z, D;
        public BankShotPoint(double x, double z, double d) { X = x; Z = z; D = d; }
    }

    /// <summary>
    /// One wall contact: where, the distance flown to it, the wall cell, the face's outward normal
    /// (one of the four grid directions) and which contact it is, from 1.
    /// </summary>
    public struct BankShotBounce
    {
        public double X, Z, D;
        public int CellX, CellZ, NormalX, NormalZ, N;
    }

    /// <summary>
    /// The ricochet rule of the Bank Shot pistol, with no drawing and no map in it: the port of
    /// Tools/VfxLab/web/sketches/lib/bank-shot-path.js, in double precision so it walks the same
    /// path as the sketch (Tests/BankShot checks it against the sketch's three layouts).
    ///
    /// Cells are integers; a cell covers [x - .5, x + .5] x [z - .5, z + .5], so a game cell's
    /// centre is its index plus 0.5 in map space and callers shift by that (BankShotMap). The bullet
    /// is a point flying level in a straight line in steps of 0.01 cells. At each step:
    ///   1. it stops at a pawn when a pawn stands in the cell the step reaches and the step is
    ///      within <see cref="BodyRadius"/> of that cell's centre;
    ///   2. it meets a wall when the step reaches a wall cell. The face it crossed is the x face or
    ///      the z face of that cell, whichever plane the ray from the last point reaches first; a
    ///      face is only a candidate when the last point was outside the cell on that axis. The
    ///      bullet moves to the face, and the matching component of its direction is negated. The
    ///      contact after <c>maxBounces</c> bounces is not a bounce: the bullet embeds there;
    ///   3. <c>stop</c> (the map's edge in game) ends the flight where it is;
    ///   4. after <c>range</c> cells of flight it is spent.
    /// </summary>
    public sealed class BankShotPath
    {
        /// <summary>A pawn is hit by a step this close to the centre of the cell it stands in.</summary>
        public const double BodyRadius = 0.38;
        private const double Step = 0.01;

        public readonly List<BankShotPoint> Points = new List<BankShotPoint>();
        public readonly List<BankShotBounce> Bounces = new List<BankShotBounce>();
        public BankShotEnd End;
        /// <summary>Where it ended. For an embed, also the wall cell and face normal in <see cref="Embed"/>.</summary>
        public BankShotPoint EndPoint;
        public BankShotBounce Embed;
        /// <summary>For a hit, the cell of the pawn it stopped at.</summary>
        public int HitCellX, HitCellZ;
        public double Length;

        /// <summary>JavaScript's Math.round: halves go up.</summary>
        public static int Round(double v) => (int)Math.Floor(v + 0.5);

        /// <summary>
        /// Flies from (<paramref name="x"/>, <paramref name="z"/>) along <paramref name="aimDegrees"/>
        /// (0 east, 90 north). <paramref name="pawn"/> and <paramref name="stop"/> may be null.
        /// </summary>
        public static BankShotPath Trace(Func<int, int, bool> wall, Func<int, int, bool> pawn, double x, double z, double aimDegrees,
            int maxBounces, double range, Func<int, int, bool> stop = null)
        {
            var path = new BankShotPath();
            double dx = Math.Cos(aimDegrees * Math.PI / 180), dz = Math.Sin(aimDegrees * Math.PI / 180), d = 0;
            path.Points.Add(new BankShotPoint(x, z, 0));
            bool ended = false;
            while (d < range)
            {
                double nx = x + dx * Step, nz = z + dz * Step;
                int cx = Round(nx), cz = Round(nz);
                if (stop != null && stop(cx, cz)) break;
                if (pawn != null && pawn(cx, cz) && Hypot(nx - cx, nz - cz) < BodyRadius)
                {
                    path.End = BankShotEnd.Hit;
                    path.EndPoint = new BankShotPoint(nx, nz, d + Step);
                    path.HitCellX = cx;
                    path.HitCellZ = cz;
                    ended = true;
                    break;
                }
                if (wall(cx, cz))
                {
                    // Which face did the ray cross first: the plane x = cell.x -/+ .5 or z = cell.z -/+ .5?
                    double px = cx - Math.Sign(dx) * 0.5, pz = cz - Math.Sign(dz) * 0.5;
                    double tx = dx != 0 && Round(x) != cx ? (px - x) / dx : double.PositiveInfinity;
                    double tz = dz != 0 && Round(z) != cz ? (pz - z) / dz : double.PositiveInfinity;
                    bool onX = tx <= tz;
                    double t = Math.Max(0, Math.Min(onX ? tx : tz, Step));
                    double bx = x + dx * t, bz = z + dz * t;
                    var contact = new BankShotBounce
                    {
                        CellX = cx, CellZ = cz,
                        NormalX = onX ? -Math.Sign(dx) : 0, NormalZ = onX ? 0 : -Math.Sign(dz),
                    };
                    d += t; x = bx; z = bz;
                    contact.X = x; contact.Z = z; contact.D = d;
                    if (path.Bounces.Count >= maxBounces)
                    {
                        contact.N = path.Bounces.Count + 1;
                        path.End = BankShotEnd.Embed;
                        path.EndPoint = new BankShotPoint(x, z, d);
                        path.Embed = contact;
                        ended = true;
                        break;
                    }
                    contact.N = path.Bounces.Count + 1;
                    path.Bounces.Add(contact);
                    path.Points.Add(new BankShotPoint(x, z, d));
                    if (onX) dx = -dx; else dz = -dz;
                    continue;
                }
                x = nx; z = nz; d += Step;
            }
            if (!ended)
            {
                path.End = BankShotEnd.Range;
                path.EndPoint = new BankShotPoint(x, z, d);
            }
            path.Points.Add(path.EndPoint);
            path.Length = path.EndPoint.D;
            return path;
        }

        /// <summary>The point at distance <paramref name="d"/> along the flight and the unit direction there.</summary>
        public void Along(double d, out double x, out double z, out double dx, out double dz)
        {
            for (int i = 1; i < Points.Count; i++)
            {
                if (d <= Points[i].D || i == Points.Count - 1)
                {
                    BankShotPoint a = Points[i - 1], b = Points[i];
                    double run = b.D - a.D;
                    if (run == 0) run = 1;
                    double u = Math.Max(0, Math.Min(1, (d - a.D) / run));
                    double ex = b.X - a.X, ez = b.Z - a.Z, len = Hypot(ex, ez);
                    if (len == 0) len = 1;
                    x = a.X + ex * u; z = a.Z + ez * u; dx = ex / len; dz = ez / len;
                    return;
                }
            }
            x = Points[0].X; z = Points[0].Z; dx = 1; dz = 0;
        }

        /// <summary>How many bounces lie at or before distance <paramref name="d"/>.</summary>
        public int BouncesBy(double d)
        {
            int n = 0;
            for (int i = 0; i < Bounces.Count; i++) if (Bounces[i].D <= d) n++;
            return n;
        }

        private static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);

        // ---- the sketch's stand-in layouts, which the previews play -------------------------------

        public enum Scene { Corner, Corridor, Room }

        /// <summary>One stand-in layout: caster and target cells, the aim, and wall runs (inclusive cell lines).</summary>
        public sealed class Layout
        {
            public int CasterX, CasterZ, EnemyX, EnemyZ;
            public double Aim;
            public int[][] Runs;
            private HashSet<long> cells;

            /// <summary>The wall cells of the runs, each once.</summary>
            public HashSet<long> Cells
            {
                get
                {
                    if (cells != null) return cells;
                    cells = new HashSet<long>();
                    foreach (int[] r in Runs)
                    {
                        int n = Math.Max(Math.Abs(r[2] - r[0]), Math.Abs(r[3] - r[1]));
                        for (int i = 0; i <= n; i++) cells.Add(Key(r[0] + Math.Sign(r[2] - r[0]) * i, r[1] + Math.Sign(r[3] - r[1]) * i));
                    }
                    return cells;
                }
            }

            public bool Wall(int x, int z) => Cells.Contains(Key(x, z));
            public bool Enemy(int x, int z) => x == EnemyX && z == EnemyZ;

            /// <summary>The sketch's shot: from the muzzle, <paramref name="muzzleAlong"/> from the caster along the aim.</summary>
            public BankShotPath Shot(double aimOffset, double muzzleAlong, int maxBounces, double range)
            {
                double aim = Aim + aimOffset, r = aim * (Math.PI / 180);
                return Trace(Wall, Enemy, CasterX + Math.Cos(r) * muzzleAlong, CasterZ + Math.Sin(r) * muzzleAlong, aim, maxBounces, range);
            }
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        // Cells are relative to the scene cell. Runs are x0, z0, x1, z1.
        public static readonly Layout Corner = new Layout
        {
            // The enemy stands east of a wall the caster cannot see past. One bounce off the wall to the north.
            CasterX = -4, CasterZ = -2, EnemyX = 3, EnemyZ = -2, Aim = 45,
            Runs = new[] { new[] { 0, -6, 0, -1 }, new[] { -6, 2, 4, 2 } },
        };

        public static readonly Layout Corridor = new Layout
        {
            // Two staggered pillars; two bounces, south wall then north wall, bring the bullet round the first.
            CasterX = -5, CasterZ = -1, EnemyX = 3, EnemyZ = 1, Aim = -27,
            Runs = new[] { new[] { -6, 2, 8, 2 }, new[] { -6, -2, 8, -2 }, new[] { 1, -1, 1, 0 }, new[] { 4, 0, 4, 1 } },
        };

        public static readonly Layout Room = new Layout
        {
            // A room with one door; the bullet goes in through the door and walks three walls to the enemy.
            CasterX = -5, CasterZ = -2, EnemyX = 1, EnemyZ = -1, Aim = 25.4,
            Runs = new[] { new[] { 0, -3, 6, -3 }, new[] { 0, 3, 6, 3 }, new[] { 0, -2, 0, -1 }, new[] { 0, 1, 0, 2 }, new[] { 6, -2, 6, 2 } },
        };

        public static Layout For(Scene scene) => scene == Scene.Corridor ? Corridor : scene == Scene.Room ? Room : Corner;
    }
}
