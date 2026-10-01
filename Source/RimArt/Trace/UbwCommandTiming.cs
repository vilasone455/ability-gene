using System;
using static RimArt.UbwBlade;

namespace RimArt
{
    /// <summary>
    /// One sword's flight in Unlimited Blade Works' commands, from the moment it leaves: turning onto its line, flying
    /// point first, then either sticking in the ground and quivering (Full Open, Pin) or stopping where it meets a shot
    /// (Intercept). Times are seconds on the command's clock; places are cells from the world's middle corner, y up.
    /// </summary>
    public struct UbwSwordShot
    {
        /// <summary>When it starts to turn onto its line, how long that takes, and how long it then flies.</summary>
        public double Launch, Lift, Fly;
        /// <summary>A stuck sword's lean (degrees) toward <see cref="Toward"/>, and the share of its length driven in.</summary>
        public double Lean, Buried;
        public UbwV3 Start, Hit;
        public UbwXZ Dir, Land, Toward;
        /// <summary>It meets a shot at <see cref="Hit"/> and stops there (Intercept) instead of sticking in the ground.</summary>
        public bool Meet;

        public double Arrive => Launch + Lift + Fly;
    }

    /// <summary>
    /// The times, sizes and poses of the commands' pictures (Tools/VfxLab/web/sketches/trace-ubw-world-commands.js):
    /// a sword pulled out turns about its middle from upright to flat and hovers 1 cell up aimed at its target; a fired
    /// one flies point first and sticks leaning back in; a pinning one goes in 60 % deep leaning 15 degrees out; Draw's
    /// spins flat; Arm's slides up and arcs to the hand; Intercept's rises and meets the shot. Geometry only, no drawing
    /// (<see cref="UbwCommandGraphics"/> draws). Gameplay numbers (speeds, counts, damage) are <c>UbwRules</c>'s.
    /// </summary>
    public static class UbwCommandTiming
    {
        /// <summary>From the order to the first sword leaving the ground (the sketch's Order 0.40 to Launch 0.50).</summary>
        public const double Launch = 0.1;
        /// <summary>A sword pulled out rises until its point is this far off the floor.</summary>
        public const double Clear = 0.25;
        /// <summary>Full Open: pulled clear in PullUp, turned flat in TurnTime, then hovering Hover up, bobbing Bob, aimed where the target was TrackLag ago.</summary>
        public const double PullUp = 0.2, TurnTime = 0.25, Hover = 1, Bob = 0.04, TrackLag = 0.15;
        /// <summary>Full Open: a fired sword turns onto its line in VolleyLift.</summary>
        public const double VolleyLift = 0.05;
        /// <summary>Where a hit lands on a pawn, in cells up.</summary>
        public const double HitHeight = 0.35;
        /// <summary>A stuck sword: it settles in Settle, quivers QuiverHz times a second up to Quiver degrees, and leans StickLean back toward the target.</summary>
        public const double Settle = 0.1, QuiverHz = 8, Quiver = 9, StickLean = 25, StickBuried = 0.2;
        /// <summary>Full Open: a stuck sword is drawn on its own this long after it lands, then stands in the baked field again.</summary>
        public const double StandAfter = 0.6;
        /// <summary>Full Open: a sword lands this many cells past the target, and up to half of LandSide to either side.</summary>
        public const double LandPastMin = 1, LandPastMax = 2, LandSide = 0.8;
        /// <summary>Full Open cancelled: a hovering sword drops back into its hole in this long.</summary>
        public const double DropTime = 0.25;

        /// <summary>Pin: lifted in LiftTime, flying PinHeight up; the pinned pawn falls in FallTime and the other swords arrive PinGap apart after it.</summary>
        public const double LiftTime = 0.18, PinHeight = 0.25, FallTime = 0.3, PinGap = 0.1;
        /// <summary>Pin: driven in PinDepth of the length, leaning PinLean out from the body; the pinned pawn jerks every Jerk seconds.</summary>
        public const double PinDepth = 0.6, PinLean = 15, Jerk = 0.9;
        /// <summary>Pin: where the swords go in the lying body's frame, [along from the feet toward the head, across to its left]: the two trouser legs, then the two sleeves.</summary>
        public static readonly UbwXZ[] Pins = { new UbwXZ(-0.05, 0.15), new UbwXZ(-0.05, -0.15), new UbwXZ(0.5, 0.31), new UbwXZ(0.5, -0.31) };
        /// <summary>Pin: a lying pawn's feet are this far from its middle along its body.</summary>
        public const double FeetBack = 0.36;

