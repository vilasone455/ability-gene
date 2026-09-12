using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class ToyCarDefOf
    {
        /// <summary>The vehicle itself, spawned by the deploy ability.</summary>
        public static ThingDef AG_ToyCar;

        /// <summary>The worn device, and the only thing that grants the deploy ability.</summary>
        public static ThingDef AG_ControlRig;

        static ToyCarDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ToyCarDefOf));
        }
    }
}
