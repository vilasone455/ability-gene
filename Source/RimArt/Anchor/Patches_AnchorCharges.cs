using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Shows the shared claps ("2 / 3") in the corner of the Clap and Double Clap gizmos, and the
    /// seconds left in Black Flash's window ("2.4 s") in the corner of Black Flash. The
    /// charges are on the gene, not on either ability, so Ability.GizmoExtraLabel does not know
    /// them; same approach as Patch_CommandAbility_ApparelChargeCount.
    /// </summary>
    [HarmonyPatch(typeof(Command_Ability), nameof(Command_Ability.TopRightLabel), MethodType.Getter)]
    static class Patch_CommandAbility_ClapChargeCount
    {
        static void Postfix(Command_Ability __instance, ref string __result)
        {
            if (__result != null) return;
            Ability ability = __instance.Ability;
            if (ability == null) return;

            CompAbilityEffect_BlackFlash flash = ability.CompOfType<CompAbilityEffect_BlackFlash>();
            if (flash != null)
            {
                float left = flash.WindowSecondsLeft(AnchorUtility.GeneOf(ability.pawn));
                if (left > 0f) __result = left.ToString("F1") + " s";
                return;
            }

            if (ability.CompOfType<CompAbilityEffect_Clap>() == null && ability.CompOfType<CompAbilityEffect_DoubleClap>() == null) return;

            Gene_Anchors gene = AnchorUtility.GeneOf(ability.pawn);
            if (gene != null) __result = gene.Charges + " / " + gene.MaxCharges;
        }
    }

    public static class DebugActions_AnchorCharges
    {
        [RimArtDebug("Todo", "refill claps", RimArtDebugKind.Pawn)]
        private static void Refill(Pawn pawn) => AnchorUtility.GeneOf(pawn)?.Refill();
    }
}
