using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The Susanoo's cast job: JobDriver_CastVacuum's shape without the hold. The picture is told
    /// the cast has begun when the warm-up starts, because the figure grows out of the floor during
    /// it; the ability then adds the hediff, which completes the figure. A cast that is interrupted
    /// leaves a half-formed figure that fades on its own.
    /// </summary>
    public class JobDriver_CastSusanoo : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
            });

            Toil begin = ToilMaker.MakeToil("SusanooBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
        }

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            MapComponent_Susanoo.For(pawn)?.Begin(pawn, job.ability.def.verbProperties.warmupTime);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "susanooPictureTick", -1);
        }
    }
}
