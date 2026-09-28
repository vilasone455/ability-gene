using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimArt
{
    public sealed class GravityCast : IExposable
    {
        public int id, cooldownUntil, tailTicks;
        public Pawn caster;
        public Map map;
        public AbilityDef def;
        public IntVec3 anchor;
        // Where the well was cast, and where its centre is now (it drifts, see MapComponent_Gravity.Drift).
        public Vector3 origin, centre;
        public GravityClock clock = new GravityClock();
        // Total weight eaten; every growth rule reads it. Pawns are counted once, by thingIDNumber.
        public float eaten;
        public List<int> eatenPawns = new List<int>();
        // Echo charge paid at Begin; given back if the well is cancelled before it opens.
        public float paid;
        // Set when it implodes; burstHit is not saved.
        public float burstDamage, burstRadius;
        public List<Pawn> burstHit = new List<Pawn>();
        public CastClips.Handle animation;
        public bool restore;
        // Drift target, chosen again every DriftPickTicks. Not saved.
        public Thing driftTarget;
        public int driftPickTick = -1;
        private Sustainer sound;
        private CompProperties_AbilityGravityWell props;
        private int validTick = -1;
        private bool valid;
        private IntVec3 clearFrom = IntVec3.Invalid;
        private int clearTick;
        private readonly Dictionary<int, bool> clearCells = new Dictionary<int, bool>();

        public CompProperties_AbilityGravityWell Props => props ??= CompProperties_AbilityGravityWell.For(def)
            ?? new CompProperties_AbilityGravityWell();
        public bool Active => clock.phase != GravityPhase.Finished;
        public bool Busy => Active || animation != null || restore;
        public bool Field => clock.phase == GravityPhase.Channel;
        public Vector3 Centre => centre;
        public IntVec3 Cell => centre.ToIntVec3();
        public float Growth => Props.Growth(eaten);
        public float Radius => Props.PullRadius(eaten);
        public float BulletRadius => Props.BulletRadius(eaten);
        public int DurationTicks => Props.DurationTicks(eaten);
        public int TicksLeft => Field ? Mathf.Max(0, DurationTicks - clock.ticks) : DurationTicks;

        // Worked out once per tick: the pull, draw and bullet code ask it many times a tick.
        public bool Valid
        {
            get
            {
                int now = Find.TickManager.TicksGame;
                if (validTick != now) { validTick = now; valid = ComputeValid(); }
                return valid;
            }
        }
        private bool ComputeValid() => caster != null && caster.Spawned && caster.Map == map && !caster.Dead
            && !caster.Downed && !caster.InMentalState && !caster.stances.stunner.Stunned
            && caster.Position == anchor && GravityAcquisition.HasAbility(caster, def)
            && caster.CurJobDef?.defName == "AM_InAnimation"
            && GravityMovement.Clear(map, anchor, Cell);

        // GravityMovement.Clear from the centre's cell, kept per cell until the centre changes cell
        // or ClearRefreshTicks pass.
        public bool ClearTo(IntVec3 target)
        {
            if (!target.InBounds(map)) return false;
            int now = Find.TickManager.TicksGame;
            IntVec3 from = Cell;
            if (from != clearFrom || now - clearTick >= GravityRules.ClearRefreshTicks)
            { clearCells.Clear(); clearFrom = from; clearTick = now; }
            int index = map.cellIndices.CellToIndex(target);
            if (!clearCells.TryGetValue(index, out bool clear))
                clearCells[index] = clear = GravityMovement.Clear(map, from, target);
            return clear;
        }

        public void Eat(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                if (eatenPawns.Contains(pawn.thingIDNumber)) return;
                eatenPawns.Add(pawn.thingIDNumber);
                eaten += Props.Mass(pawn);
                return;
            }
            // A corpse of a pawn already counted alive is the same body.
            if (!(thing is Corpse corpse) || !eatenPawns.Contains(corpse.InnerPawn.thingIDNumber))
                eaten += Rounds.Is(thing) ? Props.RoundMass(thing) : Props.Mass(thing);
            thing.Destroy(DestroyMode.Vanish);
        }

        public void Tick()
        {
            if (tailTicks > 0) tailTicks--;
            if (restore)
            {
                restore = false;
                if (caster == null || !caster.Spawned || !GravityCastAnimation.Clip.TryRestore(caster, out animation))
                { Finish(false); return; }
            }
            if (!Active)
            {
                if (animation != null)
                {
                    if (tailTicks == 0 || caster == null || !caster.Spawned || caster.Downed
                        || caster.Position != anchor || !animation.Seek(0.65f + (30 - tailTicks) / 60f))
                        StopAnimation();
                }
                return;
            }
            if (!Valid) { Finish(false); return; }
            if (animation == null || !animation.Seek(clock.phase == GravityPhase.Opening
                ? clock.ticks / 60f : 0.5f + 0.15f * Growth))
            { Finish(false); return; }
            clock.Tick(DurationTicks);
            if (Field)
            {
                if (sound == null || sound.Ended)
                    sound = GravityDefOf.AG_GravityHum.TrySpawnSustainer(
                        SoundInfo.InMap(new TargetInfo(Cell, map), MaintenanceType.PerTick));
                if (sound != null)
                {
                    sound.info.pitchFactor = 0.8f + 0.4f * Growth;
                    sound.Maintain();
                }
            }
            // The map component moves, eats, pulses damage and implodes at the deadline.
        }

        public void Finish(bool implode)
        {
            if (!Active) return;
            bool burst = clock.Finish(implode);
            var component = map?.GetComponent<MapComponent_Gravity>();
            component?.Changed();
            if (clock.activated)
            {
                cooldownUntil = Find.TickManager.TicksGame + Props.CooldownTicks;
                GameComponent_Gravity.Instance.Commit(caster, Props.CooldownTicks);
                tailTicks = 30;
            }
            else if (paid > 0f)
            {
                GameComponent_Echoes.Get?.Refund(paid);
                paid = 0f;
            }
            sound?.End(); sound = null;
            if (burst)
            {
                burstDamage = Props.Damage(eaten);
                burstRadius = Props.BurstRadius(eaten);
                burstHit = component.DamagePawns(this, burstRadius, burstDamage);
                GravityDefOf.AG_GravityImplode.PlayOneShot(new TargetInfo(Cell, map));
            }
            // Recovery is cosmetic; gameplay has already ended and cooldown is committed.
            if (!burst) StopAnimation();
            component?.ReleaseUncontrolled();
        }

        public void StopAnimation()
        {
            bool owned = animation != null;
            animation?.Stop(); animation = null;
            if (owned && caster?.CurJobDef?.defName == "AM_InAnimation")
                caster.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref anchor, "anchor");
            IntVec3 cell = IntVec3.Invalid;
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref centre, "centre");
            Scribe_Values.Look(ref clock.phase, "phase");
            Scribe_Values.Look(ref clock.ticks, "ticks");
            Scribe_Values.Look(ref clock.activated, "activated");
            Scribe_Values.Look(ref clock.imploded, "imploded");
            Scribe_Values.Look(ref cooldownUntil, "cooldownUntil");
            Scribe_Values.Look(ref tailTicks, "tailTicks");
            Scribe_Values.Look(ref eaten, "eaten");
            Scribe_Collections.Look(ref eatenPawns, "eatenPawns", LookMode.Value);
            Scribe_Values.Look(ref paid, "paid");
            Scribe_Values.Look(ref burstDamage, "burstDamage");
            Scribe_Values.Look(ref burstRadius, "burstRadius");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Saves from before the rework hold a fixed cell and no def.
                def ??= DefDatabase<AbilityDef>.GetNamedSilentFail("AG_GravityWell");
                if (centre == Vector3.zero && cell.IsValid) centre = cell.ToVector3Shifted();
                if (origin == Vector3.zero) origin = centre;
                eatenPawns ??= new List<int>();
                restore = Active || (tailTicks > 0 && clock.imploded);
            }
        }
    }
}
