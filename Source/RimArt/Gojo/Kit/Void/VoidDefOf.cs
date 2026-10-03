using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class VoidDefOf
    {
        /// <summary>Unlimited Void, the ability; its numbers are <see cref="CompProperties_AbilityUnlimitedVoid"/>.</summary>
        public static AbilityDef AG_GojoUnlimitedVoid;

        /// <summary>The domain's pocket map: one GenStep, <see cref="GenStep_UnlimitedVoid"/>.</summary>
        public static MapGeneratorDef AG_UnlimitedVoid;

        /// <summary>Void overload: <see cref="Hediff_VoidOverload"/>, the consciousness cap that follows the domain.</summary>
        public static HediffDef AG_VoidOverload;

        /// <summary>Void-scarred: consciousness and sight down for 2 days, for a pawn the overload put down.</summary>
        public static HediffDef AG_VoidScarred;

        static VoidDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VoidDefOf));
        }
    }
}
