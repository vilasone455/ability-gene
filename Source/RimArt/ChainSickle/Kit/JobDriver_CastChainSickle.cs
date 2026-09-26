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
    ///
    /// The vanilla fail conditions (target despawned, ability can no longer be cast) apply only until
    /// the weight is thrown. After it the ability is on cooldown (Ability.PreActivate starts it), and
    /// a reeled target is off the map inside a flyer; either one ended the job at the throw, and an
    /// undrafted holder then fled out of the chain's reach before the reel.
    /// </summary>
    public class JobDriver_CastChainSickle : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Thrown);
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

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("ChainSickleHold");
            hold.tickAction = () =>
            {
                MapComponent_ChainSickle casts = pawn.Map?.GetComponent<MapComponent_ChainSickle>();
                if (casts == null || !casts.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Thrown() => pawn.Map?.GetComponent<MapComponent_ChainSickle>()?.Thrown(pawn) ?? false;

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
