using RimWorld;
using Verse;

namespace AbilityGenes
{
    public class CompProperties_AbilityVent : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityVent()
        {
            compClass = typeof(CompAbilityEffect_Vent);
        }
    }

    /// <summary>
    /// Connects the hole.
    ///
    /// The hole is always there and always costs - it is a hediff on a part and the carrier is
    /// permanently worse at whatever that part does. What this buys is somewhere for it to
    /// lead, which is the half that has to be a decision made under fire rather than a number
    /// sitting on the pawn.
    /// </summary>
    public class CompAbilityEffect_Vent : CompAbilityEffect
    {
        public new CompProperties_AbilityVent Props => (CompProperties_AbilityVent)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null) return;

            gene.EnsureHole();
            if (gene.HolePart == null) return;

            // Generating here rather than on the first thing to arrive means the cost of the
            // volume existing is paid by a button the player pressed.
            gene.EnsureVolume();

            Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(InvoluteDefOf.AG_InvoluteVented);
            if (existing != null) caster.health.RemoveHediff(existing);

            caster.health.AddHediff(InvoluteDefOf.AG_InvoluteVented);
            InvoluteRegistry.Report(caster);

            Messages.Message("AG_InvoluteVented".Translate(caster.LabelShortCap, gene.HolePart.LabelCap),
                caster, MessageTypeDefOf.NeutralEvent, false);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null) return false;

            if (gene.Inside)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_InvoluteAlreadyInside".Translate(caster.LabelShortCap),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
