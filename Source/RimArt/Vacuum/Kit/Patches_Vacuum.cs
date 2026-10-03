using Verse;

namespace RimArt
{
    // While a cast's picture draws the wand, Core's held vacuum is not drawn as well.
    [StaticConstructorOnStartup]
    public static class Patches_Vacuum
    {
        static Patches_Vacuum() => HeldWeaponHide.Register(VacuumDefOf.AG_Vacuum, MapComponent_Vacuum.IsCasting);
    }
}
