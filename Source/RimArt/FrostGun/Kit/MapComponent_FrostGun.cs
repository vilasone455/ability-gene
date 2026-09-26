using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using T = RimArt.FrostGunFreezeTiming;

namespace RimArt
{
    /// <summary>
    /// Everything the Frost Gun does on a map:
    /// - plays Flash Freeze's picture for real casts, from the warmup's start (JobDriver_CastFrostGun),
    ///   and freezes the target when the picture's beam arrives;
    /// - keeps the frozen pawns: each is stunned for the freeze; the next damage it takes (the Harmony
    ///   patch on Pawn.PostApplyDamage) shatters the ice for the extra damage, else it thaws;
    /// - draws the bolts' frost puffs and the haze of every Chilled pawn;
    /// - refills the coolant of the pawns holding a gun, once a second.
    ///
    /// The clock is game ticks. Casts and pictures are not saved: a game loaded mid-cast loses the rest
    /// of the picture and a freeze not yet landed. A landed freeze is saved in the pawn itself (the
    /// AG_FrostGunFrozen hediff and the stun), and the once-a-second scan puts its ice back.
    /// </summary>
    public class MapComponent_FrostGun : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster, target;
            public int startTick, warmupTicks;
            public bool landed, home, freezeDone;
            public FrostFreezeShot shot;
            public CompProperties_FlashFreeze props;

