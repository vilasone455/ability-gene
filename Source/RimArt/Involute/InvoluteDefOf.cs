using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class InvoluteDefOf
    {
        /// <summary>The permanent hole. Sits on the part it was rolled onto.</summary>
        public static HediffDef AG_InvoluteHole;

        /// <summary>The window in which the hole is connected to the volume.</summary>
        public static HediffDef AG_InvoluteVented;

        /// <summary>The ground aperture the carrier leaves behind when they fold.</summary>
        public static ThingDef AG_InvoluteAperture;

        static InvoluteDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(InvoluteDefOf));
        }
    }
}
