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

        /// <summary>
        /// Close shots for the pawn height fit: the jar on four standing holders facing north, east, south
        /// and west; Drifting Burst 4 cells east and 2 north (the pipe at the mouth, the bubbles forming);
        /// Eye Pop at an enemy 5 cells east (the pipe raised, the burst at its face, the soap film and the
        /// tiny bubbles).
        /// </summary>
        [RimArtTest("Bubble Pipe", "height 1 jar, pipe and bubbles on real pawns (screenshots)", 2400)]
        private static IEnumerable<int> Height(RimArtTestContext t)
        {
            t.Clear();
            Rot4[] facings = { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
            var posed = new List<Pawn>();
            for (int i = 0; i < 4; i++)
            {
                Pawn pawn = t.Colonist(t.center + new IntVec3(-3 + i * 2, 0, -5));
                t.Equip(pawn, BubblePipeDefOf.AG_BubblePipe);
                pawn.drafter.Drafted = false;
                posed.Add(HeightShots.Plain(pawn, facings[i]));
            }
            Pawn caster = HeightShots.Stay(CastHoldTest.Caster(t, BubblePipeDefOf.AG_BubblePipe));
            yield return 20;
            yield return HeightShots.Shoot(t, "bubble jar N E S W", t.center + new IntVec3(0, 0, -5), posed.ToArray());
            foreach (int step in HeightShots.Cast(t, caster, BubblePipeDefOf.AG_BubblePipe_DriftingBurst, t.center + new IntVec3(4, 0, 2), t.center + new IntVec3(2, 0, 0),
                "bubble drift", null, 40, 80)) yield return step;
            yield return 200;
            Pawn enemy = HeightShots.Target(t, t.center + new IntVec3(5, 0, 0));
            yield return 5;
            foreach (int step in HeightShots.Cast(t, caster, BubblePipeDefOf.AG_BubblePipe_EyePop, enemy, t.center + new IntVec3(2, 0, 0),
                "bubble eyepop", enemy, 30, 100, 135)) yield return step;
            yield return 60;
        }
    }
}
