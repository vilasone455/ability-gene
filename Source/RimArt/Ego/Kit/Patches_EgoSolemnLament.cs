using Verse;

namespace RimArt
{
    /// <summary>
    /// Core does not draw Solemn Lament while its picture draws the pair: during a burst and while a coffin is up
    /// (corroded or overclocking), when the guns hang at the wielder's sides.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patches_EgoSolemnLament
    {
        static Patches_EgoSolemnLament() => HeldWeaponHide.Register(EgoDefOf.AG_EgoSolemnLament, pawn => pawn != null
            && GameComponent_EgoSolemnLament.Instance?.Drawing(pawn) == true);
    }
}
