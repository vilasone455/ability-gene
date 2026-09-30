using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Game tests for Gojo's Blue, Red and Hollow Purple (run with -quicktest -rimarttest=gojo): Blue's pull and
    /// implosion and what it does not do (grow, drift, bend bullets, hold Gojo); Red's throw, push, wall slam and a
    /// wall in its path; Red through Blue making Hollow Purple and what Purple erases, strikes and scars; Red passing
    /// through Blue as a plain Red while Purple is on cooldown. The abilities are given directly (no Echo), so no
    /// charge is taken; the Echo's cost for Purple is not tested here.
    /// </summary>
    public static class Tests_GojoCombat
    {
        private static AbilityDef Blue => GojoKitDefOf.AG_GojoBlue;
        private static AbilityDef Red => GojoKitDefOf.AG_GojoRed;
        private static AbilityDef Purple => GojoKitDefOf.AG_GojoHollowPurple;

        private static MapComponent_GojoKit Kit(RimArtTestContext t) => t.map.GetComponent<MapComponent_GojoKit>();
        private static MapComponent_Gravity Wells(RimArtTestContext t) => t.map.GetComponent<MapComponent_Gravity>();

        private static void Setup(RimArtTestContext t)
        {
            Restore();
            t.Clear();
            GameComponent_Gravity.Instance.ResetForTests();
            GameComponent_Echoes.Get.ResetForTests();
            Kit(t).ResetForTests();
        }

        private static void DropTraits(Pawn pawn)
        {
            foreach (TraitDef def in new[] { TraitDefOf.Wimp, DefDatabase<TraitDef>.GetNamedSilentFail("Tough") })
            {
                if (def == null) continue;
                Trait trait = pawn.story?.traits?.GetTrait(def);
                if (trait != null) pawn.story.traits.RemoveTrait(trait);
            }
        }

        /// <summary>A drafted, unarmed colonist with Blue, Red and Hollow Purple given directly (not through the Echo).</summary>
        private static Pawn Gojo(RimArtTestContext t, IntVec3 at)
        {
            Pawn pawn = t.Colonist(at);
            pawn.equipment?.DestroyAllEquipment();
            pawn.drafter.FireAtWill = false;
            DropTraits(pawn);
            pawn.abilities.GainAbility(Blue);
            pawn.abilities.GainAbility(Red);
            pawn.abilities.GainAbility(Purple);
            return pawn;
        }

        /// <summary>A hostile baseliner that stands still: unarmed and unclothed unless asked, no Wimp or Tough.</summary>
        private static Pawn Raider(RimArtTestContext t, IntVec3 at, bool geared = false)
        {
            Faction faction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            var request = new PawnGenerationRequest(faction?.def.basicMemberKind ?? PawnKindDefOf.Villager, faction,
                mustBeCapableOfViolence: true, dontGiveWeapon: !geared,
                forcedXenotype: ModsConfig.BiotechActive ? XenotypeDefOf.Baseliner : null);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, at, t.map);
            RimArtTestContext.Hold(pawn);
            if (!geared) pawn.apparel?.DestroyAll();
            DropTraits(pawn);
            return pawn;
        }

        private static Thing Item(RimArtTestContext t, ThingDef def, int count, IntVec3 at)
        {
            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            thing.stackCount = count;
            return GenSpawn.Spawn(thing, at, t.map);
        }

        private static Thing Wall(RimArtTestContext t, IntVec3 at)
        {
            Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.BlocksGranite);
            wall.SetFaction(Faction.OfPlayer);
            return GenSpawn.Spawn(wall, at, t.map);
        }

        private static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 1)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        private static float Injuries(Pawn pawn) => pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
        // Hurt: dead, injured or missing a part (a part a hit destroyed carries no injury).
        private static bool Hurt(Pawn pawn) => pawn.Dead || pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Injury || h is Hediff_MissingPart);
        private static bool Untouched(Pawn pawn) => !Hurt(pawn);
        private static string Where(Pawn pawn, IntVec3 c) => pawn.Spawned ? (pawn.Position - c).ToString() : RimArtTestContext.Describe(pawn);
        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        private static string State(GravityCast cast, IntVec3 c) => $"{cast.clock.phase} tick {cast.clock.ticks}: eaten {cast.eaten:0.#}, "
            + $"radius {cast.Radius:0.00}, core {cast.Props.coreRadius:0.0}, centre {cast.Centre - c.ToVector3Shifted()} from the cast cell, "
            + $"implosion {cast.Props.Damage(cast.eaten):0.#} within {cast.Props.BurstRadius(cast.eaten):0.#}";

        private static string Shot(RedShot shot) => shot == null ? "no shot" : $"fired {shot.Fired}, burst {shot.Burst} at {shot.burstAlong:0.00} cells"
            + (shot.wallBurst ? " (wall)" : "") + (shot.used ? ", used by Purple" : "")
            + (shot.hit == null ? ", nothing thrown" : $", threw {(shot.hit.pawn?.LabelShort ?? "a thing")} {shot.hit.cells:0.##} cells{(shot.hit.walled ? " into a wall" : "")} for {shot.hit.damage:0.#}")
            + $", {shot.pushed.Count} pushed";

        // ---- Blue ---------------------------------------------------------------------------------------------------

        [RimArtTest("Gojo", "blue 1 pulls pawns and items within 4 cells for 3 s and implodes; never grows or drifts; Gojo stays free", 1500)]
        private static IEnumerable<int> BluePull(RimArtTestContext t)
        {
            Setup(t);
            yield return 2;
            IntVec3 c = t.center;
            Pawn gojo = Gojo(t, c + new IntVec3(-8, 0, 0));
            Pawn raider = Raider(t, c + new IntVec3(3, 0, 0));
            Pawn ally = t.Colonist(c + new IntVec3(0, 0, -3));
            DropTraits(ally);
            Pawn far = Raider(t, c + new IntVec3(6, 0, 0));
            Thing steel = Item(t, ThingDefOf.Steel, 50, c + new IntVec3(0, 0, 2));
            IntVec3 gojoCell = gojo.Position, farCell = far.Position;
            var props = CompProperties_AbilityGravityWell.For(Blue);
            t.Log($"XML: range {props.range}, radius {props.minRadius}-{props.maxRadius}, core {props.coreRadius}, {props.baseSeconds} s, "
                + $"damage {props.minDamage}-{props.maxDamage} at {props.fullMass} mass, opening {props.openingSeconds} s, drift {props.driftSpeed}, "
                + $"bends bullets {props.bendsBullets}, holds caster {props.holdsCaster}");
            MapComponent_Gravity wells = Wells(t);
            if (!t.Check(wells.Begin(gojo, c, Blue), "Blue opened")) yield break;
            GravityCast cast = wells.For(gojo);
            t.Check(cast != null && cast.animation == null && !GravityCommands.Busy(gojo), "Gojo is not held: no clip, not busy");
            t.Check(gojo.CurJobDef?.defName != "AM_InAnimation", "Gojo is not in an animation job (" + gojo.CurJobDef?.defName + ")");
            Vector3 origin = cast.Centre;
            yield return props.OpeningTicks + 1;
            t.Check(cast.Field, "the pull started after " + props.OpeningTicks + " ticks (" + State(cast, c) + ")");
            bool fixedRadius = true, stayed = true;
            float raiderStart = (raider.DrawPos - origin).Yto0().magnitude, allyStart = (ally.DrawPos - origin).Yto0().magnitude;
            int opened = t.Now;
            while (cast.Active && t.Now - opened < 400)
            {
                fixedRadius &= Mathf.Approximately(cast.Radius, 4f);
                stayed &= (cast.Centre - origin).sqrMagnitude < 1e-6f;
                if ((t.Now - opened) % 30 == 0)
                    t.Log($"{t.Now - opened} ticks: raider {(raider.DrawPos - origin).Yto0().magnitude:0.00} from the centre, "
                        + $"ally {(ally.DrawPos - origin).Yto0().magnitude:0.00}, steel {(steel.Destroyed ? "crushed" : "at " + (steel.Position - c))}; " + State(cast, c));
                yield return 1;
            }
            t.Log("closed: " + State(cast, c) + $", implosion {cast.burstDamage:0.#} within {cast.burstRadius:0.#}, hit {string.Join(", ", cast.burstHit.Select(p => p.LabelShort))}");
            t.Check(!cast.Active && cast.clock.imploded, "it imploded by itself");
            t.Check(cast.clock.ticks == 180, "after 3 s of pull (" + cast.clock.ticks + " ticks)");
            t.Check(fixedRadius, "the pull radius stayed 4 cells");
            t.Check(stayed, "the centre never moved");
            t.Check(raiderStart > 2.5f && allyStart > 2.5f && cast.eatenPawns.Contains(raider.thingIDNumber) && cast.eatenPawns.Contains(ally.thingIDNumber),
                $"the raider and the ally (started {raiderStart:0.0} and {allyStart:0.0} out) were pulled into the core");
            t.Check(steel.Destroyed, "the steel reached the core and was crushed");
            float expected = Mathf.Lerp(props.minDamage, props.maxDamage, Mathf.Clamp01(cast.eaten / props.fullMass));
            t.Check(Mathf.Abs(cast.burstDamage - expected) < 0.01f && cast.burstDamage >= 10f && cast.burstDamage <= 25f,
                $"implosion {cast.burstDamage:0.#} blunt for {cast.eaten:0.#} mass (10-25)");
            t.Check(cast.burstHit.Contains(raider) && cast.burstHit.Contains(ally), "the raider and the ally took it");
            t.Check(Untouched(far) && far.Position == farCell, "the raider 6 cells out was neither pulled nor hurt (" + Where(far, c) + ")");
            t.Check(Untouched(gojo) && gojo.Position == gojoCell, "Gojo was never pulled or hurt");
            int cooldown = GameComponent_Gravity.Instance.Remaining(gojo);
            t.Check(cooldown > 1150 && cooldown <= 1200, "the cooldown runs 20 s from the close (" + cooldown + " ticks)");
            yield return t.ShotAs("blue-implosion", c, 8f);
        }

        [RimArtTest("Gojo", "blue 2 a bullet crossing Blue is neither bent nor eaten", 900)]
        private static IEnumerable<int> BlueBullets(RimArtTestContext t)
        {
            Setup(t);
            yield return 2;
            IntVec3 c = t.center;
            Pawn gojo = Gojo(t, c + new IntVec3(-8, 0, 0));
            Pawn shooter = t.Colonist(c + new IntVec3(2, 0, -8));
            shooter.drafter.FireAtWill = false;
            MapComponent_Gravity wells = Wells(t);
            if (!t.Check(wells.Begin(gojo, c, Blue), "Blue opened")) yield break;
            GravityCast cast = wells.For(gojo);
            yield return CompProperties_AbilityGravityWell.For(Blue).OpeningTicks + 1;
            // 2 cells east of the centre, inside the pull: Gravity Well would bend it in.
            IntVec3 aim = c + new IntVec3(2, 0, 9);
            var bullet = (Projectile)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Bullet_Revolver"), shooter.Position, t.map);
            bullet.Launch(shooter, shooter.DrawPos, aim, aim, ProjectileHitFlags.IntendedTarget);
            float line = shooter.DrawPos.x, worst = 0f;
            bool registered = false;
            Vector3 last = bullet.ExactPosition;
            foreach (int step in WaitFor(() => bullet.Destroyed, 90))
            {
                if (!bullet.Destroyed)
                {
                    last = bullet.ExactPosition;
                    worst = Mathf.Max(worst, Mathf.Abs(last.x - line));
                    registered |= GameComponent_GravityFlights.Instance.Get(bullet) != null;
                }
                yield return step;
            }
            t.Log($"bullet last seen at {last - c.ToVector3Shifted()} from the centre; farthest off its line {worst:0.000} cells; " + State(cast, c));
            t.Check(!registered, "the well never took the bullet's flight");
            t.Check(worst < 0.05f, "it flew straight past the centre (" + worst.ToString("0.000") + " off)");
            t.Check(cast.eaten < 0.001f, "nothing was eaten (" + cast.eaten + ")");
            cast.Finish(false);
        }

        // ---- Red ----------------------------------------------------------------------------------------------------

        [RimArtTest("Gojo", "red 1 the first pawn is thrown 6 cells for 9 blunt; a pawn beside the burst is pushed 2 cells for 8", 900)]
        private static IEnumerable<int> RedThrow(RimArtTestContext t)
        {
            Setup(t);
            yield return 2;
            IntVec3 c = t.center;
            Pawn gojo = Gojo(t, c + new IntVec3(-6, 0, 0));
            Pawn first = Raider(t, c);
            Pawn beside = Raider(t, c + new IntVec3(1, 0, 1));
            Pawn apart = Raider(t, c + new IntVec3(0, 0, 3));
            IntVec3 apartCell = apart.Position;
            Vector2 besideFrom = Flat(beside.DrawPos);
            yield return 5;
            Ability red = gojo.abilities.GetAbility(Red);
            red.QueueCastingJob(first, LocalTargetInfo.Invalid);
            int cast = t.Now;
            t.Check(gojo.CurJobDef == GojoKitDefOf.AG_CastGojoRed, "Gojo is in Red's cast job");
            RedShot shot = null;
            foreach (int w in WaitFor(() => (shot = Kit(t).Reds.FirstOrDefault(r => r.caster == gojo))?.Burst == true, 180)) yield return w;
            t.Log($"burst {t.Now - cast} ticks after the order (fire at {shot?.fireTick - cast}): " + Shot(shot));
            if (!t.Check(shot != null && shot.Burst && shot.hit?.pawn == first, "Red burst on the first pawn")) yield break;
            t.Check(Mathf.Abs(shot.burstAlong - 6f) < 0.2f, "at 6 cells (" + shot.burstAlong.ToString("0.00") + ")");
            foreach (int w in WaitFor(() => shot.hit.landed && shot.pushed.All(m => m.landed), 120)) yield return w;
            yield return 2;
            t.Log("thrown: " + RimArtTestContext.Describe(first) + ", injuries " + Injuries(first).ToString("0.#")
                + "; beside: " + RimArtTestContext.Describe(beside) + ", injuries " + Injuries(beside).ToString("0.#"));
            t.Check(first.Position == c + new IntVec3(6, 0, 0), "the first pawn landed 6 cells on (" + Where(first, c) + ")");
            t.Check(Mathf.Abs(shot.hit.damage - 9f) < 0.01f && Hurt(first), "it took 6 x 1.5 = 9 blunt (" + shot.hit.damage + " dealt as blunt, injuries " + Injuries(first).ToString("0.#") + ")");
            RedMove push = shot.pushed.FirstOrDefault(m => m.pawn == beside);
            t.Check(push != null && (Flat(beside.DrawPos) - besideFrom).magnitude > 1f
                && (Flat(beside.DrawPos) - Flat(c.ToVector3Shifted())).magnitude > (besideFrom - Flat(c.ToVector3Shifted())).magnitude + 1f,
                "the pawn beside the burst was pushed away from it (" + Where(beside, c) + ", " + (push?.cells ?? 0f).ToString("0.##") + " cells)");
            t.Check(push != null && Mathf.Abs(push.damage - 8f) < 0.01f && Hurt(beside), "it took 8 blunt (injuries " + Injuries(beside).ToString("0.#") + ")");
            t.Check(Untouched(apart) && apart.Position == apartCell, "a pawn 3 cells from the burst was not pushed (" + Where(apart, c) + ")");
            t.Check(!first.stances.stunner.Stunned && !beside.stances.stunner.Stunned, "no stun");
            t.Check(red.CooldownTicksRemaining > 0, "Red's cooldown runs (" + red.CooldownTicksRemaining + " ticks)");
            foreach (int w in WaitFor(() => gojo.CurJobDef != GojoKitDefOf.AG_CastGojoRed, 120)) yield return w;
            t.Log("Gojo free " + (t.Now - cast) + " ticks after the order: " + RimArtTestContext.Describe(gojo));
            t.Check(gojo.CurJobDef != GojoKitDefOf.AG_CastGojoRed, "Gojo is free once his arm is down");
            yield return t.ShotAs("red-throw", c + new IntVec3(2, 0, 0), 9f);
        }

        [RimArtTest("Gojo", "red 2 a pawn thrown into a wall stops at it and takes 2 x 1.5 + 10 = 13", 900)]
        private static IEnumerable<int> RedSlam(RimArtTestContext t)
        {
            Setup(t);
            yield return 2;
            IntVec3 c = t.center;
            for (int z = -1; z <= 1; z++) Wall(t, c + new IntVec3(3, 0, z));
            Pawn gojo = Gojo(t, c + new IntVec3(-6, 0, 0));
            Pawn first = Raider(t, c);
            yield return 5;
            gojo.abilities.GetAbility(Red).QueueCastingJob(first, LocalTargetInfo.Invalid);
            RedShot shot = null;
            foreach (int w in WaitFor(() => (shot = Kit(t).Reds.FirstOrDefault(r => r.caster == gojo))?.Burst == true, 180)) yield return w;
            t.Log(Shot(shot));
            if (!t.Check(shot?.hit?.pawn == first, "Red threw the first pawn")) yield break;
            foreach (int w in WaitFor(() => shot.hit.landed, 120)) yield return w;
            yield return 2;
            t.Log("thrown: " + RimArtTestContext.Describe(first) + ", injuries " + Injuries(first).ToString("0.#"));
            t.Check(shot.hit.walled && first.Position == c + new IntVec3(2, 0, 0), "it stopped at the wall's foot (" + Where(first, c) + ")");
            t.Check(Mathf.Abs(shot.hit.damage - 13f) < 0.01f && Hurt(first), "13 blunt (" + shot.hit.damage + ", injuries " + Injuries(first).ToString("0.#") + ")");
            yield return t.ShotAs("red-slam", c + new IntVec3(1, 0, 0), 8f);
        }

        [RimArtTest("Gojo", "red 3 a wall put across Red's path in flight ends it there; the pawn behind is untouched", 900)]
        private static IEnumerable<int> RedWall(RimArtTestContext t)
        {
            Setup(t);
            yield return 2;
            IntVec3 c = t.center;
            Pawn gojo = Gojo(t, c + new IntVec3(-6, 0, 0));
            Pawn behind = Raider(t, c + new IntVec3(8, 0, 0));
            IntVec3 cell = behind.Position;
            yield return 5;
            gojo.abilities.GetAbility(Red).QueueCastingJob(behind, LocalTargetInfo.Invalid);
            RedShot shot = null;
            foreach (int w in WaitFor(() => (shot = Kit(t).Reds.FirstOrDefault(r => r.caster == gojo))?.Fired == true, 120)) yield return w;
            // After the fire: the verb's line of sight check is behind it, Red is in the air.
            for (int z = -1; z <= 1; z++) Wall(t, c + new IntVec3(4, 0, z));
            t.Log("walls built " + (t.Now - shot?.fireTick) + " ticks after the fire, Red at " + shot?.along.ToString("0.00"));
            foreach (int w in WaitFor(() => shot.Burst, 90)) yield return w;
            t.Log(Shot(shot));
            // Gojo's point is x -5.5 from the centre cell's corner; the wall's face is at x 4.
            t.Check(shot.Burst && shot.wallBurst && Mathf.Abs(shot.burstAlong - 9.4f) < 0.25f, "Red burst at the wall (" + shot.burstAlong.ToString("0.00") + " cells)");
            yield return 30;
            t.Check(Untouched(behind) && behind.Position == cell, "the pawn behind the wall is untouched (" + Where(behind, c) + ")");
        }

        // ---- Hollow Purple ------------------------------------------------------------------------------------------

        // Overrides Purple's run for a test (the arena is 25 cells across); Restore puts the XML back.
        private static float savedTravel = -1f;

        private static void ShortRun(float cells)
        {
            var props = GojoKit.Props<CompProperties_GojoHollowPurple>(Purple);
            if (savedTravel < 0f) savedTravel = props.maxTravel;
            props.maxTravel = cells;
        }

        private static void Restore()
        {
            if (savedTravel >= 0f) GojoKit.Props<CompProperties_GojoHollowPurple>(Purple).maxTravel = savedTravel;
            savedTravel = -1f;
        }

        [RimArtTest("Gojo", "purple 1 Red through Blue makes Hollow Purple: erases, strikes, spares the boss, clears and scars the lane", 2400)]
        private static IEnumerable<int> PurpleErase(RimArtTestContext t)
        {
            Setup(t);
            yield return 2;
            IntVec3 c = t.center;
            Map map = t.map;
            ShortRun(14f);
            // Layout, along +x from Gojo: Blue 5 cells out at x -4; the lane runs x -5..11, rows z -1..1.
            Pawn gojo = Gojo(t, c + new IntVec3(-9, 0, 0));
            Pawn centreRow = Raider(t, c + new IntVec3(2, 0, 0), geared: true);
            IntVec3 erasedAt = centreRow.Position;
            if (centreRow.equipment.Primary == null) t.Equip(centreRow, ThingDef.Named("Gun_Revolver"));
            // Armed: it must not shoot Gojo while the test waits.
            centreRow.stances.stunner.StunFor(3000, null, false);
            Pawn sideRow = Raider(t, c + new IntVec3(3, 0, 1));
            sideRow.apparel.Wear((Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_PowerArmor")));
            Pawn outside = Raider(t, c + new IntVec3(5, 0, 2));
            PawnKindDef bossKind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.isBoss);
            Pawn boss = null;
            if (bossKind != null)
            {
                boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(bossKind, bossKind.RaceProps.IsMechanoid ? Faction.OfMechanoids : null));
                GenSpawn.Spawn(boss, c + new IntVec3(6, 0, 0), map);
                boss.stances.stunner.StunFor(3000, null, false);
            }
            else t.Log("no boss kind is loaded; the boss check is skipped");
            Thing tree = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDef.Named("Plant_TreeOak")), c + new IntVec3(1, 0, -1), map);
            ((Plant)tree).Growth = 1f;
            Thing steel = Item(t, ThingDefOf.Steel, 40, c + new IntVec3(4, 0, -1));
            Thing rock = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDef.Named("Granite")), c + new IntVec3(7, 0, -1), map);
            var walls = new Dictionary<int, Thing>();
            for (int z = -3; z <= 3; z++) walls[z] = Wall(t, c + new IntVec3(9, 0, z));
            IntVec3 floor = c + new IntVec3(0, 0, 1), water = c + new IntVec3(1, 0, 1), rough = c + new IntVec3(3, 0, 2);
            map.terrainGrid.SetTerrain(floor, TerrainDefOf.WoodPlankFloor);
            map.terrainGrid.SetTerrain(water, TerrainDefOf.WaterShallow);
            IntVec3[] roofs = { c + new IntVec3(-2, 0, 0), c + new IntVec3(-1, 0, 1), c + new IntVec3(-1, 0, -1) };
            map.roofGrid.SetRoof(roofs[0], RoofDefOf.RoofConstructed);
            map.roofGrid.SetRoof(roofs[1], RoofDefOf.RoofConstructed);
            map.roofGrid.SetRoof(roofs[2], RoofDefOf.RoofRockThick);
            var gear = centreRow.apparel.WornApparel.Cast<Thing>().Concat(centreRow.equipment.AllEquipmentListForReading).Concat(centreRow.inventory.innerContainer).ToList();
            int kills = gojo.records.GetAsInt(RecordDefOf.Kills);
            t.Log("the centre-row raider carries " + string.Join(", ", gear.Select(g => g.LabelShort)) + "; boss: " + (boss?.LabelShort ?? "none"));
            yield return 5;

            MapComponent_Gravity wells = Wells(t);
            if (!t.Check(wells.Begin(gojo, c + new IntVec3(-4, 0, 0), Blue), "Blue opened")) { Restore(); yield break; }
            GravityCast blue = wells.For(gojo);
            yield return CompProperties_AbilityGravityWell.For(Blue).OpeningTicks + 2;
            t.Check(blue.Field, "Blue is pulling");
            Ability red = gojo.abilities.GetAbility(Red), purple = gojo.abilities.GetAbility(Purple);
            t.Check(purple.GizmoDisabled(out string why) && why == CompAbilityEffect_GojoHollowPurple.HowTo, "Purple's button cannot be clicked (\"" + why + "\")");
            red.QueueCastingJob(new LocalTargetInfo(c), LocalTargetInfo.Invalid);
            int cast = t.Now;
            PurpleRun run = null;
            foreach (int w in WaitFor(() => (run = Kit(t).Purples.FirstOrDefault()) != null, 180)) yield return w;
            RedShot shot = Kit(t).Reds.FirstOrDefault(r => r.caster == gojo);
            t.Log($"Purple {t.Now - cast} ticks after the order; red: " + Shot(shot));
            if (!t.Check(run != null, "Red met Blue and made Hollow Purple")) { Restore(); yield break; }
            t.Check(shot == null || (shot.used && !shot.Burst), "Red was used up with no burst");
            t.Check(!blue.Active && !blue.clock.imploded, "Blue closed at once without imploding");
            t.Check(GameComponent_Gravity.Instance.Remaining(gojo) > 0, "Blue's cooldown started");
            t.Check(purple.CooldownTicksRemaining > 59000, "Purple's cooldown started (" + purple.CooldownTicksRemaining + " ticks)");
            t.Check(red.CooldownTicksRemaining > 0, "Red's cooldown runs");
            t.Check((run.start - Flat(blue.Centre)).magnitude < 0.01f && Vector2.Dot(run.dir, Vector2.right) > 0.999f,
                $"it formed at Blue's centre ({run.start - Flat(c.ToVector3Shifted())} from the centre cell) heading along Red ({run.dir})");
            t.Check(Mathf.Abs(run.travel - 14f) < 0.01f, "it will run 14 cells (the test's cut-down run; XML " + savedTravel + ")");
            float edge = PurpleRun.ToEdge(map, new Vector2(map.Size.x - 5.5f, c.z + 0.5f), Vector2.right);
            t.Check(Mathf.Abs(edge - 5.5f) < 0.01f, "a run from 5.5 cells short of the map edge is cut to 5.5 (" + edge.ToString("0.00") + ")");

            foreach (int w in WaitFor(() => run.Stopped, 400, 5))
            {
                if ((t.Now - run.comboTick) % 30 == 0) t.Log($"{t.Now - run.comboTick} ticks: centre {run.run:0.0} cells, {run.hits.Count} pawns met, {run.touches.Count} things touched");
                yield return w;
            }
            yield return 2;
            t.Log("hits: " + string.Join("; ", run.hits.Select(h => h.pawn.LabelShort + (h.erased ? " erased" : $" struck {h.dealt:0.#} on {h.part?.Label ?? "none"}"))));
            t.Check(run.Stopped, "Purple ran its 14 cells (" + run.run.ToString("0.0") + ")");

            // The centre row: erased, nothing left, Gojo's kill.
            t.Check(centreRow.Dead && !centreRow.Spawned, "the centre-row raider is dead and gone");
            t.Check(!map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Any(x => ((Corpse)x).InnerPawn == centreRow), "no corpse");
            t.Check(gear.All(g => g.Destroyed), "its apparel, weapon and inventory are destroyed (" + gear.Count(g => !g.Destroyed) + " left)");
            int after = gojo.records.GetAsInt(RecordDefOf.Kills);
            t.Check(after > kills, "the kill counts as Gojo's (kills " + kills + " -> " + after + ")");
            CellRect arena = CellRect.CenteredOn(c, RimArtTestContext.ArenaRadius);
            // Items anywhere in the arena, and filth where the erased raider stood. The side-row raider's own corpse (if
            // the 60 killed it) and anything at its cell (vanilla drops apparel a lost part can no longer wear) are not drops.
            List<Thing> loose = map.listerThings.AllThings.Where(x => x.Spawned && arena.Contains(x.Position)
                && x.Position != sideRow.PositionHeld
                && ((x.def.category == ThingCategory.Item && !(x is Corpse body && body.InnerPawn != centreRow))
                    || (x is Filth && x.Position == erasedAt))).ToList();
            t.Check(loose.Count == 0, "nothing is left: no drops, chunks, wood or blood (" + string.Join(", ", loose.Select(x => x.LabelShort + " at " + (x.Position - c))) + ")");

            // The side row: 60 erasure damage through power armour; a destroyed part does not bleed.
            PurpleHit side = run.hits.FirstOrDefault(h => h.pawn == sideRow);
            float partMax = side.part != null ? side.part.def.GetMaxHealth(sideRow) : 0f;
            t.Check(side.pawn == sideRow && !side.erased, "the side-row raider was struck, not erased");
            // All of it or the whole part (vanilla keeps an outside limb at 1 hp on a small overkill roll), none taken by the armour.
            t.Check(side.part != null && side.dealt >= Mathf.Min(60f, partMax) - 1.01f,
                $"it took {side.dealt:0.#} on its {side.part?.Label} ({partMax:0} hp) through power armour");
            bool erasedWound = sideRow.health.hediffSet.hediffs.Any(h => h.def == GojoKitDefOf.AG_Erased
                || (h is Hediff_MissingPart m && m.lastInjury == GojoKitDefOf.AG_Erased));
            t.Check(erasedWound, "its wound is an erasure wound (" + string.Join(", ", sideRow.health.hediffSet.hediffs.Select(h => h.LabelCap + " " + h.Part?.Label)) + ")");
            float bleed = sideRow.Dead ? 0f : sideRow.health.hediffSet.hediffs.Where(h => h.def == GojoKitDefOf.AG_Erased || h is Hediff_MissingPart).Sum(h => h.BleedRate);
            t.Check(bleed == 0f, "no bleeding from it (" + bleed.ToString("0.###") + (sideRow.Dead ? ", dead" : "") + ")");
            t.Check(Untouched(outside), "the raider 2 cells off the path is untouched");
            if (boss != null)
            {
                PurpleHit bossHit = run.hits.FirstOrDefault(h => h.pawn == boss);
                t.Check(bossHit.pawn == boss && !bossHit.erased && !boss.Destroyed, $"the boss on the centre row was struck ({bossHit.dealt:0.#}), not erased");
            }

            // Things: gone with nothing left; the wall cut through its middle three cells.
            t.Check(tree.Destroyed && steel.Destroyed && rock.Destroyed, "the tree, the steel and the granite are gone");
            t.Check(walls[-1].Destroyed && walls[0].Destroyed && walls[1].Destroyed, "the wall's three middle cells are gone");
            t.Check(!walls[-2].Destroyed && !walls[2].Destroyed && !walls[3].Destroyed && !walls[-3].Destroyed, "the rest of the wall stands");
            t.Check(run.cuts.Count >= 2, "two cut faces are drawn (" + run.cuts.Count + ")");

            // The lane: Erased ground, the floor gone with no refund, water kept, roofs off.
            var lane = new[] { c + new IntVec3(-4, 0, 0), c + new IntVec3(-5, 0, 1), floor, c + new IntVec3(5, 0, -1), c + new IntVec3(9, 0, 0), c + new IntVec3(11, 0, 0) };
            t.Check(lane.All(x => map.terrainGrid.TerrainAt(x) == GojoKitDefOf.AG_ErasedGround),
                "lane cells are Erased ground (" + string.Join(", ", lane.Select(x => (x - c) + " " + map.terrainGrid.TerrainAt(x).defName)) + ")");
            t.Check(map.terrainGrid.UnderTerrainAt(floor) == null, "the wooden floor is gone, not kept under it");
            t.Check(map.terrainGrid.TerrainAt(water) == TerrainDefOf.WaterShallow, "the water cell stays water");
            t.Check(map.terrainGrid.TerrainAt(rough) != GojoKitDefOf.AG_ErasedGround && map.terrainGrid.TerrainAt(c + new IntVec3(12, 0, 0)) != GojoKitDefOf.AG_ErasedGround,
                "cells 2 cells off the path and past the end are not");
            t.Check(roofs.All(x => !x.Roofed(map)), "the roofs over the lane are gone, thick rock roof too");
            t.Check(Untouched(gojo) && gojo.Spawned, "Gojo is unhurt");
            Restore();
            yield return t.ShotAs("purple-lane", c + new IntVec3(2, 0, 0), 12f);
        }

        [RimArtTest("Gojo", "purple 2 with Purple on cooldown Red passes through Blue as a plain Red and Blue stays open", 1200)]
        private static IEnumerable<int> PurpleCooldown(RimArtTestContext t)
        {
            Setup(t);
            yield return 2;
            IntVec3 c = t.center;
            Pawn gojo = Gojo(t, c + new IntVec3(-9, 0, 0));
            Pawn first = Raider(t, c + new IntVec3(2, 0, 0));
            Ability purple = gojo.abilities.GetAbility(Purple);
            purple.StartCooldown(60000);
            yield return 5;
            MapComponent_Gravity wells = Wells(t);
            if (!t.Check(wells.Begin(gojo, c + new IntVec3(-4, 0, 0), Blue), "Blue opened")) yield break;
            GravityCast blue = wells.For(gojo);
            yield return CompProperties_AbilityGravityWell.For(Blue).OpeningTicks + 2;
            gojo.abilities.GetAbility(Red).QueueCastingJob(first, LocalTargetInfo.Invalid);
            RedShot shot = null;
            foreach (int w in WaitFor(() => (shot = Kit(t).Reds.FirstOrDefault(r => r.caster == gojo))?.Burst == true, 180)) yield return w;
            t.Log(Shot(shot) + "; Blue: " + State(blue, c));
            t.Check(Kit(t).Purples.Count == 0, "no Hollow Purple");
            t.Check(shot != null && !shot.used && shot.hit?.pawn == first, "Red went on and threw the raider behind Blue");
            t.Check(blue.Field && !blue.clock.imploded, "Blue is still open after Red passed");
            t.Check(purple.CooldownTicksRemaining > 59000, "Purple's cooldown is untouched (" + purple.CooldownTicksRemaining + ")");
            foreach (int w in WaitFor(() => shot.hit.landed, 120)) yield return w;
            yield return 2;
            t.Check(first.Position == c + new IntVec3(8, 0, 0), "the raider landed 6 cells on (" + Where(first, c) + ")");
            foreach (int w in WaitFor(() => !blue.Active, 300)) yield return w;
            t.Check(blue.clock.imploded, "Blue then ran out and imploded as usual");
        }

        [RimArtTest("Gojo", "purple 3 with Purple ready Red flies through a raider caught in Blue's pull and makes Purple", 1200)]
        private static IEnumerable<int> PurpleThroughPull(RimArtTestContext t)
        {
            Setup(t);
            yield return 2;
            IntVec3 c = t.center;
            Pawn gojo = Gojo(t, c + new IntVec3(-9, 0, 0));
            IntVec3 centre = c + new IntVec3(-4, 0, 0);
            Pawn caught = Raider(t, c + new IntVec3(-6, 0, 0));
            yield return 5;
            MapComponent_Gravity wells = Wells(t);
            if (!t.Check(wells.Begin(gojo, centre, Blue), "Blue opened")) yield break;
            GravityCast blue = wells.For(gojo);
            yield return CompProperties_AbilityGravityWell.For(Blue).OpeningTicks + 40;
            t.Log("before Red: caught raider " + Where(caught, c) + ", Blue: " + State(blue, c));
            gojo.abilities.GetAbility(Red).QueueCastingJob(new LocalTargetInfo(centre), LocalTargetInfo.Invalid);
            RedShot shot = null;
            foreach (int w in WaitFor(() => (shot = Kit(t).Reds.FirstOrDefault(r => r.caster == gojo)) != null && (shot.Burst || shot.used), 180)) yield return w;
            t.Log(Shot(shot) + "; caught raider " + Where(caught, c));
            t.Check(shot != null && shot.used && !shot.Burst, "Red was not stopped by the caught raider and met Blue");
            t.Check(Kit(t).Purples.Count == 1, "Hollow Purple formed");
        }
    }
}