            public float Seconds(int now) => (now - startTick) / 60f;
        }

        private sealed class Frozen
        {
            public Pawn pawn, freezer;
            public CompProperties_FlashFreeze props;
            public int untilTick;
            public Cast picture;
            public bool shatterPending;
            public Thing breaker;
        }

        private struct Hit
        {
            public Vector2 at, aim;
            public bool onPawn;
            public int tick, seed;
        }

        /// <summary>A cast whose warmup ran out this long ago without landing was interrupted.</summary>
        private const float Overdue = 0.25f;
        /// <summary>Ticks between refills and between looks for who holds a gun or is Chilled or frozen.</summary>
        private const int ScanInterval = 60;

        private readonly List<Cast> casts = new List<Cast>();
        private readonly List<Frozen> frozen = new List<Frozen>();
        private readonly List<Hit> hits = new List<Hit>();
        private readonly List<Pawn> holders = new List<Pawn>();
        private readonly HashSet<Pawn> chilled = new HashSet<Pawn>();
        private readonly List<Pawn> buffer = new List<Pawn>();
        private bool shattering;

        /// <summary>Tests: the last shatter's extra damage as dealt, and the damage it did.</summary>
        internal DamageInfo? lastShatter;
        internal float lastShatterDealt = -1f;

        public MapComponent_FrostGun(Map map) : base(map) { }

        public void Register(Pawn pawn)
        {
            if (pawn != null && !holders.Contains(pawn)) holders.Add(pawn);
        }

        public void Chilled(Pawn pawn)
        {
            if (pawn != null) chilled.Add(pawn);
        }

        private static Vector2 Ground(Thing thing)
        {
            Vector3 at = thing.DrawPos;
            return new Vector2(at.x, at.z);
        }

        public bool IsFrozen(Pawn pawn) => frozen.Exists(f => f.pawn == pawn);

        /// <summary>Tests: ticks the pawn stays frozen, or -1.</summary>
        internal int FrozenTicksLeft(Pawn pawn)
        {
            Frozen f = frozen.Find(x => x.pawn == pawn);
            return f == null ? -1 : f.untilTick - Find.TickManager.TicksGame;
        }

        // ------------------------------------------------------------------ casts

        /// <summary>A cast's warmup has begun: frost gathers at the muzzle.</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            if (caster == null || ability == null || !(target.Thing is Pawn victim)) return;
            Add(caster, victim, Find.TickManager.TicksGame, ability.def.verbProperties.warmupTime);
        }

        private Cast Add(Pawn caster, Pawn victim, int startTick, float warmup)
        {
            var cast = new Cast
            {
                caster = caster, target = victim, startTick = startTick, warmupTicks = Mathf.RoundToInt(warmup * 60f),
            };
            cast.shot = new FrostFreezeShot { Warmup = cast.warmupTicks / 60f, Freeze = T.ScriptFreeze, ShatterAt = -1f, Seed = (caster.thingIDNumber * 31 + startTick) & 0xffff };
            Aim(cast);
            casts.Add(cast);
            return cast;
        }

        /// <summary>The muzzle and the target point, from where both pawns are drawn now.</summary>
        private static void Aim(Cast cast)
        {
            Vector2 from = Ground(cast.caster), to = cast.target != null ? Ground(cast.target) : from + Vector2.right;
            Vector2 run = to - from;
            cast.shot.Target = to;
            cast.shot.Muzzle = FrostGunGraphics.Muzzle(from, run.sqrMagnitude < 1e-4f ? Vector2.right : run.normalized);
        }

        /// <summary>The ability fired (end of the warmup): the beam leaves now and the freeze lands when it arrives.</summary>
        public void Land(Pawn caster, Pawn victim, float warmup, CompProperties_FlashFreeze props)
        {
            Cast cast = casts.Find(c => c.caster == caster && !c.landed)
                ?? Add(caster, victim, Find.TickManager.TicksGame - Mathf.RoundToInt(warmup * 60f), warmup);
            cast.landed = true;
            cast.target = victim;
            cast.props = props;
            cast.shot.Freeze = props.freezeSeconds;
            Aim(cast);
        }

        /// <summary>The caster's cast job is over. A cast that never landed has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (casts[i].caster != caster) continue;
                if (!casts[i].landed) casts.RemoveAt(i);
                else casts[i].home = true;
            }
        }

        /// <summary>Whether the caster's cast has landed and its job has not ended: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn caster) => casts.Exists(c => c.caster == caster && c.landed && !c.home);

        /// <summary>Whether the cast job should still hold the caster: the beam has not landed and faded yet.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            return casts.Exists(c => c.caster == caster && c.landed && !c.home && c.Seconds(now) < T.Hit(c.shot) + T.HoldAfterHit);
        }

        /// <summary>Ticks until <see cref="Holds"/> lets the caster go, 0 if it does not hold it.</summary>
        public int HoldTicksLeft(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            Cast cast = casts.Find(c => c.caster == caster && c.landed && !c.home);
            return cast == null ? 0 : Mathf.Max(0, Mathf.CeilToInt((T.Hit(cast.shot) + T.HoldAfterHit - cast.Seconds(now)) * 60f));
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % ScanInterval == 0) Scan();
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float s = cast.Seconds(now);
                if (!cast.landed)
                {
                    if (s > cast.warmupTicks / 60f + Overdue || cast.caster == null || !cast.caster.Spawned || cast.caster.Map != map) casts.RemoveAt(i);
                    continue;
                }
                // Normally the job's end sets home (Ended); this only lets go of a caster whose job is gone.
                if (!cast.home && (s >= T.Hit(cast.shot) + T.HoldAfterHit + 1f || !cast.caster.Spawned || cast.caster.Downed)) cast.home = true;
                if (!cast.freezeDone && s >= T.Hit(cast.shot))
                {
                    cast.freezeDone = true;
                    if (!Freeze(cast)) cast.shot.Missed = true;
                }
                if (s >= T.End(cast.shot) && !frozen.Exists(f => f.picture == cast)) casts.RemoveAt(i);
            }
            for (int i = frozen.Count - 1; i >= 0; i--)
            {
                Frozen f = frozen[i];
                bool gone = f.pawn == null || f.pawn.Dead || !f.pawn.Spawned || f.pawn.Map != map || f.pawn.Downed;
                if (f.shatterPending || gone)
                {
                    frozen.RemoveAt(i);
                    Shatter(f, !gone || f.shatterPending);
                }
                else if (now >= f.untilTick || !FlashFreeze.IsFrozen(f.pawn))
                {
                    frozen.RemoveAt(i);
                    Unfreeze(f);
                    Play(f.props?.soundThaw, f.pawn.Position);
                }
            }
            for (int i = hits.Count - 1; i >= 0; i--)
                if (now - hits[i].tick > FrostGunShotTiming.RimeLife * 60f) hits.RemoveAt(i);
        }

        /// <summary>The beam arrived: freeze the target if it can still be frozen. Returns whether it was.</summary>
        private bool Freeze(Cast cast)
        {
            Pawn victim = cast.target;
            CompProperties_FlashFreeze p = cast.props;
            if (p == null || victim == null || victim.Dead || !victim.Spawned || victim.Map != map || victim.Downed || IsFrozen(victim)) return false;
            int ticks = Mathf.Max(1, Mathf.RoundToInt(p.freezeSeconds * 60f));
            // Stunned, without the stun mote (the ice is the picture) and without turning to face anyone.
            victim.stances?.stunner?.StunFor(ticks, cast.caster, false, false, true);
            victim.pather?.StopDead();
            Hediff ice = victim.health.hediffSet.GetFirstHediffOfDef(FrostGunDefOf.AG_FrostGunFrozen) ?? victim.health.AddHediff(FrostGunDefOf.AG_FrostGunFrozen);
            ice.TryGetComp<HediffComp_Disappears>()?.SetDuration(ticks + 30);
            if (p.consumeCondition)
            {
                Remove(victim, WaterGunDefOf.AG_Soaked);
                Remove(victim, FrostGunDefOf.AG_FrostGunChilled);
            }
            frozen.Add(new Frozen { pawn = victim, freezer = cast.caster, props = p, untilTick = Find.TickManager.TicksGame + ticks, picture = cast });
            Play(p.soundFreeze, victim.Position);
            Shake(T.HitShake);
            return true;
        }

        private static void Remove(Pawn pawn, HediffDef def)
        {
            Hediff h = def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (h != null) pawn.health.RemoveHediff(h);
        }

        /// <summary>Takes the ice off: the stun (unless something else stunned it for longer) and the hediff.</summary>
        private static void Unfreeze(Frozen f)
        {
            Pawn pawn = f.pawn;
            if (pawn == null) return;
            StunHandler stunner = pawn.stances?.stunner;
            int left = f.untilTick - Find.TickManager.TicksGame;
            if (stunner != null && stunner.Stunned && stunner.StunTicksLeft <= left + 2) stunner.StopStun();
            if (pawn.health != null) Remove(pawn, FrostGunDefOf.AG_FrostGunFrozen);
        }

        /// <summary>The ice breaks: the picture shatters and, if a hit broke it, the pawn takes the extra damage.</summary>
        private void Shatter(Frozen f, bool damage)
        {
            Unfreeze(f);
            Cast picture = f.picture;
            if (picture != null) picture.shot.ShatterAt = picture.Seconds(Find.TickManager.TicksGame);
            IntVec3 at = f.pawn?.PositionHeld ?? IntVec3.Invalid;
            Play(f.props?.soundShatter, at);
            Shake(T.ShatterShake);
            Pawn pawn = f.pawn;
            if (!damage || pawn == null || pawn.Dead || f.props == null || f.props.shatterDamage < 1f) return;
            var dinfo = new DamageInfo(f.props.shatterDamageDef ?? DamageDefOf.Cut, f.props.shatterDamage, f.props.shatterArmorPenetration, -1f,
                f.breaker ?? f.freezer, null, FrostGunDefOf.AG_FrostGun, DamageInfo.SourceCategory.ThingOrUnknown, pawn);
            shattering = true;
            try
            {
                lastShatter = dinfo;
                lastShatterDealt = pawn.TakeDamage(dinfo).totalDamageDealt;
            }
            finally
            {
                shattering = false;
            }
        }

        /// <summary>From the Pawn.PostApplyDamage patch: the first damage a frozen pawn takes shatters its ice (on this map's next tick).</summary>
        public void Notify_Damaged(Pawn pawn, DamageInfo dinfo)
        {
            if (shattering || dinfo.Def == null || !dinfo.Def.harmsHealth || dinfo.Amount <= 0f) return;
            for (int i = 0; i < frozen.Count; i++)
            {
                if (frozen[i].pawn != pawn || frozen[i].shatterPending) continue;
                frozen[i].shatterPending = true;
                frozen[i].breaker = dinfo.Instigator;
            }
        }

        // ------------------------------------------------------------------ the shot

        public void ShotHit(Vector3 at, bool onPawn, Vector2 aim)
        {
            hits.Add(new Hit { at = new Vector2(at.x, at.z), aim = aim, onPawn = onPawn, tick = Find.TickManager.TicksGame, seed = (Find.TickManager.TicksGame * 7 + hits.Count * 131 + Mathf.RoundToInt(at.x * 31f + at.z * 17f)) & 0xffff });
            if (onPawn && Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(FrostGunShotTiming.HitShake);
        }

        // ------------------------------------------------------------------ once a second

        /// <summary>Refills the holders' coolant, and finds Chilled and frozen pawns (after a load, the freezes' ice).</summary>
        private void Scan()
        {
            holders.Clear();
            buffer.Clear();
            buffer.AddRange(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < buffer.Count; i++)
            {
                Pawn pawn = buffer[i];
                CompFrostGun gun = CompFrostGun.HeldBy(pawn);
                if (gun != null)
                {
                    holders.Add(pawn);
                    if (gun.Coolant < gun.Props.capacity) gun.Add(gun.RefillRate(pawn.Position, map) * ScanInterval / 60f);
                }
                if (pawn.health?.hediffSet == null) continue;
                if (FlashFreeze.ChilledStacks(pawn) > 0) chilled.Add(pawn);
                Hediff ice = pawn.health.hediffSet.GetFirstHediffOfDef(FrostGunDefOf.AG_FrostGunFrozen);
                if (ice != null && !IsFrozen(pawn)) Restore(pawn, ice);
            }
            chilled.RemoveWhere(p => p == null || !p.Spawned || p.Map != map || FlashFreeze.ChilledStacks(p) <= 0);
            buffer.Clear();
        }

        /// <summary>A pawn frozen before a load: its ice, already formed, for what is left of the freeze.</summary>
        private void Restore(Pawn pawn, Hediff ice)
        {
            int now = Find.TickManager.TicksGame;
            int left = Mathf.Max(1, (ice.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? 60) - 30);
            float formed = T.Grow + 0.05f;
            var cast = new Cast
            {
                caster = pawn, target = pawn, landed = true, home = true, freezeDone = true, props = FlashFreeze.Props,
                startTick = now - Mathf.RoundToInt(formed * 60f),
            };
            Vector2 at = Ground(pawn);
            cast.shot = new FrostFreezeShot { Muzzle = at, Target = at, Warmup = 0f, ShatterAt = -1f, Seed = pawn.thingIDNumber & 0xffff };
            cast.shot.Freeze = (now + left - cast.startTick) / 60f - T.Hit(cast.shot);
            casts.Add(cast);
            frozen.Add(new Frozen { pawn = pawn, freezer = null, props = FlashFreeze.Props, untilTick = now + left, picture = cast });
        }

        private void Play(SoundDef sound, IntVec3 cell)
        {
            if (sound == null || !cell.IsValid || !cell.InBounds(map)) return;
            sound.PlayOneShot(new TargetInfo(cell, map, false));
        }

        private void Shake(float size)
        {
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(size);
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
                float s = cast.Seconds(now);
                Pawn target = cast.target;
                cast.shot.LiveTarget = target != null && target.Spawned && target.Map == map ? Ground(target) : (Vector2?)null;
                if (!cast.landed)
                {
                    if (cast.caster != null && cast.caster.Spawned && target != null && target.Spawned) Aim(cast);
                    FrostGunFreezeGraphics.Draw(cast.shot, Mathf.Min(s, cast.shot.Warmup - 0.001f), map, false);
                }
                else FrostGunFreezeGraphics.Draw(cast.shot, s, map);
            }
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            for (int i = 0; i < hits.Count; i++)
            {
                Hit hit = hits[i];
                if (!VfxDraw.Shown(hit.at, map)) continue;
                VfxDraw.Begin(hit.at);
                FrostGunGraphics.Puff(hit.at, (now - hit.tick) / 60f, hit.seed, hit.onPawn, hit.aim, sun);
            }
            foreach (Pawn pawn in chilled)
            {
                if (pawn == null || !pawn.Spawned || pawn.Map != map || IsFrozen(pawn)) continue;
                int stacks = FlashFreeze.ChilledStacks(pawn);
                if (stacks <= 0) continue;
                Vector2 at = Ground(pawn);
                if (!VfxDraw.Shown(at, map)) continue;
                VfxDraw.Begin(at);
                FrostGunGraphics.ChillHaze(at, stacks, clock, pawn.thingIDNumber & 0xff);
            }
        }

        public override void MapRemoved()
        {
            casts.Clear();
            frozen.Clear();
            hits.Clear();
            chilled.Clear();
        }
    }
}
