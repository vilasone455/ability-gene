using Verse;

namespace RimArt
{
    // While a cast's picture draws the pole long, the ordinary held staff is not drawn as well.
    [StaticConstructorOnStartup]
    public static class Patches_PowerPole
    {
        static Patches_PowerPole() => HeldWeaponHide.Register(PowerPoleDefOf.AG_PowerPole, MapComponent_PowerPoleCasts.IsCasting);
    }
}
