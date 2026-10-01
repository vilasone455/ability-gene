using System;
using System.Collections.Generic;
using UnityEngine;
using T = RimArt.LastPrismTiming;

namespace RimArt
{
    /// <summary>The sketch's scenarios: a full charge fired at a group, 4 s of charge fired, the meter filling in the sun, the prism under a roof.</summary>
    public enum LastPrismScene { Fires, RunsDry, Charges, Roofed }

    /// <summary>
    /// The preview's script, not a rule: the sketch's fight (last-prism.js, replay) replayed from 0 at 60 steps a
    /// second, so the previews and the recordings aim, hit and down the same pawns on the same frames as the sketch.
    /// Positions are cells from the chosen cell's centre. The wielder stands <see cref="Back"/> cells behind it,
    /// facing the scenario's aim; the first target walks across the aim; a wall of three cells stands on the fan's
    /// right; every hit uses <see cref="LastPrismTiming"/>'s beams, burns and gaps. Pawns go down at
    /// <see cref="PainShock"/> burn (the mech at <see cref="MechDown"/>). When the target goes down the next one is
    /// the standing enemy in range with no wall in the way that needs the smallest turn; the channel ends when the
    /// charge runs out or no such enemy is left. The rules PR does this with real pawns, damage and line of sight.
    /// </summary>
    public sealed class LastPrismScript
    {
        /// <summary>The hold before the channel starts (s); the idle prism shown after the beam ends (s); the length of the idle scenarios (s).</summary>
        public const double Lead = 0.3, Tail = 1.4, ShowTime = 6;
        /// <summary>Seconds of beam in the meter: "runs dry" starts with DryStore, "charges in the sun" starts at StartLevel and gains one a second (20 x the 1 s per 20 s of full sun).</summary>
        public const float DryStore = 4f, StartLevel = 3f;
        /// <summary>Burn that downs an unarmoured pawn (vanilla pain shock at 80 %, 0.01875 pain per burn point); the mech stand-in feels no pain and goes down at 150.</summary>
        public const double PainShock = 43, MechDown = 150;
        /// <summary>The wielder stands Back cells behind the chosen cell; the first target walks across the aim over WalkTime s; the wall is WallAt cells out.</summary>
        public const double Back = 7, WalkTime = 6.3, WallAt = 10;
        private const double Step = 1.0 / 60.0;
        private static readonly double[] WallAcross = { -3, -4, -5 };
        // The cast, in the aim frame from the wielder: cells along, cells across (left positive). The first target walks
        // from +2 to -1 across. Then a raider further out, a raider in front of the wall, one behind it (no clear line),
        // one outside the fan, a mech, and a colonist standing in front.
        private static readonly double[] Along = { 12, 19, 8, 13.5, 6, 15, 5 }, Across = { 2, -1.5, -4.3, -5, -6.5, 7, 1.3 };
        private const double WalkTo = -1;
        private static readonly bool[] Mech = { false, false, false, false, false, true, false }, Ally = { false, false, false, false, false, false, true };
        /// <summary>The seven pawns of the cast.</summary>
        public static int Count => Along.Length;

        private static readonly Dictionary<(LastPrismScene, float), LastPrismScript> made = new Dictionary<(LastPrismScene, float), LastPrismScript>();

        public readonly LastPrismScene Scene;
        /// <summary>The prism's aim at each step (radians, single precision as the sketch keeps it), and the target's index.</summary>
        public readonly float[] Aims;
        public readonly int[] Targets;
        /// <summary>When the six beams join, when the beam stops, and when the charge would run out (s).</summary>
        public readonly double JoinAt, ReleaseAt, DryAt;
        /// <summary>The beam stopped because the charge ran out (it flickers out), not for want of a target.</summary>
        public readonly bool Dried;
        /// <summary>When each pawn went down, or +infinity.</summary>
        public readonly double[] DownAt = new double[Along.Length];
        /// <summary>Each time a target went down and the beam swung to the next, before the beam stopped.</summary>
        public readonly List<double> Downs = new List<double>();
        /// <summary>The wall cells' centres, cells from the chosen cell's centre.</summary>
        public readonly double[] WallX = new double[WallAcross.Length], WallZ = new double[WallAcross.Length];
        private readonly double ca, sa, casterX, casterZ;

