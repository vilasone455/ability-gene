using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Spawning and driving a car without any of the machinery that will eventually gate it.
    ///
    /// The point of these two actions is to test movement on its own. The rig, the ability, the
    /// operator lock and the leash all decide *who* may drive and *how far*; none of them change
    /// what driving is, so none of them need to exist before driving can be proven to work.
    /// </summary>
    public static class DebugActions_ToyCar
    {
        [RimArtDebug("Toy car", "spawn here")]
        private static void SpawnToyCar()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map) || cell.Impassable(map)) return;

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("AG_ToyCar");
            if (def == null)
            {
                Messages.Message("no ThingDef AG_ToyCar", MessageTypeDefOf.RejectInput, false);
                return;
            }

            GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
        }

        /// <summary>
        /// Drives whichever car is selected, or the nearest one if none is.
        ///
        /// Selection first, because testing two cars at once is the only way to find out whether
        /// they path independently.
        /// </summary>
        [RimArtDebug("Toy car", "drive to here")]
        private static void DriveToyCar()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map)) return;

            ToyCar car = Find.Selector.SelectedObjects.OfType<ToyCar>().FirstOrDefault()
                         ?? map.listerThings.AllThings.OfType<ToyCar>()
                                .OrderBy(c => c.Position.DistanceToSquared(cell)).FirstOrDefault();

            if (car == null)
            {
                Messages.Message("no toy car on this map", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!car.DriveTo(cell))
                Messages.Message("no route there", car, MessageTypeDefOf.RejectInput, false);
        }
    }
}
