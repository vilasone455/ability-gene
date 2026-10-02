using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class EgoDefOf
    {
        public static MentalStateDef AG_EgoCorroded;
        public static HediffDef AG_EgoExhausted;
        public static ThoughtDef AG_EgoOverclocked;
        public static JobDef AG_EgoCorrodedHold;
        public static JobDef AG_EgoOverclock;
        public static ThingDef AG_EgoMagicBullet;

        static EgoDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EgoDefOf));
        }
    }
}
