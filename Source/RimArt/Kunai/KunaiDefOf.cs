using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class KunaiDefOf
    {
        /// <summary>The item: belt ammunition, and what a thrown kunai drops as.</summary>
        public static ThingDef AG_Kunai;

        /// <summary>The worn belt, and the only source of the throw ability.</summary>
        public static ThingDef AG_KunaiBelt;

        public static ThingDef AG_KunaiProjectile;

        public static AbilityDef AG_ThrowKunai;

        /// <summary>A kunai stuck in a body part. Holds the kunai until it is pulled or dropped.</summary>
        public static HediffDef AG_EmbeddedKunai;

        public static JobDef AG_PullKunai;

        static KunaiDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(KunaiDefOf));
        }
    }
}
