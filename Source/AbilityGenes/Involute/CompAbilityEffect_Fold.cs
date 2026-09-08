using RimWorld;
using Verse;
using Verse.AI.Group;

namespace AbilityGenes
{
    public class CompProperties_AbilityFold : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityFold()
        {
            compClass = typeof(CompAbilityEffect_Fold);
        }
    }

    /// <summary>
    /// Step through your own hole.
    ///
    /// Going in leaves the hole standing where the carrier was: they went through it, so it is
    /// not on them any more. Coming back out is the same move in reverse and lands them exactly
    /// where they left, which is the pressure the ability never stops applying - whoever was
    /// standing around the aperture is still standing there.
    ///
    /// Nothing here moves what the carrier is holding. It does not have to: despawning a pawn
    /// takes their carryTracker with them, so a carrier can bring in exactly one downed
    /// colonist, or one item, and no more. That is the whole capacity rule and the engine
    /// already enforces it.
    /// </summary>
    public class CompAbilityEffect_Fold : CompAbilityEffect
    {
        public new CompProperties_AbilityFold Props => (CompProperties_AbilityFold)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null) return;

            if (gene.Inside) Leave(caster, gene);
            else Enter(caster, gene);
        }

        private void Enter(Pawn caster, Gene_Involute gene)
        {
            Map origin = caster.Map;
            if (origin == null) return;

            Map volume = gene.EnsureVolume();
            if (volume == null) return;

            IntVec3 mouth = InvoluteUtility.MouthCell(volume);
            if (!mouth.IsValid) return;

            IntVec3 left = caster.Position;

            // One aperture per carrier. Folding in from somewhere new moves it rather than
            // opening a second, so there is always exactly one way back and it is always the
            // last place they stepped through.
            Building_Aperture standing = gene.Aperture;
            if (standing != null && !standing.Destroyed && standing.Spawned)
            {
                InvoluteUtility.CloseFlash(standing.Position, standing.Map);
                standing.Destroy(DestroyMode.Vanish);
            }

            Building_Aperture aperture =
                (Building_Aperture)ThingMaker.MakeThing(InvoluteDefOf.AG_InvoluteAperture);
            aperture.SetOwner(caster);
            GenSpawn.Spawn(aperture, left, origin);
            gene.SetAperture(aperture);
            InvoluteUtility.OpenFlash(left, origin);

            Move(caster, mouth, volume);

            Messages.Message("AG_InvoluteFoldedIn".Translate(caster.LabelShortCap),
                new TargetInfo(left, origin), MessageTypeDefOf.NeutralEvent, false);
        }

        private void Leave(Pawn caster, Gene_Involute gene)
        {
            Building_Aperture aperture = gene.Aperture;
            if (aperture == null || aperture.Destroyed || !aperture.Spawned) return;

            Map origin = aperture.Map;
            IntVec3 cell = aperture.Position;

            // The aperture stays. Stepping back out is not the same as closing the door behind
            // you, and leaving it standing is what lets the carrier post and swallow into the
            // volume from outside it - the only state in which either of those is any use.
            //
            // It is not free. Everything fired into it still arrives inside, and what is inside
            // is now the carrier's stores, their posted bed, and anyone they put in there. An
            // open aperture is a permanent shooting lane into their own storeroom.
            Move(caster, cell, origin);

            Messages.Message("AG_InvoluteFoldedOut".Translate(caster.LabelShortCap),
                new TargetInfo(cell, origin), MessageTypeDefOf.NeutralEvent, false);
        }

        private void Move(Pawn pawn, IntVec3 cell, Map map)
        {
            Lord lord = pawn.GetLord();

            pawn.DeSpawnOrDeselect();
            GenSpawn.Spawn(pawn, cell, map, Rot4.Random);
            pawn.Notify_Teleported(false, true);

            if (lord != null) lord.Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null) return false;

            if (gene.Inside)
            {
                Building_Aperture aperture = gene.Aperture;
                if (aperture == null || aperture.Destroyed || !aperture.Spawned)
                {
                    if (throwMessages)
                    {
                        Messages.Message("AG_InvoluteNoWayBack".Translate(caster.LabelShortCap),
                            caster, MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
                return base.Valid(target, throwMessages);
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
