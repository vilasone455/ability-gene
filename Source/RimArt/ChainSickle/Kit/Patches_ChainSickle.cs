using HarmonyLib;
using Verse;

namespace RimArt
{
    // The sickle cuts a staked pawn harder: every melee hit made with the chain sickle on a pawn that
    // has AG_ChainStaked is multiplied by the weapon's stakedMeleeFactor (1.5). A melee hit carries its
    // Tool; Snag's weight hit has none, so it is never multiplied. A prefix on Pawn.PreApplyDamage
    // because no hediff or comp hook can change a hit's amount before it lands; it returns at the
    // first compare for every hit that is not from this weapon.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_ChainSickle_StakedCut
    {
        public static void Prefix(Pawn __instance, ref DamageInfo dinfo)
        {
            if (dinfo.Weapon != ChainSickleDefOf.AG_ChainSickle || dinfo.Tool == null) return;
            if (__instance.health?.hediffSet?.GetFirstHediffOfDef(ChainSickleDefOf.AG_ChainStaked) == null) return;
            float factor = dinfo.Weapon.GetCompProperties<CompProperties_ChainSickle>()?.stakedMeleeFactor ?? 1f;
            dinfo.SetAmount(dinfo.Amount * factor);
        }
    }
}