        /// <summary>Draw: torn out in TearTime, turned flat in TurnFlat, spinning DrawSpin turns a second at DrawHeight, landing at hand height.</summary>
        public const double TearTime = 0.1, TurnFlat = 0.06, DrawSpin = 5, DrawHeight = 0.45, HandHeight = 0.3;
        /// <summary>The weapon hand from a pawn's middle, as the lab's stand-in holds a copy.</summary>
        public static readonly UbwXZ Hand = new UbwXZ(0.24, 0.02);
        /// <summary>Draw: the two fainter copies behind the spinning sword, (seconds behind, alpha).</summary>
        public static readonly double[,] Blur = { { 0.024, 0.14 }, { 0.012, 0.32 } };

        /// <summary>Arm: slides up out of its hole in PullTime, arcs to the hand in ArcTime; held at HeldAngle degrees.</summary>
        public const double PullTime = 0.25, ArcTime = 0.4, HeldAngle = 55;

        /// <summary>Intercept: a sword turns onto its line in InterceptLift; after the meeting it sparks for Spark and breaks into light over Shatter.</summary>
        public const double InterceptLift = 0.08, Spark = 0.14, Shatter = 0.35;
        /// <summary>The floor breaking where a sword comes up: crumbs and a puff, this long.</summary>
        public const double BreakOut = 0.55;

        public static double Smooth(double t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }

        public static double Clamp01(double t) => t < 0 ? 0 : t > 1 ? 1 : t;

        private static UbwV3 Mix(UbwV3 a, UbwV3 b, double u) => new UbwV3(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u, a.Z + (b.Z - a.Z) * u);

        /// <summary>The unit direction on the floor from a to b and the distance.</summary>
        public static UbwXZ Toward(double ax, double az, double bx, double bz, out double d)
        {
            double dx = bx - ax, dz = bz - az;
            d = Math.Sqrt(dx * dx + dz * dz);
            double l = d > 1e-9 ? d : 1;
            return new UbwXZ(dx / l, dz / l);
        }

        /// <summary>
        /// Full Open: a sword <paramref name="u"/> seconds after it began to lift: pulled straight up until its point is
        /// <see cref="Clear"/> of the floor, turned flat about its middle, then hovering <see cref="Hover"/> up over its hole,
        /// aimed at <paramref name="foe"/>, bobbing. <paramref name="up"/>: still partly in the ground; <paramref name="aimed"/>:
        /// seconds since it was aimed (negative before).
        /// </summary>
        public static UbwPose Gathered(UbwSword sw, double u, UbwXZ foe, out bool up, out double aimed)
        {
            UbwPose from = sw.Pose;
            double rise = Clear - from.Tip.Y;
            if (u < PullUp)
            {
                up = true;
                aimed = -1;
                return Raised(from, rise * Smooth(u / PullUp));
            }
            UbwPose lifted = Raised(from, rise);
            UbwV3 m = Middle(lifted);
            double settle = Smooth((u - PullUp) / TurnTime);
            var centre = new UbwV3(m.X, m.Y + (Hover - m.Y) * settle + Bob * settle * Math.Sin(u * 7 + sw.Seed), m.Z);
            UbwXZ dir = Toward(centre.X, centre.Z, foe.X, foe.Z, out double d);
            UbwPose aim = FlatAt(sw.W, sw.Size, centre, dir, Math.Min(30, Math.Atan2(centre.Y - HitHeight, d) / D2R));
            up = false;
            aimed = u - PullUp - TurnTime;
            return settle < 1 ? TurnAbout(lifted, aim, settle) : aim;
        }

