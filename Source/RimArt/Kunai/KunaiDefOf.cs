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

        static KunaiDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(KunaiDefOf));
        }
    }
}
