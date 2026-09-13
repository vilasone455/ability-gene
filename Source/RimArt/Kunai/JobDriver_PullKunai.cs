using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Walk to a pawn and pull out every kunai stuck in it. Target A is the pawn.
    ///
    /// From anyone not fighting (downed, asleep, friendly, prisoner) all stuck kunai come out
    /// together after <see cref="KunaiDefaults.PullTicksCalm"/>. From a standing, awake, hostile
    /// pawn each kunai is its own <see cref="KunaiDefaults.PullTicksFighting"/> grip and melee roll,
    /// repeated (following the target if it moves) until none are left or the job is interrupted;
    /// a missed roll just costs that attempt. Whether the target is fighting is decided again
    /// before every grip, so an enemy that goes down mid-pull is finished the calm way.
    ///
    /// The target is not reserved, so two colonists can pull from the same enemy.
    /// </summary>
    public class JobDriver_PullKunai : JobDriver
    {
        private Pawn Target => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Target == null || KunaiEmbedding.Newest(Target) == null);

            Toil gotoTarget = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil gripFighting = Grip(KunaiDefaults.PullTicksFighting);
            Toil gripCalm = Grip(KunaiDefaults.PullTicksCalm);

            yield return gotoTarget;
            yield return Toils_Jump.JumpIf(gripCalm, () => !KunaiEmbedding.IsFighting(pawn, Target));

            // Fighting: one kunai per grip, then back round while any are left.
            yield return gripFighting;
            Toil pullOne = ToilMaker.MakeToil("PullKunaiOne");
            pullOne.initAction = () => KunaiEmbedding.TryPull(pawn, Target);
            pullOne.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pullOne;
            yield return Toils_Jump.JumpIf(gotoTarget, () => Target != null && KunaiEmbedding.Newest(Target) != null);
            Toil done = ToilMaker.MakeToil("PullKunaiDone");
            done.initAction = () => EndJobWith(JobCondition.Succeeded);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;

            // Calm: everything out in one grip.
            yield return gripCalm;
            Toil pullAll = ToilMaker.MakeToil("PullKunaiAll");
            pullAll.initAction = () =>
            {
                for (int i = 0; i < KunaiDefaults.MaxEmbeddedPerPawn * 4 && KunaiEmbedding.Newest(Target) != null; i++)
                {
                    if (KunaiEmbedding.IsFighting(pawn, Target) || !KunaiEmbedding.TryPull(pawn, Target)) break;
                }
            };
            pullAll.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pullAll;
        }

        private Toil Grip(int ticks)
        {
            Toil grip = Toils_General.Wait(ticks, TargetIndex.A);
            grip.WithProgressBarToilDelay(TargetIndex.A);
            grip.AddFailCondition(() => Target == null || !pawn.CanReachImmediate(Target, PathEndMode.Touch));
            return grip;
        }
    }
}
