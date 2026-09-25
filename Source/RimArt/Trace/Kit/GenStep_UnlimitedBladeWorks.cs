using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Builds Unlimited Blade Works on its pocket map: the world's body, not its look. Earth terrain on
    /// every cell (its texture is only a fallback: <see cref="MapComponent_UnlimitedBladeWorks"/> draws the
    /// world over it with the lab's meshes, the standing swords included, which are drawings and block
    /// nothing), thick roof everywhere and an unseen warm light every <see cref="lightSpacing"/> cells, so
    /// the light is the world's own twilight at every hour, and no fog. The landing spots the map was made
    /// for are handed to the component, which keeps its swords off them.
    /// </summary>
    public class GenStep_UnlimitedBladeWorks : GenStep
    {
        public TerrainDef earthTerrain;
        /// <summary>An unseen glower, placed on a grid over the whole map.</summary>
        public ThingDef light;
        public int lightSpacing = 6;

        public override int SeedPart => 0x0B1ADE5;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;
            var asked = UnlimitedBladeWorksMap.TakeRequest() ?? (new List<IntVec3> { IntVec3.Zero }, false);

            RoofDef roof = RoofDefOf.RoofRockThick;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (earthTerrain != null) map.terrainGrid.SetTerrain(cell, earthTerrain);
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
            map.GetComponent<MapComponent_UnlimitedBladeWorks>().Begin(asked.keep, asked.depth);
        }
    }
}
