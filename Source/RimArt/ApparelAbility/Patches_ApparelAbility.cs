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
}
