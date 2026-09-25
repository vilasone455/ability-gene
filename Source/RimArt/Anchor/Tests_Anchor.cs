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
        /// It swaps with a stone: a direct swap with a pawn throws in ClapTeleportGraphics.Card (the
        /// pawn end's suit is -1), which is a separate bug.
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
    }
}
