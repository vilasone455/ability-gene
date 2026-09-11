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
            this.FailOn(() => !OriginBladeUtility.CanStudy(pawn) || OriginBladeUtility.HasOrigin(pawn)
                || !OriginBladeUtility.IsBlade(TargetThingA?.def)
                || Current.Game.GetComponent<GameComponent_BladeStudy>().RecordFor(pawn)
                    .bladeTypes.Contains(TargetThingA.def.defName));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil study = Toils_General.Wait(OriginBladeUtility.StudyTicks, TargetIndex.A);
            study.WithProgressBarToilDelay(TargetIndex.A);
            study.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            study.tickAction = () => pawn.rotationTracker.FaceTarget(TargetA);
            yield return study;
            yield return Toils_General.Do(() => Current.Game.GetComponent<GameComponent_BladeStudy>()
                .CompleteStudy(pawn, TargetThingA.def));
        }
    }

    public class FloatMenuOptionProvider_StudyBlade : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context) =>
            OriginBladeUtility.CanStudy(context.FirstSelectedPawn)
            && !OriginBladeUtility.HasOrigin(context.FirstSelectedPawn);

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!OriginBladeUtility.IsBlade(clickedThing.def)) return null;
            Pawn pawn = context.FirstSelectedPawn;
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
    }
}
