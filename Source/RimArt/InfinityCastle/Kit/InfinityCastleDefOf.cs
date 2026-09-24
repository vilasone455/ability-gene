using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class InfinityCastleDefOf
    {
        /// <summary>The castle's pocket map: one GenStep, <see cref="GenStep_InfinityCastle"/>.</summary>
        public static MapGeneratorDef AG_InfinityCastle;

        /// <summary>The same castle's GenStep, read for its seed and room ranges.</summary>
        public static GenStepDef AG_InfinityCastleRooms;

        static InfinityCastleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(InfinityCastleDefOf));
        }
    }
}
