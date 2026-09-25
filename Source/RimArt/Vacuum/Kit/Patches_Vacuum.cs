using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    // While a cast's picture draws the wand, Core's held vacuum is not drawn as well.
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_Vacuum_EquipmentDrawing
    {
        public static bool Prefix(Thing eq)
        {
            if (eq.def != VacuumDefOf.AG_Vacuum) return true;
            Pawn pawn = (eq.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            return !MapComponent_Vacuum.IsCasting(pawn);
        }
    }
}
