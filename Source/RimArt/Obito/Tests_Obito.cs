using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Game tests for the Obito Echo (run with -quicktest -rimarttest=obito): Kamui: Phase (hits into the dimension,
    /// no attacking, carrying or casting, the pool), Kamui: Warp in and out, Kamui: Store (absorb, release, the
    /// counter), Wood Release on a line, and what happens to the dimension's contents on death, loss of the gene
    /// and a revert inside. The screenshots are the pictures: the bent body, the swirls, the branches.
    /// </summary>
    public static class Tests_Obito
    {
        private static EchoDef Obito => ObitoDefOf.AG_Echo_Obito;

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            startHealth.Clear();
            return echoes;
        }

        /// <summary>A colonist made Obito's Host and manifested, with a full pool, undrafted and standing still.</summary>
        private static Pawn Host(RimArtTestContext t, GameComponent_Echoes echoes, IntVec3 at, out EchoRecord record, out Gene_Involute gene)
        {
            Pawn host = t.Colonist(at);
            record = EchoUtility.ForceHost(Obito, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            host.drafter.Drafted = false;
            RimArtTestContext.Hold(host);
            Trait wimp = host.story?.traits?.GetTrait(TraitDefOf.Wimp);
            if (wimp != null) host.story.traits.RemoveTrait(wimp);
            gene = InvoluteUtility.GeneOf(host);
            return Noted(host);
        }

        /// <summary>The revert, then the gene taken away so the test's dimension is closed.</summary>
        private static void Finish(Pawn host, EchoRecord record)
        {
            if (record != null) EchoUtility.Revert(record, collapse: false);
            Gene_Involute gene = host?.genes?.GetFirstGeneOfType<Gene_Involute>();
            if (gene != null) host.genes.RemoveGene(gene);
            EchoDevice.workingForTests = null;
        }

        private static readonly Dictionary<Pawn, float> startHealth = new Dictionary<Pawn, float>();

        private static Pawn Noted(Pawn pawn)
        {
            startHealth[pawn] = pawn.health.summaryHealth.SummaryHealthPercent;
            foreach (Apparel apparel in pawn.apparel?.WornApparel.ToList() ?? new List<Apparel>()) pawn.apparel.Remove(apparel);
            return pawn;
        }

        /// <summary>A hostile that stands still for the test: unarmed and stunned, so it starts no fist fight.</summary>
        private static Pawn Target(RimArtTestContext t, IntVec3 at, int stunTicks = 600)
        {
            Pawn pawn = t.Enemy(at, armed: false);
            pawn.stances.stunner.StunFor(stunTicks, null, false);
            return Noted(pawn);
        }

        private static Pawn Ally(RimArtTestContext t, IntVec3 at)
        {
            Pawn pawn = t.Colonist(at);
            pawn.drafter.Drafted = true;
            pawn.drafter.FireAtWill = false;
            return Noted(pawn);
        }

        private static int Injuries(Pawn pawn) => pawn.Dead ? 999 : pawn.health.hediffSet.hediffs.Count(h => h is Hediff_Injury || h is Hediff_MissingPart);
        private static bool Stunned(Pawn pawn) => pawn.stances?.stunner?.Stunned == true;

        private static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 1)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        private static void Face(Pawn pawn, Rot4 rot)
        {
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, pawn.Position + rot.FacingCell * 3);
            wait.expiryInterval = 600;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            pawn.Rotation = rot;
        }

        // ---- Kamui: Phase ----------------------------------------------------------------------------------------

        [RimArtTest("Obito", "phase 1 phased, rounds from a raider go into the dimension and do not hurt him; he cannot shoot, cast or carry; solid 0.25 s after the toggle (screenshots)")]
        private static IEnumerable<int> Phase(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record, out Gene_Involute gene);
            t.Check(gene != null, "Obito has the Kamui gene");
            if (gene == null) yield break;
            t.Check(gene.Volume != null, "the dimension was built on awakening");
            foreach (AbilityDef def in new[] { ObitoDefOf.AG_KamuiPhase, ObitoDefOf.AG_KamuiWarp, ObitoDefOf.AG_KamuiStore, ObitoDefOf.AG_WoodRelease })
                t.Check(host.abilities.GetAbility(def) != null, "Obito has " + def.label);
            t.Equip(host, DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"));
            Face(host, Rot4.South);
            yield return 5;

            int relaunched = InvoluteUtility.Relaunched, injuries = Injuries(host);
            gene.TogglePhase();
            t.Check(gene.Phase == KamuiPhaseState.Phased, "the toggle phases him (" + gene.Phase + ")");
            yield return 6;
            yield return t.ShotAs("phase-on", host.Position, 3f);
            yield return 20;
            yield return t.ShotAs("phase-held", host.Position, 3f);

            Pawn shooter = t.Enemy(t.center + new IntVec3(7, 0, 0), armed: true);
            Noted(shooter);
            t.Equip(shooter, DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"));
            shooter.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackStatic, host), JobCondition.InterruptForced);
            foreach (int step in WaitFor(() => ObitoFX.Hits.TryGetValue(host, out var h) && h.Count > 0, 400)) yield return step;
            t.Check(gene.PassedThroughRecently(shooter), "the shooter is marked as an attacker that went through him");
            yield return 4;
            yield return t.ShotAs("phase-hit", host.Position, 3f);
            foreach (int step in WaitFor(() => InvoluteUtility.Relaunched > relaunched + 2, 400)) yield return step;
            t.Check(InvoluteUtility.Relaunched > relaunched, "rounds were relaunched inside the dimension (" + (InvoluteUtility.Relaunched - relaunched) + ")");
            t.Check(Injuries(host) == injuries, "no hit landed on him (" + Injuries(host) + " injuries, was " + injuries + ")");
            t.Log("shooter: " + RimArtTestContext.Describe(shooter));
            shooter.Destroy();

            Verb gun = host.equipment.PrimaryEq?.PrimaryVerb;
            Pawn dummy = Target(t, t.center + new IntVec3(3, 0, 2));
            t.Check(gun != null && !gun.TryStartCastOn(dummy), "his rifle will not fire while phased");
            Ability wood = host.abilities.GetAbility(ObitoDefOf.AG_WoodRelease);
            bool disabled = wood.GizmoDisabled(out string reason);
            t.Check(disabled && reason != null, "Wood Release is greyed out while phased (" + reason + ")");
            Thing steel = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Steel), t.center + new IntVec3(0, 0, 1), t.map);
            t.Check(host.carryTracker.TryStartCarry(steel, 10, false) == 0, "he cannot pick up the steel");

            float pool = gene.PoolTicks;
            gene.TogglePhase();
            t.Check(gene.Phase == KamuiPhaseState.TurningSolid, "the toggle starts the turn to solid (" + gene.Phase + ")");
            yield return 5;
            yield return t.ShotAs("phase-off", host.Position, 3f);
            yield return 12;
            t.Check(gene.Phase == KamuiPhaseState.Solid, "solid 0.25 s later (" + gene.Phase + ")");
            t.Check(pool < gene.PoolMax - 60f, "the pool was spent while phased (" + (pool / 60f).ToString("0.0") + " of " + (gene.PoolMax / 60f).ToString("0") + " s)");
            Finish(host, record);
        }

        [RimArtTest("Obito", "phase 2 the pool runs out and he turns solid; an almost empty pool will not phase; it refills 1 s per 4 s solid")]
        private static IEnumerable<int> PhasePool(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record, out Gene_Involute gene);
            if (gene == null) yield break;
            gene.SetPoolSeconds(0.5f);
            gene.TogglePhase();
            t.Check(gene.Phase == KamuiPhaseState.Solid, "0.5 s in the pool is not enough to phase");
            gene.SetPoolSeconds(1.2f);
            gene.TogglePhase();
            t.Check(gene.Phase == KamuiPhaseState.Phased, "1.2 s is enough");
            foreach (int step in WaitFor(() => gene.Phase == KamuiPhaseState.Solid, 200)) yield return step;
            t.Check(gene.Phase == KamuiPhaseState.Solid, "the spent pool turned him solid");
            float before = gene.PoolTicks;
            yield return 240;
            float gained = (gene.PoolTicks - before) / 60f;
            t.Check(gained > 0.9f && gained < 1.1f, "4 s solid refilled " + gained.ToString("0.00") + " s");
            Finish(host, record);
        }

        // ---- Kamui: Warp -----------------------------------------------------------------------------------------

        [RimArtTest("Obito", "warp 1 in after the 1 s warm-up (2 charge), out at a picked cell after the 0.5 s mark; the cooldown starts at the exit (screenshots)")]
        private static IEnumerable<int> Warp(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record, out Gene_Involute gene);
            if (gene == null) yield break;
            Face(host, Rot4.South);
            yield return 3;
            IntVec3 from = host.Position;
            Ability warp = host.abilities.GetAbility(ObitoDefOf.AG_KamuiWarp);
            float charge = echoes.charge;
            warp.QueueCastingJob(host, LocalTargetInfo.Invalid);
            yield return 25;
            yield return t.ShotAs("warp-in-early", from, 3f);
            yield return 20;
            yield return t.ShotAs("warp-in-late", from, 3f);
            foreach (int step in WaitFor(() => gene.Inside, 120)) yield return step;
            t.Check(gene.Inside, "he is inside the dimension (" + RimArtTestContext.Describe(host) + ")");
            t.Check(echoes.charge <= charge - 1.9f, "going in cost 2 charge (" + charge.ToString("0.0") + " -> " + echoes.charge.ToString("0.0") + ")");
            t.Check(warp.CooldownTicksRemaining == 0, "no cooldown runs while he is inside (" + warp.CooldownTicksRemaining + ")");
            t.Check(gene.FromMap == t.map && gene.FromCell == from, "the map and cell he left are kept");
            t.Check(warp.GetGizmos().Any(c => c.defaultLabel == "AG_KamuiWarpOutLabel".Translate()), "inside, the Warp button is the way out");
            // Inside: the camera goes to the dimension for two shots (him at the mouth, the whole field), then back.
            Current.Game.CurrentMap = gene.Volume;
            yield return 2;
            yield return t.ShotAs("warp-inside", host.Position, 11f);
            yield return t.ShotAs("warp-inside-field", gene.Volume.Center, 26f);
            Current.Game.CurrentMap = t.map;

            IntVec3 exit = t.center + new IntVec3(5, 0, 3);
            gene.BeginExit(t.map, exit);
            yield return 15;
            yield return t.ShotAs("warp-mark", exit, 3f);
            foreach (int step in WaitFor(() => !gene.Inside, 60)) yield return step;
            t.Check(host.Map == t.map && host.Position == exit, "he came out at the picked cell (" + RimArtTestContext.Describe(host) + ")");
            yield return 8;
            yield return t.ShotAs("warp-out", exit, 3f);
            t.Check(warp.CooldownTicksRemaining > 800, "the 15 s cooldown started at the exit (" + warp.CooldownTicksRemaining + ")");
            Finish(host, record);
        }

        [RimArtTest("Obito", "warp 2 a revert while he is inside puts him back where he went in")]
        private static IEnumerable<int> WarpRevert(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record, out Gene_Involute gene);
            if (gene == null) yield break;
            IntVec3 from = host.Position;
            gene.Enter();
            t.Check(gene.Inside, "in");
            EchoUtility.Revert(record, collapse: false);
            foreach (int step in WaitFor(() => !gene.Inside, 130)) yield return step;
            t.Check(host.Map == t.map && host.Position.DistanceTo(from) < 2f, "out on the map he left, where he went in (" + RimArtTestContext.Describe(host) + ")");
            Finish(host, null);
        }

        // ---- Kamui: Store ----------------------------------------------------------------------------------------

        [RimArtTest("Obito", "store 1 absorb a raider by touch (1 charge) onto an island, held stunned; absorb a steel stack; release the raider within 6 cells, stunned 2 s (screenshots)")]
        private static IEnumerable<int> Store(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record, out Gene_Involute gene);
            if (gene == null) yield break;
            Pawn raider = Target(t, t.center + new IntVec3(1, 0, 0));
            Ability store = host.abilities.GetAbility(ObitoDefOf.AG_KamuiStore);
            float charge = echoes.charge;
            store.QueueCastingJob(raider, LocalTargetInfo.Invalid);
            yield return 18;
            yield return t.ShotAs("store-reach", host.Position, 3f);
            foreach (int step in WaitFor(() => raider.MapHeld == gene.Volume, 120)) yield return step;
            t.Check(raider.MapHeld == gene.Volume, "the raider is in the dimension (" + RimArtTestContext.Describe(raider) + ")");
            yield return 10;
            yield return t.ShotAs("store-wind", host.Position, 3f);
            MapComponent_KamuiDimension kamui = gene.Volume?.GetComponent<MapComponent_KamuiDimension>();
            IntVec3 mouth = InvoluteUtility.MouthCell(gene.Volume);
            KamuiLayout layout = kamui?.Layout;
            t.Check(layout != null && layout.TopAt(raider.Position.x, raider.Position.z)?.Group != layout.TopAt(mouth.x, mouth.z)?.Group,
                "it landed on an island, away from the main top");
            t.Check(echoes.charge <= charge - 0.9f, "absorb cost 1 charge");
            t.Check(gene.Stored.Contains(raider), "it is in the stored list");
            yield return 200;
            t.Check(Stunned(raider), "held inside, it is still stunned 3 s later (" + raider.stances.stunner.StunTicksLeft + ")");
            // The raider held on its island, seen inside the dimension.
            Current.Game.CurrentMap = gene.Volume;
            yield return 2;
            yield return t.ShotAs("store-held-inside", raider.Position, 11f);
            Current.Game.CurrentMap = t.map;

            Thing steel = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Steel), t.center + new IntVec3(-2, 0, 0), t.map);
            steel.stackCount = 30;
            store.ResetCooldown();
            store.QueueCastingJob(steel, LocalTargetInfo.Invalid);
            foreach (int step in WaitFor(() => ObitoFX.WarmingUp(host, ObitoDefOf.AG_KamuiStore, out _, out _), 300)) yield return step;
            yield return 14;
            yield return t.ShotAs("store-reach-item", host.Position, 3f);
            foreach (int step in WaitFor(() => steel.MapHeld == gene.Volume, 300)) yield return step;
            t.Check(steel.MapHeld == gene.Volume && steel.stackCount == 30, "the steel stack is in the dimension (" + steel.MapHeld + ")");

            IntVec3 cell = host.Position + new IntVec3(0, 0, 4);
            Command release = store.GetGizmos().FirstOrDefault(c => c.icon == ObitoGraphics.ReleaseIcon);
            t.Check(release != null && !release.Disabled, "the release button is there");
            t.Check(!Ability_KamuiStore.ValidRelease(host, host.Position + new IntVec3(0, 0, 8), ObitoRules.Store.releaseRange), "8 cells is too far to release");
            gene.BeginRelease(raider, t.map, cell);
            yield return 25;
            yield return t.ShotAs("release-unwind", cell, 3f);
            foreach (int step in WaitFor(() => raider.MapHeld == t.map, 60)) yield return step;
            t.Check(raider.MapHeld == t.map && raider.Position.DistanceTo(cell) < 1.5f, "the raider came out at the cell (" + RimArtTestContext.Describe(raider) + ")");
            t.Check(Stunned(raider) && raider.stances.stunner.StunTicksLeft > 60, "it came out stunned (" + raider.stances.stunner.StunTicksLeft + ")");
            t.Check(!gene.Stored.Contains(raider) && gene.Stored.Contains(steel), "the raider left the list, the steel stays");
            t.Check(!gene.ReleaseReady, "release is on its 5 s cooldown");
            Finish(host, record);
        }

        [RimArtTest("Obito", "store 2 counter: a raider's knife goes through the phased Obito; solid, he absorbs it with no warm-up")]
        private static IEnumerable<int> Counter(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record, out Gene_Involute gene);
            if (gene == null) yield break;
            Pawn raider = t.Enemy(t.center + new IntVec3(1, 0, 0), armed: false);
            Noted(raider);
            t.Equip(raider, DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife"));
            // Drafted with fire at will off: he does not punch back, so no melee cooldown holds up the absorb.
            host.drafter.Drafted = true;
            host.drafter.FireAtWill = false;
            gene.TogglePhase();
            t.Log("Melee Animation lists that exclude a phased Obito: " + KamuiMeleeAnimation.Registered);
            raider.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, host), JobCondition.InterruptForced);
            foreach (int step in WaitFor(() => gene.PassedThroughRecently(raider), 400)) yield return step;
            t.Check(gene.PassedThroughRecently(raider), "the knife went through him");
            t.Check(host.CurJobDef?.defName != "AM_InAnimation", "he is not held in a Melee Animation animation (" + RimArtTestContext.Describe(host) + ")");
            yield return 2;
            yield return t.ShotAs("counter-cut", host.Position, 3f);
            yield return t.ShotAs("counter-mark", raider.Position, 3f);
            gene.TogglePhase();
            // Stunned for the rest of the test: an active enemy beside him would draw his own auto-punch (vanilla
            // melee auto-attack ignores fire at will), and the order would wait out his melee cooldown.
            raider.stances.stunner.StunFor(600, null, false);
            yield return 16;
            t.Check(gene.Phase == KamuiPhaseState.Solid, "solid");
            foreach (int step in WaitFor(() => !host.stances.FullBodyBusy, 120)) yield return step;
            t.Check(gene.PassedThroughRecently(raider), "still inside the counter window");
            Ability store = host.abilities.GetAbility(ObitoDefOf.AG_KamuiStore);
            int start = t.Now;
            store.QueueCastingJob(raider, LocalTargetInfo.Invalid);
            foreach (int step in WaitFor(() => raider.MapHeld == gene.Volume, 60)) yield return step;
            t.Check(raider.MapHeld == gene.Volume, "absorbed (" + RimArtTestContext.Describe(host) + ")");
            t.Check(t.Now - start < 16, "with no warm-up (" + (t.Now - start) + " ticks; a normal absorb waits 24)");
            Finish(host, record);
        }

        // ---- Wood Release ----------------------------------------------------------------------------------------

        [RimArtTest("Obito", "wood 1 the branches hit every pawn on the 10-cell line (an ally too), not one beside it, and stop at a wall (screenshots)")]
        private static IEnumerable<int> Wood(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record, out Gene_Involute gene);
            if (gene == null) yield break;
            Pawn near = Target(t, t.center + new IntVec3(2, 0, 0));
            Pawn ally = Ally(t, t.center + new IntVec3(4, 0, 0));
            Pawn far = Target(t, t.center + new IntVec3(6, 0, 0));
            Pawn beside = Target(t, t.center + new IntVec3(3, 0, 2));
            Thing wall = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), t.center + new IntVec3(8, 0, 0), t.map);
            Pawn behind = Target(t, t.center + new IntVec3(9, 0, 0));
            var before = new[] { near, ally, far, beside, behind }.ToDictionary(p => p, Injuries);
            Face(host, Rot4.East);
            yield return 3;
            Ability wood = host.abilities.GetAbility(ObitoDefOf.AG_WoodRelease);
            int charges = wood.RemainingCharges;
            wood.QueueCastingJob(new LocalTargetInfo(t.center + new IntVec3(9, 0, 0)), LocalTargetInfo.Invalid);
            yield return 20;
            yield return t.ShotAs("wood-warmup", host.Position + new IntVec3(1, 0, 0), 4f);
            foreach (int step in WaitFor(() => t.map.GetComponent<MapComponent_ObitoFX>().WoodCount > 0, 60)) yield return step;
            yield return 5;
            yield return t.ShotAs("wood-out", t.center + new IntVec3(4, 0, 0), 6f);
            yield return 20;
            yield return t.ShotAs("wood-crumble", t.center + new IntVec3(4, 0, 0), 6f);
            yield return 40;
            yield return t.ShotAs("wood-splinters", t.center + new IntVec3(4, 0, 0), 6f);
            t.Check(Injuries(near) > before[near], "the raider 2 cells out was skewered");
            t.Check(Injuries(ally) > before[ally], "the ally on the line was skewered too");
            t.Check(Injuries(far) > before[far], "the raider 6 cells out was skewered");
            t.Check(Injuries(beside) == before[beside], "the raider 2 cells beside the line was not");
            t.Check(Injuries(behind) == before[behind], "the raider behind the wall was not");
            t.Check(wood.RemainingCharges == charges - 1, "one of the two charges was spent (" + wood.RemainingCharges + ")");
            wall.Destroy();
            Finish(host, record);
        }

        // ---- the dimension's contents ----------------------------------------------------------------------------

        [RimArtTest("Obito", "contents 1 on his death what he stored comes out where he lies; the loss of the gene empties and closes the dimension")]
        private static IEnumerable<int> Contents(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record, out Gene_Involute gene);
            if (gene == null) yield break;
            Pawn raider = Target(t, t.center + new IntVec3(1, 0, 0));
            gene.Absorb(raider);
            t.Check(raider.MapHeld == gene.Volume, "stored");
            EchoUtility.Revert(record, collapse: false);
            host.Kill(null);
            foreach (int step in WaitFor(() => raider.MapHeld == t.map, 120)) yield return step;
            t.Check(raider.MapHeld == t.map && raider.Position.DistanceTo(t.center) < 4f, "after his death the raider is back beside his body (" + RimArtTestContext.Describe(raider) + ")");
            raider.Destroy();

            Pawn second = t.Colonist(t.center + new IntVec3(-3, 0, 0));
            second.genes.AddGene(ObitoDefOf.AG_InvoluteOrgan, xenogene: true);
            Gene_Involute other = InvoluteUtility.GeneOf(second);
            Thing steel = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Steel), second.Position + new IntVec3(0, 0, 1), t.map);
            Map volume = other?.EnsureVolume();
            other?.Absorb(steel);
            t.Check(steel.MapHeld == volume, "a second Kamui holds steel");
            second.genes.RemoveGene(other);
            yield return 2;
            t.Check(steel.MapHeld == t.map, "the gene's loss put the steel out (" + steel.MapHeld + ")");
            t.Check(volume != null && !Find.Maps.Contains(volume), "and closed the dimension");
            Finish(host, null);
        }
    }
}
