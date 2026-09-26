using Verse;

namespace RimArt
{
    public class HediffCompProperties_SamehadaFused : HediffCompProperties
    {
        /// <summary>Hit points healed off the fused pawn's injuries each second (SamehadaFeeding.Heal).</summary>
        public float hpPerSecond = 2f;

        public HediffCompProperties_SamehadaFused()
        {
            compClass = typeof(HediffComp_SamehadaFused);
        }
    }

    /// <summary>
    /// Fusion on the wielder: heals hpPerSecond once a second while it lasts, and sets the held blade to no
    /// charge when it ends. The move bonus is the hediff's stage; the length is its HediffComp_Disappears,
    /// set by the ability.
    /// </summary>
    public class HediffComp_SamehadaFused : HediffComp
    {
        private int ticks;

        public HediffCompProperties_SamehadaFused Props => (HediffCompProperties_SamehadaFused)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (++ticks < 60) return;
            ticks = 0;
            SamehadaFeeding.Heal(Pawn, Props.hpPerSecond);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            CompSamehada.HeldBy(Pawn)?.SetCharges(0);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticks, "regenTicks");
        }
    }
}
