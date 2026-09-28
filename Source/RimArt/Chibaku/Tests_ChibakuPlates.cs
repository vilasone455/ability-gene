using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Game test for the Chibaku ground plates (run with -quicktest -rimarttest=chibaku). The circle is painted
    /// in four terrains, one per quarter, with a concrete patch and a wall, so a picture captured upside down,
    /// mirrored or shifted shows the wrong terrain under a plate; the check reads the captured picture back
    /// through the plates' own UVs and compares it with each terrain's texture.
    /// </summary>
    public static class Tests_ChibakuPlates
    {
        private const float Radius = DebugActions_ChibakuPlates.Radius;
        private static readonly IntVec3 WallAt = new IntVec3(3, 0, -4);

        [RimArtTest("Chibaku", "ground plates 1 the captured ground matches the terrain; plates lift 1 cell and land", 1200)]
        private static IEnumerable<int> GroundPlates(RimArtTestContext t)
        {
            t.Clear();
            TerrainDef sand = TerrainDefOf.Sand, rich = TerrainDefOf.SoilRich, gravel = TerrainDefOf.Gravel, soil = TerrainDefOf.Soil, concrete = TerrainDefOf.Concrete;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(t.center, Radius + 2f, true))
            {
                int dx = c.x - t.center.x, dz = c.z - t.center.z;
                TerrainDef def = dx > 0 && dz > 0 ? sand : dx < 0 && dz > 0 ? rich : dx > 0 && dz < 0 ? gravel : soil;
                if (dx >= -4 && dx <= -2 && dz >= -3 && dz <= -2) def = concrete;
                t.map.terrainGrid.SetTerrain(c, def);
            }
            IntVec3 wall = t.center + WallAt;
            GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), wall, t.map);
            MapComponent_ChibakuPlates component = MapComponent_ChibakuPlates.Of(t.map);
            component.Stop();
            Find.CameraDriver.SetRootPosAndSize(t.center.ToVector3Shifted(), 10f);
            yield return 5;
            yield return t.ShotAs("chibaku-0-ground");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            ChibakuGround ground = component.Begin(t.center, Radius, .2f);
            watch.Stop();
            if (!t.Check(ground?.texture != null, "the ground under the circle was captured")) yield break;
            t.Log($"picture {ground.texture.width} px for {ground.span:0.#} cells, {ground.plates.Count} plates, {ground.plates.Count(p => p.anchored)} anchored; capture and cut took {watch.Elapsed.TotalMilliseconds:0.0} ms");
            t.Check(ground.plates.Count > 60, "the disc is cut into more than 60 plates");

            // Every cell centre inside the circle belongs to exactly one plate: the plates tile the disc.
            int wrong = 0;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(t.center, Radius - .6f, true))
                if (ground.plates.Count(p => p.cells.Contains(c)) != 1) wrong++;
            t.Check(wrong == 0, $"each cell in the circle is under exactly one plate ({wrong} are not)");
            List<ChibakuPlate> onWall = ground.plates.Where(p => p.cells.Contains(wall)).ToList();
            t.Check(onWall.Count == 1 && onWall[0].anchored, "the plate under the wall stays in the ground");
            t.Check(ground.plates.Count(p => p.anchored) == onWall.Count, "no other plate stays");

            // The captured colour over sample cells, read through the plates' UVs, is nearest the colour of the
            // cell's own terrain texture. A flipped or mirrored picture puts another quarter's terrain there.
            var terrains = new[] { sand, rich, gravel, soil, concrete };
            var reference = terrains.ToDictionary(d => d, d => AverageColour(t.map.terrainGrid.GetMaterial(d, false, null)));
            foreach (TerrainDef d in terrains) t.Log($"{d.defName,-9} texture {Show(reference[d])}");
            var samples = new (int dx, int dz, TerrainDef def)[]
                { (3, 3, sand), (2, 4, sand), (-3, 3, rich), (-4, 2, rich), (2, -2, gravel), (4, -1, gravel), (-5, -1, soil), (-1, -5, soil), (-3, -3, concrete), (-3, -2, concrete) };
            foreach (var (dx, dz, def) in samples)
            {
                Color got = ground.SampleCell(t.center + new IntVec3(dx, 0, dz));
                TerrainDef nearest = terrains.OrderBy(d => Distance(got, reference[d])).First();
                t.Check(nearest == def, $"({dx,2},{dz,2}) {def.defName}: captured {Show(got)}, nearest {nearest.defName}");
            }
            yield return t.ShotAs("chibaku-1-at-rest");

            component.Freeze(3f);
            t.Check(ground.plates.All(p => Mathf.Abs(ChibakuGround.TestHeight(p, 3f) - (p.anchored ? 0f : ChibakuGround.LiftTo)) < .001f),
                "at 3 s every plate but the anchored one is 1 cell up");
            yield return t.ShotAs("chibaku-2-lifted");
            yield return t.ShotAs("chibaku-3-lifted-close", t.center + new IntVec3(1, 0, 1), 5f);
            component.Freeze(4.3f);
            yield return t.ShotAs("chibaku-4-landing");
            component.Freeze(ChibakuGround.End);
            t.Check(ground.plates.All(p => ChibakuGround.TestHeight(p, ChibakuGround.End) == 0f), "at the end every plate is back on the ground");
            component.Stop();
            t.Check(component.Ground == null && ground.texture == null, "stopping frees the picture");
            yield return 2;
        }

        /// <summary>A texture's average colour (drawn small into a render texture and read back) times the material's colour.</summary>
        private static Color AverageColour(Material material)
        {
            Texture texture = material?.mainTexture;
            if (texture == null) return Color.magenta;
            RenderTexture small = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, small);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = small;
            var read = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(small);
            Color sum = Color.clear;
            foreach (Color c in read.GetPixels()) sum += c;
            Object.Destroy(read);
            return sum / 1024f * material.color;
        }

        private static float Distance(Color a, Color b) => Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        private static string Show(Color c) => $"({c.r:0.00} {c.g:0.00} {c.b:0.00})";
    }
}
