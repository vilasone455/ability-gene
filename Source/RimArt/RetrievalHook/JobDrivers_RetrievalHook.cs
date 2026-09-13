using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Stationary reload. Target A is the belt.
    ///
    /// Progress lives on the belt, so an interrupted job loses nothing; the job is not
    /// suspendable, so it does not resume on its own and the player has to order it again.
    /// </summary>
    public class JobDriver_ReelInTether : JobDriver
    {
        private CompRetrievalHookBelt Belt => job.targetA.Thing?.TryGetComp<CompRetrievalHookBelt>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Belt == null || Belt.Wearer != pawn || Belt.Loaded);
            this.FailOn(() => MapComponent_RetrievalHooks.IsPulling(pawn));

            Toil reel = ToilMaker.MakeToil("ReelInTether");
            reel.initAction = () => pawn.pather.StopDead();
            reel.tickIntervalAction = delta =>
            {
                if (Belt.AddReloadWork(delta))
                {
                    Messages.Message(pawn.LabelShortCap + " reeled in the retrieval hook. It is ready to fire.",
                        pawn, RimWorld.MessageTypeDefOf.SilentInput, false);
                    ReadyForNextToil();
                }
            };
            reel.defaultCompleteMode = ToilCompleteMode.Never;
            reel.WithProgressBar(TargetIndex.None, () => Belt?.ReloadFraction ?? 0f);
            yield return reel;
        }
    }

    /// <summary>
    /// Holds the wearer in place while their net flies and the target is dragged. Target A is the
    /// target. The pull itself runs in <see cref="MapComponent_RetrievalHooks"/>; this job only
    /// keeps the wearer still, carries the target reservation, and ends when the pull does.
    /// Ordering the wearer to do anything else ends this job, which interrupts the pull.
    /// </summary>
    public class JobDriver_RetrievalHookPull : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil hold = ToilMaker.MakeToil("HoldRetrievalHook");
            hold.initAction = () => pawn.pather.StopDead();
            hold.tickAction = () =>
            {
                RetrievalPull pull = MapComponent_RetrievalHooks.PullOf(pawn);
                if (pull == null)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                IntVec3 face = pull.NetPosition.ToIntVec3();
                if (face != pawn.Position) pawn.rotationTracker.FaceCell(face);
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }
    }
}
