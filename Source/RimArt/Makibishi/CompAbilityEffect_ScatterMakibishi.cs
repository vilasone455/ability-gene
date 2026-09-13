using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityScatterMakibishi : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityScatterMakibishi()
        {
            compClass = typeof(CompAbilityEffect_ScatterMakibishi);
        }
    }

    /// <summary>
    /// Throws one handful from the worn pouch at a cell. The handful lands exactly on the aimed cell
    /// (no hit roll: it is aimed at the ground) and <see cref="Projectile_Makibishi"/> spikes the
    /// patch there. The charge is spent when the throw starts, like the kunai belt.
    /// </summary>
    public class CompAbilityEffect_ScatterMakibishi : CompAbilityEffect
    {
        private CompApparelReloadable Pouch => MakibishiPouch.WornBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        private string Unavailable()
        {
            Pawn pawn = parent.pawn;
            CompApparelReloadable pouch = Pouch;
            if (pouch == null) return "Requires a makibishi pouch.";
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return pawn.LabelShortCap + " cannot manipulate.";
            if (pouch.RemainingCharges <= 0) return "No makibishi left. Reload the pouch with makibishi.";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override string ExtraTooltipPart()
        {
            CompApparelReloadable pouch = Pouch;
            return pouch == null ? null : "Makibishi: " + pouch.LabelRemaining;
        }

        /// <summary>The "+" that is always spiked, and the four corners that might be.</summary>
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Map map = parent.pawn.Map;
            if (!target.IsValid || map == null) return;

            IntVec3 center = target.Cell;
            var sure = new List<IntVec3>();
            var maybe = new List<IntVec3>();
            foreach (IntVec3 cell in CellRect.CenteredOn(center, 1))
            {
                if (!MakibishiPatch.CanSpike(center, cell, map)) continue;
                bool corner = cell.x != center.x && cell.z != center.z;
                (corner ? maybe : sure).Add(cell);
            }
            GenDraw.DrawFieldEdges(sure, Color.white);
            GenDraw.DrawFieldEdges(maybe, new Color(1f, 1f, 1f, 0.35f));
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            CompApparelReloadable pouch = Pouch;
            if (caster?.Map == null || pouch == null || pouch.RemainingCharges <= 0 || !target.IsValid) return;

            pouch.UsedOnce();
            MapComponent_Throws.Begin(caster, new LocalTargetInfo(target.Cell), MakibishiDefOf.AG_MakibishiProjectile,
                                      MakibishiDefaults.HandTexture, LocalTargetInfo.Invalid,
                                      ProjectileHitFlags.IntendedTarget, ThrowAnimation.Scatter);
        }
    }
}
