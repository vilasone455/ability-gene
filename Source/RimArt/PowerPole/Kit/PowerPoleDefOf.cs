using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class PowerPoleDefOf
    {
        public static ThingDef AG_PowerPole;
        /// <summary>Carries the wielder through Vault Strike's arc.</summary>
        public static ThingDef AG_PowerPoleVault;
        /// <summary>Slides a pawn that Extend Thrust is pushing, so it moves with the pole's tip.</summary>
        public static ThingDef AG_PowerPoleCarried;
        /// <summary>The moment after a vault's landing while the pole is pinned on the target and then retracts.</summary>
        public static JobDef AG_PowerPoleRecover;

        static PowerPoleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PowerPoleDefOf));
        }
    }
}
