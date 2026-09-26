using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Arc = RimArt.CoilGunArcTiming;

namespace RimArt
{
    /// <summary>
    /// Everything the Coil Gun does on a map:
    /// - plays Chain Arc's picture for real casts and lands each hit when the picture's bolt arrives;
    /// - draws the normal rounds' muzzle flashes and impacts (the rounds draw themselves in flight);
    /// - recharges the batteries of pawns holding a gun next to a charged battery building, once a
    ///   second, and draws the arc that jumps from that battery to the gun while it does.
    ///
    /// A cast is known from the moment its warmup starts (<see cref="Begin"/>, from
    /// JobDriver_CastCoilGun), because the coil charges during the warmup. The ability's comp then
    /// fires it (<see cref="LandArc"/>): the chain is decided there. The clock is game ticks. Nothing
    /// about a cast is saved: a game loaded mid-cast loses the rest of its picture and any hit not yet
    /// dealt (at most 0.3 s of it). The battery charge itself is saved on the gun.
    /// </summary>
    public class MapComponent_CoilGun : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster;
            public int startTick, warmupTicks;
            public bool landed, home;
            public CoilArcShot shot;
            public List<CoilArcLink> chain;
            public bool[] hitDone;
            public CompProperties_ChainArc props;
            public Pawn aimedAt;

