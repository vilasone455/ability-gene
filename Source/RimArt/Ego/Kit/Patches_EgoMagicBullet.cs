using Verse;

namespace RimArt
{
    /// <summary>
    /// Core does not draw the Magic Bullet while its picture draws the rifle: during a shot's picture, and while the gun
    /// has its wielder (corroded or overclocking), when <see cref="EgoMagicBulletCorrosion.DrawCorroded"/> draws it.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patches_EgoMagicBullet
    {
        static Patches_EgoMagicBullet() => HeldWeaponHide.Register(EgoDefOf.AG_EgoMagicBullet, pawn => pawn != null
            && (pawn.MentalState is MentalState_EgoCorroded || pawn.jobs?.curDriver is JobDriver_EgoOverclock
                || GameComponent_EgoMagicBullet.Instance?.Drawing(pawn) == true));
    }
}