        /// <summary>
        /// Where a flying sword is at <paramref name="s"/>: turning onto its line from <paramref name="from"/>, flying point
        /// first, then either stopped at the meeting point (<paramref name="age"/> since) or dropping into the ground and
        /// quivering (<paramref name="age"/> since it landed; <paramref name="wobble"/> adds to its lean).
        /// </summary>
        public static UbwPose Flight(in UbwSwordShot j, UbwPose from, double size, double s, double wobble, out bool air, out double age)
        {
            UbwWeapon w = from.W;
            double t1 = j.Launch + j.Lift, t2 = t1 + j.Fly;
            age = s - t2;
            air = true;
            if (s < t1) return Blend(from, Flying(w, size, j.Start, j.Dir), Smooth((s - j.Launch) / j.Lift));
            if (s < t2) return Flying(w, size, Mix(j.Start, j.Hit, j.Fly > 0 ? (s - t1) / j.Fly : 1), j.Dir);
            if (j.Meet) return Flying(w, size, j.Hit, j.Dir);
            double settle = Smooth(age / Settle);
            double lean = j.Lean + wobble + Quiver * Math.Exp(-age / 0.22) * Math.Sin(age * QuiverHz * Math.PI * 2) * settle;
            UbwPose final = Stuck(w, size, j.Land, j.Toward, lean, j.Buried * from.L * settle);
            air = settle < 0.5;
            return settle < 1 ? Blend(Flying(w, size, j.Hit, j.Dir), final, settle) : final;
        }

        /// <summary>
        /// Draw: <paramref name="s"/> seconds after the sword began to tear out: torn straight up, turned flat and spinning
        /// along the line from <paramref name="start"/> to <paramref name="end"/>, <paramref name="share"/> of the way.
        /// </summary>
        public static UbwPose Drawn(UbwSword sw, double s, UbwV3 start, UbwV3 end, double share, out bool up)
        {
            UbwPose from = sw.Pose;
            double rise = Clear - from.Tip.Y;
            up = s < TearTime;
            if (up) return Raised(from, rise * Smooth(s / TearTime));
            UbwPose spin = Spinning(sw, start, end, share, s - TearTime);
            double u = (s - TearTime) / TurnFlat;
            return u < 1 ? TurnAbout(Raised(from, rise), spin, Smooth(u)) : spin;
        }

        /// <summary>Draw's sword lying flat <paramref name="share"/> of the way along its line, <paramref name="flying"/> seconds into its spin.</summary>
        public static UbwPose Spinning(UbwSword sw, UbwV3 start, UbwV3 end, double share, double flying)
        {
            double a = (DrawSpin * 360 * flying + sw.Seed * 37) * D2R;
            return FlatAt(sw.W, sw.Size, Mix(start, end, Clamp01(share)), new UbwXZ(Math.Cos(a), Math.Sin(a)));
        }

        /// <summary>
        /// Arm: <paramref name="age"/> seconds after the sword began to move: slid up out of its hole along its own axis,
        /// then arcing half a cell high to the hand of the pawn whose middle is at <paramref name="pawn"/>.
        /// <paramref name="held"/> once it is there.
        /// </summary>
        public static UbwPose Armed(UbwSword sw, double age, UbwXZ pawn, out bool held)
        {
            UbwPose from = sw.Pose;
            double reach = from.L * 0.3 + 0.15;
            UbwPose lifted = Pose(from.W, from.Scale, UbwV3.Plus(from.Tip, from.A, reach), from.A, from.B);
            UbwPose inHand = HeldCopy(from.W, sw.Size * 0.85, pawn, HeldAngle);
            held = age >= PullTime + ArcTime;
            if (age < PullTime) return Blend(from, lifted, Smooth(age / PullTime));
            if (held) return inHand;
            double u = (age - PullTime) / ArcTime;
            UbwPose b = Blend(lifted, inHand, Smooth(u));
            return Pose(b.W, b.Scale, new UbwV3(b.Tip.X, b.Tip.Y + Math.Sin(u * Math.PI) * 0.5, b.Tip.Z), b.A, b.B);
        }

        /// <summary>Pin: where pin <paramref name="k"/> goes for a body lying with its feet at <paramref name="feet"/> and its head along <paramref name="f"/>.</summary>
        public static UbwXZ PinPoint(UbwXZ feet, UbwXZ f, int k)
        {
            UbwXZ q = Pins[k % Pins.Length];
            return new UbwXZ(feet.X + f.X * q.X - f.Z * q.Z, feet.Z + f.Z * q.X + f.X * q.Z);
        }

        /// <summary>Pin: the way pin <paramref name="k"/>'s sword leans, out from the body: a leg's back past the feet and a little out, a sleeve's straight out.</summary>
        public static UbwXZ PinOutward(UbwXZ f, int k)
        {
            UbwXZ q = Pins[k % Pins.Length];
            double side = Math.Sign(q.Z);
            var across = new UbwXZ(-f.Z * side, f.X * side);
            if (q.X >= 0.2) return across;
            return Toward(0, 0, -f.X + across.X * 0.6, -f.Z + across.Z * 0.6, out _);
        }
    }
}
