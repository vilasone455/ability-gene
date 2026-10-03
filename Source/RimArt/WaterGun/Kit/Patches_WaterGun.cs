using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    // While a cast's picture draws the gun raised, Core's held gun is not drawn as well.
    [StaticConstructorOnStartup]
    public static class Patches_WaterGun
    {
        static Patches_WaterGun() => HeldWeaponHide.Register(WaterGunDefOf.AG_WaterGun, MapComponent_WaterGun.IsCasting);
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
