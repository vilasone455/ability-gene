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

        [RimArtTest("Kunai", "minato 1 in Minato's hero form a throw is his sealed three-pronged kunai and stays his when planted, stuck and pulled; an ordinary colonist's is not (screenshot)")]
        private static IEnumerable<int> Minato(RimArtTestContext t)
        {
            t.Clear();
            IntVec3 c = t.center;
            HediffDef form = DefDatabase<HediffDef>.GetNamedSilentFail(KunaiSeal.MinatoForm);
            if (!t.Check(form != null, "Minato's hero form exists")) yield break;
            Pawn minato = t.Colonist(c + new IntVec3(-4, 0, 4));
            Pawn other = t.Colonist(c + new IntVec3(-4, 0, -4));
            foreach (Pawn pawn in new[] { minato, other })
            {
                RimArtTestContext.Hold(pawn);
                pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(KunaiDefOf.AG_KunaiBelt));
            }
            minato.health.AddHediff(form);
            t.Check(KunaiSeal.ThrowsSealed(minato) && !KunaiSeal.ThrowsSealed(other), "only Minato throws sealed kunai");

            // Both throw through the belt's ability at an empty cell (a wild miss may land a few cells off).
            IntVec3 aimM = c + new IntVec3(2, 0, 4), aimO = c + new IntVec3(2, 0, -4);
            var before = new HashSet<Thing>(t.map.listerThings.ThingsOfDef(KunaiDefOf.AG_Kunai));
            t.Check(Cast(minato, aimM) && Cast(other, aimO), "both threw");
            bool sawHis = false, sawPlain = false;
            for (int i = 0; i < 180 && NewKunai(t, before).Count() < 2; i++)
            {
                sawHis |= t.map.listerThings.ThingsOfDef(KunaiDefOf.AG_KunaiProjectileMinato).Any();
                sawPlain |= t.map.listerThings.ThingsOfDef(KunaiDefOf.AG_KunaiProjectile).Any();
                yield return 1;
            }
            t.Check(sawHis && sawPlain, "Minato's kunai flew as his projectile, the other's as the ordinary one");
            KunaiItem his = NewKunai(t, before).FirstOrDefault(k => k.Position.DistanceTo(aimM) < 5);
            KunaiItem plain = NewKunai(t, before).FirstOrDefault(k => k.Position.DistanceTo(aimO) < 5);
            t.Check(his != null && his.sealedByMinato, "Minato's landed sealed (" + Describe(his) + ")");
            t.Check(plain != null && !plain.sealedByMinato, "the other colonist's did not (" + Describe(plain) + ")");
            t.Check(his?.LabelNoCount == "Minato's kunai", "Minato's is labelled \"" + his?.LabelNoCount + "\"");

            // Flat: drawn three-pronged, and stacks only with his own.
            KunaiItem sealedA = (KunaiItem)GenSpawn.Spawn(KunaiEmbedding.MakeKunai(true), c + new IntVec3(0, 0, 1), t.map);
            Thing sealedB = KunaiEmbedding.MakeKunai(true), ordinary = KunaiEmbedding.MakeKunai(false);
            GenSpawn.Spawn(ordinary, c + new IntVec3(0, 0, -1), t.map);
            t.Check(sealedA.Graphic?.path == KunaiDefaults.MinatoTexture, "a flat sealed kunai uses " + sealedA.Graphic?.path);
            t.Check(ordinary.Graphic?.path == "RimArt/Kunai/Kunai", "an ordinary one uses " + ordinary.Graphic?.path);
            t.Check(sealedA.CanStackWith(sealedB) && !sealedA.CanStackWith(ordinary), "sealed kunai stack with each other, not with ordinary ones");

            // Stuck in a pawn: drawn three-pronged; pulled out it comes out sealed. The target is a
            // colonist, so the pull needs no roll; the puller wears no belt, so the kunai goes to the ground
            // (into a belt it would become a charge and lose the seal).
            Pawn target = t.Colonist(c + new IntVec3(4, 0, 0));
            RimArtTestContext.Hold(target);
            var hediffs = new HashSet<Hediff>(target.health.hediffSet.hediffs);
            target.TakeDamage(new DamageInfo(DamageDefOf.Stab, 3f, 1f, -1f, null, target.RaceProps.body.corePart));
            t.Check(KunaiEmbedding.TryEmbed(target, hediffs, sealedByMinato: true), "Minato's kunai is stuck in the target");
            yield return 60;  // past the red flash a hit gives the target
            PawnRenderNode node = Nodes(target).FirstOrDefault(n => n.hediff is Hediff_EmbeddedKunai);
            t.Check(node?.PrimaryGraphic?.path == KunaiDefaults.MinatoEmbeddedTexture,
                "the stuck kunai is drawn with " + node?.PrimaryGraphic?.path);
            yield return t.ShotAs("kunai-minato");

            var beforePull = new HashSet<Thing>(t.map.listerThings.ThingsOfDef(KunaiDefOf.AG_Kunai));
            Pawn puller = t.Colonist(c + new IntVec3(5, 0, 1));
            RimArtTestContext.Hold(puller);
            t.Check(KunaiEmbedding.TryPull(puller, target), "pulled out");
            KunaiItem pulled = NewKunai(t, beforePull).FirstOrDefault();
            t.Check(pulled != null && pulled.sealedByMinato, "the pulled kunai is still Minato's (" + Describe(pulled) + ")");
        }

        [RimArtTest("Kunai", "throw 1 the throw clip frame by frame: colonists throw east, south, north and diagonally through the belt's ability (screenshots)")]
        private static IEnumerable<int> ThrowFrames(RimArtTestContext t)
        {
            List<Pawn> pawns = Throwers(t);
            yield return 5;
            // Normal speed: Melee Animation advances a clip by frame time times the game speed, not by
            // ticks. Each screenshot pauses the game for a frame or more, so after the first one the
            // picture can be a couple of ticks off the tick number (throw 2 checks the timing unpaused).
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            CastAll(t, pawns);
            int at = 0;
            foreach (int tick in new[] { 3, 8, 12, 15, 17, 19, 22 })
            {
                yield return tick - at;
                at = tick;
                yield return t.ShotAs("throw-t" + tick.ToString("00"));
            }
        }

        [RimArtTest("Kunai", "throw 2 the kunai leaves the hand when the clip opens it, from where the hand is (log, one screenshot)")]
        private static IEnumerable<int> ThrowRelease(RimArtTestContext t)
        {
            List<Pawn> pawns = Throwers(t);
            yield return 5;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            CastAll(t, pawns);
            // Clip time against ticks, before any screenshot pauses the game.
            System.Type rendererType = HarmonyLib.AccessTools.TypeByName("AM.AnimRenderer");
            if (rendererType == null)
            {
                t.Log("Melee Animation is not loaded: no clip, the kunai launches from the middle of the pawn");
                yield break;
            }
            var tryGet = HarmonyLib.AccessTools.Method(rendererType, "TryGetAnimator", new[] { typeof(Pawn) });
            var timeField = HarmonyLib.AccessTools.Field(rendererType, "time");
            float clipAtLaunch = -1f;
            for (int tick = 1; tick <= 30 && clipAtLaunch < 0f; tick++)
            {
                yield return 1;
                object renderer = tryGet.Invoke(null, new object[] { pawns[0] });
                float clip = renderer == null ? -1f : (float)timeField.GetValue(renderer);
                int flying = t.map.listerThings.ThingsOfDef(KunaiDefOf.AG_KunaiProjectile).Count;
                t.Log("tick " + tick + ": clip " + clip.ToString("0.000") + " s, kunai in the air " + flying);
                if (flying > 0) clipAtLaunch = clip;
            }
            float release = KunaiReleaseSeconds;
            t.Check(clipAtLaunch >= release - 0.001f && clipAtLaunch <= release + 0.05f,
                "the kunai launched when the clip was at " + clipAtLaunch.ToString("0.000") + " s; it opens the hand at " + release + " s");
            yield return t.ShotAs("throw-release");
        }

        /// <summary>The kunai clip's release: ThrowAnimation.Kunai's fraction of its 0.6 s.</summary>
        private const float KunaiReleaseSeconds = 0.3f;

        // East, south, north and east-north-east (the east clip turned 34 degrees), far enough apart to crop one by one.
        private static readonly (IntVec3 from, IntVec3 to)[] ThrowLanes =
        {
            (new IntVec3(-4, 0, 3), new IntVec3(4, 0, 3)), (new IntVec3(-4, 0, -2), new IntVec3(-4, 0, -9)),
            (new IntVec3(3, 0, -4), new IntVec3(3, 0, 5)), (new IntVec3(-1, 0, -7), new IntVec3(5, 0, -3)),
        };

        private static List<Pawn> Throwers(RimArtTestContext t)
        {
            t.Clear();
            var pawns = new List<Pawn>();
            foreach ((IntVec3 from, IntVec3 _) in ThrowLanes)
            {
                Pawn pawn = t.Colonist(t.center + from);
                pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(KunaiDefOf.AG_KunaiBelt));
                RimArtTestContext.Hold(pawn);
                pawns.Add(pawn);
            }
            return pawns;
        }

        private static void CastAll(RimArtTestContext t, List<Pawn> pawns)
        {
            for (int i = 0; i < pawns.Count; i++)
                t.Check(Cast(pawns[i], t.center + ThrowLanes[i].to), pawns[i].LabelShort + " threw toward " + ThrowLanes[i].to);
        }

        private static bool Cast(Pawn pawn, IntVec3 at)
        {
            Ability ability = pawn.abilities?.GetAbility(KunaiDefOf.AG_ThrowKunai);
            return ability != null && ability.Activate(new LocalTargetInfo(at), new LocalTargetInfo(at));
        }

        private static IEnumerable<KunaiItem> NewKunai(RimArtTestContext t, HashSet<Thing> before) =>
            t.map.listerThings.ThingsOfDef(KunaiDefOf.AG_Kunai).OfType<KunaiItem>().Where(k => k.Spawned && !before.Contains(k));

        private static IEnumerable<PawnRenderNode> Nodes(Pawn pawn)
        {
            pawn.Drawer.renderer.EnsureGraphicsInitialized();
            var queue = new Queue<PawnRenderNode>();
            if (pawn.Drawer.renderer.renderTree.rootNode != null) queue.Enqueue(pawn.Drawer.renderer.renderTree.rootNode);
            while (queue.Count > 0)
            {
                PawnRenderNode node = queue.Dequeue();
                yield return node;
                if (node.children != null)
                    foreach (PawnRenderNode child in node.children) queue.Enqueue(child);
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
            : "at " + kunai.Position + (kunai.planted ? " planted " + kunai.plantAngle.ToString("0") + " deg" : " flat")
              + (kunai.sealedByMinato ? " sealed" : "") + " x" + kunai.stackCount;
    }
}
