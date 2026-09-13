using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class MakibishiDefOf
    {
        /// <summary>The item: pouch ammunition, one handful per throw.</summary>
        public static ThingDef AG_Makibishi;

        /// <summary>The worn pouch, and the only source of the scatter ability.</summary>
        public static ThingDef AG_MakibishiPouch;

        public static ThingDef AG_MakibishiProjectile;

        /// <summary>Spikes on one cell. Spawned by MakibishiPatch, gone after 30 s.</summary>
        public static ThingDef AG_MakibishiSpikes;

        public static AbilityDef AG_ScatterMakibishi;

        /// <summary>Movement penalty from stepping on a spike. -30%, -20%, -10%, 5 s each.</summary>
        public static HediffDef AG_PuncturedFoot;

        /// <summary>Core's feet group. Not in BodyPartGroupDefOf.</summary>
        public static BodyPartGroupDef Feet;

        static MakibishiDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MakibishiDefOf));
        }
    }
}
