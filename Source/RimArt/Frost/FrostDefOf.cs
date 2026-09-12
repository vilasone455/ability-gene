using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class FrostDefOf
    {
        /// <summary>The thaw. Applied at full severity by the burst and shed by the hediff itself.</summary>
        public static HediffDef AG_Frostbound;

        /// <summary>The grenade in flight.</summary>
        public static ThingDef AG_FrostBombProjectile;

        static FrostDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FrostDefOf));
        }
    }
}
