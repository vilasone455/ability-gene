using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>Game tests for Todo's kit: stone, clap, Black Flash and his Echo (run with -quicktest -rimarttest=todo).</summary>
    public static class Tests_Anchor
    {
        private static AbilityDef Mark => DefDatabase<AbilityDef>.GetNamed("AG_AnchorMark");

        /// <summary>Todo: an undrafted colonist at the centre, awakened and manifested with a full pool, so the Echo grants the kit.</summary>
        private static Pawn Carrier(RimArtTestContext t)
        {
            Pawn carrier = CastHoldTest.Caster(t);
            Host(carrier, manifest: true);
            return carrier;
        }

        private static EchoRecord Host(Pawn pawn, bool manifest)
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            EchoRecord record = EchoUtility.ForceHost(DebugActions_Todo.Echo, pawn);
            echoes.charge = 100f;
            if (manifest) EchoUtility.Manifest(record);
            return record;
        }

        private static IEnumerable<int> MarkHold(RimArtTestContext t, Pawn carrier, IntVec3 cell, float length)
        {
            var flicks = t.map.GetComponent<MapComponent_MarkFlicks>();
            foreach (int step in CastHoldTest.Run(t, carrier, Mark, cell, () => flicks.Fired(carrier), CastHoldTest.For(length))) yield return step;
        }

        [RimArtTest("Todo", "hold 1 mark placing holds an undrafted carrier until the flick clip is done")]
        private static IEnumerable<int> Place(RimArtTestContext t)
        {
            t.Clear();
            Pawn carrier = Carrier(t);
            foreach (int step in MarkHold(t, carrier, t.center + new IntVec3(5, 0, 0), MarkFlick.FlickLength)) yield return step;
        }

        [RimArtTest("Todo", "hold 2 mark lifting holds an undrafted carrier until the catch clip is done")]
        private static IEnumerable<int> Lift(RimArtTestContext t)
        {
            t.Clear();
            Pawn carrier = Carrier(t);
            IntVec3 cell = t.center + new IntVec3(5, 0, 0);
            foreach (int step in MarkHold(t, carrier, cell, MarkFlick.FlickLength)) yield return step;
            if (!t.Check(AnchorUtility.GeneOf(carrier)?.IsMarked(cell) ?? false, "the cell is marked")) yield break;
            carrier.abilities.GetAbility(Mark).ResetCooldown();
            RimArtTestContext.Hold(carrier);
            t.Log("lifting");
            foreach (int step in MarkHold(t, carrier, cell, MarkFlick.CatchLength)) yield return step;
            t.Check(!(AnchorUtility.GeneOf(carrier)?.IsMarked(cell) ?? true), "the mark was lifted");
        }

        /// <summary>
        /// The clap has no cooldown; the last charge is what makes the ability uncastable when it fires.
        /// It swaps with a stone; test 4 is the direct swap with a pawn.
        /// </summary>
        [RimArtTest("Todo", "hold 3 clap on the last charge holds an undrafted carrier until the clip is done")]
        private static IEnumerable<int> Clap(RimArtTestContext t)
        {
            t.Clear();
            Pawn carrier = Carrier(t);
            IntVec3 cell = t.center + new IntVec3(5, 0, 0);
            foreach (int step in MarkHold(t, carrier, cell, MarkFlick.FlickLength)) yield return step;
            if (!t.Check(AnchorUtility.GeneOf(carrier)?.IsMarked(cell) ?? false, "the cell is marked")) yield break;
            RimArtTestContext.Hold(carrier);
            Gene_Anchors gene = AnchorUtility.GeneOf(carrier);
            gene.Spend(gene.Charges - 1);
            t.Log("claps left: " + gene.Charges);
            AbilityDef clap = DefDatabase<AbilityDef>.GetNamed("AG_AnchorClap");
            float warmup = clap.verbProperties.warmupTime;
            var claps = t.map.GetComponent<MapComponent_ClapTeleports>();
            foreach (int step in CastHoldTest.Run(t, carrier, clap, cell, () => claps.Fired(carrier),
                CastHoldTest.For(ClapTeleport.ClipLength - ClapTeleport.ClipOffset(warmup, false)))) yield return step;
            t.Check(gene.Charges == 0, "the last clap was spent");
        }

        /// <summary>
        /// A clap straight at an enemy in sight, no stone. The pawn end is built for the cast and drawn
        /// as a Heart; before that fix its suit was -1 and ClapTeleportGraphics.Card threw every frame.
        /// The runner fails the test on that logged error. Frames are drawn while the test runs, so
        /// the picture is exercised through the warmup and the swap.
        /// </summary>
        [RimArtTest("Todo", "clap 4 direct swap with an enemy 6 cells away draws and swaps")]
        private static IEnumerable<int> DirectSwap(RimArtTestContext t)
        {
            t.Clear();
            Pawn carrier = Carrier(t);
            IntVec3 from = carrier.Position, at = t.center + new IntVec3(6, 0, 0);
            Pawn enemy = t.Enemy(at, armed: false);
            Gene_Anchors gene = AnchorUtility.GeneOf(carrier);
            int charges = gene.Charges;
            Anchor end = ClapTargets.EndFor(carrier, gene, enemy, out string reason);
            if (!t.Check(end != null && end.IsOnPawn, "the enemy is a pawn end (" + reason + ")")) yield break;
            t.Check(end.suit == ClapTeleport.Heart, "the pawn end is a Heart (suit " + end.suit + ")");
            AbilityDef clap = DefDatabase<AbilityDef>.GetNamed("AG_AnchorClap");
            float warmup = clap.verbProperties.warmupTime;
            var claps = t.map.GetComponent<MapComponent_ClapTeleports>();
            // Where both stood while the job still ran after the fire; the carrier is free to walk off after.
            IntVec3 carrierAt = IntVec3.Invalid, enemyAt = IntVec3.Invalid;
            bool Fired()
            {
                bool fired = claps.Fired(carrier);
                if (fired && carrier.CurJobDef == clap.jobDef) { carrierAt = carrier.Position; enemyAt = enemy.Position; }
                return fired;
            }
            foreach (int step in CastHoldTest.Run(t, carrier, clap, enemy, Fired,
                CastHoldTest.For(ClapTeleport.ClipLength - ClapTeleport.ClipOffset(warmup, false)))) yield return step;
            t.Check(carrierAt == at, "the carrier stood where the enemy stood (" + carrierAt + ")");
            t.Check(enemyAt == from, "the enemy stood where the carrier stood (" + enemyAt + ")");
            t.Check(gene.Charges == charges - 1, "one clap was spent (" + charges + " -> " + gene.Charges + ")");
        }

        private static AbilityDef BlackFlash => DefDatabase<AbilityDef>.GetNamed("AG_AnchorBlackFlash");

        private static HediffDef Zone => DefDatabase<HediffDef>.GetNamed("AG_BlackFlashZone");

        /// <summary>Injury severity on a pawn; the punch's damage shows up here with the enemy's apparel stripped.</summary>
        private static float Hurt(Pawn pawn)
        {
            float sum = 0f;
            foreach (Hediff h in pawn.health.hediffSet.hediffs) if (h is Hediff_Injury) sum += h.Severity;
            return sum;
        }

        private static Pawn Target(RimArtTestContext t, IntVec3 at)
        {
            Pawn enemy = t.Enemy(at, armed: false);
            enemy.apparel?.DestroyAll();
            return enemy;
        }

        /// <summary>Orders Black Flash and waits for the hit, tracing both pawns. The result is in <paramref name="hit"/>.</summary>
        private static IEnumerable<int> Punch(RimArtTestContext t, Pawn carrier, Pawn enemy, PunchResult hit)
        {
            Ability ability = carrier.abilities.GetAbility(BlackFlash);
            if (!t.Check(ability != null && ability.CanCast, "Black Flash can be cast (" + ability?.CanCast.Reason + ")")) yield break;
            float before = Hurt(enemy);
            ability.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            int cast = t.Now;
            // Ordered during the clap's clip, the punch waits in the queue until the clip's job ends.
            bool queued = false;
            foreach (QueuedJob q in carrier.jobs.jobQueue) if (q.job.def == BlackFlash.jobDef) queued = true;
            t.Check(carrier.CurJobDef == BlackFlash.jobDef || queued,
                "the punch job started or is queued (job " + carrier.CurJobDef?.defName + (queued ? ", punch queued" : "") + ")");
            for (int i = 0; i < 180 && ability.lastCastTick < cast; i++)
            {
                if (i % 6 == 0) t.Log((t.Now - cast) + " | " + RimArtTestContext.Describe(carrier) + " | " + RimArtTestContext.Describe(enemy));
                yield return 1;
            }
            hit.fired = ability.lastCastTick >= cast;
            hit.ticks = t.Now - cast;
            hit.damage = Hurt(enemy) - before;
            hit.stunTicks = enemy.stances?.stunner != null && enemy.stances.stunner.Stunned ? enemy.stances.stunner.StunTicksLeft : 0;
            hit.zone = carrier.health.hediffSet.HasHediff(Zone);
            t.Log("hit after " + hit.ticks + " ticks: damage " + hit.damage.ToString("F1") + ", target stun " + hit.stunTicks + ", zone " + hit.zone
                + " | " + RimArtTestContext.Describe(enemy));
            t.Check(hit.fired, "the punch landed");
        }

        private sealed class PunchResult
        {
            public bool fired, zone;
            public int ticks, stunTicks;
            public float damage;
        }

        /// <summary>No clap before it: an ordinary punch, no stun past the warmup's, no zone. Then a swap left 3.2 s has no window.</summary>
        [RimArtTest("Todo", "black flash 5 without a clap is a plain punch")]
        private static IEnumerable<int> PlainPunch(RimArtTestContext t)
        {
            t.Clear();
            Pawn carrier = Carrier(t);
            Pawn enemy = Target(t, t.center + new IntVec3(1, 0, 0));
            var hit = new PunchResult();
            foreach (int step in Punch(t, carrier, enemy, hit)) yield return step;
            t.Check(hit.damage > 0f, "the punch hurt (" + hit.damage.ToString("F1") + ")");
            t.Check(hit.stunTicks <= 12, "no Black Flash stun (" + hit.stunTicks + " ticks left)");
            t.Check(!hit.zone, "the carrier is not in the zone");

            Gene_Anchors gene = AnchorUtility.GeneOf(carrier);
            var flash = carrier.abilities.GetAbility(BlackFlash).CompOfType<CompAbilityEffect_BlackFlash>();
            gene.NoteSwap();
            t.Check(flash.InWindow(gene), "a swap opens the window");
            yield return 192;
            t.Check(!flash.InWindow(gene), "the window is shut 3.2 s after the swap (" + gene.TicksSinceSwap + " ticks)");
        }

        /// <summary>
        /// A real direct clap with an enemy 3 cells away, then Black Flash on it: the carrier walks 2
        /// cells and the hit must land inside the 3 s window, stun, put the carrier in the zone and spend
        /// the window.
        /// </summary>
        [RimArtTest("Todo", "black flash 6 after a clap flashes, stuns and spends the window")]
        private static IEnumerable<int> FlashAfterClap(RimArtTestContext t)
        {
            t.Clear();
            Pawn carrier = Carrier(t);
            Pawn enemy = Target(t, t.center + new IntVec3(3, 0, 0));
            Gene_Anchors gene = AnchorUtility.GeneOf(carrier);
            AbilityDef clap = DefDatabase<AbilityDef>.GetNamed("AG_AnchorClap");
            Ability clapAbility = carrier.abilities.GetAbility(clap);
            if (!t.Check(clapAbility.CanCast && clapAbility.CanApplyOn(new LocalTargetInfo(enemy)), "the clap can reach the enemy")) yield break;
            clapAbility.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            int cast = t.Now;
            for (int i = 0; i < 120 && clapAbility.lastCastTick < cast; i++) yield return 1;
            if (!t.Check(gene.TicksSinceSwap >= 0, "the clap swap opened the window (" + RimArtTestContext.Describe(carrier) + ")")) yield break;
            // The clip holds the carrier a little after the swap; Black Flash is ordered as soon as the swap lands, as a player would.
            int swapAt = t.Now;

            var hit = new PunchResult();
            foreach (int step in Punch(t, carrier, enemy, hit)) yield return step;
            t.Log("hit " + (t.Now - swapAt) + " ticks after the swap");
            t.Check(hit.stunTicks > 12, "the Black Flash stunned the target (" + hit.stunTicks + " ticks left)");
            t.Check(hit.zone, "the carrier is in the zone");
            t.Check(gene.TicksSinceSwap < 0, "the window was spent");
        }

        // ---- the Echo ----

        private static GeneDef Organ => DefDatabase<GeneDef>.GetNamed("AG_AnchorOrgan");
        private static AbilityDef Provoke => DefDatabase<AbilityDef>.GetNamed("AG_Provoke");
        private static bool Has(Pawn pawn, AbilityDef def) => pawn.abilities?.GetAbility(def) != null;

        /// <summary>
        /// Awakening gives the organ for life; the kit comes and goes with the form. A stone thrown
        /// before a revert stays on the map through it (its rare tick checks the gene), and the claps
        /// keep growing back outside hero form.
        /// </summary>
        [RimArtTest("Todo", "echo 7 organ on awakening, kit only in hero form, stones and claps kept over a revert")]
        private static IEnumerable<int> EchoForm(RimArtTestContext t)
        {
            t.Clear();
            Pawn todo = CastHoldTest.Caster(t, DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife"));
            EchoRecord record = Host(todo, manifest: false);
            yield return 1;
            t.Check(todo.genes.Xenogenes.Any(g => g.def == Organ), "the anchor organ is a xenogene after awakening");
            t.Check(todo.story.traits.HasTrait(TraitDef.Named("Brawler")), "Brawler is forced");
            t.Check(!Has(todo, Mark) && !Has(todo, BlackFlash) && !Has(todo, Provoke), "no kit before manifesting");
            t.Check(todo.equipment.Primary != null, "he holds the knife outside hero form");

            EchoUtility.Manifest(record);
            yield return 2;
            AbilityDef clap = DefDatabase<AbilityDef>.GetNamed("AG_AnchorClap"), twice = DefDatabase<AbilityDef>.GetNamed("AG_AnchorDoubleClap");
            t.Check(Has(todo, Mark) && Has(todo, clap) && Has(todo, twice) && Has(todo, BlackFlash) && Has(todo, Provoke), "the five abilities in hero form");
            t.Check(todo.equipment.Primary == null, "empty hands in hero form (" + todo.equipment.Primary?.LabelShort + ")");

            Gene_Anchors gene = AnchorUtility.GeneOf(todo);
            Thing stone = ThingMaker.MakeThing(gene.StoneDef);
            if (stone is ClapStone owned) owned.owner = todo;
            GenSpawn.Spawn(stone, t.center + new IntVec3(4, 0, 0), t.map);
            gene.Add(new Anchor(stone, t.Now));
            gene.Spend(1);
            float next = gene.SecondsToNext;

            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(!Has(todo, Mark) && !Has(todo, clap) && !Has(todo, BlackFlash) && !Has(todo, Provoke), "the kit is taken back on revert");
            t.Check(todo.genes.HasActiveGene(Organ), "the organ stays");
            // Past a rare tick (250), when a stone nobody holds destroys itself.
            yield return 260;
            t.Check(!stone.Destroyed && gene.LiveCount == 1, "the stone is still on the map (" + gene.LiveCount + " held)");
            t.Check(gene.SecondsToNext < next - 3f, "the clap grows back outside hero form (" + next.ToString("F1") + " -> " + gene.SecondsToNext.ToString("F1") + " s)");
            EchoDevice.workingForTests = null;
        }

        /// <summary>Black Flash pays 1 charge and Provoke 5; the claps pay none. Combat presence no longer grants Provoke.</summary>
        [RimArtTest("Todo", "echo 8 cast costs, provoke 90 s, combat presence grants nothing")]
        private static IEnumerable<int> EchoCosts(RimArtTestContext t)
        {
            t.Clear();
            Pawn todo = Carrier(t);
            Pawn enemy = Target(t, t.center + new IntVec3(1, 0, 0));
            yield return 2;
            EchoDef echo = DebugActions_Todo.Echo;
            t.Check(echo.CastCost(Mark) == 0f && echo.CastCost(DefDatabase<AbilityDef>.GetNamed("AG_AnchorClap")) == 0f
                && echo.CastCost(DefDatabase<AbilityDef>.GetNamed("AG_AnchorDoubleClap")) == 0f, "stone and claps cost no charge");
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;

            float before = echoes.charge;
            todo.abilities.GetAbility(BlackFlash).Activate(new LocalTargetInfo(enemy), LocalTargetInfo.Invalid);
            t.Check(Mathf.Abs(before - echoes.charge - 1f) < 0.05f, "Black Flash paid 1 (" + before.ToString("F2") + " -> " + echoes.charge.ToString("F2") + ")");

            before = echoes.charge;
            Ability provoke = todo.abilities.GetAbility(Provoke);
            provoke.Activate(new LocalTargetInfo(todo), LocalTargetInfo.Invalid);
            t.Check(Mathf.Abs(before - echoes.charge - 5f) < 0.05f, "Provoke paid 5 (" + before.ToString("F2") + " -> " + echoes.charge.ToString("F2") + ")");
            t.Check(Provoke.cooldownTicksRange.max == 5400, "Provoke's cooldown is 90 s (" + Provoke.cooldownTicksRange + ")");
            t.Check(todo.health.hediffSet.HasHediff(HediffDef.Named("AG_Provoking")), "Todo is provoking");

            Pawn other = t.Colonist(t.center + new IntVec3(-3, 0, 0));
            other.story.traits.GainTrait(new Trait(TraitDef.Named("AG_CombatPresence")));
            yield return 5;
            t.Check(!Has(other, Provoke), "Combat presence no longer grants Provoke");
            EchoDevice.workingForTests = null;
        }
    }
}
