using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Shows the belt's kunai count ("4 / 6") in the corner of the throw kunai gizmo.
    ///
    /// Command_Ability.TopRightLabel reads Ability.GizmoExtraLabel, which only knows about the
    /// ability's own charges. The kunai are charges on the belt, so the label is filled in here.
    /// </summary>
    [HarmonyPatch(typeof(Command_Ability), nameof(Command_Ability.TopRightLabel), MethodType.Getter)]
    static class Patch_CommandAbility_KunaiCount
    {
        static void Postfix(Command_Ability __instance, ref string __result)
        {
            if (__result != null) return;
            Ability ability = __instance.Ability;
            if (ability?.def != KunaiDefOf.AG_ThrowKunai) return;
            __result = KunaiBelt.WornBy(ability.pawn)?.LabelRemaining;
        }
    }
}
