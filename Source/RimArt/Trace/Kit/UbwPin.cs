using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using static RimArt.UbwCommands;
using T = RimArt.UbwCommandTiming;

namespace RimArt
{
    /// <summary>One pinning sword: which one, its pin on the body (<see cref="UbwCommandTiming.Pins"/>), when it leaves and when it goes in.</summary>
    public sealed class UbwPinSword : IExposable
    {
        public int seed, pin, launch, arrive;
        public bool struck;

        public void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref pin, "pin");
            Scribe_Values.Look(ref launch, "launch");
            Scribe_Values.Look(ref arrive, "arrive");
            Scribe_Values.Look(ref struck, "struck");
        }
    }

    /// <summary>One pawn being pinned or pinned, and its swords.</summary>
    public sealed class UbwPinning : IExposable
    {
        public Pawn target;
        public int order;
        public List<UbwPinSword> swords = new List<UbwPinSword>();

        /// <summary>When the last sword goes in.</summary>
        public int Last
        {
            get
            {
                int last = order;
                foreach (UbwPinSword s in swords) last = Mathf.Max(last, s.arrive);
                return last;
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref order, "order");
            Scribe_Collections.Look(ref swords, "swords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && swords == null) swords = new List<UbwPinSword>();
        }
    }

    /// <summary>
    /// Pin (the caster's command). The pinSwords swords nearest the picked pawn leave the ground together, 0.1 s after
    /// the order, and fly in low at pinSpeed. The first goes through a trouser leg when it arrives: the pawn gets
    /// AG_UbwPinned (Moving capped at 0, so vanilla counts it as downed and it can be captured) for pinSeconds, added
    /// with no DamageInfo so no death-on-downed roll is made. The others arrive 0.1 s apart once it has fallen (0.35 s
    /// later) and pin the other leg and both sleeves. Each sword does pinDamage Cut and costs swordCostSeconds of the
    /// world as it leaves. The pinning swords are gone from the field; they are drawn in the pawn while it stays pinned
    /// and in the world. There is no prisoner bed inside, so a capture happens after the return if time is left.
    /// </summary>
    public sealed class UbwPin : IExposable
    {
        public List<UbwPinning> pins = new List<UbwPinning>();

        public void Begin(UbwCast cast, Pawn foe, int now)
        {
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (inside == null || !cast.CommandsOpen || !cast.InWorld(foe) || foe == cast.caster) return;
            UbwRules rules = UbwRules.Of;
            UbwFieldState field = inside.Field;
            Vector2 at = inside.Local(foe.DrawPos);
            var picked = new List<UbwSword>();
            for (int i = 0; i < rules.pinSwords; i++)
            {
                UbwSword sw = field.Nearest(at, float.PositiveInfinity, s => !picked.Contains(s));
                if (sw == null) break;
                picked.Add(sw);
            }
            if (picked.Count == 0) return;

            var pinning = new UbwPinning { target = foe, order = now };
            int launch = now + Ticks(T.Launch), first = 0;
            for (int k = 0; k < picked.Count; k++)
            {
                UbwSword sw = picked[k];
                field.Take(sw);
                cast.Spend(1);
                float fly = Vector2.Distance(new Vector2((float)sw.X, (float)sw.Z), at) / rules.pinSpeed;
                int arrive;
                if (k == 0) first = arrive = launch + Ticks(T.LiftTime + fly);
                else arrive = Mathf.Max(launch + Ticks(T.LiftTime + fly), first + Ticks(T.FallTime + 0.05 + T.PinGap * (k - 1)));
                pinning.swords.Add(new UbwPinSword { seed = sw.Seed, pin = k, launch = Mathf.Max(now + 1, arrive - Ticks(T.LiftTime + fly)), arrive = arrive });
            }
            pins.Add(pinning);
        }

        public void Tick(UbwCast cast, int now)
        {
            UbwRules rules = UbwRules.Of;
            for (int i = pins.Count - 1; i >= 0; i--)
            {
                UbwPinning p = pins[i];
                bool there = cast.InWorld(p.target);
                foreach (UbwPinSword s in p.swords)
                {
                    if (s.struck || now < s.arrive) continue;
                    s.struck = true;
                    if (!there) continue;
                    if (s.pin == 0) Pin(p.target, rules.pinSeconds);
                    var dinfo = new DamageInfo(DamageDefOf.Cut, rules.pinDamage, 0f, -1f, cast.caster, null, null, DamageInfo.SourceCategory.ThingOrUnknown, p.target);
                    dinfo.SetBodyRegion(s.pin < 2 ? BodyPartHeight.Bottom : BodyPartHeight.Middle, BodyPartDepth.Outside);
                    p.target.TakeDamage(dinfo);
                }
                if (now > p.Last && !Pinned(p.target, cast))
                {
                    Rejoin(p.target, cast);
                    pins.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// The pin is over. Going down took the pawn out of its lord (Pawn_HealthTracker.MakeDowned calls
        /// Lord.Notify_PawnLost with PawnLostCondition.Incapped), so a hostile that gets up inside the world would stand
        /// with no duty for the rest of it: it gets a new assault lord of its faction, as the take gave it. A pawn still
        /// down from its wounds is left alone; the return puts everyone back into a lord at home.
        /// </summary>
        private static void Rejoin(Pawn pawn, UbwCast cast)
        {
            if (!cast.InWorld(pawn) || pawn.Downed || pawn.GetLord() != null) return;
            CrossMapMove.Assault(new List<Pawn> { pawn }, cast.world);
        }

        /// <summary>Adds AG_UbwPinned for <paramref name="seconds"/>, or sets the time of the one it has.</summary>
        private static void Pin(Pawn pawn, float seconds)
        {
            Hediff pinned = pawn.health.hediffSet.GetFirstHediffOfDef(UbwDefOf.AG_UbwPinned);
            if (pinned == null)
            {
                pinned = HediffMaker.MakeHediff(UbwDefOf.AG_UbwPinned, pawn);
                SetTime(pinned, seconds);
                pawn.health.AddHediff(pinned);
            }
            else SetTime(pinned, seconds);
        }

        private static void SetTime(Hediff hediff, float seconds)
        {
            var gone = hediff.TryGetComp<HediffComp_Disappears>();
            if (gone == null) return;
            gone.ticksToDisappear = gone.disappearsAfterTicks = Ticks(seconds);
        }

        /// <summary>Still held by the pins: alive, in the world, with AG_UbwPinned.</summary>
        public static bool Pinned(Pawn pawn, UbwCast cast) =>
            cast.InWorld(pawn) && pawn.health.hediffSet.HasHediff(UbwDefOf.AG_UbwPinned);

        /// <summary>
        /// The ring under the target, the swords flying in low and, while it stays pinned, the swords driven in through
        /// its clothes: at the pins of its body as the game lays it down (its downed angle; upright before it falls),
        /// jerking a little every <see cref="UbwCommandTiming.Jerk"/> seconds as it struggles.
        /// </summary>
        internal void Draw(UbwCast cast, MapComponent_UnlimitedBladeWorks inside, in UbwCommandLook k)
        {
            UbwFieldState field = inside.Field;
            int slot = 0;
            foreach (UbwPinning p in pins)
            {
                Pawn pawn = p.target;
                if (!cast.InWorld(pawn)) continue;
                Vector2 m = inside.Local(pawn.DrawPos);
                var mid = new UbwXZ(m.x, m.y);
                var f = new UbwXZ(0, 1);
                if (pawn.Downed)
                {
                    float a = pawn.Drawer.renderer.BodyAngle(PawnRenderFlags.None) * Mathf.Deg2Rad;
                    f = new UbwXZ(Mathf.Sin(a), Mathf.Cos(a));
                }
                var feet = new UbwXZ(mid.X - f.X * T.FeetBack, mid.Z - f.Z * T.FeetBack);
                double ring = UbwClock.Since(p.order) / 0.5;
                if (ring < 1.6) UbwCommandGraphics.TargetRing(k, mid, 0.55 * (0.6 + 0.4 * T.Smooth(System.Math.Min(1.0, ring))), (float)(0.6 * (1 - T.Smooth((ring - 0.6) / 1.0))));
                double struggle = UbwClock.Since(p.Last) - 0.4, beat = struggle >= 0 ? struggle % T.Jerk : -1;
                double jerk = beat >= 0 && beat < 0.22 ? 0.035 * System.Math.Sin(beat / 0.22 * System.Math.PI * 2) : 0;
                bool pinned = UbwPin.Pinned(pawn, cast);
                foreach (UbwPinSword s in p.swords)
                {
                    UbwSword sw = field.BySeed(s.seed);
                    if (sw == null) continue;
                    string key = "ubw pin " + slot++;
                    double t = UbwClock.Since(s.launch);
                    if (t < 0)
                    {
                        UbwCommandGraphics.Standing(k, key, sw);
                        continue;
                    }
                    double fly = System.Math.Max(0.02, (s.arrive - s.launch) / 60.0 - T.LiftTime);
                    if (t >= T.LiftTime + fly && !pinned) continue;
                    UbwXZ point = T.PinPoint(feet, f, s.pin), dir = T.Toward(sw.X, sw.Z, point.X, point.Z, out _);
                    var shot = new UbwSwordShot
                    {
                        Launch = 0, Lift = T.LiftTime, Fly = fly, Dir = dir, Land = point, Toward = T.PinOutward(f, s.pin), Lean = T.PinLean, Buried = T.PinDepth,
                        Start = new UbwV3(sw.X + dir.X * 0.2, T.PinHeight, sw.Z + dir.Z * 0.2), Hit = new UbwV3(point.X - dir.X * 0.05, T.PinHeight, point.Z - dir.Z * 0.05),
                    };
                    UbwCommandGraphics.Shot(k, key, sw, sw.Pose, shot, t, jerk * 60, true);
                    UbwCommandGraphics.BreakOut(k, sw.Cut, t, sw.Seed + 3);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref pins, "pins", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pins == null) pins = new List<UbwPinning>();
                pins.RemoveAll(p => p?.target == null);
            }
        }
    }
}
