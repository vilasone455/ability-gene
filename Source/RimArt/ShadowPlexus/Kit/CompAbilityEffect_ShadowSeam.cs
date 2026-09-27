using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_ShadowSeam : CompProperties_EffectWithDest
    {
        /// <summary>Full reach, in cells; times the light at the origin. Both picks must be within it.</summary>
        public float reach = 15.9f;
        /// <summary>How long the two stay sewn.</summary>
        public float holdSeconds = 20f;
        /// <summary>How far apart the two can get.</summary>
        public float maxApart = 4f;

        public CompProperties_ShadowSeam()
        {
            compClass = typeof(CompAbilityEffect_ShadowSeam);
            // Two picks, the way the Skip psycast picks a thing and then a place; range 0 keeps the
            // base from drawing its own ring round the first pick.
            destination = AbilityEffectDestination.Selected;
            range = 0f;
        }
    }

    /// <summary>
    /// Shadow seam. Two picks, each a pawn or a loose item within reach standing in light; for the
    /// hold time they cannot get more than maxApart cells apart: whoever moves drags the other, and
    /// when both pull the larger body wins (<see cref="MapComponent_ShadowPlexus"/>). The seam runs
    /// between the two, not to the caster, and breaks like every shadow line.
    ///
    /// CanApplyOn is called twice, the first time with no destination (the first click), the way
    /// <see cref="CompAbilityEffect_DoubleClap"/> explains. The second pick cannot show a refusal, so
    /// the mouse text says why a thing is refused.
    /// </summary>
    public class CompAbilityEffect_ShadowSeam : CompAbilityEffect_WithDest
    {
        public new CompProperties_ShadowSeam Props => (CompProperties_ShadowSeam)props;

        public override TargetingParameters targetParams => new TargetingParameters
        {
            canTargetPawns = true, canTargetAnimals = true, canTargetMechs = true, canTargetItems = true,
            canTargetSelf = false, canTargetBuildings = false, canTargetLocations = false,
        };

        /// <summary>Why a thing cannot be one end of the seam, or null.</summary>
        private string EndRefusal(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            Thing thing = target.Thing;
            if (thing == null || !thing.Spawned || thing.Map != caster.Map) return "AG_ShadowSeamNeedsThing".Translate();
            if (thing is Pawn pawn)
            {
                if (pawn.Dead) return "AG_ShadowSeamNeedsThing".Translate();
                if (pawn == caster) return "AG_ShadowNotSelf".Translate();
            }
            else if (thing.def.category != ThingCategory.Item) return "AG_ShadowSeamNeedsThing".Translate();
            if (ShadowLight.Dark(caster.Map, thing.Position)) return "AG_ShadowTargetDark".Translate();
            return ShadowPlexusCast.Unavailable(caster) ?? ShadowPlexusCast.OutOfReach(caster, thing.Position, Props.reach)
                ?? ShadowPlexusCast.LineBlocked(caster, thing.Position, thing);
        }

        private string PairRefusal(LocalTargetInfo target, LocalTargetInfo dest)
        {
            string first = EndRefusal(target);
            if (first != null) return first;
            if (!dest.IsValid) return null;
            if (dest.Thing == target.Thing) return "AG_ShadowSeamSameThing".Translate();
            string second = EndRefusal(dest);
            if (second != null) return second;
            LineBreak broke = ShadowLines.Check(parent.pawn.Map, target.Cell, dest.Cell, target.Thing, dest.Thing, light: true, smoke: true, out _, out Pawn by);
            return broke == LineBreak.None ? null : "AG_ShadowLineBlocked".Translate(ShadowLines.Word(broke, by)).ToString();
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
            !ShadowPlexusCast.Refuse(EndRefusal(target), throwMessages, target.Thing) && base.Valid(target, throwMessages);

        public override bool CanHitTarget(LocalTargetInfo target) => selectedTarget.IsValid && PairRefusal(selectedTarget, target) == null;

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => PairRefusal(target, dest) == null && base.CanApplyOn(target, dest);

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target) =>
            selectedTarget.IsValid ? PairRefusal(selectedTarget, target) : EndRefusal(target);

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
            if (caster?.Map == null || PairRefusal(target, dest) != null || !dest.IsValid) return;
            MapComponent_ShadowPlexus.Of(caster.Map)?.LandSeam(caster, target.Thing, dest.Thing, parent.def.verbProperties.warmupTime, Props);
        }
    }
}
