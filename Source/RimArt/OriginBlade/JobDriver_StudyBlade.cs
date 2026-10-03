using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    public class JobDriver_StudyBlade : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
            this.FailOn(() => !OriginBladeUtility.StudyAdds(pawn, TargetThingA));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil study = Toils_General.Wait(OriginBladeUtility.StudyTicks, TargetIndex.A);
            study.WithProgressBarToilDelay(TargetIndex.A);
            study.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            study.tickAction = () => pawn.rotationTracker.FaceTarget(TargetA);
            yield return study;
            yield return Toils_General.Do(() => Current.Game.GetComponent<GameComponent_BladeStudy>()
                .CompleteStudy(pawn, TargetThingA));
        }
    }

    public class FloatMenuOptionProvider_StudyBlade : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context) => OriginBladeUtility.CanStudy(context.FirstSelectedPawn);

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!OriginBladeUtility.IsBlade(clickedThing.def)) return null;
            Pawn pawn = context.FirstSelectedPawn;
            if (OriginBladeUtility.HasOrigin(pawn)) return TraceStudyOption(pawn, clickedThing);
            BladeStudyRecord record = Current.Game.GetComponent<GameComponent_BladeStudy>().RecordFor(pawn);
            string label = "AG_OriginBladeStudy".Translate(clickedThing.LabelShort,
                record.bladeTypes.Count, OriginBladeUtility.BladesRequired);
            if (record.bladeTypes.Contains(clickedThing.def.defName))
                return new FloatMenuOption(label + ": " + "AG_OriginBladeAlreadyStudied".Translate(), null);
            if (record.bladeTypes.Count >= OriginBladeUtility.BladesRequired)
                return new FloatMenuOption(label + ": " + "AG_OriginBladeStudiesComplete".Translate(), null);
            if (!pawn.CanReserveAndReach(clickedThing, PathEndMode.Touch, Danger.Some))
                return new FloatMenuOption(label + ": " + "AG_OriginBladeUnavailable".Translate(), null);

            return new FloatMenuOption(label, () => Find.WindowStack.Add(new Dialog_MessageBox(
                OriginBladeUtility.ProgressText(pawn), "AG_OriginBladeBeginStudy".Translate(), () =>
                {
                    clickedThing.SetForbidden(false);
                    pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(OriginBladeDefOf.AG_StudyBlade, clickedThing),
                        JobTag.Misc);
                }, "Cancel".Translate()))) { tooltip = "AG_OriginBladeWarning".Translate().ToString() };
        }

        /// <summary>
        /// After awakening: a study for Trace On's library, started at once (the awakening's warning no longer
        /// applies). Refused when the library already has this weapon and material at this quality or better.
        /// </summary>
        private static FloatMenuOption TraceStudyOption(Pawn pawn, Thing blade)
        {
            string label = "AG_TraceStudy".Translate(blade.LabelShort);
            if (!TraceLibrary.Adds(TraceLibrary.Of(pawn), blade))
                return new FloatMenuOption(label + ": " + "AG_TraceStudyKnown".Translate(), null);
            if (!pawn.CanReserveAndReach(blade, PathEndMode.Touch, Danger.Some))
                return new FloatMenuOption(label + ": " + "AG_OriginBladeUnavailable".Translate(), null);
            return new FloatMenuOption(label, () =>
            {
                blade.SetForbidden(false);
                pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(OriginBladeDefOf.AG_StudyBlade, blade), JobTag.Misc);
            });
        }
    }
}
