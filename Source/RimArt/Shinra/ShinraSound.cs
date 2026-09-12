using RimWorld;
using Verse;
using Verse.Sound;

namespace RimArt
{
    [DefOf]
    public static class ShinraSoundDefOf
    {
        public static SoundDef AG_ShinraCharge;
        public static SoundDef AG_ShinraRelease;

        static ShinraSoundDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(ShinraSoundDefOf)); }
    }

    /// <summary>
    /// The release rides the effect clock rather than the cast, so a paused or slowed gesture
    /// keeps the boom on the frame the dome appears. The charge is on the verb's soundCast and
    /// needs no code.
    /// </summary>
    internal static class ShinraSound
    {
        public static void Release(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map)) return;
            ShinraSoundDefOf.AG_ShinraRelease.PlayOneShot(new TargetInfo(cell, map, false));
        }
    }
}
