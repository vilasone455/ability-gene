using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for the Water Gun kit (run with -quicktest -rimarttest=water).</summary>
    public static class Tests_WaterGun
    {
        private static IEnumerable<int> Hold(RimArtTestContext t, string ability, int enemyAt)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, WaterGunDefOf.AG_WaterGun);
            Pawn enemy = t.Enemy(t.center + new IntVec3(enemyAt, 0, 0), armed: false);
            var guns = t.map.GetComponent<MapComponent_WaterGun>();
            foreach (int step in CastHoldTest.Run(t, caster, DefDatabase<AbilityDef>.GetNamed(ability), enemy,
                () => guns.Fired(caster), CastHoldTest.Asking(() => guns.Holds(caster)))) yield return step;
        }

        [RimArtTest("Water Gun", "hold 1 hydro pump holds an undrafted caster until the gun is down")]
        private static IEnumerable<int> HydroPump(RimArtTestContext t) => Hold(t, "AG_WaterGun_HydroPump", 3);

        [RimArtTest("Water Gun", "hold 2 stream shot holds an undrafted caster until the gun is down")]
        private static IEnumerable<int> StreamShot(RimArtTestContext t) => Hold(t, "AG_WaterGun_StreamShot", 6);

        /// <summary>
        /// Close shots for the pawn height fit: the bag on a standing holder, Stream Shot 6 cells east
        /// (gun up and the jet out, the splash on the target, drips), then Hydro Pump on a pawn 3 cells
        /// east (gun up, the burst on it).
        /// </summary>
        [RimArtTest("Water Gun", "height 1 bag, gun, jet and splash on real pawns (screenshots)", 1500)]
        private static IEnumerable<int> Height(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = HeightShots.Stay(CastHoldTest.Caster(t, WaterGunDefOf.AG_WaterGun));
            Pawn enemy = HeightShots.Target(t, t.center + new IntVec3(6, 0, 0));
            IntVec3 camera = t.center + new IntVec3(3, 0, 0);
            yield return 10;
            yield return HeightShots.Shoot(t, "water idle", camera, caster, enemy);
            foreach (int step in HeightShots.Cast(t, caster, DefDatabase<AbilityDef>.GetNamed("AG_WaterGun_StreamShot"), enemy, camera,
                "water stream", enemy, 22, 33, 70)) yield return step;
            yield return 200;
            enemy.Destroy();
            Pawn victim = HeightShots.Target(t, t.center + new IntVec3(3, 0, 0));
            yield return 10;
            foreach (int step in HeightShots.Cast(t, caster, DefDatabase<AbilityDef>.GetNamed("AG_WaterGun_HydroPump"), victim, camera,
                "water pump", victim, 30, 46)) yield return step;
            yield return 300;
        }
    }
}
