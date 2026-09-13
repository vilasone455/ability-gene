using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityThrowKunai : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityThrowKunai()
        {
            compClass = typeof(CompAbilityEffect_ThrowKunai);
        }
    }

    /// <summary>
    /// Throws one kunai from the worn belt.
    ///
    /// The hit roll is vanilla Verb_LaunchProjectile.TryCastShot's, done here because an ability
    /// verb does not launch anything: <see cref="ShotReport"/> built from the ability's own verb,
    /// so the wearer's Shooting accuracy, distance, weather, smoke, target size and cover all
    /// count, and the accuracy numbers come from the ability's verbProperties. A wild miss flies
    /// to a scattered cell, a cover miss flies into the cover, and a hit flies at the target.
    ///
    /// The roll happens at cast. The launch waits for the kunai throw clip's release tick (18) in
    /// <see cref="MapComponent_Throws"/>, or happens at once without Melee Animation.
    /// </summary>
    public class CompAbilityEffect_ThrowKunai : CompAbilityEffect
    {
        private CompApparelReloadable Belt => KunaiBelt.WornBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        /// <summary>Why no kunai can be thrown right now, or null.</summary>
        private string Unavailable()
        {
            Pawn pawn = parent.pawn;
            CompApparelReloadable belt = Belt;
            if (belt == null) return "Requires a kunai belt.";
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return pawn.LabelShortCap + " cannot manipulate.";
            if (belt.RemainingCharges <= 0) return "No kunai left. Reload the belt with kunai.";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (!target.IsValid || parent.pawn.Map == null) return null;
            ShotReport report = ShotReport.HitReportFor(parent.pawn, parent.verb, target);
            return "Hit chance: " + report.TotalEstimatedHitChance.ToStringPercent("F0");
        }

        public override string ExtraTooltipPart()
        {
            CompApparelReloadable belt = Belt;
            return belt == null ? null : "Kunai: " + belt.LabelRemaining;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            CompApparelReloadable belt = Belt;
            if (caster?.Map == null || belt == null || belt.RemainingCharges <= 0 || !target.IsValid) return;

            belt.UsedOnce();

            LocalTargetInfo flyTo = Aim(caster, target, out ProjectileHitFlags flags);
            MapComponent_Throws.Begin(caster, flyTo, KunaiDefOf.AG_KunaiProjectile, KunaiDefaults.HandTexture,
                                      target, flags, ThrowAnimation.Kunai);
        }

        /// <summary>
        /// Where the kunai goes and what it can hit. Same order and flags as
        /// Verb_LaunchProjectile.TryCastShot: wild miss, then cover, then hit. The forced-miss
        /// step is left out because the projectile is not explosive.
        /// </summary>
        private LocalTargetInfo Aim(Pawn caster, LocalTargetInfo target, out ProjectileHitFlags flags)
        {
            Verb verb = parent.verb;
            ShotReport report = ShotReport.HitReportFor(caster, verb, target);

            if (!Rand.Chance(report.AimOnTargetChance_IgnoringPosture))
            {
                if (!verb.TryFindShootLineFromTo(caster.Position, target, out ShootLine line))
                    line = new ShootLine(caster.Position, target.Cell);
                line.ChangeDestToMissWild(report.AimOnTargetChance_StandardTarget, false, caster.Map);

                flags = ProjectileHitFlags.NonTargetWorld;
                if (Rand.Chance(0.5f)) flags |= ProjectileHitFlags.NonTargetPawns;
                return line.Dest;
            }

            Thing cover = report.GetRandomCoverToMissInto();
            if (cover != null && target.Thing != null && target.Thing.def.CanBenefitFromCover
                && !Rand.Chance(report.PassCoverChance))
            {
                flags = ProjectileHitFlags.NonTargetWorld | ProjectileHitFlags.NonTargetPawns;
                return cover;
            }

            flags = ProjectileHitFlags.IntendedTarget;
            return target;
        }
    }
}
