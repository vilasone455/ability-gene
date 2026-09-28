using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>A pawn with Black Receiver rods in it cannot use abilities or psycasts (every psycast is an Ability).</summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.CanCast), MethodType.Getter)]
    static class Patch_Ability_CanCast_PainRods
    {
        static void Postfix(Ability __instance, ref AcceptanceReport __result)
        {
            if (__result.Accepted && PainRods.Count(__instance.pawn) > 0) __result = "Chakra rods block abilities.";
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GizmoDisabled))]
    static class Patch_Ability_GizmoDisabled_PainRods
    {
        static void Postfix(Ability __instance, ref bool __result, ref string reason)
        {
            if (__result || PainRods.Count(__instance.pawn) == 0) return;
            __result = true;
            reason = "Chakra rods block abilities.";
        }
    }
}
