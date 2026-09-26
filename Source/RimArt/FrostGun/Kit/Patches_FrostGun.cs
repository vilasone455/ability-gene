using HarmonyLib;
using Verse;

namespace RimArt
{
    // The first damage a frozen pawn takes shatters its ice. A postfix on Pawn.PostApplyDamage runs for
    // every hit that was not absorbed by a shield, armour-stopped hits too, and also when the hit
    // killed or downed the pawn (Pawn_HealthTracker.PostApplyDamage returns early then, before the
    // hediffs are told). The shatter itself is dealt on the map's next tick.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_FrostGun_ShatterOnDamage
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo)
        {
            if (!FlashFreeze.IsFrozen(__instance)) return;
            __instance.MapHeld?.GetComponent<MapComponent_FrostGun>()?.Notify_Damaged(__instance, dinfo);
        }
    }
}
