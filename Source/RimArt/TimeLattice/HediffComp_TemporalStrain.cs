using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_TemporalStrain : HediffCompProperties
    {
        /// <summary>Severity shed per day when not altering time. The whole point is that it outlives the fight.</summary>
        public float decayPerDay = 0.15f;

        /// <summary>At or above this severity the lattice starts doing damage that never heals.</summary>
        public float permanentInjuryThreshold = 0.85f;

        /// <summary>How often a check for permanent injury happens while critical.</summary>
        public int injuryCheckIntervalTicks = 300;

        /// <summary>Chance per check that an internal part takes a permanent scar.</summary>
        public float injuryChance = 0.35f;

        public FloatRange injurySeverity = new FloatRange(4f, 9f);

        /// <summary>Injury applied at critical strain. Set in XML so the def name is checkable.</summary>
        public HediffDef injuryDef;

        public HediffCompProperties_TemporalStrain()
        {
            compClass = typeof(HediffComp_TemporalStrain);
        }
    }

    /// <summary>
    /// The bill for accelerating. Strain is added by <see cref="HediffComp_TimeAlter"/> and shed
    /// slowly here, so it accumulates across a campaign rather than resetting between fights.
    /// At critical severity it starts leaving permanent scars - without that, a competent doctor
    /// would make the entire cost temporary and the gene would be free power on a cooldown.
    /// </summary>
    public class HediffComp_TemporalStrain : HediffComp
    {
        private int ticksToInjuryCheck;
        private int lastGameTickProcessed = -1;

        public HediffCompProperties_TemporalStrain Props => (HediffCompProperties_TemporalStrain)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead) return;

            // Once per game tick: an accelerated pawn runs this comp several times per tick,
            // which would otherwise scale decay and injury rolls by the multiplier.
            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            bool altering = pawn.health.hediffSet.hediffs
                .Any(h => h.TryGetComp<HediffComp_TimeAlter>() != null);

            if (!altering)
            {
                severityAdjustment -= Props.decayPerDay / 60000f;
            }

            if (parent.Severity < Props.permanentInjuryThreshold) return;

            ticksToInjuryCheck--;
            if (ticksToInjuryCheck > 0) return;
            ticksToInjuryCheck = Props.injuryCheckIntervalTicks;

            if (Rand.Value < Props.injuryChance) TryInflictPermanentInjury(pawn);
        }

        private void TryInflictPermanentInjury(Pawn pawn)
        {
            if (Props.injuryDef == null) return;

            List<BodyPartRecord> candidates = pawn.health.hediffSet
                .GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Inside)
                .Where(p => p.def.canScarify && !p.def.conceptual)
                .ToList();

            if (candidates.Count == 0) return;

            BodyPartRecord part = candidates.RandomElement();
            Hediff_Injury injury = HediffMaker.MakeHediff(Props.injuryDef, pawn, part) as Hediff_Injury;
            if (injury == null) return;

            injury.Severity = Props.injurySeverity.RandomInRange;

            HediffComp_GetsPermanent permanent = injury.TryGetComp<HediffComp_GetsPermanent>();
            if (permanent != null) permanent.IsPermanent = true;

            pawn.health.AddHediff(injury, part);

            Messages.Message(
                "AG_StrainInjury".Translate(pawn.LabelShort, part.Label),
                pawn, MessageTypeDefOf.NegativeHealthEvent, false);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksToInjuryCheck, "ticksToInjuryCheck", 0);
        }
    }
}
