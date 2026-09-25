using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for the Paper Bomb kit (run with -quicktest -rimarttest=paper).</summary>
    public static class Tests_PaperBomb
    {
        private static IEnumerable<int> Hold(RimArtTestContext t, AbilityDef ability, bool atEnemy, float length)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, DefDatabase<ThingDef>.GetNamed("AG_TagScroll"));
            Pawn enemy = t.Enemy(t.center + new IntVec3(6, 0, 0), armed: false);
            var bombs = t.map.GetComponent<MapComponent_PaperBomb>();
            LocalTargetInfo target = atEnemy ? enemy : new LocalTargetInfo(t.center + new IntVec3(0, 0, 6));
            foreach (int step in CastHoldTest.Run(t, caster, ability, target,
                () => bombs.Fired(caster), CastHoldTest.For(length))) yield return step;
        }

        [RimArtTest("Paper Bomb", "hold 1 tag throw holds an undrafted caster until the clip is done")]
        private static IEnumerable<int> TagThrow(RimArtTestContext t) =>
            Hold(t, PaperBombDefOf.AG_PaperBomb_TagThrow, true, JobDriver_CastPaperBomb.ThrowLength);

        [RimArtTest("Paper Bomb", "hold 2 tag line holds an undrafted caster until the strip is out")]
        private static IEnumerable<int> TagLine(RimArtTestContext t) =>
            Hold(t, PaperBombDefOf.AG_PaperBomb_TagLine, false, JobDriver_CastPaperBomb.FlickLength);

        [RimArtTest("Paper Bomb", "hold 3 shroud holds an undrafted caster until the clip is done")]
        private static IEnumerable<int> Shroud(RimArtTestContext t) =>
            Hold(t, PaperBombDefOf.AG_PaperBomb_Shroud, true, JobDriver_CastPaperBomb.FanLength);
    }
}
