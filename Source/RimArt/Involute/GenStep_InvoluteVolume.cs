using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Builds the inside of the volume: Kamui's dimension, from <see cref="KamuiLayout"/>, the same map the
    /// lab's Kamui dimension sketch draws for the same seed, size, cover and island count. The layout's
    /// cell (x, z) is the map's cell (x, z); the map is square (the gene's volumeSizeX is used for both
    /// sides, see <see cref="InvoluteUtility.GenerateVolume"/>).
    ///
    /// What it places is the dimension's body, not its look: void terrain nobody can walk on every cell,
    /// block-top terrain under every walkable top, thick roof everywhere, an unseen cold light every
    /// <see cref="lightSpacing"/> cells (so the light is even and the same at every hour: nothing in the
    /// source casts it), and no fog. <see cref="MapComponent_KamuiDimension"/> draws the blocks over the
    /// terrain with the sketch's meshes.
    ///
    /// Every def named here is the mod's own; nothing resolves to a DLC.
    /// </summary>
    public class GenStep_InvoluteVolume : GenStep
    {
        public TerrainDef voidTerrain;
        public TerrainDef topTerrain;
        /// <summary>An unseen glower. Placed on a grid over the whole map, void included.</summary>
        public ThingDef light;
        public int lightSpacing = 6;
        /// <summary>Walkable share of the map the generator grows toward, and the fewest islands it makes.</summary>
        public double cover = KamuiLayout.DefaultCover;
        public int islands = 6;
        /// <summary>A new volume takes its seed from these (the sketch's seed slider runs 1 to 60).</summary>
        public IntRange seeds = new IntRange(1, 60);
        /// <summary>"fight" (the anime seen from above) or "still" (the darker Narutopedia picture).</summary>
        public string palette = KamuiGraphics.Fight;

        public override int SeedPart => 0x1CF01E;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;

            int seed = seeds.RandomInRange;
            int size = System.Math.Min(map.Size.x, map.Size.z);
            KamuiLayout layout = KamuiLayout.Generate(seed, size, cover, islands);

            RoofDef roof = RoofDefOf.RoofRockThick;
            foreach (IntVec3 cell in map.AllCells)
            {
                map.terrainGrid.SetTerrain(cell, layout.IsWalkable(cell.x, cell.z) ? topTerrain : voidTerrain);
                map.roofGrid.SetRoof(cell, roof);
            }

            if (light != null && lightSpacing > 0)
            {
                int first = lightSpacing / 2;
                for (int x = first; x < map.Size.x; x += lightSpacing)
                    for (int z = first; z < map.Size.z; z += lightSpacing)
                        GenSpawn.Spawn(ThingMaker.MakeThing(light), new IntVec3(x, 0, z), map);
            }

            // A pocket map generated behind the player's back would otherwise open fogged.
            map.fogGrid.ClearAllFog();
            map.GetComponent<MapComponent_KamuiDimension>().Begin(seed, cover, islands, palette);
        }
    }
}
