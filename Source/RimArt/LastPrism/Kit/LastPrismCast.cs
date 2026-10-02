using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.LastPrismBeam;
using T = RimArt.LastPrismTiming;

namespace RimArt
{
    /// <summary>
    /// One beam of the Last Prism, from the warmup until the beam has faded (VergilCast's shape, for one ability). The
    /// cast job (<see cref="JobDriver_CastLastPrism"/>) makes it when the warmup begins; the ability fires it
    /// (<see cref="MarkFired"/>), which starts the channel. Its clock is the picture's: 0 at the start of the warmup, in
    /// game ticks, so the rules use the picture's own beams (<see cref="LastPrismTiming.FanBeam"/>) and hit where the
    /// beams are drawn.
    ///
    /// Each tick while it fires (agreed 2026-09-30 and 2026-10-02):
    /// - it spends 1/60 s of the prism's charge; at 0 the beam stops (dried: it flickers out);
    /// - a pawn target that is down, dead or gone is replaced by the standing enemy within range, with no wall in the
    ///   way, that needs the smallest turn; with none the beam stops. A cell target is held until Stop or the charge runs out;
    /// - the aim turns toward the target at turnDegreesPerSecond;
    /// - the fan (before joinSeconds) burns each standing pawn a beam crosses, at most once per fanEverySeconds; the
    ///   joined beam burns each standing pawn in its lane, each on its own joinedEverySeconds timer, so a pawn the beam
    ///   sweeps over always takes a hit. Friend or foe; never the wielder; downed pawns are passed over.
    ///
    /// The hit timers and the picture's down times are not saved: a game loaded mid-beam can hit a pawn once early.
    /// </summary>
    public sealed class LastPrismCast : IExposable
    {
        public Pawn caster;
        public ThingWithComps prism;
        public Map home;
        /// <summary>The tick the clock reads 0: the start of the warmup.</summary>
        public int startTick;
        /// <summary>The tick the beams started, -1 before; set back by joinSeconds when the beam starts joined (rejoinSeconds).</summary>
        public int channelTick = -1;
        /// <summary>The tick the beam stopped, -1 while it fires.</summary>
        public int releaseTick = -1;
        /// <summary>It stopped because the charge ran out.</summary>
        public bool dried;
        public LocalTargetInfo target;
        /// <summary>Where the prism points, radians, 0 east, pi/2 north.</summary>
        public float aim;

        private readonly Dictionary<Pawn, int> fanHits = new Dictionary<Pawn, int>(), joinedHits = new Dictionary<Pawn, int>();
        private readonly Dictionary<Pawn, int> downs = new Dictionary<Pawn, int>();
        private readonly Fan fan = new Fan();
        private bool shaken;
        private LastPrismWalls walls;

        private static readonly List<Pawn> candidates = new List<Pawn>();

        public CompProperties_LastPrismFire Props => CompProperties_LastPrismFire.Of;
        public CompLastPrism Comp => prism?.GetComp<CompLastPrism>();
        public bool Fired => channelTick >= 0;
        public bool Firing => Fired && releaseTick < 0;
        public bool HeldOnCell => !target.HasThing;

        /// <summary>The clock on whole ticks, for the rules.</summary>
        public double Seconds(int tick) => (tick - startTick) / 60.0;

        public bool Joined(int now) => Fired && now - channelTick >= Ticks(Props.joinSeconds);

        private static int Ticks(float seconds) => Mathf.RoundToInt(seconds * 60f);

        private Vector2 Wielder => Ground(caster.DrawPos);

        /// <summary>
        /// The beams start now, aimed straight at the target as the sketch's are. They start joined when this cast's
        /// warmup began (<see cref="startTick"/>: the tick Fire was pressed) within rejoinSeconds of the last beam from
        /// this prism stopping; measured from the fire tick the warmup would eat 0.3 s of the window.
        /// </summary>
        public void MarkFired(int now)
        {
            if (Fired) return;
            home = caster.Map;
            channelTick = now;
            CompLastPrism comp = Comp;
            if (comp != null && startTick - comp.lastReleaseTick <= Ticks(Props.rejoinSeconds))
            {
                channelTick = now - Ticks(Props.joinSeconds);
                shaken = true;
            }
            aim = Toward(Wielder, Point(target), aim);
        }

