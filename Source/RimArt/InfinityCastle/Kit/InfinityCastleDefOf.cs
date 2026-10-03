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

        /// <summary>Nakime's ability, from her Echo: <see cref="CompAbilityEffect_InfinityCastle"/>.</summary>
        public static AbilityDef AG_Nakime_InfinityCastle;

        /// <summary>The carrier on the dais while her castle stands: <see cref="JobDriver_CastlePlay"/>.</summary>
        public static JobDef AG_CastlePlay;

        /// <summary>Nakime's gene, given on awakening: sunlight burns her (<see cref="Gene_CastleOrgan"/>).</summary>
        [MayRequireBiotech]
        public static GeneDef AG_CastleOrgan;

        static InfinityCastleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(InfinityCastleDefOf));
        }
    }
}
