using RimWorld;
using Verse;

namespace AbilityGenes
{
    public class CompProperties_AbilityCollapse : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityCollapse()
        {
            compClass = typeof(CompAbilityEffect_Collapse);
        }
    }

    /// <summary>
    /// Ends the volume and everything in it.
    ///
    /// Castable only from outside, which is the one line of code that keeps this from being an
    /// accident. Swallow something and then collapse and you have an unconditional kill with no
    /// corpse - and it costs the carrier their home, everything stored in it, and every round
    /// the hole has ever taken. That price needed no invented number: it is just what the
    /// ability does, read honestly.
    /// </summary>
    public class CompAbilityEffect_Collapse : CompAbilityEffect
    {
        public new CompProperties_AbilityCollapse Props => (CompProperties_AbilityCollapse)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null || gene.Volume == null) return;

            gene.CollapseVolume();

            Messages.Message("AG_InvoluteCollapsed".Translate(caster.LabelShortCap),
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
                    Messages.Message("AG_InvoluteCantCollapseInside".Translate(caster.LabelShortCap),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (gene.Volume == null)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_InvoluteNoVolume".Translate(),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
