using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class DispersalDefOf
    {
        /// <summary>
        /// The few seconds at the far end where the carrier is not quite a person again.
        /// Added by <see cref="Scatter"/> and by nothing else.
        /// </summary>
        public static HediffDef AG_Reassembling;

        /// <summary>
        /// The flock the carrier travels inside during a murder. A Core PawnFlyer with this
        /// mod's own thingClass on it and nothing else changed.
        /// </summary>
        public static ThingDef AG_DispersalFlock;

        static DispersalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DispersalDefOf));
        }
    }
}