        /// <summary>
        /// Retarget: the beam turns toward <paramref name="to"/> from where it points now, joined or not. A standing pawn
        /// is followed; a downed or dead one, or anything else, is held as its cell (<see cref="Gone"/> would otherwise
        /// drop a downed pawn on the next tick and swing the beam elsewhere or stop it).
        /// </summary>
        public void Retarget(LocalTargetInfo to)
        {
            if (!Firing || !to.IsValid) return;
            target = to.Thing is Pawn pawn && !pawn.Dead && !pawn.Downed ? new LocalTargetInfo(pawn) : new LocalTargetInfo(to.Cell);
        }

        public void Release(int now, bool ranDry)
        {
            if (!Firing) return;
            releaseTick = now;
            dried = ranDry;
            CompLastPrism comp = Comp;
            if (comp != null) comp.lastReleaseTick = now;
        }

        /// <summary>One game tick. False once the cast is over and its picture has gone.</summary>
        public bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            if (!Firing) return now - releaseTick < Ticks(T.Sputter + 0.15f);
            CompLastPrism comp = Comp;
            if (caster == null || caster.Dead || !caster.Spawned || caster.Map != home || comp == null || CompLastPrism.HeldBy(caster) != comp)
            {
                Release(now, false);
                return true;
            }
            if (comp.Level <= 0f)
            {
                Release(now, true);
                return true;
            }
            if (Gone(target))
            {
                Pawn next = Pick();
                if (next == null)
                {
                    Release(now, false);
                    return true;
                }
                target = next;
            }
            aim = TurnToward(aim, Toward(Wielder, Point(target), aim), Props.turnDegreesPerSecond * Mathf.Deg2Rad / 60f);
            Hits(now);
            comp.Level -= 1f / 60f;
            if (comp.Level <= 0f) Release(now, true);
            if (!shaken && Joined(now))
            {
                shaken = true;
                if (home == Find.CurrentMap) Find.CameraDriver?.shaker?.DoShake(0.05f);
            }
            return true;
        }

        /// <summary>A pawn target that no longer stands on the beam's map. A cell is never gone.</summary>
        private bool Gone(LocalTargetInfo t)
        {
            if (!t.HasThing) return false;
            Thing thing = t.Thing;
            return thing.Destroyed || !thing.Spawned || thing.Map != home || thing is Pawn pawn && (pawn.Dead || pawn.Downed);
        }

