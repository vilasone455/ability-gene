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
        public IntVec3 anchor, cell;
        public GravityClock clock = new GravityClock();
        public float mass;
        public GravityCastAnimation.Handle animation;
        public bool restore;
        private Sustainer sound;
        public bool Active => clock.phase != GravityPhase.Finished;
        public bool Busy => Active || animation != null || restore;
        public bool Field => clock.phase == GravityPhase.Channel;
        public Vector3 Centre => cell.ToVector3Shifted();

        public bool Valid => caster != null && caster.Spawned && caster.Map == map && !caster.Dead
            && !caster.Downed && !caster.InMentalState && !caster.stances.stunner.Stunned
            && caster.Position == anchor && GravityAcquisition.HasEye(caster)
            && caster.CurJobDef?.defName == "AM_InAnimation"
            && GravityMovement.Clear(map, anchor, cell);

        public void Tick()
        {
            if (tailTicks > 0) tailTicks--;
            if (restore)
            {
                restore = false;
                if (caster == null || !caster.Spawned || !GravityCastAnimation.TryRestore(caster, out animation))
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
                ? clock.ticks / 60f : 0.5f + 0.15f * GravityRules.Clamp(mass / GravityRules.FullMass)))
            { Finish(false); return; }
            clock.Tick();
            if (Field)
            {
                if (sound == null || sound.Ended)
                    sound = GravityDefOf.AG_GravityHum.TrySpawnSustainer(
                        SoundInfo.InMap(new TargetInfo(cell, map), MaintenanceType.PerTick));
                if (sound != null)
                {
                    sound.info.pitchFactor = 0.8f + 0.4f * GravityRules.Clamp(mass / GravityRules.FullMass);
                    sound.Maintain();
                }
            }
            // The map controller applies the final pull/mass update and damage pulse before
            // the automatic implosion at this deadline.
        }

        public void Finish(bool implode)
        {
            if (!Active) return;
            // Commit before damage/animation callbacks: a kill or interruption cannot burst twice.
            if (implode && Field) mass = map.GetComponent<MapComponent_Gravity>().CoreMass(this);
            bool burst = clock.Finish(implode);
            if (clock.activated)
            {
                cooldownUntil = Find.TickManager.TicksGame + GravityRules.CooldownTicks;
                GameComponent_Gravity.Instance.Commit(caster);
                tailTicks = 30;
            }
            sound?.End(); sound = null;
            if (burst)
            {
                map.GetComponent<MapComponent_Gravity>().DamagePawns(this, GravityRules.BurstRadius, GravityRules.Damage(mass));
                GravityDefOf.AG_GravityImplode.PlayOneShot(new TargetInfo(cell, map));
            }
            // Recovery is cosmetic; gameplay has already ended and cooldown is committed.
            if (!burst) StopAnimation();
            map?.GetComponent<MapComponent_Gravity>().ReleaseUncontrolled();
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
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref clock.phase, "phase");
            Scribe_Values.Look(ref clock.ticks, "ticks");
            Scribe_Values.Look(ref clock.activated, "activated");
            Scribe_Values.Look(ref clock.imploded, "imploded");
            Scribe_Values.Look(ref cooldownUntil, "cooldownUntil");
            Scribe_Values.Look(ref tailTicks, "tailTicks");
            Scribe_Values.Look(ref mass, "mass");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) restore = Active || (tailTicks > 0 && clock.imploded);
        }
    }
}
