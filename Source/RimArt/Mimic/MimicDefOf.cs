using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class MimicDefOf
    {
        /// <summary>The projection standing on the map. Spawned by the projectile, never crafted.</summary>
        public static ThingDef AG_MimicDecoy;

        static MimicDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MimicDefOf));
        }
    }
}
