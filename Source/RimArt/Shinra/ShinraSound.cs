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

    internal static class ShinraSound
    {
        public static void Charge(Map map, IntVec3 cell) =>
            ShinraSoundDefOf.AG_ShinraCharge.PlayOneShot(new TargetInfo(cell, map, false));

        public static void Release(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map)) return;
            ShinraSoundDefOf.AG_ShinraRelease.PlayOneShot(new TargetInfo(cell, map, false));
        }
    }
}
