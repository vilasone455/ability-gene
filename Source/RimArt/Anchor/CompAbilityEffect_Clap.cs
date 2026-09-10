using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityClap : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityClap()
        {
            compClass = typeof(CompAbilityEffect_Clap);
        }
    }

    /// <summary>
    /// The carrier and one of their marks change places. A marked pawn is a swap - they arrive
    /// where the carrier was standing, which is the whole cost of the ability and the reason it
    /// is not simply an escape. A marked tile is a move, because a tile has nothing to send back.
    ///
    /// The mark is spent either way.
    /// </summary>
    public class CompAbilityEffect_Clap : CompAbilityEffect
    {
        public new CompProperties_AbilityClap Props => (CompProperties_AbilityClap)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            if (gene == null) return;

            Anchor anchor = gene.AnchorFor(target);
            if (anchor == null) return;

            gene.Remove(anchor);
            AnchorSwap.Resolve(caster, anchor);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            if (gene == null) return false;

            if (!AnchorClapCheck.HandsFree(caster, throwMessages)) return false;

            if (gene.AnchorFor(target) == null)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_AnchorNotMarked".Translate(),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
