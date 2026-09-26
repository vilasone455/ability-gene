using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class EchoDefOf
    {
        public static ThingDef AG_EchoDevice;
        public static HediffDef AG_EchoCollapse;
        public static LetterDef AG_EchoAwakening;

        static EchoDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(EchoDefOf));
    }
}
