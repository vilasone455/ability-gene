using System;
using System.Collections.Generic;

namespace RimArt
{
    /// <summary>A plain 3D point or direction: y is height above the floor, not altitude. Doubles, as the lab's.</summary>
    public struct UbwV3
    {
        public double X, Y, Z;
        public UbwV3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static UbwV3 Plus(UbwV3 a, UbwV3 b, double f = 1) => new UbwV3(a.X + b.X * f, a.Y + b.Y * f, a.Z + b.Z * f);
        public static double Dot(UbwV3 a, UbwV3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static UbwV3 Cross(UbwV3 a, UbwV3 b) => new UbwV3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static UbwV3 Unit(UbwV3 a)
        {
            double l = Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
            if (l == 0) l = 1;
            return new UbwV3(a.X / l, a.Y / l, a.Z / l);
        }
        public static UbwV3 Neg(UbwV3 a) => new UbwV3(-a.X, -a.Y, -a.Z);
    }

    /// <summary>A texture point, v up.</summary>
    public struct UbwUV
    {
        public double U, V;
        public UbwUV(double u, double v) { U = u; V = v; }
    }

    /// <summary>A point on the floor or on the screen, in cells.</summary>
    public struct UbwXZ
    {
        public double X, Z;
        public UbwXZ(double x, double z) { X = x; Z = z; }
    }

    /// <summary>
    /// A blade as a plane: tip point, A from tip to pommel, B across, N out of the face toward the camera,
    /// Scale in cells per uv unit, L its length in cells. lib/trace.js pose().
    /// </summary>
    public sealed class UbwPose
    {
        public UbwWeapon W;
        public double Scale, L;
        public UbwV3 Tip, A, B, N;
    }

    /// <summary>The cut across a blade on the floor: centre, direction along it, the side facing the camera, half length.</summary>
    public struct UbwCut
    {
        public double X, Z, Half;
        public UbwXZ D, F;
    }

    /// <summary>
    /// The planted-blade geometry of the lab's lib/trace.js: a weapon's texture on a flat plane in 3D, cut
    /// by height planes, drawn with the kit's height rule (0.60 cells north per cell up) or cast along
    /// the sun. Doubles throughout, as the sketch's, so Tests/Ubw can check the poses against it. System
    /// only.
    /// </summary>
    public static class UbwBlade
    {
        public const double Lift = 0.60, D2R = Math.PI / 180.0;
        public static readonly UbwV3 Up = new UbwV3(0, 1, 0);
        /// <summary>Points along this direction land on the same pixel.</summary>
        public static readonly UbwV3 View = UbwV3.Unit(new UbwV3(0, 1, -Lift));
        public static readonly UbwUV[] Square = { new UbwUV(0, 0), new UbwUV(1, 0), new UbwUV(1, 1), new UbwUV(0, 1) };

        public static UbwPose Pose(UbwWeapon w, double scale, UbwV3 tip, UbwV3 a, UbwV3 across)
        {
            UbwV3 b = UbwV3.Unit(UbwV3.Plus(across, a, -UbwV3.Dot(across, a)));
            UbwV3 n = UbwV3.Unit(UbwV3.Cross(a, b));
            if (UbwV3.Dot(n, View) < 0) n = UbwV3.Neg(n);
            return new UbwPose { W = w, Scale = scale, Tip = tip, A = a, B = b, N = n, L = w.Length * scale };
        }

        /// <summary>The tip of a blade whose axis enters the ground at g with `buried` cells of it underground.</summary>
        public static UbwV3 TipUnder(UbwXZ g, UbwV3 a, double buried) => new UbwV3(g.X - a.X * buried, -a.Y * buried, g.Z - a.Z * buried);

        /// <summary>
        /// A blade standing in the ground at g: lean from upright toward dir (degrees, 0 east, 90 north), its
        /// flat side turned by turn degrees from east-west, sink of its length underground. raise 0 has the
        /// pommel at the floor, 1 is standing.
        /// </summary>
        public static UbwPose Upright(UbwWeapon w, double size, UbwXZ g, double lean, double dir, double turn, double sink, double raise = 1)
        {
            double scale = w.Image * size, L = w.Length * scale, l = lean * D2R, d = dir * D2R, t = turn * D2R;
            var a = new UbwV3(Math.Sin(l) * Math.Cos(d), Math.Cos(l), Math.Sin(l) * Math.Sin(d));
            return Pose(w, scale, TipUnder(g, a, sink * L + (1 - raise) * (1 - sink) * L), a, new UbwV3(Math.Cos(t), 0, Math.Sin(t)));
        }

        /// <summary>
        /// A blade in flight, point first along dir (a unit direction on the floor), flat side up, tip at the 3D
        /// point tip; the pommel rides pitch degrees above the tip. This is the texture as the game draws an
        /// item: seen from above.
        /// </summary>
        public static UbwPose Flying(UbwWeapon w, double size, UbwV3 tip, UbwXZ dir, double pitch = 8)
        {
            double p = pitch * D2R;
            var a = new UbwV3(-dir.X * Math.Cos(p), Math.Sin(p), -dir.Z * Math.Cos(p));
            return Pose(w, w.Image * size, tip, a, new UbwV3(-dir.Z, 0, dir.X));
        }

        /// <summary>Weapon w lying flat with its middle at the 3D point m, point along dir, the pommel pitch degrees above the point.</summary>
        public static UbwPose FlatAt(UbwWeapon w, double size, UbwV3 m, UbwXZ dir, double pitch = 0)
        {
            UbwPose f = Flying(w, size, m, dir, pitch);
            return Pose(w, f.Scale, UbwV3.Plus(m, f.A, -f.L / 2), f.A, f.B);
        }

