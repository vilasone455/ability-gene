using HarmonyLib;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Core never draws the held sword's texture: GameComponent_EgoMimicry draws the sword in the hand wherever Core would show
    /// a held weapon (drafted, attacking), during every swing, and while corroded or overclocking.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patches_EgoMimicry
    {
        static Patches_EgoMimicry() => HeldWeaponHide.Register(EgoDefOf.AG_EgoMimicry, _ => true);
    }

    /// <summary>A corroded or overclocking wielder hit for real damage loses an arm stage (<see cref="EgoMimicry.HitTaken"/>).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_Pawn_PostApplyDamage_EgoMimicryArm
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (totalDamageDealt > 0f) EgoMimicry.HitTaken(__instance, dinfo, totalDamageDealt);
        }
    }
}
