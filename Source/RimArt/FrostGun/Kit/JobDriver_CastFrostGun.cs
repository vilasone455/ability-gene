using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Flash Freeze. It is JobDriver_CastAbility with two additions, as
    /// JobDriver_CastWaterGun has: the picture is told the cast has begun when the warmup starts,
    /// because frost gathers at the muzzle during the warmup; and the job holds until the beam has
    /// landed (<see cref="FrostGunFreezeTiming.HoldAfterHit"/> after it), with the gun kept aimed at the
    /// target by a cooldown stance, so the caster does not turn away while the beam is still crossing.
    /// No animation clip and no drawn gun: the held gun is Core's.
    /// </summary>
    public class JobDriver_CastFrostGun : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_FrostGun>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("FrostGunBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("FrostGunHold");
            hold.initAction = () =>
            {
                // Keeps Core's held gun aimed at the target while the beam crosses. Exactly as long as the
                // hold: the job's toils do not tick while the pawn is in a busy stance.
                int ticks = pawn.Map?.GetComponent<MapComponent_FrostGun>()?.HoldTicksLeft(pawn) ?? 0;
                if (ticks > 0 && job.targetA.IsValid && job.verbToUse != null && !(pawn.stances.curStance is Stance_Busy))
                    pawn.stances.SetStance(new Stance_Cooldown(ticks, job.targetA, job.verbToUse));
            };
            hold.tickAction = () =>
            {
                MapComponent_FrostGun guns = pawn.Map?.GetComponent<MapComponent_FrostGun>();
                if (guns == null || !guns.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pawn.Map?.GetComponent<MapComponent_FrostGun>()?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_FrostGun>().Begin(pawn, job.ability, job.targetA);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "frostGunPictureTick", -1);
        }
    }
}
