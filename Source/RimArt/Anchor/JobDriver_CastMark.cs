using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using F = RimArt.MarkFlick;

namespace RimArt
{
    /// <summary>
    /// The cast job of Mark. It is JobDriver_CastAbility with the additions JobDriver_CastClap
    /// makes, for the same reasons: the clip starts with the warmup, so the stone leaves the hand
    /// before the warmup ends and lands as it is placed; the stone throw picture is told the cast has
    /// begun, so the stone glows in the hand (or flares in its cell, to be taken back); and the job holds until the clip is done, because Melee Animation cancels a clip the
    /// moment its pawn is in any other job.
    ///
    /// Placing plays the card flick, lifting plays the catch. Both are throw clip sets, so they go
    /// through ThrowAnimation, which picks the facing and sets the aim; this job owns their clock.
    /// </summary>
    public class JobDriver_CastMark : JobDriver_CastAbility
    {
        private int startTick = -1;
        private bool lifting;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                ThrowAnimation.UpdateCustomThrow(pawn, job.def, 0f, true);
                pawn.MapHeld?.GetComponent<MapComponent_MarkFlicks>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("MarkBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            cast.AddPreTickAction(() => Seek());
            yield return cast;

            Toil hold = ToilMaker.MakeToil("MarkHold");
            hold.tickAction = () => { if (!Seek()) ReadyForNextToil(); };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pawn.Map?.GetComponent<MapComponent_MarkFlicks>()?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            if (startTick >= 0) return;
            startTick = Find.TickManager.TicksGame;
            Anchor taken = AnchorUtility.GeneOf(pawn)?.AnchorFor(job.targetA);
            lifting = taken != null;
            ThrowAnimation.TryThrow(pawn, job.targetA.Cell, null, lifting ? ThrowAnimation.MarkCatch : ThrowAnimation.MarkFlick, out _, job.def);
            pawn.Map.GetComponent<MapComponent_MarkFlicks>().Begin(pawn, job.targetA.Cell, job.ability.def.verbProperties.warmupTime, taken);
        }

        /// <summary>Moves the clip to now. False once the clip is over, or when there is none.</summary>
        private bool Seek()
        {
            float seconds = (Find.TickManager.TicksGame - startTick) / 60f;
            return seconds < (lifting ? F.CatchLength : F.FlickLength) && ThrowAnimation.UpdateCustomThrow(pawn, job.def, seconds);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "markStartTick", -1);
            Scribe_Values.Look(ref lifting, "markLifting");
        }
    }
}
