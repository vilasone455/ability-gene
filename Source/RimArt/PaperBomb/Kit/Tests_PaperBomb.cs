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

        /// <summary>
        /// Close shots for the pawn height fit: Tag Throw at an enemy 6 cells east (the release, the tag
        /// stuck in it), Tag Line 6 cells north (the strip from the hand), Detonate (the seal glint),
        /// then Shroud on an enemy 4 cells east (the six stuck tags, the seal glint).
        /// </summary>
        [RimArtTest("Paper Bomb", "height 1 tags, strip and seal glint on real pawns (screenshots)", 2400)]
        private static IEnumerable<int> Height(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = HeightShots.Stay(CastHoldTest.Caster(t, DefDatabase<ThingDef>.GetNamed("AG_TagScroll")));
            Pawn enemy = HeightShots.Target(t, t.center + new IntVec3(6, 0, 0));
            IntVec3 camera = t.center + new IntVec3(3, 0, 0);
            yield return 10;
            foreach (int step in HeightShots.Cast(t, caster, PaperBombDefOf.AG_PaperBomb_TagThrow, enemy, camera, "paper throw", enemy, 20, 60, 120)) yield return step;
            yield return 90;
            if (enemy.Spawned) enemy.Destroy();
            yield return 5;
            foreach (int step in HeightShots.Cast(t, caster, PaperBombDefOf.AG_PaperBomb_TagLine, t.center + new IntVec3(0, 0, 6), t.center + new IntVec3(0, 0, 2),
                "paper line", null, 80, 140)) yield return step;
            yield return 60;
            foreach (int step in HeightShots.Cast(t, caster, PaperBombDefOf.AG_PaperBomb_Detonate, caster, t.center, "paper detonate", null, 22, 28)) yield return step;
            yield return 90;
            Pawn victim = HeightShots.Target(t, t.center + new IntVec3(4, 0, 0));
            yield return 5;
            foreach (int step in HeightShots.Cast(t, caster, PaperBombDefOf.AG_PaperBomb_Shroud, victim, t.center + new IntVec3(2, 0, 0), "paper shroud", victim, 110, 158))
                yield return step;
            yield return 90;
        }

        /// <summary>
        /// Close shots of the Melee Animation hands in the three Paper Bomb clips, facing east and facing south:
        /// Tag Throw (rest, at the roll, cocked, release), Tag Line's flick and the arm over the strip, and
        /// Shroud's fan across the front. For checking the clips' hands against a real pawn's body.
        /// </summary>
        [RimArtTest("Paper Bomb", "height 2 the clips' hands facing east and south (screenshots)", 3000)]
        private static IEnumerable<int> HeightClips(RimArtTestContext t)
        {
            t.Clear();
            ThingDef scroll = DefDatabase<ThingDef>.GetNamed("AG_TagScroll");
            foreach (IntVec3 way in new[] { new IntVec3(1, 0, 0), new IntVec3(0, 0, -1) })
            {
                string side = way.x > 0 ? "east" : "south";
                Pawn caster = HeightShots.Stay(CastHoldTest.Caster(t, scroll));
                Pawn target = HeightShots.Target(t, t.center + way * 6);
                yield return 10;
                foreach (int step in HeightShots.Cast(t, caster, PaperBombDefOf.AG_PaperBomb_TagThrow, target, t.center, "clip throw " + side, null, 2, 5, 11, 16, 19))
                    yield return step;
                yield return 60;
                foreach (int step in HeightShots.Cast(t, caster, PaperBombDefOf.AG_PaperBomb_TagLine, t.center + way * 6, t.center, "clip flick " + side, null, 6, 12, 19, 40))
                    yield return step;
                yield return 180;
                if (target.Spawned) target.Destroy();
                target = HeightShots.Target(t, t.center + way * 4);
                yield return 5;
                foreach (int step in HeightShots.Cast(t, caster, PaperBombDefOf.AG_PaperBomb_Shroud, target, t.center, "clip fan " + side, null, 8, 17, 30, 42))
                    yield return step;
                yield return 200;
                if (caster.Spawned) caster.Destroy();
                if (target.Spawned) target.Destroy();
                t.Clear();
                yield return 10;
            }
        }
    }
}
