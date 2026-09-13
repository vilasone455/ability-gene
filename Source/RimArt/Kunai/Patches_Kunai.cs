using System.Collections.Generic;
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

    /// <summary>
    /// Halves the bleeding of a wound while a kunai is stuck in it. The injury's own BleedRate
    /// already covers tending, age and the part; this only scales the result, so a tended wound
    /// still bleeds nothing. Pulling the kunai removes the hediff and the full rate comes back.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Injury), nameof(Hediff_Injury.BleedRate), MethodType.Getter)]
    static class Patch_HediffInjury_EmbeddedKunaiBleed
    {
        static void Postfix(Hediff_Injury __instance, ref float __result)
        {
            if (__result <= 0f) return;
            List<Hediff> hediffs = __instance.pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_EmbeddedKunai kunai && kunai.wound == __instance && kunai.Active)
                {
                    __result *= KunaiDefaults.EmbeddedBleedFactor;
                    return;
                }
            }
        }
    }
}
