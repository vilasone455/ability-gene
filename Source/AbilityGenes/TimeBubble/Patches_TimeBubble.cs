using HarmonyLib;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Thing.DoTick is the sole tick dispatch point in 1.6 (TickList.Tick calls it, and it
    /// then fans out to Tick / TickInterval / TickRare / TickLong and held contents).
    /// Skipping it stops the thing completely, and because tickDelta is only incremented
    /// inside DoTick there is no catch-up burst when the bubble lifts.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
    public static class Patch_Thing_DoTick
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Thing __instance)
        {
            if (TimeBubbleRegistry.ActiveCount == 0) return true;
            return !TimeBubbleRegistry.IsFrozen(__instance);
        }
    }

    /// <summary>
    /// Stopped time also means nothing inside can be hurt. Without this the bubble would be
    /// a free execution window rather than a stall, since frozen pawns cannot fight back.
    /// Controlled per-bubble by frozenAreInvulnerable on the ability comp.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Thing __instance, ref DamageWorker.DamageResult __result)
        {
            if (TimeBubbleRegistry.ActiveCount == 0) return true;
            if (!TimeBubbleRegistry.IsProtected(__instance)) return true;
            __result = new DamageWorker.DamageResult();
            return false;
        }
    }
}
