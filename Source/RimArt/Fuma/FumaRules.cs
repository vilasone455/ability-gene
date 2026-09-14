using System;
using System.Collections.Generic;
using System.Linq;

namespace RimArt
{
    // Engine-independent geometry, shared by the cursor and the swept projectile.
    public static class FumaRules
    {
        public const float Range = 12f;
        public const int WarmupTicks = 48;
        public const int AnimationTicks = 72;
        public const int CooldownTicks = 240;

        public static int Damage(int priorHits) => Math.Max(8, (int)Math.Round(30 * Math.Pow(0.8, priorHits)));
        public static int Remaining(int readyTick, int now) => Math.Max(0, readyTick - now);

        public readonly struct Cell
        {
            public readonly int X, Z;
            // Corner neighbours block a diagonal but are not part of its damage corridor.
            public readonly bool Guard;
            public readonly double Entry;
            public readonly double Exit;
            public Cell(int x, int z, bool guard = false, double entry = 0, double exit = 1)
            { X = x; Z = z; Guard = guard; Entry = entry; Exit = exit; }
        }

        // Clip the original ray to this tick's progress, rather than retracing rounded float
        // positions. Otherwise a diagonal can briefly wander into an adjacent damage cell.
        public static IEnumerable<Cell> Sweep(double x0, double z0, double x1, double z1, double from, double to)
        {
            Cell[] path = Trace(x0, z0, x1, z1).ToArray();
            for (int i = 0; i < path.Length; i++)
            {
                Cell cell = path[i];
                if (cell.Entry > to) yield break;
                if (cell.Guard)
                {
                    if (cell.Entry > from || from == 0) yield return cell;
                    continue;
                }
                double exit = i + 1 < path.Length ? path[i + 1].Entry : 1;
                if (exit <= from && from > 0) continue;
                yield return new Cell(cell.X, cell.Z, false, Math.Max(cell.Entry, from), Math.Min(exit, to));
            }
        }

        public static IEnumerable<Cell> Trace(double x0, double z0, double x1, double z1)
        {
            int x = (int)Math.Floor(x0), z = (int)Math.Floor(z0);
            int endX = (int)Math.Floor(x1), endZ = (int)Math.Floor(z1);
            yield return new Cell(x, z);
            double dx = x1 - x0, dz = z1 - z0;
            int sx = Math.Sign(dx), sz = Math.Sign(dz);
            double stepX = sx == 0 ? double.PositiveInfinity : Math.Abs(1 / dx);
            double stepZ = sz == 0 ? double.PositiveInfinity : Math.Abs(1 / dz);
            double tx = sx == 0 ? double.PositiveInfinity : ((sx > 0 ? x + 1 : x) - x0) / dx;
            double tz = sz == 0 ? double.PositiveInfinity : ((sz > 0 ? z + 1 : z) - z0) / dz;
            while (x != endX || z != endZ)
            {
                // A negative-going endpoint on a grid boundary belongs to its floor cell.
                // Do not step past it while the other axis finishes (mixed-sign diagonals).
                if (x == endX) tx = double.PositiveInfinity;
                if (z == endZ) tz = double.PositiveInfinity;
                double entry = Math.Min(tx, tz);
                if (Math.Abs(tx - tz) < 1e-9)
                {
                    yield return new Cell(x + sx, z, true, entry);
                    yield return new Cell(x, z + sz, true, entry);
                    x += sx; z += sz; tx += stepX; tz += stepZ;
                }
                else if (tx < tz) { x += sx; tx += stepX; }
                else { z += sz; tz += stepZ; }
                yield return new Cell(x, z, false, entry);
            }
        }
    }
}