            public float Seconds(int now) => Arc.Lead + (now - startTick) / 60f;
        }

        private struct Mark
        {
            public Vector2 at;
            public float aim;
            public bool onPawn, mech;
            public int tick, seed;
        }

        /// <summary>A cast whose warmup ran out this long ago without firing was interrupted.</summary>
        private const float Overdue = 0.25f;
        /// <summary>Seconds after the hold's end at which a cast whose job never ended is let go anyway.</summary>
        private const float Stray = 1f;
        private const int RefillInterval = 60;

        private readonly List<Cast> casts = new List<Cast>();
        private readonly List<Mark> impacts = new List<Mark>(), flashes = new List<Mark>();
        private readonly List<Pawn> holders = new List<Pawn>();
        /// <summary>The battery each holder took charge from at the last refill, for the recharge arc.</summary>
        private readonly Dictionary<Pawn, Thing> chargingFrom = new Dictionary<Pawn, Thing>();

        /// <summary>What a test reads back: every hit this map dealt, the damage asked for and the damage the target took.</summary>
        internal struct HitRecord
        {
            public Thing victim;
            /// <summary>Where it landed, for a round that hit nothing.</summary>
            public Vector3 at;
            public float amount, dealt;
            public bool arc;
            public int tick;
        }

        internal readonly List<HitRecord> hitsForTests = new List<HitRecord>();
        /// <summary>Rounds the gun has fired on this map (tests).</summary>
        internal int roundsLaunched;

        public MapComponent_CoilGun(Map map) : base(map) { }

        public void Register(Pawn pawn)
        {
            if (pawn != null && !holders.Contains(pawn)) holders.Add(pawn);
        }

        private static Vector2 Ground(Thing thing)
        {
            Vector3 at = thing.DrawPos;
            return new Vector2(at.x, at.z);
        }

        private static Vector2 Muzzle(Pawn caster, Vector2 toward)
        {
            Vector2 feet = Ground(caster), run = toward - feet;
            return feet + (run.sqrMagnitude < 0.0001f ? Vector2.right : run.normalized) * CoilGunGraphics.MuzzleAlong;
        }

        // ------------------------------------------------------------------ Chain Arc

        /// <summary>A cast's warmup has begun: the coil charges at the muzzle, pointed at the target.</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            if (caster == null || ability == null || !target.IsValid) return;
            Add(caster, target.Thing as Pawn, target, Find.TickManager.TicksGame, ability.def.verbProperties.warmupTime);
        }

        private Cast Add(Pawn caster, Pawn aimedAt, LocalTargetInfo target, int startTick, float warmup)
        {
            Vector2 to = aimedAt != null ? Ground(aimedAt) : new Vector2(target.Cell.x + 0.5f, target.Cell.z + 0.5f);
            int warmupTicks = Mathf.RoundToInt(warmup * 60f);
            var cast = new Cast
            {
                caster = caster, aimedAt = aimedAt, startTick = startTick, warmupTicks = warmupTicks,
                shot = new CoilArcShot { Muzzle = Muzzle(caster, to), Fire = Arc.Lead + warmupTicks / 60f, Hops = new CoilArcHop[0], Seed = caster.thingIDNumber % 997 + startTick % 101 },
            };
            casts.Add(cast);
            return cast;
        }

        /// <summary>The gun fires: the chain is decided now and each hit is scheduled on the picture's clock.</summary>
        public void LandArc(Pawn caster, Pawn first, float warmup, CompProperties_ChainArc props)
        {
            int now = Find.TickManager.TicksGame;
            Cast cast = casts.Find(c => c.caster == caster && !c.landed)
                ?? Add(caster, first, first, now - Mathf.RoundToInt(warmup * 60f), warmup);
            cast.landed = true;
            cast.props = props;
            cast.chain = CoilArcChain.Find(caster, first, props);
            lastChainForTests = cast.chain;
            cast.hitDone = new bool[cast.chain.Count];
            cast.shot.Fire = cast.Seconds(now);
            if (cast.chain.Count > 0) cast.shot.Muzzle = Muzzle(caster, Ground(cast.chain[0].pawn));
            var hops = new CoilArcHop[cast.chain.Count];
            for (int i = 0; i < hops.Length; i++)
            {
                CoilArcLink link = cast.chain[i];
                hops[i] = new CoilArcHop { At = Ground(link.pawn), Soaked = link.soaked, Mech = link.mech, Reach = link.reach };
            }
            cast.shot.Hops = hops;
        }

        /// <summary>The caster's cast job is over. A cast that never fired has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (casts[i].caster != caster) continue;
                if (!casts[i].landed) casts.RemoveAt(i);
                else casts[i].home = true;
            }
        }

        /// <summary>Whether the caster's cast has fired and its job has not ended: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && !casts[i].home) return true;
            return false;
        }

        /// <summary>Whether the caster's cast job should still hold it in place: the chain is still running from its muzzle.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast c = casts[i];
                if (c.caster == caster && c.landed && !c.home && c.Seconds(now) < Arc.Holds(c.shot.Fire, c.chain.Count)) return true;
            }
            return false;
        }

        /// <summary>The chain of the last cast that fired on this map, in order (tests).</summary>
        internal List<CoilArcLink> lastChainForTests;

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % RefillInterval == 0) Refill(RefillInterval / 60f);
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float seconds = cast.Seconds(now);
                if (!cast.landed)
                {
                    if (seconds > Arc.Lead + cast.warmupTicks / 60f + Overdue || cast.caster == null || !cast.caster.Spawned || cast.caster.Map != map) casts.RemoveAt(i);
                    continue;
                }
                // The cast job ends the hold itself (Ended, from its finish action); this is only for a job
                // that never says so. Setting it at the hold's end would make Fired false while the job's
                // CastVerb toil still waits out the aim cooldown, and CastJobFail would end it Incompletable.
                if (!cast.home && (seconds >= Arc.Holds(cast.shot.Fire, cast.chain.Count) + Stray || !cast.caster.Spawned || cast.caster.Downed)) cast.home = true;
                for (int h = 0; h < cast.chain.Count; h++)
                {
                    if (cast.hitDone[h] || seconds < Arc.Hit(cast.shot.Fire, h)) continue;
                    cast.hitDone[h] = true;
                    HitLink(cast, h);
                }
                if (seconds >= Arc.End(cast.shot.Fire, cast.chain.Count)) casts.RemoveAt(i);
            }
            for (int i = impacts.Count - 1; i >= 0; i--)
                if ((now - impacts[i].tick) / 60f >= CoilGunShotTiming.ImpactEnd) impacts.RemoveAt(i);
            for (int i = flashes.Count - 1; i >= 0; i--)
                if (now - flashes[i].tick > 10) flashes.RemoveAt(i);
        }

        private void HitLink(Cast cast, int index)
        {
            CoilArcLink link = cast.chain[index];
            Pawn victim = link.pawn;
            if (victim == null || !victim.Spawned || victim.Map != map || victim.Dead) return;
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(Arc.HitShake);
            float damage = CoilArcChain.Damage(link, cast.props);
            Vector2 from = index == 0 ? Ground(cast.caster) : Ground(cast.chain[index - 1].pawn);
            float angle = new Vector3(victim.DrawPos.x - from.x, 0f, victim.DrawPos.z - from.y).AngleFlat();
            DamageWorker.DamageResult result = victim.TakeDamage(new DamageInfo(CoilGunDefOf.AG_CoilBurn, damage, cast.props.armorPenetration, angle, cast.caster, null,
                CoilGunDefOf.AG_CoilGun, DamageInfo.SourceCategory.ThingOrUnknown, victim));
            Note(victim, damage, result?.totalDamageDealt ?? 0f, true);
        }

        // ------------------------------------------------------------------ the normal rounds

        public void AddFlash(Vector2 feet, float aimDegrees)
        {
            flashes.Add(new Mark { at = feet, aim = aimDegrees, tick = Find.TickManager.TicksGame });
            roundsLaunched++;
        }

        internal void Note(Thing victim, float amount, float dealt, bool arc, Vector3 at = default)
        {
            hitsForTests.Add(new HitRecord { victim = victim, at = at, amount = amount, dealt = dealt, arc = arc, tick = Find.TickManager.TicksGame });
            if (hitsForTests.Count > 64) hitsForTests.RemoveAt(0);
        }

        public void AddImpact(Vector2 at, bool onPawn, bool mech, int seed) =>
            impacts.Add(new Mark { at = at, onPawn = onPawn, mech = mech, tick = Find.TickManager.TicksGame, seed = seed % 9973 });

        // ------------------------------------------------------------------ recharge

        /// <summary>Tests only: runs the recharge as if <paramref name="seconds"/> had passed next to whatever battery each holder stands by.</summary>
        internal void RefillForTest(float seconds) => Refill(seconds);

        private void Refill(float seconds)
        {
            holders.Clear();
            chargingFrom.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                CompCoilGun gun = CompCoilGun.HeldBy(pawn);
                if (gun == null) continue;
                holders.Add(pawn);
                if (gun.Full) continue;
                CompProperties_CoilGun props = gun.Props;
                CompPowerBattery battery = NearestBattery(pawn, props.rechargeRadius, props.wattDaysPerCharge * 0.01f);
                if (battery == null) continue;
                float perCharge = Mathf.Max(0.0001f, props.wattDaysPerCharge);
                float want = Mathf.Min(props.chargePerSecond * seconds, props.capacity - gun.Charge);
                float added = gun.Add(Mathf.Min(want, battery.StoredEnergy / perCharge));
                if (added <= 0f) continue;
                battery.DrawPower(Mathf.Min(battery.StoredEnergy, added * perCharge));
                chargingFrom[pawn] = battery.parent;
            }
        }

        /// <summary>The nearest battery building of the pawn's faction whose nearest cell is within <paramref name="radius"/> and that stores more than <paramref name="least"/> Wd.</summary>
        public static CompPowerBattery NearestBattery(Pawn pawn, float radius, float least)
        {
            Map map = pawn.Map;
            CompPowerBattery best = null;
            float bestDist = float.MaxValue;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(pawn.Position, map, radius + 2f, true))
            {
                if (!(thing is Building building) || building.Faction != pawn.Faction) continue;
                CompPowerBattery battery = building.TryGetComp<CompPowerBattery>();
                if (battery == null || battery.StoredEnergy <= least) continue;
                float d = Mathf.Sqrt(building.OccupiedRect().ClosestDistSquaredTo(pawn.Position));
                if (d > radius + 0.001f || d >= bestDist) continue;
                best = battery;
                bestDist = d;
            }
            return best;
        }

        // ------------------------------------------------------------------ drawing

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            float clock = now / 60f;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                float seconds = cast.Seconds(now);
                if (!cast.landed)
                {
                    // Until it fires the clock stops just before the shot. The charge follows the target.
                    if (cast.aimedAt != null && cast.aimedAt.Spawned && cast.caster.Spawned) cast.shot.Muzzle = Muzzle(cast.caster, Ground(cast.aimedAt));
                    CoilGunArcGraphics.Draw(cast.shot, Mathf.Min(seconds, cast.shot.Fire - 0.001f), map);
                    continue;
                }
                for (int h = 0; h < cast.chain.Count; h++)
                {
                    Pawn p = cast.chain[h].pawn;
                    cast.shot.Hops[h].LiveAt = p != null && p.Spawned && p.Map == map ? Ground(p) : (Vector2?)null;
                }
                CoilGunArcGraphics.Draw(cast.shot, seconds, map);
            }
            for (int i = 0; i < flashes.Count; i++)
                CoilGunShotGraphics.Flash(flashes[i].at, flashes[i].aim, (now - flashes[i].tick) / 60f, map);
            for (int i = 0; i < impacts.Count; i++)
                CoilGunShotGraphics.Impact(impacts[i].at, impacts[i].onPawn, impacts[i].mech, (now - impacts[i].tick) / 60f, clock, impacts[i].seed, map);
            foreach (KeyValuePair<Pawn, Thing> pair in chargingFrom)
            {
                Pawn pawn = pair.Key;
                Thing battery = pair.Value;
                if (pawn == null || !pawn.Spawned || pawn.Map != map || battery == null || !battery.Spawned) continue;
                CompCoilGun gun = CompCoilGun.HeldBy(pawn);
                if (gun == null || !gun.Charging || gun.Full) continue;
                Vector2 feet = Ground(pawn), cell = Ground(battery);
                Vector2 gunAt = feet + (cell - feet).normalized * 0.25f + new Vector2(0f, -0.05f);
                VfxDraw.Begin(feet);
                CoilGunGraphics.Recharge(new Vector2(cell.x, cell.y + 0.35f), gunAt, clock, pawn.thingIDNumber % 997);
            }
        }
    }
}
