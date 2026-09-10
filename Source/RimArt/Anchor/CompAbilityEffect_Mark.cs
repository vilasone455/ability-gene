using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityMark : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityMark()
        {
            compClass = typeof(CompAbilityEffect_Mark);
        }
    }

    /// <summary>
    /// Places or lifts a mark. Marking is the expensive half of the gene and the range on the
    /// AbilityDef is the reason: you have to have been near the thing you intend to move, which
    /// makes the clap a plan rather than a reaction.
    ///
    /// Marking something already marked lifts it instead, so a slot can be freed without
    /// waiting out the duration.
    /// </summary>
    public class CompAbilityEffect_Mark : CompAbilityEffect
    {
        public new CompProperties_AbilityMark Props => (CompProperties_AbilityMark)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            if (gene == null) return;

            Anchor existing = gene.AnchorFor(target);
            if (existing != null)
            {
                gene.Remove(existing);
                Messages.Message("AG_AnchorLifted".Translate(caster.LabelShort, existing.Label),
                    caster, MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            int now = Find.TickManager.TicksGame;
            Anchor anchor = target.Pawn != null
                ? new Anchor(target.Pawn, now)
                : new Anchor(target.Cell, now);

            gene.Add(anchor);
            Messages.Message("AG_AnchorPlaced".Translate(caster.LabelShort, anchor.Label),
                caster, MessageTypeDefOf.NeutralEvent, false);
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

            // Lifting an existing mark is always allowed, including when the carrier is full.
            if (gene.IsMarked(target)) return base.Valid(target, throwMessages);

            if (target.Pawn == null)
            {
                Map map = caster.Map;
                IntVec3 cell = target.Cell;
                if (map == null || !cell.IsValid || !cell.InBounds(map) || !cell.Standable(map))
                {
                    if (throwMessages)
                    {
                        Messages.Message("AG_AnchorBadTile".Translate(),
                            caster, MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }

            if (gene.LiveCount >= gene.MaxAnchors)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_AnchorFull".Translate(caster.LabelShort, gene.MaxAnchors),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
