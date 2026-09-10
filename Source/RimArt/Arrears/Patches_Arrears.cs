using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where the body stops being told.
    ///
    /// Every damage instance aimed at a carrier is cancelled here and written to their ledger
    /// instead. Nothing is reduced and nothing is refused - the wound is real, it has already
    /// been through armour, and it is going to be applied in full. It is simply going to be
    /// applied later, along with everything else, at once.
    ///
    /// Priority.Low is deliberate and sets this below the mod's other two TakeDamage prefixes:
    ///
    /// - The stasis field sits at Priority.First and returns false for anything frozen, so a
    ///   pawn in a bubble is immune and runs up no debt at all.
    /// - The vector reflex runs at default priority and cancels what it returns to sender. A
    ///   reflected round was never received, so there is nothing to owe for it.
    /// - The halving membrane runs at default priority and *scales* verbless damage rather than
    ///   cancelling it, so what lands on the books here is the reduced figure. That ordering is
    ///   the one that had to be right: recorded before scaling, a carrier holding both would owe
    ///   the full blast they never actually took.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage_Arrears
    {
        [HarmonyPriority(Priority.Low)]
        public static bool Prefix(Thing __instance, ref DamageInfo dinfo, ref DamageWorker.DamageResult __result)
        {
            if (ArrearsRegistry.HolderCount == 0) return true;

            HediffComp_Arrears holder = ArrearsRegistry.HolderFor(__instance);
            if (holder == null) return true;

            holder.Record(dinfo);

            __result = new DamageWorker.DamageResult();
            return false;
        }
    }
}
