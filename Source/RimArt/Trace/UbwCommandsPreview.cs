using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using T = RimArt.UbwCommandTiming;

namespace RimArt
{
    internal enum UbwCommandPreview { FullOpen, Pin, Draw, Arm, Intercept }

    /// <summary>
    /// The commands' gameplay numbers the previews play with. In game they come from <c>UbwRules</c> on the ability def
    /// (Kit/UbwCommands.cs sets <see cref="Provider"/>); the lab's recorder links this folder without the defs and plays
    /// the defaults here, the XML's values of 2026-10-01.
    /// </summary>
    public sealed class UbwCommandNumbers
    {
        public float fullOpenSwordSeconds = 0.1f, fullOpenVolleySeconds = 0.3f, fullOpenSpeed = 16f, pinSpeed = 16f, drawSpeed = 22f;
        public float interceptRiseSeconds = 0.1f, interceptSpeed = 20f, interceptClearOfGun = 1.2f, interceptShortOfTarget = 1.5f;
        public int pinSwords = 4;

        internal static Func<UbwCommandNumbers> Provider;

        public static UbwCommandNumbers Current => Provider?.Invoke() ?? new UbwCommandNumbers();
    }

    /// <summary>
    /// The commands' previews: the world v4 standing over the map on screen (its fire already run out), and one command
    /// played on it with the sketch's stand-ins at the sketch's places and times (trace-ubw-world-commands.js plan()):
    /// Full Open on a raider walking north 3.4 cells east (16 swords, released 0.35 s after the last is aimed), Pin on a
    /// raider walking in from the north-west, Draw of a big sword about 5 cells out past two raiders on its lane and one
    /// 1.25 cells off it, Arm by an ally south-west, Intercept of two shots and a rocket from a raider 9 cells out. The
    /// speeds, counts and the meeting rule are the XML's (<see cref="UbwCommandNumbers"/>); the stand-ins' walks, the shots' speeds and
    /// the order times are the sketch's. Nobody is moved and nothing is hit. The swords a command takes leave their
    /// holes in the field, which is baked in rows here as in the world.
    /// </summary>
    internal static class UbwCommandsPreview
    {
        private const double Order = 0.4, Launch = Order + T.Launch, Hold = 0.35;
        private static readonly UbwXZ Mark = new UbwXZ(3.4, 0.1), MarkWalk = new UbwXZ(0, 0.5), PinFrom = new UbwXZ(-3.4, 2.5), Helper = new UbwXZ(-1.9, -1.7);
        private const int Gathered = 16;
        private const double PinWalk = 0.8, DrawBearing = -25, ShooterAngle = -20, ShooterAt = 9, BulletSpeed = 24, RocketSpeed = 11;
        private static readonly double[,] DrawFoes = { { 0.42, 0.25 }, { 0.68, -0.4 }, { 0.3, 1.25 } };
        private static readonly (double t, bool rocket)[] Shots = { (0.6, false), (1.4, false), (2.2, true) };
        private static readonly Color Enemy = new Color(0.55f, 0.38f, 0.27f), Ally = new Color(0.39f, 0.58f, 0.65f), Caster = new Color(0.55f, 0.3f, 0.24f),
            Skin = new Color(0.83f, 0.7f, 0.54f), Smoke = new Color(0.36f, 0.34f, 0.33f), Casing = new Color(0.18f, 0.18f, 0.2f), Hurt = new Color(1f, 0.25f, 0.2f);

        /// <summary>One sword of the command: when it leaves the ground and its flight.</summary>
        private sealed class Job
        {
            public UbwSword Sw;
            public double Leaves, Pull, Arrive;
            public UbwPose Hover;
            public UbwSwordShot Shot;
            public int Pin;
            public bool Rocket, Met;
            public double ShotAt, Speed, Meet;
            public UbwV3 End;
        }

        private sealed class Plan
        {
            public UbwCommandPreview Command;
            public List<UbwSword> Swords;
            public UbwFieldBake Bake;
            public UbwCrestWorld World;
            public Vector2 Sun;
            public float Strength;
            public readonly List<Job> Jobs = new List<Job>();
            public readonly List<UbwXZ> Foes = new List<UbwXZ>();
            public double End, Release, T1, Last, Down = double.PositiveInfinity, Caught, Reach;
            public UbwXZ F, Body, Target;
        }

