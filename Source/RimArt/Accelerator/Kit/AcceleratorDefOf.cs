using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class AcceleratorDefOf
    {
        public static AbilityDef AG_VectorShove;
        public static AbilityDef AG_VectorPlasma;
        public static AbilityDef AG_VectorFlick;
        public static AbilityDef AG_VectorSurge;
        public static JobDef AG_VectorPlasmaChannel;
        public static ThingDef AG_VectorThrown;
        public static EchoDef AG_Echo_Accelerator;

        static AcceleratorDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AcceleratorDefOf));
        }
    }
}
