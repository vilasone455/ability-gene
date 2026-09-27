using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Who has a Susanoo standing right now. Same self-healing shape as <see cref="ArrearsRegistry"/>:
    /// the hediff comp reports itself when added and every tick interval, and drops itself on removal,
    /// so the damage prefix costs one integer read while nobody has one up.
    /// </summary>
    public static class SusanooRegistry
    {
        private static readonly List<HediffComp_Susanoo> holders = new List<HediffComp_Susanoo>();

        public static int Count => holders.Count;

        public static void Report(HediffComp_Susanoo comp)
        {
            if (comp != null && !holders.Contains(comp)) holders.Add(comp);
        }

        public static void Drop(HediffComp_Susanoo comp)
        {
            holders.Remove(comp);
        }

        /// <summary>The Susanoo this pawn is standing in, or null.</summary>
        public static HediffComp_Susanoo HolderFor(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null) return null;
            for (int i = holders.Count - 1; i >= 0; i--)
            {
                HediffComp_Susanoo comp = holders[i];
                if (comp.Pawn == null || comp.Pawn.Dead || comp.parent == null
                    || comp.Pawn.health?.hediffSet?.hediffs.Contains(comp.parent) != true)
                {
                    holders.RemoveAt(i);
                    continue;
                }
                if (comp.Pawn == pawn) return comp;
            }
            return null;
        }
    }
}
