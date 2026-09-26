using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Shark Skin and Fusion: JobDriver_CastVacuum's shape. The picture is told the cast
    /// has begun when the warmup starts; the job holds the wielder in place until the tear and the flare
    /// are done (Shark Skin) or the merge is (Fusion). The picture draws the blade, so the job hides the
    /// held weapon (neverShowWeapon).
    ///
    /// The vanilla fail conditions apply only until the ability fires (CastJobFail.FailBeforeFired).
    /// </summary>
    public class JobDriver_CastSamehada : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_Samehada>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("SamehadaBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("SamehadaHold");
            hold.tickAction = () =>
            {
                MapComponent_Samehada samehada = pawn.Map?.GetComponent<MapComponent_Samehada>();
                if (samehada == null || !samehada.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pawn.Map?.GetComponent<MapComponent_Samehada>()?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_Samehada>().Begin(pawn, job.ability);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "samehadaPictureTick", -1);
        }
    }
}
