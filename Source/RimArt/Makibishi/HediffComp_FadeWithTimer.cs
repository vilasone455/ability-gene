using Verse;

namespace RimArt
{
    public class HediffCompProperties_FadeWithTimer : HediffCompProperties
    {
        public HediffCompProperties_FadeWithTimer()
        {
            compClass = typeof(HediffComp_FadeWithTimer);
        }
    }

    /// <summary>
    /// Sets severity to the fraction of the hediff's <see cref="HediffComp_Disappears"/> timer left,
    /// so severity stages step down on exact times. Vanilla HediffComp_SeverityPerDay updates on a
    /// hashed 200-tick interval, which moves each stage change by up to 3.3 s per pawn.
    /// </summary>
    public class HediffComp_FadeWithTimer : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            HediffComp_Disappears timer = parent.TryGetComp<HediffComp_Disappears>();
            if (timer == null || timer.disappearsAfterTicks <= 0) return;
            // Kept above zero: Hediff.ShouldRemove at severity 0 would end it before the timer does.
            parent.Severity = UnityEngine.Mathf.Max(0.001f, (float)timer.ticksToDisappear / timer.disappearsAfterTicks);
        }
    }
}
