using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_ShadowGrasp : CompProperties_EffectWithDest
    {
        /// <summary>Full reach, in cells; times the light at the origin. The thing and the cell must both be within it.</summary>
        public float reach = 24.9f;
        /// <summary>Cells per second an item slides.</summary>
        public float itemSpeed = 12f;
        /// <summary>Cells per second a downed body is dragged.</summary>
        public float bodySpeed = 6f;

        public CompProperties_ShadowGrasp()
        {
            compClass = typeof(CompAbilityEffect_ShadowGrasp);
            destination = AbilityEffectDestination.Selected;
            range = 0f;
        }
    }

    /// <summary>
    /// Shadow grasp. First pick a loose item, a weapon on the ground, a corpse or a downed pawn;
    /// then a cell. A hand of shadow slides it there over the ground, cell by cell, along a straight
    /// path (<see cref="MapComponent_ShadowPlexus"/>): a pawn on the path stops it in that pawn's
    /// cell, a wall or another item stops it before. Thrown grenades are projectiles in flight, not
    /// items on the ground, so they cannot be picked.
    /// </summary>
    public class CompAbilityEffect_ShadowGrasp : CompAbilityEffect_WithDest
    {
        public new CompProperties_ShadowGrasp Props => (CompProperties_ShadowGrasp)props;

        /// <summary>The second pick: a cell.</summary>
        public override TargetingParameters targetParams => new TargetingParameters
        {
            canTargetLocations = true, canTargetPawns = false, canTargetItems = false, canTargetBuildings = false,
        };

        private string ThingRefusal(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            Thing thing = target.Thing;
            if (thing == null || !thing.Spawned || thing.Map != caster.Map) return "AG_ShadowGraspNeedsThing".Translate();
            if (thing is Pawn pawn)
            {
                if (pawn == caster) return "AG_ShadowNotSelf".Translate();
                if (!pawn.Downed && !pawn.Dead) return "AG_ShadowGraspNeedsDowned".Translate();
            }
            else if (thing.def.category != ThingCategory.Item) return "AG_ShadowGraspNeedsThing".Translate();
            return ShadowPlexusCast.Unavailable(caster) ?? ShadowPlexusCast.OutOfReach(caster, thing.Position, Props.reach)
                ?? ShadowPlexusCast.LineBlocked(caster, thing.Position, thing);
        }

        private string CellRefusal(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent.pawn;
            IntVec3 cell = dest.Cell;
            if (!cell.InBounds(caster.Map) || !cell.Walkable(caster.Map)) return "AG_ShadowDoubleNoFloor".Translate();
            if (cell == target.Cell) return "AG_ShadowGraspSameCell".Translate();
            return ShadowPlexusCast.OutOfReach(caster, cell, Props.reach);
        }

        private string PairRefusal(LocalTargetInfo target, LocalTargetInfo dest)
        {
            string first = ThingRefusal(target);
            if (first != null) return first;
            return dest.IsValid ? CellRefusal(target, dest) : null;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
            !ShadowPlexusCast.Refuse(ThingRefusal(target), throwMessages, target.Thing) && base.Valid(target, throwMessages);

        public override bool CanHitTarget(LocalTargetInfo target) => selectedTarget.IsValid && target.IsValid && PairRefusal(selectedTarget, target) == null;

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => PairRefusal(target, dest) == null && base.CanApplyOn(target, dest);

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target) =>
            selectedTarget.IsValid ? PairRefusal(selectedTarget, target) : ThingRefusal(target);

        public override bool GizmoDisabled(out string reason)
        {
            reason = ShadowPlexusCast.Unavailable(parent.pawn);
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override void OnGizmoUpdate()
        {
            base.OnGizmoUpdate();
            ShadowPlexusCast.DrawReach(parent.pawn, Props.reach);
        }

        public override string ExtraTooltipPart() => ShadowPlexusCast.ReachTip(parent.pawn, Props.reach);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !dest.IsValid || PairRefusal(target, dest) != null) return;
            MapComponent_ShadowPlexus.Of(caster.Map)?.LandGrasp(caster, target.Thing, dest.Cell, parent.def.verbProperties.warmupTime, Props);
        }
    }
}
