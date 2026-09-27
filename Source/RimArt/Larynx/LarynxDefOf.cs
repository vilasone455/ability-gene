using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class LarynxDefOf
    {
        /// <summary>Wear on the speaker's own neck. Severity is the whole cost model.</summary>
        public static HediffDef AG_LarynxWear;

        /// <summary>Drinking one herbal medicine as throat syrup (<see cref="JobDriver_DrinkThroatSyrup"/>).</summary>
        public static JobDef AG_DrinkThroatSyrup;

        static LarynxDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LarynxDefOf));
        }
    }
}
