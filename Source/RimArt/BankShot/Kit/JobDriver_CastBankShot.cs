using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Bank Shot's charge. It is JobDriver_CastAbility with one addition, the one
    /// JobDriver_CastPowerPole makes: the picture is told the cast has begun when the warmup starts,
    /// because the charge glow builds on the barrel over the warmup. The warmup is the charge
    /// (1.5 s in the def), so the job holds the pawn for exactly that long; the bullet then flies on
    /// its own and the pawn is free.
    /// </summary>
    public class JobDriver_CastBankShot : JobDriver_CastAbility
    {
        private bool begun;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_BankShot>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("BankShotBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            cast.FailOn(() => !job.ability.CanCast && !job.ability.Casting);
            yield return cast;
        }

        private void Begin()
        {
            pawn.pather.StopDead();
            if (begun || job.ability == null || !job.ability.CanCast) return;
            begun = true;
            pawn.Map.GetComponent<MapComponent_BankShot>().Begin(pawn, job.ability, job.targetA);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref begun, "bankShotBegun");
        }
    }
}
