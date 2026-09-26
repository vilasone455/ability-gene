using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    // The wearer is immune to burns while it holds the flame gauntlet: fire damage (Flame, Burn, or
    // anything that leaves a burn) is absorbed and turned into heatPerBlockedHit Heat. A prefix on
    // Pawn.PreApplyDamage, as Patch_ChainSickle_StakedCut is, because nothing else can stop a hit
    // before it lands; it returns at the first check for every pawn without the gauntlet.
    // Overheating's arm burns are added as injuries (FlameGauntletHeat.BurnArm), so they get through.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_FlameGauntlet_BurnImmunity
    {
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            CompFlameGauntlet gauntlet = CompFlameGauntlet.HeldBy(__instance);
            if (gauntlet == null || !IsFire(dinfo.Def)) return true;
            absorbed = true;
            gauntlet.Add(gauntlet.Props.heatPerBlockedHit);
            return false;
        }

        public static bool IsFire(DamageDef def) =>
            def != null && (def == DamageDefOf.Flame || def == DamageDefOf.Burn || def.hediff == DamageDefOf.Burn.hediff);
    }

    // The wearer never catches fire: no fire attaches to a pawn holding the gauntlet, from fire
    // spreading, an incendiary, or its own Release.
    [HarmonyPatch(typeof(FireUtility), nameof(FireUtility.CanEverAttachFire))]
    public static class Patch_FlameGauntlet_NoAttachedFire
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            if (__result && t is Pawn pawn && CompFlameGauntlet.HeldBy(pawn) != null) __result = false;
        }
    }
}