        private static Plan plan;

        private static double Dist(UbwXZ a, UbwXZ b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
        private static UbwXZ Add(UbwXZ a, UbwXZ d, double f) => new UbwXZ(a.X + d.X * f, a.Z + d.Z * f);
        private static UbwXZ Home(UbwSword sw) => new UbwXZ(sw.X, sw.Z);

        /// <summary>In the ground and inside the map, as the rules' UbwFieldState.Usable.</summary>
        private static bool Usable(UbwSword sw) => !sw.Hole && !sw.Far;

        private static List<UbwSword> Nearest(List<UbwSword> all, UbwXZ q) =>
            all.Where(Usable).OrderBy(sw => Dist(Home(sw), q)).ToList();

        /// <summary>The preview's world: the v4 field made for these stand-in spots, so no sword stands over one.</summary>
        private static List<UbwSword> Field(IList<UbwXZ> keep) =>
            UbwField.Make(UbwField.CrestLook, keep, UbwGraphics.Set.Weapons, UbwTerrainGraphics.Ground().HeightAt);

        private static double LiftAt(UbwXZ p) => UbwTerrainGraphics.Ground().HeightAt(p.X, p.Z) * UbwBlade.Lift;

        public static float Duration(UbwCommandPreview command, Map map) => (float)PlanFor(command, map, true).End;

        private static Plan PlanFor(UbwCommandPreview command, Map map, bool fresh)
        {
            if (!fresh && plan != null && plan.Command == command) return plan;
            plan?.Bake?.Release();
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            sun = UbwCrestWorld.LowSun(sun);
            var p = new Plan { Command = command, Sun = sun, Strength = strength, World = UbwCrestWorld.For(sun) };
            UbwCommandNumbers rules = UbwCommandNumbers.Current;
            if (command == UbwCommandPreview.FullOpen) FullOpen(p, rules);
            else if (command == UbwCommandPreview.Pin) Pin(p, rules);
            else if (command == UbwCommandPreview.Draw) Draw(p, rules);
            else if (command == UbwCommandPreview.Arm) Arm(p);
            else Intercept(p, rules);
            p.Bake = UbwFieldBake.Banded(p.Swords, sun, UbwGraphics.Set);
            plan = p;
            return p;
        }

        // ---- the plans (the sketch's plan()) ---------------------------------------------------------------------------

        private static UbwXZ MarkAt(Plan p, double s)
        {
            double k = Math.Min(Math.Max(0, s), p.Release);
            return new UbwXZ(Mark.X + MarkWalk.X * k, Mark.Z + MarkWalk.Z * k);
        }

        private static void FullOpen(Plan p, UbwCommandNumbers rules)
        {
            var keep = new List<UbwXZ> { new UbwXZ(0, 0) };
            foreach (double z in new[] { 0, 0.7, 1.4, 2.1 }) { keep.Add(new UbwXZ(Mark.X, Mark.Z + z)); keep.Add(new UbwXZ(Mark.X + 0.5, Mark.Z + z)); }
            p.Swords = Field(keep);
            double every = rules.fullOpenSwordSeconds;
            p.Release = Launch + (Gathered - 1) * every + T.PullUp + T.TurnTime + Hold;
            List<UbwSword> picked = Nearest(p.Swords, MarkAt(p, Order)).Take(Gathered).ToList();
            int n = picked.Count;
            List<UbwSword> order = picked.OrderBy(sw => VfxMath.Rand(sw.Seed * 31)).ToList();
            UbwXZ foe = MarkAt(p, p.Release);
            for (int i = 0; i < n; i++)
            {
                UbwSword sw = picked[i];
                int k = order.IndexOf(sw);
                var j = new Job { Sw = sw, Pull = Launch + i * every, Leaves = Launch + i * every };
                double launch = p.Release + (n > 1 ? rules.fullOpenVolleySeconds * k / (n - 1) : 0);
                j.Hover = T.Gathered(sw, launch - j.Pull, MarkAt(p, launch - T.TrackLag), out _, out _);
                UbwV3 start = j.Hover.Tip;
                UbwXZ dir = T.Toward(start.X, start.Z, foe.X, foe.Z, out _);
                var hit = new UbwV3(foe.X - dir.X * 0.15, T.HitHeight, foe.Z - dir.Z * 0.15);
                double fly = Math.Sqrt(Math.Pow(hit.X - start.X, 2) + Math.Pow(hit.Y - start.Y, 2) + Math.Pow(hit.Z - start.Z, 2)) / rules.fullOpenSpeed;
                double past = T.LandPastMin + (T.LandPastMax - T.LandPastMin) * VfxMath.Rand(sw.Seed * 29), side = (VfxMath.Rand(sw.Seed * 23) - 0.5) * T.LandSide;
                var land = new UbwXZ(foe.X + dir.X * past - dir.Z * side, foe.Z + dir.Z * past + dir.X * side);
                j.Shot = new UbwSwordShot
                {
                    Launch = launch, Lift = T.VolleyLift, Fly = fly, Start = start, Hit = hit, Dir = dir, Toward = new UbwXZ(-dir.X, -dir.Z),
                    Land = new UbwXZ(land.X, land.Z + LiftAt(land)), Lean = T.StickLean, Buried = T.StickBuried,
                };
                j.Arrive = j.Shot.Arrive;
                p.Jobs.Add(j);
            }
            p.Last = p.Jobs.Max(j => j.Arrive);
            if (n >= 5) p.Down = p.Last + 0.05;
            p.End = p.Last + 1.6;
        }

        private static UbwXZ Walk(UbwXZ f, double s) => Add(PinFrom, f, PinWalk * Math.Max(0, s));

        /// <summary>A pinning sword flying in low to <paramref name="point"/>, arriving at <paramref name="arrive"/>.</summary>
        private static Job LowShot(UbwSword sw, UbwXZ point, double arrive, int pin, UbwXZ outward, double speed)
        {
            UbwXZ dir = T.Toward(sw.X, sw.Z, point.X, point.Z, out _);
            var start = new UbwV3(sw.X + dir.X * 0.2, T.PinHeight, sw.Z + dir.Z * 0.2);
            var hit = new UbwV3(point.X - dir.X * 0.05, T.PinHeight, point.Z - dir.Z * 0.05);
            double fly = Math.Sqrt(Math.Pow(hit.X - start.X, 2) + Math.Pow(hit.Z - start.Z, 2)) / speed, launch = arrive - fly - T.LiftTime;
            return new Job
            {
                Sw = sw, Leaves = launch, Arrive = arrive, Pin = pin,
                Shot = new UbwSwordShot { Launch = launch, Lift = T.LiftTime, Fly = fly, Start = start, Hit = hit, Dir = dir, Land = point, Toward = outward, Lean = T.PinLean, Buried = T.PinDepth },
            };
        }

        private static void Pin(Plan p, UbwCommandNumbers rules)
        {
            UbwXZ f = T.Toward(PinFrom.X, PinFrom.Z, 0, 0, out _), side = new UbwXZ(-f.Z, f.X);
            var keep = new List<UbwXZ> { new UbwXZ(0, 0) };
            foreach (double d in new[] { 0, 0.6, 1.1 }) keep.Add(Add(PinFrom, f, d));
            p.Swords = Field(keep);
            List<UbwSword> picked = Nearest(p.Swords, Walk(f, Order)).Take(rules.pinSwords).ToList();
            if (picked.Count == 0) { p.End = 4; return; }
            UbwXZ g0 = Home(picked[0]), at0 = Walk(f, Launch);
            int leg = (g0.X - at0.X) * side.X + (g0.Z - at0.Z) * side.Z >= 0 ? 0 : 1;
            double t1 = Launch + T.LiftTime + Dist(g0, at0) / rules.pinSpeed;
            Job first = null;
            for (int i = 0; i < 5; i++)
            {
                UbwXZ feet = Walk(f, t1);
                first = LowShot(picked[0], T.PinPoint(feet, f, leg), t1, leg, T.PinOutward(f, leg), rules.pinSpeed);
                t1 = Launch + T.LiftTime + first.Shot.Fly;
            }
            p.T1 = t1;
            p.F = Walk(f, t1);
            p.Body = f;
            p.Jobs.Add(first);
            // The other pins, each sword coming in from outside the body where it can (the best of the orders).
            List<int> rest = Enumerable.Range(0, T.Pins.Length).Where(k => k != leg).Take(picked.Count - 1).ToList();
            List<int> best = null;
            double bestScore = double.NegativeInfinity;
            foreach (List<int> perm in Permutations(rest))
            {
                double score = 0;
                for (int k = 0; k < perm.Count; k++)
                {
                    UbwXZ q = T.PinPoint(p.F, f, perm[k]), d = T.Toward(picked[k + 1].X, picked[k + 1].Z, q.X, q.Z, out _), o = T.PinOutward(f, perm[k]);
                    score -= d.X * o.X + d.Z * o.Z;
                }
                if (score > bestScore) { bestScore = score; best = perm; }
            }
            for (int k = 0; best != null && k < best.Count; k++)
                p.Jobs.Add(LowShot(picked[k + 1], T.PinPoint(p.F, f, best[k]), t1 + T.FallTime + 0.05 + T.PinGap * k, best[k], T.PinOutward(f, best[k]), rules.pinSpeed));
            p.Last = p.Jobs.Max(j => j.Arrive);
            p.End = Math.Max(4, p.Last + 2.4);
        }

        private static IEnumerable<List<int>> Permutations(List<int> a)
        {
            if (a.Count < 2) { yield return new List<int>(a); yield break; }
            for (int i = 0; i < a.Count; i++)
                foreach (List<int> r in Permutations(a.Where((_, k) => k != i).ToList()))
                {
                    r.Insert(0, a[i]);
                    yield return r;
                }
        }

        /// <summary>A big sword 4.2 to 6 cells out nearest the bearing, the sketch's drawTarget.</summary>
        private static UbwSword DrawTarget(List<UbwSword> all)
        {
            double b0 = DrawBearing * UbwBlade.D2R;
            double Off(UbwSword sw) { double a = Math.Atan2(sw.Z, sw.X) - b0; return Math.Abs(Math.Atan2(Math.Sin(a), Math.Cos(a))); }
            bool Big(UbwSword sw) => new[] { "LongSword", "MonoSword", "LargeSword", "Wyrmslayer" }.Any(n => sw.W.Name.Contains(n));
            List<UbwSword> pool = all.Where(sw => Usable(sw) && Big(sw) && sw.D > 4.2 && sw.D < 6).ToList();
            return (pool.Count > 0 ? pool : all.Where(Usable).ToList()).OrderBy(Off).FirstOrDefault();
        }

        private static void Draw(Plan p, UbwCommandNumbers rules)
        {
            UbwSword first = DrawTarget(Field(new List<UbwXZ> { new UbwXZ(0, 0) }));
            UbwXZ g = first != null ? Home(first) : new UbwXZ(5, -2);
            UbwXZ to = T.Toward(g.X, g.Z, T.Hand.X, T.Hand.Z, out double length), across = new UbwXZ(-to.Z, to.X);
            var keep = new List<UbwXZ> { new UbwXZ(0, 0) };
            for (int i = 0; i < DrawFoes.GetLength(0); i++)
            {
                double share = DrawFoes[i, 0], off = DrawFoes[i, 1];
                var foe = new UbwXZ(g.X + (T.Hand.X - g.X) * share + across.X * off, g.Z + (T.Hand.Z - g.Z) * share + across.Z * off);
                p.Foes.Add(foe);
                keep.Add(foe);
            }
            p.Swords = Field(keep);
            UbwSword sw = first == null ? null : p.Swords.FirstOrDefault(q => q.Seed == first.Seed) ?? DrawTarget(p.Swords);
            if (sw == null) { p.End = 3; return; }
            g = Home(sw);
            to = T.Toward(g.X, g.Z, T.Hand.X, T.Hand.Z, out length);
            p.Target = g;
            p.Reach = sw.W.Length * sw.W.Image * sw.Size / 2;
            double fly0 = Launch + T.TearTime, fly = length / rules.drawSpeed;
            p.Caught = fly0 + fly;
            p.Jobs.Add(new Job
            {
                Sw = sw, Leaves = Launch, Arrive = p.Caught, Speed = rules.drawSpeed, Meet = length,
                Shot = new UbwSwordShot { Start = new UbwV3(g.X, T.DrawHeight, g.Z), Dir = to }, End = new UbwV3(T.Hand.X, T.HandHeight, T.Hand.Z),
            });
            p.End = p.Caught + 1.3;
        }

        private static void Arm(Plan p)
        {
            p.Swords = Field(new List<UbwXZ> { new UbwXZ(0, 0), Helper });
            UbwSword sw = Nearest(p.Swords, Helper).FirstOrDefault();
            if (sw != null) p.Jobs.Add(new Job { Sw = sw, Leaves = Launch, Arrive = Launch + T.PullTime + T.ArcTime });
            p.End = 3;
        }

        private static void Intercept(Plan p, UbwCommandNumbers rules)
        {
            double a = ShooterAngle * UbwBlade.D2R;
            var dirOut = new UbwXZ(Math.Cos(a), Math.Sin(a));
            var shotDir = new UbwXZ(-dirOut.X, -dirOut.Z);
            p.Target = new UbwXZ(dirOut.X * ShooterAt, dirOut.Z * ShooterAt);
            p.Swords = Field(new List<UbwXZ> { new UbwXZ(0, 0), p.Target });
            UbwXZ muzzle = Add(p.Target, shotDir, 0.45);
            p.F = muzzle;
            p.Body = shotDir;
            double toCaster = Dist(muzzle, new UbwXZ(0, 0));
            var used = new HashSet<int>();
            foreach ((double t, bool rocket) in Shots)
            {
                double speed = rocket ? RocketSpeed : BulletSpeed;
                UbwSword best = null;
                double bestPerp = double.MaxValue, bestAlong = 0;
                foreach (UbwSword sw in p.Swords)
                {
                    if (!Usable(sw) || used.Contains(sw.Seed)) continue;
                    double along = (sw.X - muzzle.X) * shotDir.X + (sw.Z - muzzle.Z) * shotDir.Z;
                    if (along > toCaster - rules.interceptShortOfTarget || along < rules.interceptClearOfGun) continue;
                    UbwXZ foot = Add(muzzle, shotDir, along);
                    double perp = Dist(Home(sw), foot);
                    if (along / speed < rules.interceptRiseSeconds + perp / rules.interceptSpeed || perp >= bestPerp) continue;
                    best = sw;
                    bestPerp = perp;
                    bestAlong = along;
                }
                var j = new Job { Rocket = rocket, ShotAt = t, Speed = speed };
                if (best == null)
                {
                    j.Meet = t + (toCaster - 0.2) / speed;
                    j.End = new UbwV3(muzzle.X + shotDir.X * (toCaster - 0.2), T.HitHeight, muzzle.Z + shotDir.Z * (toCaster - 0.2));
                    p.Jobs.Add(j);
                    continue;
                }
                used.Add(best.Seed);
                UbwXZ meetAt = Add(muzzle, shotDir, bestAlong), dir = T.Toward(best.X, best.Z, meetAt.X, meetAt.Z, out _);
                j.Sw = best;
                j.Met = true;
                j.Leaves = t + 0.02;
                j.Meet = t + bestAlong / speed;
                j.End = new UbwV3(meetAt.X, T.HitHeight, meetAt.Z);
                j.Shot = new UbwSwordShot
                {
                    Launch = j.Leaves, Lift = T.InterceptLift, Fly = Math.Max(0.04, j.Meet - j.Leaves - T.InterceptLift), Dir = dir, Meet = true,
                    Start = new UbwV3(best.X + dir.X * 0.1, 0.4, best.Z + dir.Z * 0.1), Hit = j.End,
                };
                p.Jobs.Add(j);
            }
            p.End = 4;
        }

        // ---- drawing --------------------------------------------------------------------------------------------------

        /// <summary>The preview at <paramref name="s"/> seconds, the world centred 2 cells north of <paramref name="cell"/> as the sketches put it.</summary>
        public static void Draw(UbwCommandPreview command, Vector3 cell, float s, Map map)
        {
            Plan p = PlanFor(command, map, false);
            var o = new Vector2(cell.x, cell.z + UbwWorldGraphics.SceneNorth);
            // The holes of the swords gone by now, in the rows they are in.
            var rows = new HashSet<int>();
            foreach (Job j in p.Jobs)
                if (j.Sw != null && j.Sw.Hole != s >= j.Leaves)
                {
                    j.Sw.Hole = s >= j.Leaves;
                    rows.Add(UbwFieldBake.BandOf(j.Sw.Z + j.Sw.Lift));
                }
            if (rows.Count > 0) p.Bake.Rebuild(p.Swords, rows);
            UbwWorldGraphics.Draw(o, p.Bake, UbwWorldTiming.Swept + 1f + s, float.PositiveInfinity, UbwLayers.Preview, p.Sun, p.Strength, map, p.World);
            UbwCommandLook k = UbwCommandLook.For(o, p.Sun, p.Strength, preview: true);
            Begin(o);
            StandIn(k, new UbwXZ(0, 0), Caster, null);
            if (command == UbwCommandPreview.FullOpen) DrawFullOpen(p, k, s);
            else if (command == UbwCommandPreview.Pin) DrawPin(p, k, s);
            else if (command == UbwCommandPreview.Draw) DrawDraw(p, k, s);
            else if (command == UbwCommandPreview.Arm) DrawArm(p, k, s);
            else DrawIntercept(p, k, s);
        }

        private static void DrawFullOpen(Plan p, in UbwCommandLook k, double s)
        {
            int slot = 0;
            foreach (Job j in p.Jobs)
            {
                if (s < j.Pull) continue;
                string key = "ubw preview " + slot++;
                if (s < j.Shot.Launch) UbwCommandGraphics.Gathering(k, key, j.Sw, s - j.Pull, MarkAt(p, s - T.TrackLag));
                else
                {
                    UbwCommandGraphics.Shot(k, key, j.Sw, j.Hover, j.Shot, s, 0, false);
                    UbwCommandGraphics.BreakOut(k, j.Sw.Cut, s - j.Pull, j.Sw.Seed + 3);
                }
            }
            float u = (float)T.Smooth((s - Order) / 0.3), gone = 1f - (float)T.Smooth((s - p.Release) / 0.25);
            UbwXZ at = MarkAt(p, s);
            if (u > 0f && gone > 0f) UbwCommandGraphics.TargetRing(k, at, 0.42 + 0.04 * Math.Sin(s * 9), 0.55f * u * gone);
            StandIn(k, at, Enemy, s >= p.Down ? T.Toward(0, 0, at.X, at.Z, out _) : (UbwXZ?)null);
        }

        private static void DrawPin(Plan p, in UbwCommandLook k, double s)
        {
            double beat = s > p.Last + 0.4 ? (s - p.Last - 0.4) % T.Jerk : -1;
            double jerk = beat >= 0 && beat < 0.22 ? 0.035 * Math.Sin(beat / 0.22 * Math.PI * 2) : 0;
            int slot = 0;
            foreach (Job j in p.Jobs)
            {
                if (s < j.Leaves) continue;
                string key = "ubw preview " + slot++;
                UbwCommandGraphics.Shot(k, key, j.Sw, j.Sw.Pose, j.Shot, s, jerk * 60, true);
                UbwCommandGraphics.BreakOut(k, j.Sw.Cut, s - j.Leaves, j.Sw.Seed + 3);
            }
            double u = (s - Order) / 0.5;
            UbwXZ at = Walk(p.Body, Math.Min(s, p.T1));
            if (u >= 0 && u < 1.6) UbwCommandGraphics.TargetRing(k, at, 0.55 * (0.6 + 0.4 * T.Smooth(Math.Min(1, u))), (float)(0.6 * (1 - T.Smooth((u - 0.6) / 1))));
            StandIn(k, at, Enemy, s >= p.T1 + T.FallTime * 0.5 ? p.Body : (UbwXZ?)null);
        }

        private static void DrawDraw(Plan p, in UbwCommandLook k, double s)
        {
            if (p.Jobs.Count == 0) return;
            Job j = p.Jobs[0];
            const string key = "ubw preview draw";
            var hand = new UbwXZ(T.Hand.X, T.Hand.Z);
            if (s >= Order && s < p.Caught + 0.25)
            {
                float fade = s < Launch ? (float)T.Smooth((s - Order) / 0.1) : 1f - (float)T.Smooth((s - p.Caught) / 0.25);
                UbwCommandGraphics.Lane(k, p.Target, hand, p.Reach, fade);
                if (s < Launch + 0.15) UbwCommandGraphics.Order(k, new UbwXZ(0, 0), p.Target, 0.32, s < Launch ? fade : 1f - (float)T.Smooth((s - Launch) / 0.15));
            }
            double fly0 = Launch + T.TearTime;
            double Share(double x) => T.Clamp01((x - fly0) / Math.Max(1e-3, p.Caught - fly0));
            if (s >= Launch && s < p.Caught) UbwCommandGraphics.Drawing(k, key, j.Sw, s - Launch, j.Shot.Start, j.End, Share(s), lag => Share(s - lag));
            else if (s >= p.Caught)
            {
                UbwCommandGraphics.Held(k, key, j.Sw, new UbwXZ(0, 0));
                UbwCommandGraphics.Caught(k, hand, s - p.Caught);
            }
            UbwXZ to = j.Shot.Dir, across = new UbwXZ(-to.Z, to.X);
            foreach (UbwXZ foe in p.Foes)
            {
                double rx = foe.X - p.Target.X, rz = foe.Z - p.Target.Z, share = (rx * to.X + rz * to.Z) / j.Meet, off = rx * across.X + rz * across.Z;
                bool hit = Math.Abs(off) <= p.Reach && share > 0 && share < 1;
                double pass = fly0 + (p.Caught - fly0) * T.Clamp01(share), age = s - pass;
                if (hit) UbwCommandGraphics.Spark(k, new UbwV3(foe.X, T.HitHeight + 0.1, foe.Z), age, 0.15, 0.34f, Color.white);
                double push = hit && age >= 0 ? 0.12 * T.Smooth(Math.Min(1, age / 0.12)) * (Math.Sign(off) == 0 ? 1 : Math.Sign(off)) : 0;
                StandIn(k, Add(foe, across, push), hit && age >= 0 && age < 0.2 ? Hurt : Enemy, null);
            }
        }

        private static void DrawArm(Plan p, in UbwCommandLook k, double s)
        {
            if (p.Jobs.Count > 0)
            {
                Job j = p.Jobs[0];
                if (s >= Order && s < Launch + 0.15) UbwCommandGraphics.Order(k, Helper, Home(j.Sw), 0.28, 1f - (float)T.Smooth((s - Launch) / 0.15));
                if (s >= Launch) UbwCommandGraphics.Arming(k, "ubw preview arm", j.Sw, s - Launch, Helper, drawHeld: true);
                if (s >= j.Arrive) UbwCommandGraphics.Caught(k, new UbwXZ(Helper.X + T.Hand.X, Helper.Z + T.Hand.Z), s - j.Arrive);
            }
            StandIn(k, Helper, Ally, null);
        }

        private static void DrawIntercept(Plan p, in UbwCommandLook k, double s)
        {
            UbwXZ muzzle = p.F, dir = p.Body;
            int i = 0;
            foreach (Job j in p.Jobs)
            {
                string key = "ubw preview shot " + i++;
                double age = s - j.ShotAt;
                if (age >= 0 && age < 0.1) GokuGraphics.Glint(k.Map(muzzle) + new Vector2(0f, (float)(T.HitHeight * UbwBlade.Lift)), 0.3f, 1f - (float)(age / 0.1), UbwGraphics.FireCore);
                if (age >= 0 && s < j.Meet)
                {
                    double d = age * j.Speed;
                    Vector2 at = k.Screen(new UbwV3(muzzle.X + dir.X * d, T.HitHeight, muzzle.Z + dir.Z * d)), tail = k.Screen(new UbwV3(muzzle.X + dir.X * (d - 0.45), T.HitHeight, muzzle.Z + dir.Z * (d - 0.45)));
                    if (j.Rocket)
                    {
                        Sprite(at, 0.34f, 0.14f, Casing, soft, Overhead + 0.016f, -Mathf.Atan2((float)dir.Z, (float)dir.X) * Mathf.Rad2Deg);
                        Sprite(tail, 0.3f, 0.3f, Fade(UbwGraphics.FireOuter, 0.8f), glow, Overhead + 0.015f);
                        for (int q = 1; q < 8; q++)
                            if (q * 0.25 <= d) Sprite(at - (at - tail) * (q * 0.25f / 0.45f), 0.18f + q * 0.06f, 0.16f + q * 0.05f, Fade(Smoke, 0.45f * (1f - q / 8f)), soft, Overhead + 0.012f);
                    }
                    else Streak(tail, at, 0.05f, Fade(UbwGraphics.FireCore, 0.95f), whiteGlow, Overhead + 0.015f, 2);
                }
                if (j.Met && s >= j.Leaves) UbwCommandGraphics.Meeting(k, key, j.Sw, j.Shot, s);
                if (!j.Met) UbwCommandGraphics.Spark(k, j.End, s - j.Meet, 0.15, 0.3f, Hurt);
                double u = (s - j.Meet) / 0.45;
                if (j.Rocket && u >= 0 && u < 1)
                {
                    Vector2 at = k.Screen(j.End);
                    Sprite(at, 2.4f * (0.5f + (float)u), 2f * (0.5f + (float)u), Fade(UbwGraphics.FireCore, (float)((1 - u) * (1 - u))), glow, Overhead + 0.03f);
                    UbwCommandGraphics.TargetRing(k, new UbwXZ(j.End.X, j.End.Z), 0.3 + 1.4 * T.Smooth(u), 0.7f * (1f - (float)u));
                }
            }
            StandIn(k, p.Target, Enemy, null);
            Streak(k.Map(new UbwXZ(p.Target.X, p.Target.Z + 0.2)), k.Map(new UbwXZ(muzzle.X, muzzle.Z + 0.2)), 0.07f, Casing, solid, AltitudeLayer.Pawn.AltitudeFor() + 0.01f, 2);
        }

        /// <summary>The lab's stand-in pawn (lib/goku.js pawn): a shadow, a body and a head, in the world's warm light; lying along <paramref name="lying"/> if given.</summary>
        private static void StandIn(in UbwCommandLook k, UbwXZ at, Color colour, UbwXZ? lying)
        {
            Vector2 pos = k.Map(at), sun = new Vector2((float)k.Sun.X, (float)k.Sun.Z).normalized * 0.45f;
            float layer = AltitudeLayer.Pawn.AltitudeFor(), shade = k.Layers.FieldShadow + 0.003f;
            Color body = Color.Lerp(colour, UbwGraphics.Twilight, 0.24f), skin = Color.Lerp(Skin, UbwGraphics.Twilight, 0.24f);
            if (lying.HasValue)
            {
                UbwXZ f = lying.Value;
                float angle = -Mathf.Atan2((float)f.Z, (float)f.X) * Mathf.Rad2Deg;
                Sprite(pos + new Vector2((float)f.X, (float)f.Z) * 0.36f, 1.05f, 0.45f, Fade(Ink, k.Strength), soft, shade, angle);
                DrawMesh(disc, pos + new Vector2((float)f.X, (float)f.Z) * 0.32f, layer, 0.36f, 0.2f, angle, body, solid);
                DrawMesh(disc, pos + new Vector2((float)f.X, (float)f.Z) * 0.74f, layer + 0.002f, 0.16f, 0.17f, 0f, skin, solid);
                return;
            }
            Sprite(pos + sun, 0.85f, 0.4f, Fade(Ink, k.Strength), soft, shade);
            DrawMesh(disc, pos + new Vector2(0f, 0.18f), layer, 0.22f, 0.32f, 0f, body, solid);
            DrawMesh(disc, pos + new Vector2(0f, 0.58f), layer + 0.002f, 0.16f, 0.17f, 0f, skin, solid);
        }
    }
}
