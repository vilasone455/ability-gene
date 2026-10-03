using System.Collections.Generic;
using RimWorld;
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

        /// <summary>
        /// Close shots for the pawn height fit: the charge 60 ticks in (the glow at the hand against the
        /// game's aimed pistol), the muzzle flash on the fire tick, and the wound on an enemy 8 cells east.
        /// </summary>
        [RimArtTest("Bank Shot", "height 1 charge, muzzle and wound on real pawns (screenshots)")]
        private static IEnumerable<int> Height(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = HeightShots.Stay(CastHoldTest.Caster(t, BankShotDefOf.AG_BankShot));
            Pawn enemy = HeightShots.Target(t, t.center + new IntVec3(8, 0, 0));
            IntVec3 camera = t.center + new IntVec3(4, 0, 0);
            yield return 10;
            Ability charge = caster.abilities.GetAbility(BankShotDefOf.AG_BankShot_Charge);
            foreach (int step in HeightShots.Cast(t, caster, BankShotDefOf.AG_BankShot_Charge, enemy, camera, "bank charge", enemy, 60)) yield return step;
            int cast = t.Now - 60;
            for (int i = 0; i < 120 && charge.lastCastTick < cast; i++) yield return 1;
            if (!t.Check(charge.lastCastTick >= cast, "the shot fired")) yield break;
            int fire = charge.lastCastTick;
            foreach (int at in new[] { 1, 14, 16, 18 })
            {
                yield return fire + at - t.Now;
                yield return HeightShots.Shoot(t, "bank fire " + at, camera, caster, enemy);
            }
            yield return 30;
        }
    }
}
