using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for the Bubble Pipe kit (run with -quicktest -rimarttest=bubble).</summary>
    public static class Tests_BubblePipe
    {
        private static IEnumerable<int> Hold(RimArtTestContext t, AbilityDef ability, bool atEnemy)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, BubblePipeDefOf.AG_BubblePipe);
            Pawn enemy = t.Enemy(t.center + new IntVec3(5, 0, 0), armed: false);
            var pipes = t.map.GetComponent<MapComponent_BubblePipe>();
            LocalTargetInfo target = atEnemy ? enemy : new LocalTargetInfo(t.center + new IntVec3(4, 0, 2));
            foreach (int step in CastHoldTest.Run(t, caster, ability, target,
                () => pipes.Fired(caster), CastHoldTest.Asking(() => pipes.Holds(caster)))) yield return step;
        }

        [RimArtTest("Bubble Pipe", "hold 1 drifting burst holds an undrafted caster until the pipe is lowered")]
        private static IEnumerable<int> DriftingBurst(RimArtTestContext t) => Hold(t, BubblePipeDefOf.AG_BubblePipe_DriftingBurst, false);

        [RimArtTest("Bubble Pipe", "hold 2 eye pop holds an undrafted caster until the pipe is lowered")]
        private static IEnumerable<int> EyePop(RimArtTestContext t) => Hold(t, BubblePipeDefOf.AG_BubblePipe_EyePop, true);
    }
}