        /// <summary>The next target: a standing enemy of the wielder within range of the tip, with no wall in the way, needing the smallest turn.</summary>
        public Pawn Pick()
        {
            Vector2 tip = Tip(Wielder, aim);
            float range = Props.Range;
            Pawn best = null;
            double least = double.PositiveInfinity;
            IReadOnlyList<Pawn> pawns = home.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == caster || pawn.Dead || pawn.Downed || !pawn.HostileTo(caster)) continue;
                Vector2 at = Ground(pawn.DrawPos);
                float d = Vector2.Distance(tip, at), angle = Toward(tip, at, aim);
                if (d > range || Reach(home, tip, angle, d) < d - 0.01f) continue;
                double turn = System.Math.Abs(T.Wrap(angle - aim));
                if (turn < least)
                {
                    least = turn;
                    best = pawn;
                }
            }
            return best;
        }

        private void Hits(int now)
        {
            CompProperties_LastPrismFire p = Props;
            Vector2 tip = Tip(Wielder, aim);
            float range = p.Range;
            bool joined = Joined(now);
            float lane = 0f;
            if (joined) lane = Reach(home, tip, aim, range);
            else fan.Set(home, tip, aim, Seconds(now), Seconds(channelTick), p);

            candidates.Clear();
            IReadOnlyList<Pawn> pawns = home.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == caster || pawn.Dead || pawn.Downed) continue;
                if ((Ground(pawn.DrawPos) - tip).sqrMagnitude <= (range + 1f) * (range + 1f)) candidates.Add(pawn);
            }
            // Copied first: a hit can kill a pawn and change the map's pawn list.
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn pawn = candidates[i];
                if (pawn.Dead || pawn.Downed || !pawn.Spawned) continue;
                Vector2 at = Ground(pawn.DrawPos);
                if (joined)
                {
                    if (!T.OnLine(at.x, at.y, tip.x, tip.y, aim, lane, p.width / 2.0) || Recent(joinedHits, pawn, now, p.joinedEverySeconds)) continue;
                    joinedHits[pawn] = now;
                    Hurt(pawn, p.joinedDamage, p.joinedArmorPenetration, now);
                }
                else
                {
                    if (!fan.Crosses(at, p.fanReach) || Recent(fanHits, pawn, now, p.fanEverySeconds)) continue;
                    fanHits[pawn] = now;
                    Hurt(pawn, p.fanDamage, p.fanArmorPenetration, now);
                }
            }
            candidates.Clear();
        }

        private static bool Recent(Dictionary<Pawn, int> hits, Pawn pawn, int now, float every) =>
            hits.TryGetValue(pawn, out int last) && now - last < Ticks(every);

        private void Hurt(Pawn pawn, float amount, float armorPenetration, int now)
        {
            var dinfo = new DamageInfo(Props.damageDef ?? DamageDefOf.Burn, amount, armorPenetration, AngleFlat(aim), caster, null, prism?.def);
            pawn.TakeDamage(dinfo);
            if ((pawn.Dead || pawn.Downed) && !downs.ContainsKey(pawn)) downs[pawn] = now;
        }

        // ---- the picture ------------------------------------------------------------------------------------------

        private static readonly LastPrismPawn[] shown = new LastPrismPawn[64];

        /// <summary>The beam this frame, from the cast; before the fire the idle prism turned to the target.</summary>
        public void Draw()
        {
            if (caster == null || !caster.Spawned || (Fired && caster.Map != home)) return;
            Map map = caster.Map;
            float s = UbwClock.Since(startTick);
            CompProperties_LastPrismFire p = Props;
            CompLastPrism comp = Comp;
            if (walls == null) walls = (from, radians, max) => Reach(caster?.Map, from, radians, max);
            var shot = new LastPrismShot
            {
                Wielder = Wielder, Aim = aim * Mathf.Rad2Deg, Firing = Fired,
                ChannelAt = Fired ? (float)Seconds(channelTick) : 0f,
                ReleaseAt = releaseTick >= 0 ? (float)Seconds(releaseTick) : float.PositiveInfinity,
                Dried = dried, Join = p.joinSeconds, Fan = p.fanDegrees, Range = p.Range, Width = p.width, HitReach = p.fanReach,
                Walls = walls, Target = Point(target), Pawns = shown, Roofed = caster.Position.Roofed(map),
                Level = comp?.Level ?? 0f, Store = comp?.Props.store ?? 0f,
            };
            if (Fired) shot.PawnCount = Shown(map, Tip(shot.Wielder, aim), p.Range + 2f);
            LastPrismGraphics.Draw(shot, s, map);
        }

        // The pawns near the beam for its hit flickers and smoke: those it downed smoke from then; others already down do neither.
        private int Shown(Map map, Vector2 tip, float near)
        {
            int n = 0;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count && n < shown.Length; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == caster) continue;
                Vector2 at = Ground(pawn.DrawPos);
                if ((at - tip).sqrMagnitude > near * near) continue;
                float down = downs.TryGetValue(pawn, out int tick) ? (float)Seconds(tick) : pawn.Downed ? -1e6f : float.PositiveInfinity;
                shown[n++] = new LastPrismPawn { At = at, DownAt = down };
            }
            return n;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster", true);
            Scribe_References.Look(ref prism, "prism");
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref channelTick, "channelTick", -1);
            Scribe_Values.Look(ref releaseTick, "releaseTick", -1);
            Scribe_Values.Look(ref dried, "dried");
            Scribe_TargetInfo.Look(ref target, "target");
            Scribe_Values.Look(ref aim, "aim");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) shaken = Fired;
        }
    }
}
