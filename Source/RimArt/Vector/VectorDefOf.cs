using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class VectorDefOf
    {
        /// <summary>The bill for manipulating vectors. Severity is the whole cost model.</summary>
        public static HediffDef AG_VectorStrain;

        /// <summary>
        /// Vector manipulation. Kept under the old reflection defName so that saves made before
        /// the rework still find the ability the reflex booster grants.
        /// </summary>
        public static AbilityDef AG_VectorReflection;

        static VectorDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VectorDefOf));
        }
    }
}
