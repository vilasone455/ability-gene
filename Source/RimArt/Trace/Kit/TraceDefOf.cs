using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class TraceDefOf
    {
        /// <summary>Trace On: a copy of a studied blade in the hand.</summary>
        public static AbilityDef AG_Trace_On;

        /// <summary>Reinforcement: faster and harder melee for a while (<see cref="AG_TraceReinforced"/>).</summary>
        public static AbilityDef AG_Trace_Reinforcement;

        public static HediffDef AG_TraceReinforced;

        /// <summary>The cast job of Trace On and Reinforcement (<see cref="JobDriver_CastTrace"/>).</summary>
        public static JobDef AG_CastTrace;

        static TraceDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(TraceDefOf));
        }
    }
}
