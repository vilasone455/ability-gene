using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_MetabolicOverdrive : HediffCompProperties
    {
        public int intervalTicks = 60;
        public float nutritionPerInterval = 0.05f;
        public float healPerInterval = 1.5f;

        public HediffCompProperties_MetabolicOverdrive()
        {
            compClass = typeof(HediffComp_MetabolicOverdrive);
        }
    }

    /// <summary>
    /// Converts food into closed wounds, tick by tick. It ends itself the moment the pawn
    /// runs out of nutrition or out of injuries, so the real cost is measured in meals —
    /// heal a lot now and go hungry, or let the wounds close on their own.
    /// </summary>
    public class HediffComp_MetabolicOverdrive : HediffComp
    {
        private int ticksToNext;

        public HediffCompProperties_MetabolicOverdrive Props => (HediffCompProperties_MetabolicOverdrive)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned) return;

            ticksToNext -= delta;
            if (ticksToNext > 0) return;
            ticksToNext = Props.intervalTicks;

            Need_Food food = pawn.needs?.food;
            if (food == null || food.CurLevel < Props.nutritionPerInterval)
            {
                Stop(pawn, "AG_OverdriveOutOfFood".Translate(pawn.LabelShort));
                return;
            }

            Hediff_Injury injury = WorstInjury(pawn);
            if (injury == null)
            {
                Stop(pawn, "AG_OverdriveNothingToHeal".Translate(pawn.LabelShort));
                return;
            }

            food.CurLevel -= Props.nutritionPerInterval;
            injury.Heal(Props.healPerInterval);
        }

        private void Stop(Pawn pawn, string message)
        {
            Messages.Message(message, pawn, MessageTypeDefOf.NeutralEvent, false);
            pawn.health.RemoveHediff(parent);
        }

        /// <summary>Bleeding wounds first, then whatever is most severe.</summary>
        private static Hediff_Injury WorstInjury(Pawn pawn)
        {
            List<Hediff> all = pawn.health.hediffSet.hediffs;
            Hediff_Injury best = null;
            float bestScore = 0f;
            for (int i = 0; i < all.Count; i++)
            {
                Hediff_Injury injury = all[i] as Hediff_Injury;
                if (injury == null || injury.IsPermanent() || injury.Severity <= 0f) continue;

                float score = injury.Severity + (injury.Bleeding ? 1000f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = injury;
                }
            }
            return best;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksToNext, "ticksToNext", 0);
        }
    }
}
