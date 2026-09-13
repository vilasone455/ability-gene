using System.Linq;
using HarmonyLib;
using Verse;

namespace RimArt
{
    public static class ShinraTargeting
    {
        public static void Install(Harmony harmony)
        {
            var type = AccessTools.TypeByName("AM.Patches.Patch_InvisibilityUtility_IsPsychologicallyInvisible");
            if (type == null) return;
            var prefix = AccessTools.Method(type, "Prefix", new[] { typeof(Pawn), typeof(bool).MakeByRefType() });
            if (prefix == null)
            {
                Log.Error("[RimArt] Cannot disable Melee Animation's targeting invisibility for Shinra: its prefix changed.");
                return;
            }
            harmony.Patch(prefix, prefix: new HarmonyMethod(typeof(ShinraTargeting), nameof(AllowNormalTargeting)));
        }

        // Patch only AM's prefix, not the final invisibility result. Returning true from that
        // prefix lets vanilla and other mods evaluate genuine invisibility normally.
        public static bool AllowNormalTargeting(Pawn pawn, ref bool __result)
        {
            if (GameComponent_Shinra.Instance?.States.Any(s => s.pawn == pawn && s.active) != true) return true;
            __result = true;
            return false;
        }
    }
}
