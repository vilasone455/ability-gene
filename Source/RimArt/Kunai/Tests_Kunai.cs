using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for the kunai (run with -quicktest -rimarttest=kunai).</summary>
    public static class Tests_Kunai
    {
        [RimArtTest("Kunai", "look 1 the kunai on the ground, the belt on the ground, a kunai stuck in a pawn and one in flight (screenshot)")]
        private static IEnumerable<int> Look(RimArtTestContext t)
        {
            t.Clear();
            IntVec3 c = t.center;
            Thing one = GenSpawn.Spawn(ThingMaker.MakeThing(KunaiDefOf.AG_Kunai), c + new IntVec3(-3, 0, 1), t.map);
            Thing five = ThingMaker.MakeThing(KunaiDefOf.AG_Kunai);
            five.stackCount = 5;
            GenSpawn.Spawn(five, c + new IntVec3(-3, 0, -1), t.map);
            GenSpawn.Spawn(ThingMaker.MakeThing(KunaiDefOf.AG_KunaiBelt), c + new IntVec3(-2, 0, 0), t.map);
            t.Check(one.Graphic?.path == "RimArt/Kunai/Kunai", "the kunai item uses the kunai texture (" + one.Graphic?.path + ")");

            // Stuck in a pawn: a light stab, then the kunai goes into that wound, as a hit does.
            Pawn target = t.Enemy(c + new IntVec3(2, 0, 0), armed: false);
            target.apparel?.DestroyAll();
            var before = new HashSet<Hediff>(target.health.hediffSet.hediffs);
            target.TakeDamage(new DamageInfo(DamageDefOf.Stab, 3f, 1f, -1f, null, target.RaceProps.body.corePart));
            t.Check(KunaiEmbedding.TryEmbed(target, before), "a kunai is stuck in the target");
            t.Check(KunaiEmbedding.CountOn(target) == 1, "one kunai on the target (" + KunaiEmbedding.CountOn(target) + ")");
            yield return 60;  // past the red flash a hit gives the target

            // In flight: thrown from a colonist at a cell to the east, shot 3 ticks after it leaves.
            Pawn thrower = t.Colonist(c + new IntVec3(-1, 0, -3));
            thrower.drafter.Drafted = true;
            RimArtTestContext.Hold(thrower);
            var shot = (Projectile)GenSpawn.Spawn(ThingMaker.MakeThing(KunaiDefOf.AG_KunaiProjectile), thrower.Position, t.map);
            IntVec3 aim = c + new IntVec3(8, 0, -1);
            shot.Launch(thrower, thrower.DrawPos, aim, aim, ProjectileHitFlags.None);
            yield return 3;
            t.Check(!shot.Destroyed, "the kunai is still in the air");
            yield return t.ShotAs("kunai-look");
        }

        [RimArtTest("Kunai", "plant 1 a miss onto open ground stands in it the way it flew, starts forbidden and never stacks; into water or off a wall it lies flat; picked up it lies flat (screenshot)")]
        private static IEnumerable<int> Plant(RimArtTestContext t)
        {
            t.Clear();
            IntVec3 c = t.center;
            Pawn thrower = t.Colonist(c);
            RimArtTestContext.Hold(thrower);

            // One throw each way round the thrower, 4 cells out, at empty cells: nothing to hit.
            var aims = new List<IntVec3>();
            foreach ((int x, int z) in new[] { (0, 4), (3, 3), (4, 0), (3, -3), (0, -4), (-3, -3), (-4, 0), (-3, 3) })
                aims.Add(c + new IntVec3(x, 0, z));
            var shots = new List<Projectile>();
            foreach (IntVec3 aim in aims) shots.Add(Throw(t, thrower, aim));
            for (int i = 0; i < 60 && shots.Any(s => !s.Destroyed); i++) yield return 1;
            foreach (IntVec3 aim in aims)
            {
                // Launch aims at a random point up to 0.3 cells round the cell's centre: 4.3 degrees at 4 cells.
                float want = (aim.ToVector3Shifted() - c.ToVector3Shifted()).AngleFlat();
                KunaiItem kunai = KunaiNear(t, aim, 0f).FirstOrDefault();
                t.Check(kunai != null && kunai.planted && kunai.Position == aim,
                    "the throw toward " + (aim - c) + " planted in its cell (" + Describe(kunai) + ")");
                t.Check(kunai != null && kunai.IsForbidden(Faction.OfPlayer), "  it starts forbidden");
                if (kunai != null)
                    t.Check(Mathf.Abs(Mathf.DeltaAngle(kunai.plantAngle, want)) < 6f,
                        "  pointing " + kunai.plantAngle.ToString("0") + " degrees, thrown toward " + want.ToString("0"));
            }

            // Two misses into one cell stay two kunai: the second plants in the next cell.
            IntVec3 twice = c + new IntVec3(-2, 0, -7);
            Projectile a = Throw(t, thrower, twice);
            for (int i = 0; i < 60 && !a.Destroyed; i++) yield return 1;
            Projectile b = Throw(t, thrower, twice);
            for (int i = 0; i < 60 && !b.Destroyed; i++) yield return 1;
            List<KunaiItem> pair = KunaiNear(t, twice, 1.5f).ToList();
            t.Check(pair.Count == 2 && pair.All(k => k.planted && k.stackCount == 1) && pair[0].Position != pair[1].Position,
                "two throws into one cell: two planted kunai in two cells (" + string.Join(", ", pair.Select(Describe)) + ")");

            // Into water: it lies flat.
            IntVec3 water = c + new IntVec3(5, 0, -7);
            t.map.terrainGrid.SetTerrain(water, TerrainDefOf.WaterShallow);
            Projectile w = Throw(t, thrower, water);
            for (int i = 0; i < 60 && !w.Destroyed; i++) yield return 1;
            KunaiItem wet = KunaiNear(t, water, 1.5f).FirstOrDefault();
            t.Check(wet != null && !wet.planted, "a throw into water lies flat (" + Describe(wet) + ")");

            // Off a wall: it lies flat beside it. A hit breaks one in 5, so throw until one drops.
            IntVec3 wallCell = c + new IntVec3(-7, 0, 2);
            Thing wall = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), wallCell, t.map);
            KunaiItem bounced = null;
            for (int n = 1; n <= 5 && bounced == null; n++)
            {
                Projectile h = Throw(t, thrower, wall, ProjectileHitFlags.All);
                for (int i = 0; i < 60 && !h.Destroyed; i++) yield return 1;
                bounced = KunaiNear(t, wallCell, 1.5f).FirstOrDefault();
                if (bounced == null) t.Log("throw " + n + " at the wall broke the kunai");
            }
            t.Check(bounced != null && !bounced.planted, "a kunai off the wall lies flat (" + Describe(bounced) + ")");
            t.Check(bounced != null && !bounced.IsForbidden(Faction.OfPlayer), "  and is not forbidden");

            yield return t.ShotAs("kunai-plant");

            // Picked up it comes out of the ground; dropped it lies flat.
            KunaiItem first = KunaiNear(t, aims[0], 0f).FirstOrDefault();
            if (first != null)
            {
                // As a pick-up or haul does: SplitOff takes a whole stack off the map, then it goes in.
                t.Check(thrower.inventory.innerContainer.TryAddOrTransfer(first.SplitOff(first.stackCount)), "the thrower picked up the kunai");
                t.Check(!first.planted, "a picked-up kunai is no longer planted");
                t.Check(!first.IsForbidden(Faction.OfPlayer), "  nor forbidden");
                thrower.inventory.innerContainer.TryDrop(first, c + new IntVec3(1, 0, 1), t.map, ThingPlaceMode.Near, out Thing dropped);
                t.Check(dropped is KunaiItem k && !k.planted, "dropped again it lies flat (" + Describe(dropped as KunaiItem) + ")");
            }
        }

        private static Projectile Throw(RimArtTestContext t, Pawn thrower, LocalTargetInfo target,
            ProjectileHitFlags flags = ProjectileHitFlags.None)
        {
            var shot = (Projectile)GenSpawn.Spawn(ThingMaker.MakeThing(KunaiDefOf.AG_KunaiProjectile), thrower.Position, t.map);
            shot.Launch(thrower, thrower.DrawPos, target, target, flags);
            return shot;
        }

        private static IEnumerable<KunaiItem> KunaiNear(RimArtTestContext t, IntVec3 cell, float radius) =>
            t.map.listerThings.ThingsOfDef(KunaiDefOf.AG_Kunai).OfType<KunaiItem>()
                .Where(k => k.Spawned && k.Position.DistanceTo(cell) <= radius);

        private static string Describe(KunaiItem kunai) => kunai == null ? "none"
            : "at " + kunai.Position + (kunai.planted ? " planted " + kunai.plantAngle.ToString("0") + " deg" : " flat") + " x" + kunai.stackCount;
    }
}
