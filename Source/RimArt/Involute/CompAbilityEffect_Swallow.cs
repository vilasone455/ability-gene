using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimArt
{
    public class CompProperties_AbilitySwallow : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySwallow()
        {
            compClass = typeof(CompAbilityEffect_Swallow);
        }
    }

    /// <summary>
    /// Puts one person into the volume.
    ///
    /// Deliberately not enemies-only. It is one rule - a pawn goes in - and what it means
    /// changes entirely with who it is pointed at. An enemy is taken out of the fight and put
    /// in the room the carrier lives in. An ally is pulled out of a firefight from across the
    /// field without anyone walking into the open to reach them.
    ///
    /// Getting back out is not this comp's business and needs no ability. A pawn who went in
    /// downed can be picked up and carried out through the fold; one who walked in on their own
    /// feet cannot be picked up at all, and stays until something puts them off them. That is
    /// the engine's own carry rule and it is the whole difference between a rescue and a
    /// prison.
    /// </summary>
    public class CompAbilityEffect_Swallow : CompAbilityEffect
    {
        public new CompProperties_AbilitySwallow Props => (CompProperties_AbilitySwallow)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;
            if (victim == null) return;

            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null) return;

            Map volume = gene.EnsureVolume();
            if (volume == null) return;

            IntVec3 mouth = InvoluteUtility.MouthCell(volume);
            if (!mouth.IsValid) return;

            InvoluteUtility.FlashAt(victim);

            Lord lord = victim.GetLord();

            victim.DeSpawnOrDeselect();
            GenSpawn.Spawn(victim, mouth, volume, Rot4.Random);
            victim.Notify_Teleported(false, true);

            if (lord != null) lord.Notify_PawnLost(victim, PawnLostCondition.ExitedMap);

            Messages.Message("AG_InvoluteSwallowed".Translate(caster.LabelShortCap, victim.LabelShortCap),
                victim, MessageTypeDefOf.NeutralEvent, false);
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

            if (target.Pawn == null)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_InvoluteNeedsPawn".Translate(),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (target.Pawn == caster)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_InvoluteNotSelf".Translate(),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            InvoluteGeneExtension ext = gene.Ext;
            float maxSize = ext != null ? ext.maxSwallowBodySize : 1.2f;
            if (target.Pawn.BodySize > maxSize)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_InvoluteTooBig".Translate(target.Pawn.LabelShortCap),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (gene.Inside)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_InvoluteAlreadyInside".Translate(caster.LabelShortCap),
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
