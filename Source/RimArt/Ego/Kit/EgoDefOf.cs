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
        public static JobDef AG_EgoCorrodedWalk;
        public static ThingDef AG_EgoMagicBullet;
        public static ThingDef AG_EgoSolemnLament;
        public static HediffDef AG_EgoButterfly;
        public static ThingDef AG_EgoParadiseLost;
        public static HediffDef AG_EgoParadiseLostSlow;
        public static ThoughtDef AG_EgoParadiseLostSanity;
        public static DamageDef AG_EgoPale;
        public static ThingDef AG_EgoMimicry;

        static EgoDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EgoDefOf));
        }
    }
}
