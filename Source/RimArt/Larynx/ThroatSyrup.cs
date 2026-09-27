using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// "Drink as throat syrup" when a pawn with a strained larynx right-clicks the syrup thing
    /// (herbal medicine, <see cref="LarynxExtension.syrupThing"/>). Found by FloatMenuMakerMap, which
    /// instantiates every FloatMenuOptionProvider subclass.
    /// </summary>
    public class FloatMenuOptionProvider_ThroatSyrup : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            LarynxExtension ext = LarynxExtension.Get;
            Pawn pawn = context.FirstSelectedPawn;
            if (pawn == null || ext.syrupThing == null || clickedThing.def != ext.syrupThing) return null;
            if (!pawn.health.hediffSet.HasHediff(LarynxDefOf.AG_LarynxWear)) return null;

            string label = "AG_LarynxSyrup".Translate(ext.syrupRelief.ToStringPercent("F0"));
            if (!pawn.CanReach(clickedThing, PathEndMode.ClosestTouch, Danger.Deadly))
                return new FloatMenuOption(label + ": " + "NoPath".Translate().CapitalizeFirst(), null);
            if (!pawn.CanReserve(clickedThing, 1, 1))
                return new FloatMenuOption(label + ": " + "Reserved".Translate().CapitalizeFirst(), null);

            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                clickedThing.SetForbidden(false, warnOnFail: false);
                Job job = JobMaker.MakeJob(LarynxDefOf.AG_DrinkThroatSyrup, clickedThing);
                job.count = 1;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }), pawn, clickedThing);
        }
    }

    /// <summary>
    /// Walk to the syrup thing, drink one for <see cref="LarynxExtension.syrupTicks"/>, and take
    /// <see cref="LarynxExtension.syrupRelief"/> off the throat. Target A is the stack.
    /// </summary>
    public class JobDriver_DrinkThroatSyrup : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            LarynxExtension ext = LarynxExtension.Get;

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

            Toil drink = Toils_General.Wait(ext.syrupTicks, TargetIndex.A);
            drink.WithProgressBarToilDelay(TargetIndex.A);
            yield return drink;

            Toil finish = ToilMaker.MakeToil("DrinkThroatSyrup");
            finish.initAction = () =>
            {
                Thing stack = job.targetA.Thing;
                if (stack == null || stack.Destroyed) return;
                Thing one = stack.stackCount > 1 ? stack.SplitOff(1) : stack;
                one.Destroy();
                Hediff wear = pawn.health.hediffSet.GetFirstHediffOfDef(LarynxDefOf.AG_LarynxWear);
                if (wear == null) return;
                wear.Severity -= ext.syrupRelief;
                if (wear.Severity <= 0.0001f) pawn.health.RemoveHediff(wear);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
