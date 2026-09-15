using HarmonyLib;
using Verse;

namespace RimArt
{
    public static class GravityTargeting
    {
        public static void Install(Harmony harmony)
        {
            var type = AccessTools.TypeByName("AM.Patches.Patch_InvisibilityUtility_IsPsychologicallyInvisible");
            if (type == null) return;
            var prefix = AccessTools.Method(type, "Prefix", new[] { typeof(Pawn), typeof(bool).MakeByRefType() });
            if (prefix == null)
            { Log.Error("[RimArt] Cannot resolve Melee Animation's targeting hook for Gravity Well."); return; }
            harmony.Patch(prefix, prefix: new HarmonyMethod(typeof(GravityTargeting), nameof(AllowTargeting)));
        }
        public static bool AllowTargeting(Pawn pawn, ref bool __result)
        {
            if (!GravityCommands.Busy(pawn)) return true;
            // Bypass only AM's automatic invisibility, leaving genuine invisibility intact.
            __result = true; return false;
        }
    }
}
