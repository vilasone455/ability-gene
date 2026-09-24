using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Builds the Infinity Castle on its pocket map from <see cref="CastleLayout"/>: the same castle the
    /// lab's Castle sketch draws for the same seed and room count. The castle's cell (x, z) is the map's
    /// cell (x, z), so the map is <see cref="CastleLayout.Size"/> square.
    ///
    /// What it places is the castle's body, not its look: void terrain nobody can walk on every cell,
    /// castle floor under every room (its wall ring included, and the doorways, which are open passages),
    /// a castle wall in every wall cell but the doorways, a lantern at every lantern of every room, thick
    /// roof everywhere, and no fog (Castle sight). The walls and lanterns have plain or no graphics;
    /// <see cref="MapComponent_InfinityCastle"/> draws the rooms with the sketch's meshes over them.
    /// </summary>
    public class GenStep_InfinityCastle : GenStep
    {
        public TerrainDef voidTerrain;
        public TerrainDef floorTerrain;
        public ThingDef wall;
        public ThingDef lantern;
        /// <summary>A castle made with no seed asked for takes one from these (the sketch's sliders: seed 1 to 60, 30 to 45 rooms).</summary>
        public IntRange seeds = new IntRange(1, 60);
        public IntRange rooms = new IntRange(30, 45);

        public override int SeedPart => 0x1CA57E;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null) return;
            (int seed, int count) = InfinityCastleMap.TakeRequest() ?? (seeds.RandomInRange, rooms.RandomInRange);
            CastleLayout castle = CastleLayout.Generate(seed, count);

            RoofDef roof = RoofDefOf.RoofRockThick;
            foreach (IntVec3 cell in map.AllCells)
            {
                map.terrainGrid.SetTerrain(cell, voidTerrain);
                map.roofGrid.SetRoof(cell, roof);
            }

            var doorways = new HashSet<IntVec3>();
            foreach (CastleDoorway door in castle.Doorways)
                foreach (var (x, z) in door.Cells) doorways.Add(new IntVec3(x, 0, z));

            foreach (CastleRoom room in castle.Rooms)
            {
                for (int x = room.X; x < room.X + room.W; x++)
                    for (int z = room.Z; z < room.Z + room.H; z++)
                    {
                        var cell = new IntVec3(x, 0, z);
                        if (!cell.InBounds(map)) continue;
                        map.terrainGrid.SetTerrain(cell, floorTerrain);
                        if (wall != null && room.IsWall(x, z) && !doorways.Contains(cell))
                            GenSpawn.Spawn(ThingMaker.MakeThing(wall), cell, map);
                    }
                if (lantern == null) continue;
                double cx = room.X + room.W / 2.0, cz = room.Z + room.H / 2.0;
                foreach (var (lx, lz) in CastleLayout.LanternsOf(room))
                {
                    var cell = new IntVec3((int)System.Math.Floor(cx + lx), 0, (int)System.Math.Floor(cz + lz));
                    if (cell.InBounds(map) && cell.GetFirstThing(map, lantern) == null)
                        GenSpawn.Spawn(ThingMaker.MakeThing(lantern), cell, map);
                }
            }

            map.fogGrid.ClearAllFog();
            map.GetComponent<MapComponent_InfinityCastle>().Begin(seed, count);
        }
    }
}
