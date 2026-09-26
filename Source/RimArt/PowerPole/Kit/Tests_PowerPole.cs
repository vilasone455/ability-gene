using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for the Power Pole kit (run with -quicktest -rimarttest=power).</summary>
    public static class Tests_PowerPole
    {
        private static IEnumerable<int> Hold(RimArtTestContext t, string ability, int enemyAt)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, PowerPoleDefOf.AG_PowerPole);
            Pawn enemy = t.Enemy(t.center + new IntVec3(enemyAt, 0, 0), armed: false);
            var casts = t.map.GetComponent<MapComponent_PowerPoleCasts>();
            foreach (int step in CastHoldTest.Run(t, caster, DefDatabase<AbilityDef>.GetNamed(ability), enemy,
                () => casts.Fired(caster), CastHoldTest.Asking(() => casts.Holds(caster)))) yield return step;
        }

        /// <summary>The thrust carries the enemy off in a flyer, which also ended the job at the hit.</summary>
        [RimArtTest("Power Pole", "hold 1 extend thrust holds an undrafted wielder until the pole is back")]
        private static IEnumerable<int> Thrust(RimArtTestContext t) => Hold(t, "AG_PowerPole_ExtendThrust", 6);

        [RimArtTest("Power Pole", "hold 2 sweep holds an undrafted wielder until the pole is back")]
        private static IEnumerable<int> Sweep(RimArtTestContext t) => Hold(t, "AG_PowerPole_Sweep", 3);
    }
}