        public static LastPrismScript For(LastPrismScene scene, float aimDegrees)
        {
            if (!made.TryGetValue((scene, aimDegrees), out LastPrismScript script)) made[(scene, aimDegrees)] = script = new LastPrismScript(scene, aimDegrees);
            return script;
        }

        public static bool Shoots(LastPrismScene scene) => scene == LastPrismScene.Fires || scene == LastPrismScene.RunsDry;

        public bool Joins => JoinAt < ReleaseAt;

        /// <summary>The wielder's point, cells from the chosen cell's centre.</summary>
        public Vector2 Caster => new Vector2((float)casterX, (float)casterZ);

        /// <summary>The preview's length: the idle scenarios show <see cref="ShowTime"/> s; a channel ends Tail s after the beam has faded or flickered out.</summary>
        public double End => Shoots(Scene) ? ReleaseAt + (Dried ? T.Sputter : T.Fade) + Tail : ShowTime;

        /// <summary>The step whose aim and target the frame at <paramref name="s"/> shows (the sketch's Math.round).</summary>
        public int Frame(double s) => Math.Min(Aims.Length - 1, (int)Math.Floor(s / Step + 0.5));

        /// <summary>Pawn <paramref name="j"/>'s point at <paramref name="s"/>, cells from the chosen cell's centre. The first target stops walking when it goes down.</summary>
        public Vector2 Pos(int j, double s)
        {
            Place(j, s, out double x, out double z);
            return new Vector2((float)x, (float)z);
        }

        /// <summary>The cells a beam from (x, z) along <paramref name="angle"/> runs before a wall, at most <paramref name="max"/>.</summary>
        public double Reach(double x, double z, double angle, double max) => T.RayWall(x, z, angle, WallX, WallZ, WallX.Length, max);

        /// <summary>Seconds of beam left in the meter at <paramref name="s"/>; the idle scenarios gain <paramref name="light"/> a second in the sun.</summary>
        public float Level(double s, float light)
        {
            if (Scene == LastPrismScene.Roofed) return StartLevel;
            if (Scene == LastPrismScene.Charges) return Mathf.Min(T.Store, StartLevel + (float)s * light);
            double store = Scene == LastPrismScene.RunsDry ? DryStore : T.Store;
            return (float)Math.Max(0, store - Math.Max(0, Math.Min(s - Lead, ReleaseAt - Lead)));
        }

        /// <summary>The meter's red flash when the charge ran out: on and off 3 times a second for 1.2 s.</summary>
        public float Warn(double s)
        {
            double after = s - ReleaseAt;
            return Shoots(Scene) && Dried && after > 0 && after < 1.2 ? (Math.Floor(after * 6) % 2 != 0 ? 1f : 0.25f) : 0f;
        }

