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
    }
}
