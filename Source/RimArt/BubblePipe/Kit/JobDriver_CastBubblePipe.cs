using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Drifting Burst and Eye Pop. It is JobDriver_CastAbility with the two additions
    /// JobDriver_CastPowerPole makes: the picture is told the cast has begun when the warmup starts,
    /// because the pipe goes up to the mouth during the warmup (the warmup, 0.45 s, ends at the blow);
    /// and the job holds until the pipe is lowered again, so the caster does not walk off with it at
    /// the mouth. For Drifting Burst that is until 0.1 s after the last bubble leaves the tip, then
    /// the 0.3 s lowering: 1.85 s from the start with 6 bubbles. For Eye Pop it is 1.05 s.
    ///
    /// The job def has neverShowWeapon, so the held pipe is not drawn while the picture draws it.
    /// </summary>
    public class JobDriver_CastBubblePipe : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_BubblePipe>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("BubblePipeBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            cast.FailOn(() => !job.ability.CanCast && !job.ability.Casting);
            yield return cast;

            Toil hold = ToilMaker.MakeToil("BubblePipeHold");
            hold.tickAction = () =>
            {
                MapComponent_BubblePipe pipes = pawn.Map?.GetComponent<MapComponent_BubblePipe>();
                if (pipes == null || !pipes.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_BubblePipe>().Begin(pawn, job.ability, job.targetA);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "bubblePipePictureTick", -1);
        }
    }
}
