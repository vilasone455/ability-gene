using RimWorld;
using Verse;

namespace AbilityGenes
{
    public class CompProperties_AbilityPost : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityPost()
        {
            compClass = typeof(CompAbilityEffect_Post);
        }
    }

    /// <summary>
    /// Pushes one item through.
    ///
    /// This is what makes the volume somewhere worth arriving at. A wounded pawn dropped into
    /// an empty void with no doctor is worse off than where they fell - time runs at normal
    /// rate in there, which was the whole point of not touching it. Minified furniture is an
    /// item, so a carrier who thought ahead has a bed, medicine and food in there before the
    /// raid starts, and one who did not has a hole to die in.
    /// </summary>
    public class CompAbilityEffect_Post : CompAbilityEffect
    {
        public new CompProperties_AbilityPost Props => (CompProperties_AbilityPost)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Thing item = target.Thing;
            if (item == null || item is Pawn) return;

            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null) return;

            Map volume = gene.EnsureVolume();
            if (volume == null) return;

            IntVec3 mouth = InvoluteUtility.MouthCell(volume);
            if (!mouth.IsValid) return;

            InvoluteUtility.FlashAt(item);

            if (item.Spawned) item.DeSpawn();
            GenPlace.TryPlaceThing(item, mouth, volume, ThingPlaceMode.Near);

            Messages.Message("AG_InvolutePosted".Translate(caster.LabelShortCap, item.LabelShortCap),
                caster, MessageTypeDefOf.NeutralEvent, false);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null) return false;

            if (target.Thing == null || target.Thing is Pawn)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_InvoluteNeedsItem".Translate(),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (!InvoluteUtility.IsOpen(gene))
            {
                if (throwMessages)
                {
                    Messages.Message("AG_InvoluteClosed".Translate(caster.LabelShortCap),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
