using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class PanoplyDefOf
    {
        /// <summary>The blade standing in the ground.</summary>
        public static ThingDef AG_PanoplyBlade;

        /// <summary>A blade on its way down.</summary>
        public static ThingDef AG_PanoplyBladeFalling;

        /// <summary>A blade on its way to a hand.</summary>
        public static ThingDef AG_PanoplyBladeFlying;

        /// <summary>A blade that has been loosed at something.</summary>
        public static ThingDef AG_PanoplyBladeShot;

        /// <summary>Severity is the blade count. Nothing else writes to it.</summary>
        public static HediffDef AG_PanoplyDebt;

        /// <summary>What a grasped blade becomes once it is in a hand: ordinary steel.</summary>
        public static ThingDef MeleeWeapon_LongSword;

        static PanoplyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PanoplyDefOf));
        }
    }
}
