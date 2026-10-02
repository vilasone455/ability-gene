using Verse;

namespace RimArt
{
    // Core never draws the held staff's texture: GameComponent_EgoParadiseLost draws the staff upright in the right hand
    // wherever Core would show a held weapon (drafted, aiming), and while the wielder is corroded or overclocking.
    [StaticConstructorOnStartup]
    public static class Patches_EgoParadiseLost
    {
        static Patches_EgoParadiseLost() => HeldWeaponHide.Register(EgoDefOf.AG_EgoParadiseLost, _ => true);
    }
}
