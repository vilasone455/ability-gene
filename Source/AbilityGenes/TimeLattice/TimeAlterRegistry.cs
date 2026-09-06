using System.Collections.Generic;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Tracks which pawns are currently running at a non-normal rate.
    ///
    /// Entries are self-healing rather than added and removed by hooks: the hediff comp
    /// reports itself on every tick, and <see cref="MapComponent_TimeAlter"/> prunes anything
    /// that has stopped reporting. That survives save/load and hediff removal without relying
    /// on a removal callback firing.
    ///
    /// As with the stasis field, the counts exist so the Thing.DoTick prefix costs one static
    /// read when nobody is altering time, which is almost always.
    /// </summary>
    public static class TimeAlterRegistry
    {
        private static readonly List<HediffComp_TimeAlter> active = new List<HediffComp_TimeAlter>();
        private static readonly HashSet<Pawn> stagnated = new HashSet<Pawn>();

        public static int ActiveCount => active.Count;
        public static int StagnatedCount => stagnated.Count;
        public static List<HediffComp_TimeAlter> Active => active;

        public static void Report(HediffComp_TimeAlter comp)
        {
            if (!active.Contains(comp)) active.Add(comp);

            Pawn pawn = comp.Pawn;
            if (pawn == null) return;

            if (comp.IsStagnating) stagnated.Add(pawn);
            else stagnated.Remove(pawn);
        }

        public static void Drop(HediffComp_TimeAlter comp)
        {
            active.Remove(comp);
            if (comp.Pawn != null) stagnated.Remove(comp.Pawn);
        }

        /// <summary>Hot path, only reached when at least one pawn is stagnating.</summary>
        public static bool IsStagnated(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            return pawn != null && stagnated.Contains(pawn);
        }
    }
}
