using Verse;

namespace RimArt
{
    // The animation supplies all five weapon pieces. Hide only this weapon's ordinary draw: every def made as a FumaWeapon.
    [StaticConstructorOnStartup]
    public static class Patches_Fuma
    {
        static Patches_Fuma()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                if (def.thingClass != null && typeof(FumaWeapon).IsAssignableFrom(def.thingClass))
                    HeldWeaponHide.Register(def, pawn => pawn?.jobs?.curDriver is JobDriver_ThrowFuma driver && driver.Animated);
        }
    }
}
