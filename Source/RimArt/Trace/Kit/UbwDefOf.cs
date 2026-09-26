using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class UbwDefOf
    {
        /// <summary>The world's pocket map: one GenStep, <see cref="GenStep_UnlimitedBladeWorks"/>.</summary>
        public static MapGeneratorDef AG_UnlimitedBladeWorks;

        /// <summary>The same world's GenStep, read for its terrain and light.</summary>
        public static GenStepDef AG_UnlimitedBladeWorksField;

        /// <summary>The ability; it carries <see cref="UbwRules"/>.</summary>
        public static AbilityDef AG_Trace_UnlimitedBladeWorks;

        /// <summary>The chant: the caster stands and says the verses until the release.</summary>
        public static JobDef AG_UbwChant;

        static UbwDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(UbwDefOf));
        }
    }
}