        private LastPrismScript(LastPrismScene scene, float aimDegrees)
        {
            Scene = scene;
            double a0 = aimDegrees * (Math.PI / 180.0);
            ca = Math.Cos(a0);
            sa = Math.Sin(a0);
            casterX = -ca * Back;
            casterZ = -sa * Back;
            for (int w = 0; w < WallAcross.Length; w++)
            {
                World(WallAt, WallAcross[w], out double x, out double z);
                WallX[w] = Math.Floor(x + 0.5);
                WallZ[w] = Math.Floor(z + 0.5);
            }
            for (int j = 0; j < DownAt.Length; j++) DownAt[j] = double.PositiveInfinity;
            JoinAt = Lead + T.Join;
            DryAt = Lead + (scene == LastPrismScene.RunsDry ? DryStore : T.Store);
            int n = (int)Math.Ceiling((DryAt + T.Sputter + Tail + 0.5) / Step);
            Aims = new float[n + 1];
            Targets = new int[n + 1];
            if (!Shoots(scene))
            {
                ReleaseAt = DryAt;
                return;
            }

            var total = new double[Along.Length];
            var lastFan = new double[Along.Length];
            var lastJoin = new double[Along.Length];
            for (int j = 0; j < Along.Length; j++) lastFan[j] = lastJoin[j] = -9;
            double release = DryAt, aim;
            int target = 0;
            Place(0, 0, out double tx, out double tz);
            aim = Math.Atan2(tz - casterZ, tx - casterX);
            for (int k = 0; k <= n; k++)
            {
                double s = k * Step;
                if (s < release && DownAt[target] <= s)
                {
                    int next = Pick(s, aim);
                    if (next < 0) release = s;
                    else
                    {
                        Downs.Add(s);
                        target = next;
                    }
                }
                Place(target, s, out tx, out tz);
                double step = T.Turn * (Math.PI / 180.0) * Step;
                aim += Math.Max(-step, Math.Min(step, T.Wrap(Math.Atan2(tz - casterZ, tx - casterX) - aim)));
                Aims[k] = (float)aim;
                Targets[k] = target;
                if (s < Lead || s >= release) continue;
                double tipX = casterX + Math.Cos(aim) * (T.PrismGap + T.PrismLen), tipZ = casterZ + Math.Sin(aim) * (T.PrismGap + T.PrismLen);
                if (s < JoinAt)
                {
                    for (int j = 0; j < Along.Length; j++)
                    {
                        if (DownAt[j] <= s || s - lastFan[j] < T.FanEvery) continue;
                        Place(j, s, out double qx, out double qz);
                        if (!Crossed(qx, qz, s, aim, tipX, tipZ)) continue;
                        lastFan[j] = s;
                        Hurt(j, T.FanHit, s, total);
                    }
                }
                else
                {
                    double lane = Reach(tipX, tipZ, aim, T.Range);
                    for (int j = 0; j < Along.Length; j++)
                    {
                        if (DownAt[j] <= s || s - lastJoin[j] < T.JoinEvery) continue;
                        Place(j, s, out double qx, out double qz);
                        if (!T.OnLine(qx, qz, tipX, tipZ, aim, lane, T.Width / 2.0)) continue;
                        lastJoin[j] = s;
                        Hurt(j, T.JoinHit, s, total);
                    }
                }
            }
            ReleaseAt = release;
            Dried = release >= DryAt;
            Downs.RemoveAll(t => t >= release);
        }

        // True when any of the six fan beams at s passes within HitReach of (qx, qz).
        private bool Crossed(double qx, double qz, double s, double aim, double tipX, double tipZ)
        {
            for (int i = 0; i < T.Beams; i++)
            {
                T.FanBeam(i, s, Lead, T.Join, T.Fan, out double turn, out double shift);
                double angle = aim + turn, x = tipX - Math.Sin(aim) * shift, z = tipZ + Math.Cos(aim) * shift;
                if (T.OnLine(qx, qz, x, z, angle, Reach(x, z, angle, T.Range), T.HitReach)) return true;
            }
            return false;
        }

        private void Hurt(int j, double amount, double s, double[] total)
        {
            total[j] += amount;
            if (total[j] >= (Mech[j] ? MechDown : PainShock) && double.IsPositiveInfinity(DownAt[j])) DownAt[j] = s;
        }

        // The next target: a standing enemy in range with no wall in the way, needing the smallest turn; -1 if none.
        private int Pick(double s, double aim)
        {
            double tipX = casterX + Math.Cos(aim) * (T.PrismGap + T.PrismLen), tipZ = casterZ + Math.Sin(aim) * (T.PrismGap + T.PrismLen);
            int best = -1;
            double least = double.PositiveInfinity;
            for (int j = 0; j < Along.Length; j++)
            {
                if (Ally[j] || DownAt[j] <= s) continue;
                Place(j, s, out double qx, out double qz);
                double d = Math.Sqrt((qx - tipX) * (qx - tipX) + (qz - tipZ) * (qz - tipZ)), angle = Math.Atan2(qz - tipZ, qx - tipX);
                if (d > T.Range || Reach(tipX, tipZ, angle, d) < d - 0.01) continue;
                double turn = Math.Abs(T.Wrap(angle - aim));
                if (turn < least)
                {
                    least = turn;
                    best = j;
                }
            }
            return best;
        }

        private void Place(int j, double s, out double x, out double z)
        {
            double across = Across[j];
            if (j == 0) across = Across[0] + (WalkTo - Across[0]) * T.Clamp01(Math.Min(s, DownAt[0]) / WalkTime);
            World(Along[j], across, out x, out z);
        }

        private void World(double along, double across, out double x, out double z)
        {
            x = casterX + along * ca - across * sa;
            z = casterZ + along * sa + across * ca;
        }
    }
}
