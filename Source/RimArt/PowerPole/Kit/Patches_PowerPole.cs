using HarmonyLib;
using Verse;

namespace RimArt
{
    // While a cast's picture draws the pole long, the ordinary held staff is not drawn as well.
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_PowerPole_EquipmentDrawing
    {
        public static bool Prefix(Thing eq)
        {
            if (eq.def != PowerPoleDefOf.AG_PowerPole) return true;
            Pawn pawn = (eq.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            return !MapComponent_PowerPoleCasts.IsCasting(pawn);
        }
    }
}
