using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.VergilGraphics;
using T = RimArt.SummonedSwordsTiming;

namespace RimArt
{
    public class CompProperties_SummonedSwords : CompProperties_AbilityEffect
    {
        public float seconds = 20f;
        public int blades = 8;
        public float fireEverySeconds = 1f;
        public float range = 12f;
        public float damage = 9f;
        public DamageDef damageDef;
        public float armorPenetration = 0.3f;
        /// <summary>A pawn holding this many blades is not shot at.</summary>
        public int maxPins = 4;
        public float stuckSeconds = 3f;
        public float regrowSeconds = 2.4f;
        /// <summary>The slow per stuck blade; its stages (one per blade) hold the numbers.</summary>
        public HediffDef pinHediff;
        public float spinRadius = 1.6f;
        public float spinEverySeconds = 0.9f;
        public float spinDamage = 5f;
        public DamageDef spinDamageDef;
        public float spinArmorPenetration = 0.2f;
        public float styleGainPerHit = 1f;

        public CompProperties_SummonedSwords()
        {
            compClass = typeof(CompAbilityEffect_SummonedSwords);
        }
    }

    /// <summary>Summoned Swords. On the fire tick the ring is formed and the 20 s begin.</summary>
    public class CompAbilityEffect_SummonedSwords : CompAbilityEffect_Vergil
    {
        public new CompProperties_SummonedSwords Props => (CompProperties_SummonedSwords)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            // A second cast while the ring is still out ends the old one first.
            GameComponent_Vergil.Instance?.Latest<SummonedSwordsCast>(parent.pawn)?.StopNow(Find.TickManager.TicksGame);
            SummonedSwordsCast cast = VergilCasts.For<SummonedSwordsCast>(parent, target);
            cast?.MarkFired(Find.TickManager.TicksGame);
        }
    }

    /// <summary>One blade that left the ring.</summary>
    public sealed class SwordFlight : IExposable
    {
        public int slot, fireTick, hitTick = -1, breakTick = -1, seed;
        public Pawn target;
        /// <summary>Where it left the ring, and the ring angle it left at.</summary>
        public Vector2 from;
        public float startAngle;
        /// <summary>It reached its target and stuck; otherwise it broke in the air.</summary>
        public bool stuck;
        /// <summary>Where it was when it broke, and its direction then.</summary>
        public Vector2 brokeAt;
        public float brokeDeg;

        public void ExposeData()
        {
            Scribe_Values.Look(ref slot, "slot");
            Scribe_Values.Look(ref fireTick, "fireTick");
            Scribe_Values.Look(ref hitTick, "hitTick", -1);
            Scribe_Values.Look(ref breakTick, "breakTick", -1);
            Scribe_Values.Look(ref seed, "seed");
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref startAngle, "startAngle");
            Scribe_Values.Look(ref stuck, "stuck");
            Scribe_Values.Look(ref brokeAt, "brokeAt");
            Scribe_Values.Look(ref brokeDeg, "brokeDeg");
        }
    }

    /// <summary>
    /// One Summoned Swords (the sketch vergil-summoned-swords.js). The warmup is the blades rising out of a
    /// ring on the floor. From the fire tick the ring circles Vergil for the ability's seconds while he keeps
    /// fighting. In the fire mode one blade leaves every fireEverySeconds for the nearest hostile in range and
    /// in sight holding fewer than maxPins blades, turns point first, flies and sticks; each stuck blade is a
    /// stack of the pin hediff until it breaks. In the spin mode the ring widens and speeds up, nothing is
    /// thrown, and every hostile within spinRadius is cut every spinEverySeconds. The mode button switches
    /// between them; the ring takes 0.3 s to change. At the end every blade breaks, the stuck ones too.
    /// </summary>
    public sealed class SummonedSwordsCast : VergilCast
    {
        public bool spin;
        /// <summary>0 the fire ring, 1 the spinning ring; moves toward <see cref="spin"/> over 0.3 s.</summary>
        private float spun;
        /// <summary>Slot 0's angle on the ring at <see cref="angleTick"/>, and how fast it turns now (degrees a second).</summary>
        private float angle, rate = T.Spin;
        private int angleTick = -1;
        private int stopTick = -1, nextShot = -1, nextSpin = -1;
        /// <summary>When each slot was last emptied by a shot, -1 while it holds a blade.</summary>
        private List<int> emptied = new List<int>();
        private List<SwordFlight> flights = new List<SwordFlight>();
        private readonly List<(Pawn pawn, float at, float deg)> spinHits = new List<(Pawn, float, float)>();
        private int seeds;
        /// <summary>Every blade has broken: the time is up or the ring was stopped.</summary>
        private bool broken;
        /// <summary>Where the ring was when it broke, for the shards.</summary>
        private Vector2 stopCentre;

        private CompProperties_SummonedSwords Props => VergilKit.Props<CompProperties_SummonedSwords>(VergilDefOf.AG_VergilSummonedSwords) ?? new CompProperties_SummonedSwords();
        private float Form => VergilDefOf.AG_VergilSummonedSwords.verbProperties.warmupTime;
        public override AbilityDef Def => VergilDefOf.AG_VergilSummonedSwords;
        protected override float Lead => T.Lead;
        protected override float FireAt => T.Lead + Form;
        private int Slots => Mathf.Max(1, Props.blades);

        /// <summary>The swords are out: formed and not yet broken.</summary>
        public bool Out(int now) => Fired && (stopTick < 0 || now < stopTick);

        private float RingRadius => Mathf.Lerp(T.Ring, T.SpinRing, Smooth(spun));

        /// <summary>The mode button: fire at range, or spin and cut what is close.</summary>
        public Command_Action ModeCommand() => new Command_Action
        {
            defaultLabel = spin ? "Swords: spin" : "Swords: fire",
            defaultDesc = spin
                ? "The blades spin round Vergil and cut every hostile close to him. Click to throw them at range instead."
                : "The blades fly at hostiles in range one at a time. Click to spin them round Vergil and cut what is close instead.",
            icon = VergilDefOf.AG_VergilSummonedSwords.uiIcon,
            action = () => spin = !spin,
        };

        public void StopNow(int now)
        {
            if (!Fired || broken) return;
            stopTick = now;
            Break(now);
        }

        public override bool Tick(int now)
        {
            if (angleTick < 0) angleTick = startTick;
            CompProperties_SummonedSwords props = Props;
            // The ring turns from the moment the blades start to rise, as the sketch's SlotAngle does.
            float step = (now - angleTick) / 60f;
            angleTick = now;
            spun = Mathf.MoveTowards(spun, spin ? 1f : 0f, step / T.Ramp);
            rate = Mathf.Lerp(T.Spin, T.SpinRate, Smooth(spun));
            angle = (angle + rate * step) % 360f;
            while (emptied.Count < Slots) emptied.Add(-1);

            if (!Fired) return now - startTick < 600;
            if (stopTick < 0)
            {
                stopTick = fireTick + Mathf.RoundToInt(props.seconds * 60f);
                nextShot = fireTick + Mathf.RoundToInt(T.FirstShot * 60f);
                nextSpin = fireTick + Mathf.RoundToInt(T.FirstShot * 60f);
            }
            bool ending = caster == null || caster.Dead || caster.Downed || !caster.Spawned || caster.Map != home || VergilKit.Manifested(caster) == null;
            if (!broken && (now >= stopTick || ending)) StopNow(now);

            if (now < stopTick)
            {
                if (!spin && spun < 0.01f && now >= nextShot)
                {
                    nextShot = now + Mathf.Max(1, Mathf.RoundToInt(props.fireEverySeconds * 60f));
                    Shoot(now, props);
                }
                if (spin && now >= nextSpin)
                {
                    nextSpin = now + Mathf.Max(1, Mathf.RoundToInt(props.spinEverySeconds * 60f));
                    if (spun > 0.5f) SpinCut(now, props);
                }
                for (int i = 0; i < flights.Count; i++)
                {
                    SwordFlight f = flights[i];
                    if (f.breakTick < 0 && !f.stuck && now >= f.hitTick) Land(f, now, props);
                    else if (f.stuck && f.breakTick < 0 && now >= f.hitTick + Mathf.RoundToInt(props.stuckSeconds * 60f)) Unpin(f, now, props);
                }
            }
            return now < stopTick + Mathf.RoundToInt(T.Tail * 60f);
        }

        /// <summary>Slot <paramref name="i"/>'s angle on the ring, degrees.</summary>
        private float SlotAngle(int i, float extraSeconds) => angle + rate * extraSeconds + i * 360f / Slots;

        private Vector2 Centre => caster != null && caster.Spawned ? VergilKit.Ground(caster.DrawPos) : stopCentre;

        /// <summary>A blade that holds a slot and has grown back, ready to fly.</summary>
        private bool Loaded(int i, int now, CompProperties_SummonedSwords props) =>
            emptied[i] < 0 || now >= emptied[i] + Mathf.RoundToInt((props.regrowSeconds + T.Grow) * 60f);

        private int Held(Pawn pawn)
        {
            int held = 0;
            for (int i = 0; i < flights.Count; i++)
                if (flights[i].target == pawn && flights[i].breakTick < 0) held++;
            return held;
        }

        private void Shoot(int now, CompProperties_SummonedSwords props)
        {
            Map map = caster.Map;
            Pawn best = null;
            float near = props.range * props.range;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!VergilKit.Foe(caster, pawn) || pawn.Downed) continue;
                float d = (pawn.Position - caster.Position).LengthHorizontalSquared;
                if (d > near || Held(pawn) >= props.maxPins) continue;
                if (!GenSight.LineOfSight(caster.Position, pawn.Position, map, true)) continue;
                best = pawn;
                near = d;
            }
            if (best == null) return;

            Vector2 c = VergilKit.Ground(caster.DrawPos), chest = VergilKit.Ground(best.DrawPos) + new Vector2(0f, T.Chest);
            float aim = Mathf.Atan2(chest.y - (c.y + T.Height * SixPathsHeight.Lift), chest.x - c.x) * Mathf.Rad2Deg;
            int slot = -1;
            float off = float.MaxValue;
            for (int i = 0; i < Slots; i++)
            {
                if (!Loaded(i, now, props)) continue;
                float o = Mathf.Abs(T.TurnTo(0f, SlotAngle(i, 0f) - aim, 1f));
                if (o < off) { off = o; slot = i; }
            }
            if (slot < 0) return;
            emptied[slot] = now;
            float at = SlotAngle(slot, 0f);
            Vector2 from = T.RingPoint(c, at, RingRadius, T.Height);
            float flight = T.TurnTime + Mathf.Max(0f, Vector2.Distance(chest, from) - T.Reach) / T.Speed;
            flights.Add(new SwordFlight
            {
                slot = slot, fireTick = now, hitTick = now + Mathf.Max(1, Mathf.RoundToInt(flight * 60f)), target = best,
                from = from, startAngle = at, seed = ++seeds,
            });
            VergilSound.Play(VergilSoundDefOf.AG_VergilSwordFire, map, caster.Position);
        }

        /// <summary>The blade reaches its target: 9 Stab, one pin, Style. A target gone by then leaves the blade to break in the air.</summary>
        private void Land(SwordFlight f, int now, CompProperties_SummonedSwords props)
        {
            Pawn target = f.target;
            if (target == null || target.Dead || !target.Spawned || target.Map != home)
            {
                f.breakTick = now;
                f.brokeAt = Flying(f, (now - f.fireTick) / 60f, out f.brokeDeg);
                return;
            }
            Vector2 chest = VergilKit.Ground(target.DrawPos) + new Vector2(0f, T.Chest);
            float deg = Mathf.Atan2(chest.y - f.from.y, chest.x - f.from.x) * Mathf.Rad2Deg;
            VergilSound.Play(VergilSoundDefOf.AG_VergilSwordHit, home, target.Position);
            VergilKit.Cut(target, caster, props.damageDef ?? DamageDefOf.Stab, props.damage, props.armorPenetration, 90f - deg);
            VergilStyle.Hit(caster, target, props.styleGainPerHit);
            if (target.Dead || !target.Spawned)
            {
                f.breakTick = now;
                f.brokeAt = chest;
                f.brokeDeg = deg;
                return;
            }
            f.stuck = true;
            f.brokeDeg = deg;
            Pin(target, 1f, props);
        }

        private void Unpin(SwordFlight f, int now, CompProperties_SummonedSwords props)
        {
            f.breakTick = now;
            if (f.target != null && f.target.Spawned) f.brokeAt = VergilKit.Ground(f.target.DrawPos) + new Vector2(0f, T.Chest);
            Pin(f.target, -1f, props);
        }

        /// <summary>Adds or removes one stuck blade's slow.</summary>
        private static void Pin(Pawn pawn, float change, CompProperties_SummonedSwords props)
        {
            HediffDef def = props.pinHediff ?? VergilDefOf.AG_VergilSwordPinned;
            if (pawn?.health == null || def == null) return;
            Hediff pin = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (change > 0f)
            {
                if (pin == null) pawn.health.AddHediff(def);
                else pin.Severity = Mathf.Min(def.maxSeverity, pin.Severity + change);
                pawn.health.hediffSet.GetFirstHediffOfDef(def)?.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                return;
            }
            if (pin == null) return;
            if (pin.Severity + change < 0.5f) pawn.health.RemoveHediff(pin);
            else pin.Severity += change;
        }

        private static readonly List<Pawn> close = new List<Pawn>();

        private void SpinCut(int now, CompProperties_SummonedSwords props)
        {
            close.Clear();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(caster.Position, caster.Map, props.spinRadius, true))
                if (thing is Pawn pawn && VergilKit.Foe(caster, pawn)) close.Add(pawn);
            float at = Seconds(now);
            for (int i = 0; i < close.Count; i++)
            {
                Pawn pawn = close[i];
                VergilKit.Cut(pawn, caster, props.spinDamageDef ?? DamageDefOf.Cut, props.spinDamage, props.spinArmorPenetration,
                    (pawn.Position - caster.Position).AngleFlat);
                VergilStyle.Hit(caster, pawn, props.styleGainPerHit);
                spinHits.Add((pawn, at, (seeds++ * 53) % 180));
            }
            // A tick that reaches nobody is silent, as in the sketch.
            if (close.Count > 0) VergilSound.Play(VergilSoundDefOf.AG_VergilSwordsSpin, caster.Map, caster.Position);
            close.Clear();
            // Only the last few cuts are drawn, or they pile into a scribble.
            if (spinHits.Count > 24) spinHits.RemoveRange(0, spinHits.Count - 24);
        }

        /// <summary>The ring's time is up: every blade breaks, the stuck ones too, and their slow goes.</summary>
        private void Break(int now)
        {
            broken = true;
            stopCentre = caster != null && caster.Spawned ? VergilKit.Ground(caster.DrawPos) : stopCentre;
            VergilSound.Play(VergilSoundDefOf.AG_VergilSwordsBreak, home, new IntVec3(Mathf.FloorToInt(stopCentre.x), 0, Mathf.FloorToInt(stopCentre.y)));
            CompProperties_SummonedSwords props = Props;
            for (int i = 0; i < flights.Count; i++)
            {
                SwordFlight f = flights[i];
                if (f.breakTick >= 0) continue;
                if (f.stuck) Unpin(f, now, props);
                else
                {
                    f.breakTick = now;
                    f.brokeAt = Flying(f, (now - f.fireTick) / 60f, out f.brokeDeg);
                }
            }
        }

        public override void Discard()
        {
            CompProperties_SummonedSwords props = Props;
            for (int i = 0; i < flights.Count; i++)
                if (flights[i].stuck && flights[i].breakTick < 0) Pin(flights[i].target, -1f, props);
            flights.Clear();
        }

        /// <summary>Where a flying blade is <paramref name="age"/> seconds after it left, and which way it points.</summary>
        private Vector2 Flying(SwordFlight f, float age, out float deg)
        {
            Vector2 chest = f.target != null && f.target.Spawned ? VergilKit.Ground(f.target.DrawPos) + new Vector2(0f, T.Chest) : f.from + Turn(f.startAngle) * 3f;
            Vector2 toward = chest - f.from;
            float dist = toward.magnitude;
            Vector2 d = dist > 1e-4f ? toward / dist : Turn(f.startAngle);
            float aim = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float u = Mathf.Clamp01(age / T.TurnTime), flown = Mathf.Min(Mathf.Max(0f, age - T.TurnTime) * T.Speed, Mathf.Max(0f, dist - T.Reach));
            deg = T.TurnTo(f.startAngle, aim, Smooth(u));
            return f.from + Turn(f.startAngle) * (0.2f * Smooth(u) * (1f - Mathf.Clamp01(flown))) + d * flown;
        }

        public override void Pose(float s) { }

        public override void Draw(float s)
        {
            if (home == null) return;
            int now = Find.TickManager.TicksGame;
            CompProperties_SummonedSwords props = Props;
            Vector2 c = Centre;
            if (!Shown(c, home)) return;
            Begin(c);
            PowerPoleGraphics.Sun(home, out Vector2 sun, out float shadow);
            float between = angleTick >= 0 ? PictureClock.Since(angleTick) : 0f;
            float stopS = stopTick >= 0 ? T.Lead + (stopTick - startTick) / 60f : float.MaxValue;
            float sinceStop = s - stopS, left = 1f - Smooth(sinceStop / T.RingFade), R = RingRadius, formedAt = T.Lead + Form;
            int n = Slots;

            // --- the floor: the reach of the mode, and the ring's mark under Vergil ---------------------------
            if (Fired || s >= T.Lead)
            {
                float grown = Smooth((s - T.Lead) / (Form * 0.6f));
                PaperBombGraphics.RingAt(c, spun > 0.5f ? props.spinRadius : props.range, Fade(Blue, (spun > 0.5f ? 0.45f : 0.12f) * left * grown), Floor + 0.015f);
                PaperBombGraphics.RingAt(c, R * grown, Fade(Blue, (s < formedAt ? 0.7f : 0.3f) * left), Floor + 0.02f);
            }
            if (s >= T.Lead && s < formedAt + 0.3f)
                GokuGraphics.Aura(c, s, 0.5f * Mathf.Clamp01((s - T.Lead) / Form) * (1f - Mathf.Clamp01((s - formedAt) / 0.3f)), Blue);

            // --- the ring -----------------------------------------------------------------------------------
            for (int i = 0; i < n; i++)
            {
                float deg = SlotAngle(i, between);
                bool north = Mathf.Sin(deg * Mathf.Deg2Rad) > 0f;
                float layer = north ? PawnLayer - 0.02f : Overhead + 0.03f;
                int gone = i < emptied.Count ? emptied[i] : -1;

                if (sinceStop >= 0f)
                {
                    // Every blade in the ring breaks where it was; an empty slot has nothing to break.
                    bool held = gone < 0 || stopTick >= gone + Mathf.RoundToInt((props.regrowSeconds + T.Grow * 0.5f) * 60f);
                    if (held)
                    {
                        float stopDeg = angle + i * 360f / n;
                        SummonedSwordsGraphics.Shatter(i + 1, T.RingPoint(stopCentre, stopDeg, R, T.Height), stopDeg, sinceStop);
                    }
                    continue;
                }

                if (spun > 0f)
                {
                    Vector2[] arc = GokuGraphics.Points(9);
                    for (int j = 0; j < 9; j++) arc[j] = T.RingPoint(c, deg - T.ArcBehind * spun * j / 8f, R, T.Height);
                    GokuGraphics.Line(arc, 0.1f, Fade(Blue, 0.5f * spun), whiteGlow, layer - 0.001f);
                }

                if (gone >= 0)
                {
                    float regrow = (now - gone) / 60f - props.regrowSeconds + between;
                    if (regrow < 0f) continue;
                    float u = Mathf.Clamp01(regrow / T.Grow);
                    Vector2 mid = T.RingPoint(c, deg, R, T.Height);
                    if (u < 1f) Glint(mid, 0.22f * (1f - u), 1f - u, Snow, 30f);
                    SummonedSwordsGraphics.Blade(mid, deg, u, layer, hot: 1f - u, shown: Smooth(u));
                    SummonedSwordsGraphics.BladeShadow(mid, T.Height, deg, sun, shadow * 0.6f * u);
                    continue;
                }

                float appear = T.Lead + i / (float)n * Mathf.Max(0f, Form - T.Rise), rise = Mathf.Clamp01((s - appear) / T.Rise);
                if (s < appear) continue;
                float height = T.Height * Smooth(rise);
                Vector2 point = T.RingPoint(c, deg, R, height);
                if (rise < 1f) Glint(T.RingPoint(c, deg, R, 0f), 0.3f * (1f - rise), 1f - rise, Snow, 30f);
                SummonedSwordsGraphics.Blade(point, deg, rise, layer, hot: 1f - rise);
                SummonedSwordsGraphics.BladeShadow(point, height, deg, sun, shadow * 0.6f * rise);
            }

            // A thin line of light joins the blades, so the ring reads as one thing.
            Vector2 ringCentre = sinceStop >= 0f ? stopCentre : c;
            if (s >= formedAt - 0.1f && sinceStop < T.RingFade)
                PaperBombGraphics.RingAt(new Vector2(ringCentre.x, ringCentre.y + T.Height * SixPathsHeight.Lift), R,
                    Fade(Blue, 0.16f * Mathf.Clamp01((s - formedAt + 0.1f) / 0.2f) * left), Overhead + 0.02f, false, whiteGlow);

            // --- the blades that left: turn, fly, stick, break ------------------------------------------------
            for (int k = 0; k < flights.Count; k++)
            {
                SwordFlight f = flights[k];
                float age = (now - f.fireTick) / 60f + between;
                if (age < 0.12f) Glint(f.from, 0.3f * (1f - age / 0.12f), 1f - age / 0.12f, Snow, 30f);
                if (f.breakTick >= 0)
                {
                    float broke = (now - f.breakTick) / 60f + between;
                    if (broke < T.Break) SummonedSwordsGraphics.Shatter(f.seed + 20, f.brokeAt - Turn(f.brokeDeg) * (T.StuckOut * 0.6f), f.brokeDeg, broke);
                    continue;
                }
                if (!f.stuck)
                {
                    Vector2 mid = Flying(f, age, out float deg);
                    float flown = Mathf.Max(0f, age - T.TurnTime) * T.Speed;
                    if (flown > 0f)
                    {
                        Vector2 d = Turn(deg);
                        float back = Mathf.Min(flown, 2.4f);
                        Vector2 tail = mid - d * back;
                        Sprite((mid + tail) / 2f, back * 1.2f, 0.5f, Fade(Blue, 0.4f), glow, Overhead + 0.024f, -deg);
                        Streak(tail, mid, 0.07f, Fade(Ice, 0.6f), whiteGlow, Overhead + 0.025f, 6);
                    }
                    SummonedSwordsGraphics.Blade(mid, deg, 1f, Overhead + 0.03f, hot: Mathf.Clamp01(age / T.TurnTime));
                    continue;
                }
                if (f.target == null || !f.target.Spawned || f.target.Map != home) continue;
                Vector2 chest = VergilKit.Ground(f.target.DrawPos) + new Vector2(0f, T.Chest);
                float lean = f.brokeDeg + (Rand(f.seed + 3) - 0.5f) * 24f;
                var tip = new Vector2(chest.x + (Rand(f.seed + 9) - 0.5f) * 0.14f, chest.y + (Rand(f.seed + 15) - 0.5f) * 0.14f);
                float since = (now - f.hitTick) / 60f + between;
                SummonedSwordsGraphics.Blade(tip - Turn(lean) * (T.StuckOut * 0.6f), lean, 1f, Overhead + 0.03f, hot: Mathf.Clamp01(1f - since / 0.15f), length: T.StuckOut);
                float hit = since / 0.16f;
                if (hit < 1f)
                {
                    Sprite(tip, 0.7f * (1f - hit) + 0.2f, 0.7f * (1f - hit) + 0.2f, Fade(Ice, 0.6f * (1f - hit)), glow, Overhead + 0.04f);
                    Glint(tip, 0.4f * (1f - hit), 1f - hit, Snow, lean);
                }
            }

            // --- the spin's cuts ------------------------------------------------------------------------------
            for (int i = 0; i < spinHits.Count; i++)
            {
                (Pawn pawn, float at, float deg) = spinHits[i];
                if (pawn.Spawned && pawn.Map == home) HitCut(VergilKit.Ground(pawn.DrawPos), deg, s - at);
            }

            // --- it ends: one thin ring front ------------------------------------------------------------------
            if (sinceStop >= 0f && sinceStop < 0.3f)
                PaperBombGraphics.RingAt(new Vector2(stopCentre.x, stopCentre.y + T.Height * SixPathsHeight.Lift), R * (1f + 0.6f * Smooth(sinceStop / 0.3f)),
                    Fade(Ice, 0.6f * (1f - sinceStop / 0.3f)), Overhead + 0.03f, false, whiteGlow);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref spin, "spin");
            Scribe_Values.Look(ref spun, "spun");
            Scribe_Values.Look(ref angle, "angle");
            Scribe_Values.Look(ref rate, "rate", T.Spin);
            Scribe_Values.Look(ref angleTick, "angleTick", -1);
            Scribe_Values.Look(ref stopTick, "stopTick", -1);
            Scribe_Values.Look(ref nextShot, "nextShot", -1);
            Scribe_Values.Look(ref nextSpin, "nextSpin", -1);
            Scribe_Values.Look(ref seeds, "seeds");
            Scribe_Values.Look(ref broken, "broken");
            Scribe_Values.Look(ref stopCentre, "stopCentre");
            Scribe_Collections.Look(ref emptied, "emptied", LookMode.Value);
            Scribe_Collections.Look(ref flights, "flights", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (emptied == null) emptied = new List<int>();
                if (flights == null) flights = new List<SwordFlight>();
            }
        }
    }
}
