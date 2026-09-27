using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class GokuDefOf
    {
        public static AbilityDef AG_GokuSolarFlare;
        public static AbilityDef AG_GokuInstantTransmission;
        public static AbilityDef AG_GokuKamehameha;
        public static AbilityDef AG_GokuSpiritBomb;

        /// <summary>Solar Flare's and Instant Transmission's cast job: it starts the picture with the warmup.</summary>
        public static JobDef AG_CastGoku;
        /// <summary>The Kamehameha and Spirit Bomb channel: the caster stands until the cast lets it go.</summary>
        public static JobDef AG_GokuChannel;
        /// <summary>A colonist lending energy to a Spirit Bomb.</summary>
        public static JobDef AG_GokuLend;

        public static HediffDef AG_GokuFlashBlind;
        /// <summary>Slides a pawn the Kamehameha beam carries, over the picture's 0.5 s carry.</summary>
        public static ThingDef AG_GokuPushed;

        public static EchoDef AG_Echo_Goku;

        static GokuDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GokuDefOf));
        }
    }
}
