using HarmonyLib;
using Verse;

namespace RimArt
{
    // The animation supplies all five weapon pieces. Hide only this weapon's ordinary draw.
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_Fuma_EquipmentDrawing
    {
        public static bool Prefix(Thing eq)
        {
            if (eq is not FumaWeapon) return true;
            Pawn pawn = (eq.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            return pawn?.jobs?.curDriver is not JobDriver_ThrowFuma driver || !driver.Animated;
        }
    }
}
