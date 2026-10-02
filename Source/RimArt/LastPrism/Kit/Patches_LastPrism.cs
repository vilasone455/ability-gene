using Verse;

namespace RimArt
{
    // Core never draws the held prism's texture: GameComponent_LastPrism draws the prism at the chest wherever Core
    // would show a held weapon (PawnRenderUtility.CarryWeaponOpenly), and the beam's picture draws it while it fires.
    [StaticConstructorOnStartup]
    public static class Patches_LastPrism
    {
        static Patches_LastPrism() => HeldWeaponHide.Register(LastPrismDefOf.AG_LastPrism, _ => true);
    }
}
