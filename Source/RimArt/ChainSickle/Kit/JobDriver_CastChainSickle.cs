using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Snag and Stake. It is JobDriver_CastAbility with the two additions
    /// JobDriver_CastWaterGun makes: the picture is told the cast has begun when the warmup starts,
    /// because the weight is spun up (Snag) or the chain held taut (Stake) during the warmup; and the
    /// job holds the holder in place until the reel is over (Snag) or the weight is in the floor
    /// (Stake). The picture draws the sickle, so the job hides the held weapon (neverShowWeapon).
    /// A reeled target leaves the map inside a flyer, which ends this job early (FailOnDespawnedOrNull);
    /// the picture and the reel go on without it.
    /// </summary>
    public class JobDriver_CastChainSickle : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_ChainSickle>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("ChainSickleBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            cast.FailOn(() => !job.ability.CanCast && !job.ability.Casting);
            yield return cast;

            Toil hold = ToilMaker.MakeToil("ChainSickleHold");
            hold.tickAction = () =>
            {
                MapComponent_ChainSickle casts = pawn.Map?.GetComponent<MapComponent_ChainSickle>();
                if (casts == null || !casts.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_ChainSickle>().Begin(pawn, job.ability, job.targetA);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "chainSicklePictureTick", -1);
        }
    }
}
