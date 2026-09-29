using System.Collections.Generic;
using System.Linq;
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
            if (!ShinraCastAnimation.Clip.Present)
            { reason = "Shinra Tensei's hand animation requires Melee Animation."; return true; }
            if (!ShinraCastAnimation.Clip.CanAnimate(parent.pawn))
            { reason = "The caster must be a standing humanlike pawn, outside another animation."; return true; }
            if (parent.pawn.Map.GetComponent<MapComponent_ShinraCasts>().Running(parent.pawn))
            { reason = "Shinra Tensei is still playing."; return true; }
            if (PainKit.ChibakuLock(parent.pawn) is string held)
            { reason = held; return true; }
            return base.GizmoDisabled(out reason);
        }

        /// <summary>Anything that casts the ability itself (the button does not) gets the quick version.</summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;
            GameComponent_Shinra.Instance.Start(pawn, true);
        }
    }
}
