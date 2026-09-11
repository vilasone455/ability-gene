using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Who is currently running faster than the world, which the engine expresses as the world
    /// running slower than them.
    ///
    /// The count in front exists because <see cref="Verse.TickManager.TickRateMultiplier"/> is
    /// read several times every frame, and from inside the tick loop's own condition. Nobody
    /// surging must cost one static integer read, which is the same rule every other registry
    /// in this mod follows.
    ///
    /// Entries are added and removed outright rather than aged out. A surge is held open by a
    /// hediff comp that reports itself while it ticks and drops itself when the hediff goes, and
    /// a paused game - which is exactly what the editor does - stops those ticks without the
    /// surge having ended. Anything keyed on a tick count would quietly expire mid-edit.
    /// </summary>
    public static class VectorSurgeRegistry
    {
        private static readonly List<HediffComp_VectorSurge> holders =
            new List<HediffComp_VectorSurge>();

        public static int HolderCount => holders.Count;

        public static void Report(HediffComp_VectorSurge comp)
        {
            if (comp == null || holders.Contains(comp)) return;
            holders.Add(comp);
        }

        public static void Drop(HediffComp_VectorSurge comp)
        {
            holders.Remove(comp);
        }

        public static void Clear()
        {
            holders.Clear();
        }

        /// <summary>
        /// What the world's tick rate should be, or 1 for no change.
        ///
        /// The holder list is swept here rather than on a timer because this is the one place
        /// that has to be right: a stale holder would slow the entire game for the rest of the
        /// session, and there is no second chance to notice. The list is at most a handful of
        /// entries, so the sweep costs nothing worth measuring.
        /// </summary>
        public static float Rate
        {
            get
            {
                for (int i = holders.Count - 1; i >= 0; i--)
                {
                    HediffComp_VectorSurge holder = holders[i];
                    Pawn pawn = holder?.Pawn;
                    if (pawn == null || pawn.Dead || pawn.health == null
                        || !pawn.health.hediffSet.hediffs.Contains(holder.parent))
                    {
                        holders.RemoveAt(i);
                    }
                }

                return holders.Count > 0 ? VectorEditDefaults.SurgeTickRate : 1f;
            }
        }
    }
}
