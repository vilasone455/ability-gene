using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Thing.DoTick is the sole tick dispatch point in 1.6 (TickList.Tick calls it, and it
    /// then fans out to Tick / TickInterval / TickRare / TickLong and held contents).
    /// Skipping it stops the thing completely, and because tickDelta is only incremented
    /// inside DoTick there is no catch-up burst when the bubble lifts.
    ///
    /// This is also where stagnation lives - a pawn running at 1/3 rate ticks on one game
    /// tick in three. Acceleration needs no patch at all; MapComponent_TimeAlter simply calls
    /// DoTick extra times, and those calls pass back through here, which is why a stasis field
    /// correctly overrides an accelerating pawn.
    ///
    /// The offset by thingIDNumber matters: without it every stagnating thing would tick on
    /// the same game tick, which reads as synchronised stuttering rather than slow motion.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
    public static class Patch_Thing_DoTick
    {
        private const int StagnateInterval = 3;

        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Thing __instance)
        {
            if (TimeBubbleRegistry.ActiveCount > 0 && TimeBubbleRegistry.IsFrozen(__instance))
            {
                return false;
            }

            if (TimeAlterRegistry.StagnatedCount > 0 && TimeAlterRegistry.IsStagnated(__instance))
            {
                return (Find.TickManager.TicksGame + __instance.thingIDNumber) % StagnateInterval == 0;
            }

            return true;
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
