using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityShinraTensei : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityShinraTensei() { compClass = typeof(CompAbilityEffect_ShinraTensei); }
    }

    public class CompAbilityEffect_ShinraTensei : CompAbilityEffect
    {
        public override bool GizmoDisabled(out string reason)
        {
            if (!ShinraCastAnimation.Present)
            { reason = "Shinra Tensei's hand animation requires Melee Animation."; return true; }
            if (!ShinraCastAnimation.CanAnimate(parent.pawn))
            { reason = "The caster must be a standing humanlike pawn, outside another animation."; return true; }
            if (parent.pawn.Map.GetComponent<MapComponent_ShinraCasts>().Running(parent.pawn))
            { reason = "Shinra Tensei is still playing."; return true; }
            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;
            MapComponent_ShinraCasts component = pawn.Map.GetComponent<MapComponent_ShinraCasts>();
            if (component.Running(pawn)) return;
            if (ShinraCastAnimation.TryStart(pawn, out ShinraCastAnimation.Handle animation))
                component.Begin(pawn, animation);
        }
    }
}
