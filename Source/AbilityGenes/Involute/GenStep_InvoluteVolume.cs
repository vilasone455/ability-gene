using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Builds the inside of the volume.
    ///
    /// This is written rather than borrowed because the mod depends on no framework. Alpha
    /// Genes lays its pocket plane out with KCSG structure layouts, which come from Vanilla
    /// Expanded Framework; adding a framework dependency for set dressing is not a trade worth
    /// making, and scattering procedurally gives a different room every time instead of four
    /// fixed prefabs.
    ///
    /// Every def named here is Core or Biotech. Nothing in this file resolves to a texture the
    /// mod ships, and nothing resolves to a DLC the mod does not already require.
    /// </summary>
    public class GenStep_InvoluteVolume : GenStep
    {
        public TerrainDef floor;
        public TerrainDef patchTerrain;
        public TerrainDef poolTerrain;

        /// <summary>Share of cells that get the patch terrain, as loose blotches.</summary>
        public float patchChance = 0.18f;

        /// <summary>Number of pools, and how big each one gets.</summary>
        public IntRange poolCount = new IntRange(2, 5);
        public IntRange poolRadius = new IntRange(1, 3);

        /// <summary>Bioluminescence. This is the only light in here, so it is not optional.</summary>
        public List<ThingDef> lights = new List<ThingDef>();
        public IntRange lightCount = new IntRange(14, 22);

        /// <summary>Wreckage, for scale. Something was here and it was not a person.</summary>
        public List<ThingDef> wreckage = new List<ThingDef>();
        public IntRange wreckageCount = new IntRange(3, 7);

        public override int SeedPart => 0x1CF01E;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;

            PaintFloor(map);
            PourPools(map);
            Roof(map);
            Scatter(map, lights, lightCount.RandomInRange);
            Scatter(map, wreckage, wreckageCount.RandomInRange);

            // A pocket map generated behind the player's back would otherwise open fogged, and
            // the carrier arrives in the middle of it with no way to explore outward.
            map.fogGrid.ClearAllFog();
        }

        private void PaintFloor(Map map)
        {
            TerrainDef basic = floor ?? TerrainDefOf.Gravel;
            TerrainDef patch = patchTerrain;

            foreach (IntVec3 cell in map.AllCells)
            {
                TerrainDef chosen = basic;
                if (patch != null && Rand.Chance(patchChance)) chosen = patch;
                map.terrainGrid.SetTerrain(cell, chosen);
            }
        }

        private void PourPools(Map map)
        {
            if (poolTerrain == null) return;

            int count = poolCount.RandomInRange;
            for (int i = 0; i < count; i++)
            {
                IntVec3 centre;
                if (!CellFinderLoose.TryGetRandomCellWith(c => c.InBounds(map), map, 200, out centre)) continue;

                int radius = poolRadius.RandomInRange;
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(centre, radius, true))
                {
                    if (!cell.InBounds(map)) continue;
                    map.terrainGrid.SetTerrain(cell, poolTerrain);
                }
            }
        }

        /// <summary>
        /// Roofed to the edges, and thick.
        ///
        /// Nothing here is decoration. A roofed cell takes no sky glow, so the light level is
        /// entirely what this GenStep scattered - which is what makes the place look the same
        /// at every hour and lets the weather's sky colours be a tint rather than a clock.
        /// </summary>
        private void Roof(Map map)
        {
            RoofDef thick = RoofDefOf.RoofRockThick;
            foreach (IntVec3 cell in map.AllCells)
            {
                map.roofGrid.SetRoof(cell, thick);
            }
        }

        private void Scatter(Map map, List<ThingDef> pool, int count)
        {
            if (pool == null || pool.Count == 0) return;

            for (int i = 0; i < count; i++)
            {
                ThingDef def = pool.RandomElement();
                if (def == null) continue;

                IntVec3 cell;
                if (!CellFinderLoose.TryGetRandomCellWith(c => Fits(map, c, def), map, 300, out cell)) continue;

                Thing thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                GenSpawn.Spawn(thing, cell, map, Rot4.Random);
            }
        }

        /// <summary>
        /// Whether a thing of this size can stand here. Checked over the whole footprint rather
        /// than the origin cell, because the wreckage is the part of this that is several cells
        /// across and half of it landing inside a pool reads as a bug.
        /// </summary>
        private bool Fits(Map map, IntVec3 cell, ThingDef def)
        {
            CellRect rect = GenAdj.OccupiedRect(cell, Rot4.North, def.size);
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map)) return false;
                if (!c.Standable(map)) return false;
                if (c.GetFirstBuilding(map) != null) return false;
                if (c.GetPlant(map) != null) return false;
            }
            return true;
        }
    }
}
