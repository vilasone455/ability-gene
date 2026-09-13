using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class RetrievalHookDefOf
    {
        /// <summary>The worn belt, and the only source of the hook ability.</summary>
        public static ThingDef AG_RetrievalHookBelt;

        public static AbilityDef AG_RetrievalHook;

        /// <summary>Holds the wearer still while the net flies and the target is dragged.</summary>
        public static JobDef AG_RetrievalHookPull;

        /// <summary>Stationary reload work.</summary>
        public static JobDef AG_ReelInTether;

        static RetrievalHookDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RetrievalHookDefOf));
        }
    }
}
