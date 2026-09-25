using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class BankShotDefOf
    {
        /// <summary>The ricochet pistol.</summary>
        public static ThingDef AG_BankShot;
        /// <summary>Its charge mode, granted while it is equipped.</summary>
        public static AbilityDef AG_BankShot_Charge;

        static BankShotDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BankShotDefOf));
        }
    }
}
