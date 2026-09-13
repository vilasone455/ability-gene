using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where one thrown handful lands. 3x3 around the landing cell: the centre and its four
    /// neighbours always, each corner with <see cref="MakibishiDefaults.CornerChance"/>.
    ///
    /// The "+" is what makes it block: the middle row and the middle column are always full, so a
    /// pawn crossing the patch in any straight line through a gap up to 3 wide enters at least one
    /// spiked cell.
    /// </summary>
    public static class MakibishiPatch
    {
        private static readonly IntVec3[] Plus =
        {
            IntVec3.Zero, IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West,
        };

        private static readonly IntVec3[] Corners =
        {
            IntVec3.NorthEast, IntVec3.SouthEast, IntVec3.SouthWest, IntVec3.NorthWest,
        };

        /// <summary>The cells a handful landing on <paramref name="center"/> covers, rolled once.</summary>
        public static List<IntVec3> RollCells(IntVec3 center, Map map)
        {
            var cells = new List<IntVec3>(9);
            foreach (IntVec3 offset in Plus)
                if (CanSpike(center, center + offset, map)) cells.Add(center + offset);
            foreach (IntVec3 offset in Corners)
                if (Rand.Chance(MakibishiDefaults.CornerChance) && CanSpike(center, center + offset, map))
                    cells.Add(center + offset);
            return cells;
        }

        /// <summary>
        /// A cell can hold spikes when a pawn could walk on it, it is not water, and it can be seen
        /// from the landing cell. The last rule keeps spikes off the far side of a wall corner.
        /// Doors count as walkable, so a door cell gets spikes and keeps its door.
        /// </summary>
        public static bool CanSpike(IntVec3 center, IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || !cell.WalkableByAny(map)) return false;
            if (map.terrainGrid.TerrainAt(cell)?.IsWater == true) return false;
            return cell == center || GenSight.LineOfSight(center, cell, map, true);
        }

        /// <summary>Spikes the patch around <paramref name="center"/>. Returns how many cells were spiked.</summary>
        public static int Scatter(IntVec3 center, Map map, Pawn thrower)
        {
            if (map == null || !center.InBounds(map)) return 0;

            List<IntVec3> cells = RollCells(center, map);
            for (int i = 0; i < cells.Count; i++)
            {
                Makibishi existing = map.thingGrid.ThingAt<Makibishi>(cells[i]);
                if (existing != null)
                {
                    existing.Arm(thrower);
                    continue;
                }

                var spikes = (Makibishi)ThingMaker.MakeThing(MakibishiDefOf.AG_MakibishiSpikes);
                spikes.Arm(thrower);
                // Wipes nothing: not an edifice, fillPercent 0, no blockPlants, so doors, items,
                // filth and plants all stay (GenSpawn.SpawningWipes).
                GenSpawn.Spawn(spikes, cells[i], map);
            }
            return cells.Count;
        }
    }
}
