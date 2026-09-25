using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Suck, Spit and Digest: JobDriver_CastFlameGauntlet's shape. The picture is told
    /// the cast has begun when the warmup starts, because the canister rises out of the floor during
    /// the warmup; the job holds the holder in place until the canister is back in the floor. The
    /// picture draws the wand, the hose and the canister, so the job hides the held weapon
    /// (neverShowWeapon). Digest runs under its own job def, which the player can interrupt; the chew
    /// then stops and what is left stays inside.
    ///
    /// The vanilla fail conditions apply only until the ability fires (CastJobFail.FailBeforeFired).
    /// </summary>
    public class JobDriver_CastVacuum : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_Vacuum>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("VacuumBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("VacuumHold");
            hold.tickAction = () =>
            {
                MapComponent_Vacuum vacuums = pawn.Map?.GetComponent<MapComponent_Vacuum>();
                if (vacuums == null || !vacuums.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pawn.Map?.GetComponent<MapComponent_Vacuum>()?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_Vacuum>().Begin(pawn, job.ability, job.targetA);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "vacuumPictureTick", -1);
        }
    }
}
