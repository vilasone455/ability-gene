using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Shows the shared claps ("2 / 3") in the corner of the Clap and Double Clap gizmos. The
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
