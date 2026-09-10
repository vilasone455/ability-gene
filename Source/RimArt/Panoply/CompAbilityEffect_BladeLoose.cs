using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityBladeLoose : CompProperties_AbilityEffect
    {
        /// <summary>How far from the aimed point a blade can be and still hear the order.</summary>
        public float radius = 12.9f;

        public CompProperties_AbilityBladeLoose()
        {
            compClass = typeof(CompAbilityEffect_BladeLoose);
        }
    }

    /// <summary>
    /// Everything standing near the point goes at it.
    ///
    /// The ability itself does almost nothing: it finds the blades, hands each one the target
    /// and a number of ticks to wait, and stops. Each blade then lifts, turns and launches on
    /// its own count - which is what makes this a volley rather than a single event, and means
    /// a blade destroyed or expiring mid-launch simply does not fire.
    ///
    /// Nothing here decides how much damage arrives. That is decided by where the rain fell,
    /// how many blades are still standing, and what is between each of them and the target -
    /// all of it resolved by the projectile, none of it by this mod.
    /// </summary>
    public class CompAbilityEffect_BladeLoose : CompAbilityEffect
    {
        public new CompProperties_AbilityBladeLoose Props => (CompProperties_AbilityBladeLoose)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return;

            List<PlantedBlade> blades = PanoplyUtility.BladesNear(caster, map, target.Cell, Props.radius);
            if (blades.Count == 0) return;

            int fired = 0;
            for (int i = 0; i < blades.Count; i++)
            {
                if (blades[i].Launching) continue;
                blades[i].OrderLaunch(target,
                    PanoplyDefaults.LooseWindupTicks + fired * PanoplyDefaults.LooseStaggerTicks);
                fired++;
            }

            Messages.Message("AG_PanoplyLoosed".Translate(caster.LabelShortCap, fired),
                caster, MessageTypeDefOf.NeutralEvent, false);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return false;

            if (PanoplyUtility.BladesNear(caster, map, target.Cell, Props.radius).Count == 0)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_PanoplyNoBladesNear".Translate(caster.LabelShortCap),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }
    }
}
