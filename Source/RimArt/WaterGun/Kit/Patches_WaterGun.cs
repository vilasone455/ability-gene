using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    // While a cast's picture draws the gun raised, Core's held gun is not drawn as well.
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_WaterGun_EquipmentDrawing
    {
        public static bool Prefix(Thing eq)
        {
            if (eq.def != WaterGunDefOf.AG_WaterGun) return true;
            Pawn pawn = (eq.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            return !MapComponent_WaterGun.IsCasting(pawn);
        }
    }

    // A Soaked pawn cannot catch fire. TryAttachFire asks this before attaching, and so does fire
    // spreading onto a pawn standing in it; WaterGunSoak.Apply puts out a fire already burning.
    [HarmonyPatch(typeof(FireUtility), nameof(FireUtility.CanEverAttachFire))]
    public static class Patch_WaterGun_SoakedCannotBurn
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            if (__result && t is Pawn pawn && WaterGunSoak.IsSoaked(pawn)) __result = false;
        }
    }
}
