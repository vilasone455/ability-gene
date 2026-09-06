using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    public class CompProperties_AbilityTimeBubble : CompProperties_AbilityEffect
    {
        /// <summary>Sphere radius in cells, measured from the caster.</summary>
        public float radius = 6.9f;

        /// <summary>How long time stays stopped, in ticks. 60 ticks = 1 second.</summary>
        public int durationTicks = 900;

        /// <summary>
        /// If true, nothing inside can be damaged while frozen. Set false to make the
        /// bubble a free-hit window instead of a stall — it is a large balance change.
        /// </summary>
        public bool frozenAreInvulnerable = true;

        /// <summary>
        /// Dome tint. Vanilla force fields are grey; keeping this off-white and blue is
        /// what stops a stasis field being mistaken for a bullet shield at a glance.
        /// </summary>
        public Color domeColor = TimeBubbleDefaults.DomeColor;

        public CompProperties_AbilityTimeBubble()
        {
            compClass = typeof(CompAbilityEffect_TimeBubble);
        }
    }

    public class CompAbilityEffect_TimeBubble : CompAbilityEffect
    {
        public new CompProperties_AbilityTimeBubble Props => (CompProperties_AbilityTimeBubble)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return;

            MapComponent_TimeBubbles comp = map.GetComponent<MapComponent_TimeBubbles>();
            if (comp == null) return;

            comp.AddBubble(caster.Position, Props.radius, Props.durationTicks,
                Props.frozenAreInvulnerable, Props.domeColor);

            Messages.Message(
                "AG_TimeBubbleFormed".Translate(caster.LabelShort),
                new TargetInfo(caster.Position, map),
                MessageTypeDefOf.NeutralEvent, false);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(parent.pawn.Position, Props.radius);
        }

        public override string ExtraTooltipPart()
        {
            return "AG_TimeBubbleTooltip".Translate(
                Props.radius.ToString("F1"),
                (Props.durationTicks / 60f).ToString("F0"));
        }
    }
}
