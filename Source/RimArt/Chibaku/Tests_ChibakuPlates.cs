using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

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

        /// <summary>The cleared arena with the circle painted in four terrains, a concrete patch and a wall; returns the wall's cell.</summary>
        private static IntVec3 Arena(RimArtTestContext t)
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
            // The debug log opens by itself on other mods' startup warnings and would cover the screenshots.
            Find.WindowStack.TryRemove(typeof(LudeonTK.EditWindow_Log), false);
            return wall;
        }

        [RimArtTest("Chibaku", "ground plates 1 the captured ground matches the terrain; plates lift 1 cell and land", 1200)]
        private static IEnumerable<int> GroundPlates(RimArtTestContext t)
        {
            TerrainDef sand = TerrainDefOf.Sand, rich = TerrainDefOf.SoilRich, gravel = TerrainDefOf.Gravel, soil = TerrainDefOf.Soil, concrete = TerrainDefOf.Concrete;
            IntVec3 wall = Arena(t);
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

        [RimArtTest("Chibaku", "ball 1 the plates fly into a ball of the captured ground, it holds, bursts and the rocks land", 1200)]
        private static IEnumerable<int> Ball(RimArtTestContext t)
        {
            Arena(t);
            MapComponent_ChibakuPlates component = MapComponent_ChibakuPlates.Of(t.map);
            component.Stop();
            Find.CameraDriver.SetRootPosAndSize(t.center.ToVector3Shifted(), 10f);
            yield return 5;
            ChibakuBall ball = component.BeginBall(t.center, Radius, .9f);
            if (!t.Check(ball != null, "the ball was made from the captured ground")) yield break;
            t.Log($"{ball.Caught} plates pulled, {ball.Slots} on the ball's surface, last arrives at {ball.LastArrival:0.00} s, formed at {ChibakuBall.Formed:0.00} s, bursts at {ChibakuBall.Burst:0.00} s");
            t.Check(ball.LastArrival <= ChibakuBall.Formed, "every plate reaches the ball before it is formed");
            t.Check(ball.FilledSlots(ChibakuBall.Formed) == ball.Slots, "the ball's surface is full when it is formed");
            t.Check(Mathf.Abs(ball.BallRadiusAt(ChibakuBall.Pull) - .3f * ChibakuBall.Radius) < .001f && Mathf.Abs(ball.BallRadiusAt(ChibakuBall.Formed) - ChibakuBall.Radius) < .001f,
                "the ball grows from 0.3 to 1 times its radius");
            float lands = ball.LastChunkLands();
            t.Check(lands < ChibakuBall.Tail, $"every rock has landed {lands:0.00} s after the burst, before the preview ends");

            // The cost of drawing one frame mid-pull (the calls only; the GPU work is not in it).
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) ball.Draw(ChibakuBall.Pull + 1.5f);
            watch.Stop();
            t.Log($"one mid-pull frame's draw calls take {watch.Elapsed.TotalMilliseconds / 20:0.00} ms on the CPU");

            foreach (var (at, name) in new[] { (.9f, "1-cracks"), (1.9f, "2-pull"), (3.0f, "3-forming"), (ChibakuBall.Formed + 1.25f, "4-held"), (ChibakuBall.Crack + .2f, "6-seams"), (ChibakuBall.Burst + .2f, "7-burst"),
                (ChibakuBall.Burst + .75f, "8-rocks-falling"), (ChibakuBall.End - .45f, "9-result") })
            {
                component.Freeze(at);
                yield return t.ShotAs("chibaku-ball-" + name);
                if (name == "4-held") yield return t.ShotAs("chibaku-ball-5-held-close", t.center + new IntVec3(0, 0, 2), 5f);
            }
            component.Stop();
            t.Check(component.Ball == null && component.Ground == null, "stopping frees the ball and the picture");
            yield return 2;
        }

        [RimArtTest("Chibaku", "pawns 1 pawns on the circle go into the ball and fall out hurt and stunned; roofed and outside ones stay", 2400)]
        private static IEnumerable<int> Pawns(RimArtTestContext t)
        {
            Arena(t);
            IntVec3 c = t.center;
            List<Pawn> raiders = new[] { new IntVec3(2, 0, 2), new IntVec3(-2, 0, 3), new IntVec3(1, 0, -2) }.Select(d => t.Enemy(c + d, armed: false)).ToList();
            Lord group = LordMaker.MakeNewLord(raiders[0].Faction, new LordJob_AssaultColony(raiders[0].Faction, false, false, false, false, false), t.map, raiders);
            Pawn colonist = t.Colonist(c + new IntVec3(-4, 0, -1));
            Pawn outside = t.Enemy(c + new IntVec3(0, 0, -9), armed: false);
            IntVec3 roofedCell = c + new IntVec3(-3, 0, 1);
            t.map.roofGrid.SetRoof(roofedCell, RoofDefOf.RoofConstructed);
            Pawn underRoof = t.Enemy(roofedCell, armed: false);
            foreach (Pawn p in new[] { colonist, outside, underRoof }) RimArtTestContext.Hold(p);
            var caught = raiders.Concat(new[] { colonist }).ToList();
            var injuries = caught.ToDictionary(p => p, Injuries);
            MapComponent_ChibakuPlates component = MapComponent_ChibakuPlates.Of(t.map);
            component.Stop();
            Find.CameraDriver.SetRootPosAndSize(c.ToVector3Shifted(), 10f);
            yield return 2;

            ChibakuBall ball = component.BeginBall(c, Radius);
            if (!t.Check(ball != null && component.Pull != null, "the live ball began")) yield break;
            int start = t.Now;
            int Until(float seconds) => Mathf.Max(0, start + Mathf.CeilToInt(seconds * 60f) - t.Now);

            yield return Until(ChibakuBall.Pull + .1f);
            foreach (Pawn p in caught) t.Check(p.stances.stunner.Stunned, $"{p.LabelShort} on the circle is held by the pull");
            t.Check(!outside.stances.stunner.Stunned && !underRoof.stances.stunner.Stunned, "the raider outside the circle and the one under the roof are not");
            yield return t.ShotAs("chibaku-pawns-1-held-by-the-pull");
            yield return Until(ChibakuBall.Pull + 1.1f);
            yield return t.ShotAs("chibaku-pawns-2-pulled-in");

            yield return Until(ChibakuPull.LastCatch + ChibakuPull.FlySeconds);
            foreach (Pawn p in caught)
                t.Check(!p.Spawned && p.ParentHolder == component && p.MapHeld == t.map, $"{p.LabelShort} is inside the ball (off the map, still counted on it)");
            t.Check(outside.Spawned && underRoof.Spawned && underRoof.Position == roofedCell, "the outside and roofed raiders are still standing where they were");
            t.Log($"{component.Pull.pawns.Count} pawns caught; the raid group {(t.map.lordManager.lords.Contains(group) ? "still exists" : "ended when its pawns were taken")}");
            yield return Until(ChibakuBall.Formed + 1f);
            yield return t.ShotAs("chibaku-pawns-3-held-in-the-ball");

            yield return Until(ChibakuBall.Burst + .3f);
            yield return t.ShotAs("chibaku-pawns-4-falling-out");
            yield return Until(ChibakuBall.Burst + ChibakuPull.FallTime + .1f);
            foreach (Pawn p in caught)
            {
                if (p.Dead) { t.Log($"{p.LabelShort} died of the crush and the fall"); continue; }
                t.Log($"{p.LabelShort}: {Describe(p)}, {Injuries(p) - injuries[p]} new injuries, {(p.Position - c).LengthHorizontal:0.0} cells from the middle");
                t.Check(p.Spawned && (p.Position - c).LengthHorizontal <= 3.5f, $"{p.LabelShort} landed under the ball");
                t.Check(Injuries(p) > injuries[p], $"{p.LabelShort} was hurt by the crush and the fall");
                t.Check(p.stances.stunner.Stunned || p.Downed, $"{p.LabelShort} is stunned (or down)");
            }
            foreach (Pawn p in raiders.Where(p => !p.Dead))
                t.Check(p.GetLord()?.LordJob is LordJob_AssaultColony, $"{p.LabelShort} is back in an assault group");
            t.Check(colonist.Dead || colonist.GetLord() == null, "the colonist is in no group");
            yield return t.ShotAs("chibaku-pawns-5-landed");

            yield return Until(ChibakuBall.End + .2f);
            t.Check(component.Ball == null && component.Inner.Count == 0, "the preview ended by itself with nothing left inside");
        }

        private static int Injuries(Pawn p) => p.health.hediffSet.hediffs.Count(h => h is Hediff_Injury || h is Hediff_MissingPart);

        private static string Describe(Pawn p) => p.Downed ? "down" : p.stances.stunner.Stunned ? "stunned" : "up";

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
