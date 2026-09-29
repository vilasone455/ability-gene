using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>The Game's mark on an enemy: who marked it and the ability's numbers at the cast.</summary>
    public class Hediff_SatoGameMark : HediffWithComps
    {
        public Pawn marker;
        public float damageFactor = 1f;
        public float killRefund;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref marker, "marker");
            Scribe_Values.Look(ref damageFactor, "damageFactor", 1f);
            Scribe_Values.Look(ref killRefund, "killRefund");
        }
    }

    public static class TheGame
    {
        public static Hediff_SatoGameMark MarkOn(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(SatoDefOf.AG_SatoGameMark) as Hediff_SatoGameMark;

        public static void ClearMarksBy(Pawn marker)
        {
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    Hediff_SatoGameMark mark = MarkOn(pawn);
                    if (mark != null && mark.marker == marker) pawn.health.RemoveHediff(mark);
                }
        }

        /// <summary>His ranged hits: the damage's weapon is a ranged weapon (a projectile carries its gun's def).</summary>
        public static bool IsHisShot(DamageInfo dinfo, Pawn marker) =>
            dinfo.Instigator == marker && dinfo.Weapon != null && dinfo.Weapon.IsRangedWeapon;
    }

    /// <summary>The marked pawn takes the mark's factor on Satō's ranged hits.</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    static class Patch_Thing_TakeDamage_TheGame
    {
        static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (!(__instance is Pawn pawn) || !(dinfo.Instigator is Pawn)) return;
            Hediff_SatoGameMark mark = TheGame.MarkOn(pawn);
            if (mark == null || !TheGame.IsHisShot(dinfo, mark.marker)) return;
            dinfo.SetAmount(dinfo.Amount * mark.damageFactor);
        }
    }

    /// <summary>A marked pawn that dies while marked, to anyone, refunds charge to the pool.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    static class Patch_Pawn_Kill_TheGame
    {
        static void Prefix(Pawn __instance, out float __state)
        {
            __state = 0f;
            if (__instance.Dead) return;
            Hediff_SatoGameMark mark = TheGame.MarkOn(__instance);
            if (mark != null) __state = mark.killRefund;
        }

        static void Postfix(Pawn __instance, float __state)
        {
            if (__state > 0f && __instance.Dead) GameComponent_Echoes.Get?.Refund(__state);
        }
    }
}
