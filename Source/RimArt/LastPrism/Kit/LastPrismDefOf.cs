using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class LastPrismDefOf
    {
        public static ThingDef AG_LastPrism;
        public static AbilityDef AG_LastPrism_Fire;
        public static JobDef AG_CastLastPrism;

        static LastPrismDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LastPrismDefOf));
        }
    }
}
