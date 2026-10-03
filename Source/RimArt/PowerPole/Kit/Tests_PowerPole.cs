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

        /// <summary>
        /// Close shots for the pawn height fit: Extend Thrust at an enemy 6 cells east (the pole at the
        /// hands, the hit flash, fully out), Sweep at one 3 cells east (the pole at the hands, the hit),
        /// then Vault Strike at one 6 cells east (the carry, the plant, in the air, the strike).
        /// </summary>
        [RimArtTest("Power Pole", "height 1 pole, hands and flashes on real pawns (screenshots)", 2400)]
        private static IEnumerable<int> Height(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = HeightShots.Stay(CastHoldTest.Caster(t, PowerPoleDefOf.AG_PowerPole));
            Pawn enemy = HeightShots.Target(t, t.center + new IntVec3(6, 0, 0));
            IntVec3 camera = t.center + new IntVec3(3, 0, 0);
            yield return 10;
            foreach (int step in HeightShots.Cast(t, caster, DefDatabase<AbilityDef>.GetNamed("AG_PowerPole_ExtendThrust"), enemy, camera, "pole thrust", enemy, 12, 29, 44))
                yield return step;
            yield return 90;
            if (enemy.Spawned) enemy.Destroy();
            enemy = HeightShots.Target(t, t.center + new IntVec3(3, 0, 0));
            yield return 5;
            foreach (int step in HeightShots.Cast(t, caster, DefDatabase<AbilityDef>.GetNamed("AG_PowerPole_Sweep"), enemy, camera, "pole sweep", enemy, 12, 31))
                yield return step;
            yield return 90;
            if (enemy.Spawned) enemy.Destroy();
            enemy = HeightShots.Target(t, t.center + new IntVec3(6, 0, 0));
            yield return 5;
            foreach (int step in HeightShots.Cast(t, caster, DefDatabase<AbilityDef>.GetNamed("AG_PowerPole_VaultStrike"), enemy, camera, "pole vault", enemy, 3, 15, 50, 75, 88))
                yield return step;
            yield return 90;
        }
    }
}
