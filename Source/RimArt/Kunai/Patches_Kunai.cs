using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
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
