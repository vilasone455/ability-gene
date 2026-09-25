using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for the Bank Shot kit (run with -quicktest -rimarttest=bank).</summary>
    public static class Tests_BankShot
    {
        /// <summary>The charge is the warmup, so nothing holds after the shot; the job has to end through its own toils.</summary>
        [RimArtTest("Bank Shot", "hold 1 charge holds an undrafted caster through the charge")]
        private static IEnumerable<int> Charge(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, BankShotDefOf.AG_BankShot);
            Pawn enemy = t.Enemy(t.center + new IntVec3(8, 0, 0), armed: false);
            var shots = t.map.GetComponent<MapComponent_BankShot>();
            foreach (int step in CastHoldTest.Run(t, caster, BankShotDefOf.AG_BankShot_Charge, enemy,
                () => shots.Fired(caster), _ => false)) yield return step;
        }
    }
}
