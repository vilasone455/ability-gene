using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class ItachiDefOf
    {
        /// <summary>False Face: the victim attacks its nearest ally, believing it is Itachi.</summary>
        public static MentalStateDef AG_FalseFace;

        /// <summary>The Susanoo standing round him: Yata Mirror and Totsuka Blade live on its comp.</summary>
        public static HediffDef AG_Susanoo;
        /// <summary>What the Susanoo leaves behind: his illness, for a few hours.</summary>
        public static HediffDef AG_SusanooDrained;

        /// <summary>Crow Dispersal and Carrion, granted by his Echo while manifested.</summary>
        public static AbilityDef AG_DispersalMurder;
        public static AbilityDef AG_DispersalCarrion;
        public static AbilityDef AG_ItachiFalseFace;
        public static AbilityDef AG_ItachiSusanoo;

        public static FleckDef AG_ItachiGlint;
        public static FleckDef AG_ItachiFlash;
        public static FleckDef AG_ItachiFalseFaceMark;

        static ItachiDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ItachiDefOf));
        }
    }
}
