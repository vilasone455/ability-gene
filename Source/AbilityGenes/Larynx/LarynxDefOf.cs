using RimWorld;
using Verse;

namespace AbilityGenes
{
    [DefOf]
    public static class LarynxDefOf
    {
        /// <summary>Wear on the speaker's own neck. Severity is the whole cost model.</summary>
        public static HediffDef AG_LarynxWear;

        static LarynxDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LarynxDefOf));
        }
    }
}
