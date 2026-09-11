using UnityEngine;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_VectorStrain : HediffCompProperties
    {
        /// <summary>
        /// Severity shed per day. The whole limit on the ability is here: the cooldown is five
        /// seconds, so how often a carrier can turn a field around is decided by how fast this
        /// bleeds off and nothing else.
        /// </summary>
        public float decayPerDay = 1f;

        public HediffCompProperties_VectorStrain()
        {
            compClass = typeof(HediffComp_VectorStrain);
        }
    }

    /// <summary>
    /// Sheds brain strain. Added to only by <see cref="VectorStrain.Add"/>, at the moment a
    /// manipulation is applied.
    ///
    /// Decay is counted once per game tick rather than once per comp tick, for the same reason
    /// <see cref="HediffComp_TemporalStrain"/> does it: a pawn carrying a neural accelerator
    /// runs its own hediff comps several times inside one game tick, and shedding at that
    /// multiplier would make acceleration a way of paying less for this.
    /// </summary>
    public class HediffComp_VectorStrain : HediffComp
    {
        private int lastGameTickProcessed = -1;

        public HediffCompProperties_VectorStrain Props => (HediffCompProperties_VectorStrain)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead) return;

            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            severityAdjustment -= Props.decayPerDay / 60000f * Mathf.Max(1, delta);
        }
    }
}
