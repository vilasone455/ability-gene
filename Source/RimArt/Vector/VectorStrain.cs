using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The cost of a manipulation, and the collapse at the top of it.
    ///
    /// Strain is the only real limit on the ability: the cooldown is five seconds and exists to
    /// stop double-clicks, not to ration anything. Everything that matters about how often a
    /// carrier can turn a field of rounds around is in the severity on this hediff and the rate
    /// it sheds.
    /// </summary>
    public static class VectorStrain
    {
        /// <summary>Strain the pawn is carrying now, 0 to 1.</summary>
        public static float Current(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return 0f;

            Hediff strain = pawn.health.hediffSet.GetFirstHediffOfDef(VectorDefOf.AG_VectorStrain);
            return strain == null ? 0f : strain.Severity;
        }

        /// <summary>What the carrier would be at after paying this. Clamped, so a huge edit reads honestly.</summary>
        public static float Projected(Pawn pawn, float cost)
        {
            return Mathf.Clamp01(Current(pawn) + cost);
        }

        public static bool WouldCollapse(Pawn pawn, float cost)
        {
            return Projected(pawn, cost) >= VectorEditDefaults.StrainCollapseThreshold
                   && Current(pawn) < VectorEditDefaults.StrainCollapseThreshold;
        }

        /// <summary>
        /// Adds the cost and, if that crosses into the overload stage, drops the carrier.
        ///
        /// The collapse is not a timer. The overload stage's consciousness penalty is what puts
        /// them down, and the strain shedding back out of that stage is what stands them up
        /// again - so the length of the collapse is decided by the same decay rate as everything
        /// else, and there is no second duration to keep in step with anything.
        /// </summary>
        public static void Add(Pawn pawn, float cost)
        {
            if (pawn == null || pawn.health == null || cost <= 0f) return;

            bool collapses = WouldCollapse(pawn, cost);

            Hediff strain = pawn.health.hediffSet.GetFirstHediffOfDef(VectorDefOf.AG_VectorStrain);
            if (strain == null)
            {
                strain = pawn.health.AddHediff(VectorDefOf.AG_VectorStrain);
                if (strain == null) return;
                strain.Severity = 0f;
            }

            strain.Severity = Mathf.Clamp01(strain.Severity + cost);

            if (!collapses) return;

            if (pawn.stances != null && pawn.stances.stunner != null)
            {
                pawn.stances.stunner.StunFor(VectorEditDefaults.OverloadStunTicks, pawn, false, true);
            }

            Messages.Message("AG_VectorOverload".Translate(pawn.LabelShort),
                pawn, MessageTypeDefOf.NegativeHealthEvent, false);
        }
    }
}
