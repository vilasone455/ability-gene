using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class SatoDefOf
    {
        /// <summary>The passive's carrier; its AjinExtension holds the Reset numbers.</summary>
        public static TraitDef AG_Ajin;
        public static HediffDef AG_AjinReset;
        public static HediffDef AG_SatoGameMark;

        public static AbilityDef AG_SatoSever;
        public static AbilityDef AG_SatoHeadshotReset;
        public static AbilityDef AG_SatoGrenadeReset;
        public static AbilityDef AG_SatoTheGame;
        public static AbilityDef AG_SatoBlackGhost;

        public static ThingDef AG_AjinAnchor;
        public static ThingDef AG_AjinRemains;
        public static ThingDef AG_TornLimb;
        public static ThingDef AG_BlackGhost;
        [DefAlias("AG_BlackGhost")]
        public static PawnKindDef AG_BlackGhostKind;

        public static JobDef AG_BlackGhostTear;

        public static EchoDef AG_Echo_Sato;
        public static HediffDef AG_EchoManifest_Sato;

        static SatoDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SatoDefOf));
        }
    }
}
