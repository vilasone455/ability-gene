using System;
using System.Collections.Generic;
using System.Linq;

namespace RimArt
{
    /// <summary>One standing sword of the world, in cells from the caster.</summary>
    public sealed class UbwSword
    {
        public int Seed;
        public double X, Z, D, Lean, Dir, Turn, Sink, Size, Lift;
        /// <summary>Past the map edge: drawn without lips or cracks, and hazed.</summary>
        public bool Far;
        public UbwWeapon W;
        /// <summary>Its standing pose, the top of its pommel and its cut on the floor, all relative to the caster.</summary>
        public UbwPose Pose;
        public double Top;
        public UbwCut Cut;
    }

    /// <summary>The world's look, as the lab's Look (lib/ubw-pocket.js): the user passed it on 2026-09-24.</summary>
    public struct UbwFieldSettings
    {
        /// <summary>Swords per cell on the map; the hill of swords' radius; how far the field goes on past the map edge; sword size (x image); the most lean, in degrees.</summary>
        public double Density, Hill, Beyond, Size, Lean;

        public UbwFieldSettings(double density, double hill, double beyond, double size, double lean)
        {
            Density = density; Hill = hill; Beyond = beyond; Size = size; Lean = lean;
        }
    }

    /// <summary>
    /// The standing field of Unlimited Blade Works: every sword of the world laid out once, relative to
    /// the caster, north first. The port of makeField, standingPose and the field's poses in
    /// Tools/VfxLab/web/sketches/lib/ubw-pocket.js, in double precision with the lab's sine hash, so the
    /// same settings and landing spots give the same swords in game and in the lab (Tests/Ubw checks it).
    /// System only.
    /// </summary>
    public static class UbwField
    {
        /// <summary>The pocket map is 40 x 40 with the caster in the middle.</summary>
        public const double MapHalf = 20;
        /// <summary>A jittered grid this many cells apart keeps the field even.</summary>
        public const double Step = 1.2;
        public static readonly UbwFieldSettings Look = new UbwFieldSettings(0.24, 5.5, 16, 1.3, 22);
        /// <summary>The rest of the world's look: the twilight light and the gear shadows' opacity.</summary>
        public const double Twilight = 0.8, GearShadow = 0.28;

        /// <summary>0 to 1, fixed per index: the lab's rand (six-paths-impact.js), in double precision as it is there.</summary>
        public static double Rand(int i)
        {
            double n = Math.Sin(i * 127.1 + 17) * 43758.5453;
            return n - Math.Floor(n);
        }

        /// <summary>
        /// Every sword of the world, relative to the caster, north first. Density is swords per cell on the
        /// map, doubling toward the top of the hill, 0.6 of it past the map edge. On the hill they lean out,
        /// down its slope. Every sword is its weapon's own size, give or take 10 %. keep: the landing spots;
        /// no sword is drawn over one. weapons: the set the mix picks from.
        /// </summary>
        public static List<UbwSword> Make(UbwFieldSettings o, IList<UbwXZ> keep, UbwWeapon[] weapons)
        {
            double reach = MapHalf + o.Beyond;
            var list = new List<UbwSword>();
            int n = 0;
            for (double gz = -reach; gz <= reach + 1e-6; gz += Step)
                for (double gx = -reach; gx <= reach + 1e-6; gx += Step)
                {
                    int seed = ++n;
                    double x = gx + (Rand(seed * 3 + 1) - .5) * Step * .9, z = gz + (Rand(seed * 5 + 2) - .5) * Step * .9;
                    double d = Math.Sqrt(x * x + z * z);
                    bool far = Math.Max(Math.Abs(x), Math.Abs(z)) > MapHalf, onHill = d < o.Hill;
                    double want = o.Density * (far ? .6 : 1);
                    if (onHill) want *= 1 + 2 * (1 - d / o.Hill);
                    if (Rand(seed * 7 + 3) > want * Step * Step || d < 1) continue;
                    var sw = new UbwSword
                    {
                        Seed = seed, X = x, Z = z, D = d, Far = far,
                        W = UbwWeapons.Pick(weapons, (int)Math.Floor(Rand(seed * 11 + 4) * UbwWeapons.Mix.Length)),
                        Lean = o.Lean * (onHill ? .45 + .55 * Rand(seed * 13) : Rand(seed * 13)),
                        Dir = onHill ? Math.Atan2(z, x) / UbwBlade.D2R + (Rand(seed * 17) - .5) * 60 : Rand(seed * 17) * 360,
                        Turn = (Rand(seed * 19) - .5) * 60, Sink = .16 + Rand(seed * 23) * .12, Size = o.Size * (.9 + .2 * Rand(seed * 29)),
                        Lift = 0,
                    };
                    if (ClearOf(ScreenBox(sw, .3), keep)) list.Add(sw);
                }
            // North first, by the screen foot. OrderBy is stable, as the sketch's sort is.
            list = list.OrderByDescending(sw => sw.Z + sw.Lift).ToList();
            foreach (UbwSword sw in list)
            {
                sw.Pose = UbwBlade.Upright(sw.W, sw.Size, new UbwXZ(sw.X, sw.Z + sw.Lift), sw.Lean, sw.Dir, sw.Turn, sw.Sink);
                sw.Top = UbwBlade.PommelOf(sw.Pose).Y + .05;
                sw.Cut = UbwBlade.CutOf(sw.Pose, sw.Sink);
            }
            return list;
        }

        /// <summary>
        /// The part of the screen a standing sword covers: from its foot to its pommel drawn with the height
        /// rule, margin wider either side. A pawn draws over every sword, so a sword whose blade rises through
        /// a landing spot would look run through the pawn standing there.
        /// </summary>
        private static (double x0, double x1, double z0, double z1) ScreenBox(UbwSword sw, double margin)
        {
            double L = sw.W.Length * sw.W.Image * sw.Size * (1 - sw.Sink), l = sw.Lean * UbwBlade.D2R, d = sw.Dir * UbwBlade.D2R, sz = sw.Z + sw.Lift;
            double px = sw.X + Math.Sin(l) * Math.Cos(d) * L, pz = sz + Math.Sin(l) * Math.Sin(d) * L + Math.Cos(l) * L * UbwBlade.Lift;
            return (Math.Min(sw.X, px) - margin, Math.Max(sw.X, px) + margin, Math.Min(sz, pz) - .15, Math.Max(sz, pz) + .15);
        }

        /// <summary>A pawn on the screen: its shadow's reach below its feet to the top of its head.</summary>
        private static bool ClearOf((double x0, double x1, double z0, double z1) box, IList<UbwXZ> keep)
        {
            for (int i = 0; i < keep.Count; i++)
            {
                double bx0 = keep[i].X - .32, bx1 = keep[i].X + .32, bz0 = keep[i].Z - .2, bz1 = keep[i].Z + .9;
                if (box.x0 < bx1 && bx0 < box.x1 && box.z0 < bz1 && bz0 < box.z1) return false;
            }
            return true;
        }
    }
}