        /// <summary>
        /// A copy in the hand of a stand-in pawn drawn at pos: flat, pointing angle degrees (0 east, 90 north), as
        /// the lab stands in for the game's carry pose; side 1 holds it in the east hand, -1 in the west one.
        /// </summary>
        public static UbwPose HeldCopy(UbwWeapon w, double size, UbwXZ pos, double angle, double side = 1)
        {
            double d = angle * D2R, reach = w.Length * w.Image * size * 0.62;
            var dir = new UbwXZ(Math.Cos(d), Math.Sin(d));
            return Flying(w, size, new UbwV3(pos.X + 0.24 * side + dir.X * reach, 0.3, pos.Z + 0.02 + dir.Z * reach), dir, 0);
        }

        /// <summary>Part way (u 0..1) from pose a to pose b of the same weapon: tip, axis and flat side blended.</summary>
        public static UbwPose Blend(UbwPose a, UbwPose b, double u)
        {
            UbwV3 Mix(UbwV3 p, UbwV3 q) => new UbwV3(p.X + (q.X - p.X) * u, p.Y + (q.Y - p.Y) * u, p.Z + (q.Z - p.Z) * u);
            UbwV3 across = UbwV3.Dot(a.B, b.B) < 0 ? UbwV3.Neg(b.B) : b.B;
            return Pose(a.W, a.Scale, Mix(a.Tip, b.Tip), UbwV3.Unit(Mix(a.A, b.A)), Mix(a.B, across));
        }

        /// <summary>
        /// Along weapon w's axis in uv, from its point (0) to its pommel (w.Length). Linear in (u, v), so
        /// Clip(poly, q => Along(w, q) - a) keeps exactly the part from the pommel down to a from the point.
        /// </summary>
        public static double Along(UbwWeapon w, UbwUV q) => (q.U - w.TipU) * w.AxisU + (q.V - w.TipV) * w.AxisV;

        /// <summary>Texture point q of blade b, in 3D.</summary>
        public static UbwV3 At3(UbwPose b, UbwUV q)
        {
            double du = q.U - b.W.TipU, dv = q.V - b.W.TipV;
            double s = (du * b.W.AxisU + dv * b.W.AxisV) * b.Scale, c = (du * b.W.AcrossU + dv * b.W.AcrossV) * b.Scale;
            return new UbwV3(b.Tip.X + b.A.X * s + b.B.X * c, b.Tip.Y + b.A.Y * s + b.B.Y * c, b.Tip.Z + b.A.Z * s + b.B.Z * c);
        }

        public static UbwV3 PommelOf(UbwPose b) => At3(b, new UbwUV(b.W.PommelU, b.W.PommelV));

        /// <summary>The polygon cut to where keep(q) >= 0. keep is linear in (u, v), so one cut is exact.</summary>
        public static List<UbwUV> Clip(IList<UbwUV> poly, Func<UbwUV, double> keep)
        {
            var result = new List<UbwUV>(poly.Count + 2);
            for (int i = 0; i < poly.Count; i++)
            {
                UbwUV a = poly[i], b = poly[(i + 1) % poly.Count];
                double fa = keep(a), fb = keep(b);
                if (fa >= 0) result.Add(a);
                if ((fa >= 0) != (fb >= 0))
                {
                    double t = fa / (fa - fb);
                    result.Add(new UbwUV(a.U + (b.U - a.U) * t, a.V + (b.V - a.V) * t));
                }
            }
            return result;
        }

        public static Func<UbwUV, double> HigherThan(UbwPose b, double h) => q => At3(b, q).Y - h;
        public static Func<UbwUV, double> LowerThan(UbwPose b, double h) => q => h - At3(b, q).Y;

        /// <summary>Where a 3D point is drawn: height goes north.</summary>
        public static UbwXZ OnScreen(UbwV3 q) => new UbwXZ(q.X, q.Z + q.Y * Lift);
        /// <summary>Where a 3D point's shadow falls, for a shadow vector per cell of height.</summary>
        public static UbwXZ AlongSun(UbwV3 q, UbwXZ sun) => new UbwXZ(q.X + sun.X * q.Y, q.Z + sun.Z * q.Y);

        /// <summary>The cut across the blade on the floor. sink is the share of the blade underground, which picks the width.</summary>
        public static UbwCut CutOf(UbwPose b, double sink)
        {
            UbwV3 d = UbwV3.Cross(b.N, Up);
            d = UbwV3.Unit(new UbwV3(d.X, 0, d.Z));
            if (UbwV3.Dot(d, b.B) < 0) d = UbwV3.Neg(d);
            int k = Math.Min(15, Math.Max(0, (int)Math.Floor(sink * 16)));
            double a = b.W.Width[k, 0] * b.Scale, c = b.W.Width[k, 1] * b.Scale, mid = (a + c) / 2;
            double gx = b.Tip.X + b.A.X * (-b.Tip.Y / b.A.Y), gz = b.Tip.Z + b.A.Z * (-b.Tip.Y / b.A.Y);
            var f = new UbwV3(-d.Z, 0, d.X);
            if (f.Z > 0) f = UbwV3.Neg(f);
            return new UbwCut { X = gx + d.X * mid, Z = gz + d.Z * mid, D = new UbwXZ(d.X, d.Z), F = new UbwXZ(f.X, f.Z), Half = (c - a) / 2 };
        }
    }
}
