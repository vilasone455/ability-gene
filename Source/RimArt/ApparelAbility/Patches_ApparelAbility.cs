using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Apparel has Notify_Equipped but no Notify_Unequipped, and the tracker is the only place
    /// that sees both halves, so both hooks are taken from there rather than one from each.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Notify_ApparelAdded))]
    static class Patch_ApparelTracker_AbilityAdded
    {
        static void Postfix(Pawn_ApparelTracker __instance, Apparel apparel)
        {
            apparel?.TryGetComp<CompApparelAbility>()?.GrantTo(__instance.pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Notify_ApparelRemoved))]
    static class Patch_ApparelTracker_AbilityRemoved
    {
        static void Postfix(Pawn_ApparelTracker __instance, Apparel apparel)
        {
            apparel?.TryGetComp<CompApparelAbility>()?.RevokeFrom(__instance.pawn);
        }
    }

    /// <summary>
    /// Shows the charge count ("4 / 6") in the corner of an ability gizmo whose ability comes from
    /// worn apparel with a <see cref="CompApparelReloadable"/>: the kunai belt and the makibishi
    /// pouch.
    ///
    /// Command_Ability.TopRightLabel reads Ability.GizmoExtraLabel, which only knows about the
    /// ability's own charges. These charges are on the apparel, so the label is filled in here.
    /// </summary>
    [HarmonyPatch(typeof(Command_Ability), nameof(Command_Ability.TopRightLabel), MethodType.Getter)]
    static class Patch_CommandAbility_ApparelChargeCount
    {
        static void Postfix(Command_Ability __instance, ref string __result)
        {
            if (__result != null) return;
            Ability ability = __instance.Ability;
            List<Apparel> worn = ability?.pawn?.apparel?.WornApparel;
            if (worn == null) return;
            for (int i = 0; i < worn.Count; i++)
            {
                List<AbilityDef> granted = worn[i].TryGetComp<CompApparelAbility>()?.Props.abilities;
                if (granted == null || !granted.Contains(ability.def)) continue;
                CompApparelReloadable charges = worn[i].TryGetComp<CompApparelReloadable>();
                if (charges == null) continue;
                __result = charges.LabelRemaining;
                return;
            }
        }
    }
}
