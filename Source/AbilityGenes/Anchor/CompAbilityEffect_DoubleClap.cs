using RimWorld;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Two selected targets, the same way the Skip psycast picks a thing and then a place.
    /// CompAbilityEffect_WithDest is what makes the targeter ask twice; the destination is
    /// Selected so the second pick is the player's rather than derived from the first.
    /// </summary>
    public class CompProperties_AbilityDoubleClap : CompProperties_AbilityTeleport
    {
        public CompProperties_AbilityDoubleClap()
        {
            compClass = typeof(CompAbilityEffect_DoubleClap);
            destination = AbilityEffectDestination.Selected;
        }
    }

    /// <summary>
    /// Any two of the carrier's marks change places with each other, chosen at cast time. The
    /// carrier does not move and is not one of the ends, which is what separates this from the
    /// clap: it is the version that puts somebody else somewhere dangerous.
    ///
    /// Both marks are spent. Holding three means one survives a double clap, which is the whole
    /// reason the carrier holds more than two.
    /// </summary>
    public class CompAbilityEffect_DoubleClap : CompAbilityEffect_WithDest
    {
        public new CompProperties_AbilityDoubleClap Props => (CompProperties_AbilityDoubleClap)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            if (gene == null) return;

            Anchor a = gene.AnchorFor(target);
            Anchor b = gene.AnchorFor(dest);
            if (a == null || b == null || a == b) return;

            gene.Remove(a);
            gene.Remove(b);
            AnchorSwap.Resolve(a, b);
        }

        /// <summary>
        /// Called twice with different meanings, which is the whole subtlety here.
        ///
        /// The first call is the targeter validating the first click, and it passes
        /// LocalTargetInfo.Invalid as the destination because the player has not been offered a
        /// second pick yet. Demanding a valid far end at that point refuses the first click and
        /// the destination step never opens at all - the ability silently does nothing.
        ///
        /// The second call is the real one, after CompAbilityEffect_WithDest has collected a
        /// destination. Only then is there a pair to check. There is no throwMessages here, so
        /// an invalid pair is refused by the targeter rather than explained; the two rules that
        /// make a pair invalid are stated in the ability description instead.
        /// </summary>
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Gene_Anchors gene = AnchorUtility.GeneOf(parent.pawn);
            if (gene == null) return false;
            if (!Valid(target)) return false;

            // No destination chosen yet: this is the first click, and the first mark is enough.
            if (!dest.IsValid) return base.CanApplyOn(target, dest);

            Anchor a = gene.AnchorFor(target);
            Anchor b = gene.AnchorFor(dest);
            if (a == null || b == null || a == b) return false;
            if (!a.IsOnPawn && !b.IsOnPawn) return false;

            return base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            if (gene == null) return false;

            if (!AnchorClapCheck.HandsFree(caster, throwMessages)) return false;

            if (gene.LiveCount < 2)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_AnchorNeedTwo".Translate(caster.LabelShort),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

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
