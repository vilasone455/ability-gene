using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class ShadowPlexusDefOf
    {
        public static AbilityDef AG_ShadowImitation;
        public static AbilityDef AG_ShadowSeam;
        public static AbilityDef AG_ShadowGrasp;
        public static AbilityDef AG_ShadowDouble;
        public static AbilityDef AG_ShadowNeckBind;

        /// <summary>Neck bind's suffocation on the held pawn. Severity is the meter.</summary>
        public static HediffDef AG_ShadowChoked;

        public static JobDef AG_CastShadowPlexus;
        /// <summary>Neck bind's channel: the same driver, but the player can interrupt it.</summary>
        public static JobDef AG_CastShadowNeckBind;

        public static EchoDef AG_Echo_Shikamaru;

        static ShadowPlexusDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ShadowPlexusDefOf));
        }
    }
}
