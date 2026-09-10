using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityMurder : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityMurder()
        {
            compClass = typeof(CompAbilityEffect_Murder);
        }
    }

    /// <summary>
    /// The cast spends one shared charge and hands the carrier to Core's flyer. Deliberate
    /// flight has no blood cost or landing debuff, so it can be used to engage or escape.
    /// </summary>
    public class CompAbilityEffect_Murder : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn carrier = parent.pawn;
            if (carrier == null || carrier.Map == null) return;

            Gene_Dispersal gene = DispersalRegistry.CarrierFor(carrier);
            if (gene == null || !gene.HasCharge) return;

            Map map = carrier.Map;
            IntVec3 cell = target.Cell;
            if (!JumpUtility.ValidJumpTarget(carrier, map, cell)) return;

            // Everything that reads the carrier's place on the map has to happen here, before
            // MakeFlyer: that call despawns the pawn into the flyer's container, after which
            // they have no draw position and are no longer selected.
            Vector3 origin = carrier.DrawPos;
            bool wasSelected = Find.Selector.IsSelected(carrier);

            DispersalFX.Depart(origin, map);

            PawnFlyer flyer = PawnFlyer.MakeFlyer(DispersalDefOf.AG_DispersalFlock, carrier, cell,
                null, null, false, null, parent, target);
            if (flyer == null) return;

            gene.Spend();

            GenSpawn.Spawn(flyer, cell, map);

            // Core's own jump does this and it is not cosmetic: the pawn was despawned a moment
            // ago and lost the player's selection with it, so without this the colonist they
            // were commanding vanishes for the whole flight and has to be found again wherever
            // the birds put them down.
            if (wasSelected) Find.Selector.Select(carrier, false, false);
        }

        /// <summary>
        /// Greys the gizmo out when the plexus is empty, rather than letting the player open a
        /// targeter, pick a cell and only then be told no. It is also the only place the two
        /// halves of the gene are visibly one thing: the button goes dark because something
        /// shot at this colonist a minute ago.
        /// </summary>
        public override bool GizmoDisabled(out string reason)
        {
            Pawn carrier = parent.pawn;
            Gene_Dispersal gene = carrier != null ? DispersalRegistry.CarrierFor(carrier) : null;

            if (gene != null && !gene.HasCharge)
            {
                reason = "AG_DispersalEmptyShort".Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn carrier = parent.pawn;
            if (carrier == null || carrier.Map == null) return false;

            Gene_Dispersal gene = DispersalRegistry.CarrierFor(carrier);
            if (gene == null) return false;

            if (!gene.HasCharge)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_DispersalEmpty".Translate(carrier.LabelShortCap),
                        carrier, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            // The same test Core's longjump lands against: the flock will cross anything, but
            // it has to have somewhere to put a person down at the far end.
            if (!JumpUtility.ValidJumpTarget(carrier, carrier.Map, target.Cell))
            {
                if (throwMessages)
                {
                    Messages.Message("AG_DispersalNoLanding".Translate(),
                        carrier, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
