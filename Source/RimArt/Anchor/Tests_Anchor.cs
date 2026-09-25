using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for Todo's anchor kit, Mark and Clap (run with -quicktest -rimarttest=anchor).</summary>
    public static class Tests_Anchor
    {
        private static AbilityDef Mark => DefDatabase<AbilityDef>.GetNamed("AG_AnchorMark");

        private static Pawn Carrier(RimArtTestContext t)
        {
            Pawn carrier = CastHoldTest.Caster(t);
            carrier.genes.AddGene(DefDatabase<GeneDef>.GetNamed("AG_AnchorOrgan"), true);
            return carrier;
        }

        private static IEnumerable<int> MarkHold(RimArtTestContext t, Pawn carrier, IntVec3 cell, float length)
        {
            var flicks = t.map.GetComponent<MapComponent_MarkFlicks>();
            foreach (int step in CastHoldTest.Run(t, carrier, Mark, cell, () => flicks.Fired(carrier), CastHoldTest.For(length))) yield return step;
        }

        [RimArtTest("Anchor", "hold 1 mark placing holds an undrafted carrier until the flick clip is done")]
        private static IEnumerable<int> Place(RimArtTestContext t)
        {
            t.Clear();
            Pawn carrier = Carrier(t);
            foreach (int step in MarkHold(t, carrier, t.center + new IntVec3(5, 0, 0), MarkFlick.FlickLength)) yield return step;
        }

        [RimArtTest("Anchor", "hold 2 mark lifting holds an undrafted carrier until the catch clip is done")]
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
        [RimArtTest("Anchor", "hold 3 clap on the last charge holds an undrafted carrier until the clip is done")]
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
        [RimArtTest("Anchor", "clap 4 direct swap with an enemy 6 cells away draws and swaps")]
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
    }
}
