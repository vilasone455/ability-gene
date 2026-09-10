using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityCarrion : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityCarrion() { compClass = typeof(CompAbilityEffect_Carrion); }
    }

    public class CompAbilityEffect_Carrion : CompAbilityEffect
    {
        public override bool GizmoDisabled(out string reason)
        {
            Gene_Dispersal gene = parent.pawn.genes?.GetFirstGeneOfType<Gene_Dispersal>();
            if (gene == null || !gene.Active || !gene.HasCharge)
            {
                reason = "AG_DispersalEmptyShort".Translate();
                return true;
            }
            if (parent.pawn.Map?.GetComponent<MapComponent_Carrion>()?.Running(parent.pawn) == true)
            {
                reason = "AG_CarrionBusy".Translate();
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = parent.pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead) return false;
            string reason;
            if (GizmoDisabled(out reason)) return Reject(reason, throwMessages);
            Corpse corpse = target.Thing as Corpse;
            if (!CarrionRun.ValidCorpse(corpse, pawn.Map)
                || corpse.Position.Fogged(pawn.Map)
                || (corpse.Position - pawn.Position).LengthHorizontalSquared > 100)
                return Reject("AG_CarrionInvalid".Translate(), throwMessages);
            if (pawn.Map.GetComponent<MapComponent_Carrion>().Claimed(corpse))
                return Reject("AG_CarrionClaimed".Translate(), throwMessages);
            return base.Valid(target, throwMessages);
        }

        private bool Reject(string reason, bool show)
        {
            if (show) Messages.Message(reason, parent.pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!Valid(target)) return;
            Pawn pawn = parent.pawn;
            if (!pawn.Map.GetComponent<MapComponent_Carrion>().Begin(pawn, (Corpse)target.Thing)) return;
            base.Apply(target, dest);
        }
    }
}
