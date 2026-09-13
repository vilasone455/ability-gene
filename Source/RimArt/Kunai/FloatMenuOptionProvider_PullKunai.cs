using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// "Pull out kunai" when right-clicking a pawn with kunai stuck in it. One order pulls all of
    /// them (<see cref="JobDriver_PullKunai"/>). Found by RimWorld's
    /// FloatMenuMakerMap, which instantiates every FloatMenuOptionProvider subclass.
    ///
    /// Pulling from a standing, awake enemy needs the puller drafted and capable of violence, and
    /// shows the success chance. Anyone else can be pulled from drafted or undrafted.
    /// </summary>
    public class FloatMenuOptionProvider_PullKunai : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn puller = context.FirstSelectedPawn;
            int count = KunaiEmbedding.CountOn(clickedPawn);
            if (puller == null || count == 0) return null;

            string label = "Pull out kunai (" + count + " stuck in " + clickedPawn.LabelShort + ")";

            if (KunaiEmbedding.IsFighting(puller, clickedPawn))
            {
                if (puller.WorkTagIsDisabled(WorkTags.Violent))
                    return new FloatMenuOption(label + ": " + puller.LabelShort + " is incapable of violence", null);
                if (!puller.Drafted)
                    return new FloatMenuOption(label + ": draft to pull from a standing enemy", null);
                label += " - " + KunaiEmbedding.SuccessChance(puller, clickedPawn).ToStringPercent("F0") + " chance each";
            }

            if (!puller.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
                return new FloatMenuOption(label + ": no path", null);

            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                Job job = JobMaker.MakeJob(KunaiDefOf.AG_PullKunai, clickedPawn);
                puller.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }, MenuOptionPriority.Default, null, clickedPawn), puller, clickedPawn);
        }
    }
}
