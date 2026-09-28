using RimWorld;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class ObitoDefOf
    {
        /// <summary>Obito's Echo-only gene (the fold organ): the Kamui dimension and the state of his four abilities.</summary>
        [MayRequireBiotech] public static GeneDef AG_InvoluteOrgan;

        public static AbilityDef AG_KamuiPhase;
        public static AbilityDef AG_KamuiWarp;
        public static AbilityDef AG_KamuiStore;
        public static AbilityDef AG_WoodRelease;

        public static EchoDef AG_Echo_Obito;

        static ObitoDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ObitoDefOf));
        }
    }

    /// <summary>
    /// The kit's numbers, read off the ability defs' comps (XML), with the comp defaults when a def
    /// lacks one. Kamui: Phase's pool and Store's release live on the gene, so the gene asks here.
    /// </summary>
    public static class ObitoRules
    {
        private static CompProperties_AbilityKamuiPhase phase;
        private static CompProperties_AbilityKamuiWarp warp;
        private static CompProperties_AbilityKamuiStore store;
        private static CompProperties_AbilityWoodRelease wood;

        public static CompProperties_AbilityKamuiPhase Phase =>
            phase ?? (phase = Find<CompProperties_AbilityKamuiPhase>(ObitoDefOf.AG_KamuiPhase) ?? new CompProperties_AbilityKamuiPhase());
        public static CompProperties_AbilityKamuiWarp Warp =>
            warp ?? (warp = Find<CompProperties_AbilityKamuiWarp>(ObitoDefOf.AG_KamuiWarp) ?? new CompProperties_AbilityKamuiWarp());
        public static CompProperties_AbilityKamuiStore Store =>
            store ?? (store = Find<CompProperties_AbilityKamuiStore>(ObitoDefOf.AG_KamuiStore) ?? new CompProperties_AbilityKamuiStore());
        public static CompProperties_AbilityWoodRelease Wood =>
            wood ?? (wood = Find<CompProperties_AbilityWoodRelease>(ObitoDefOf.AG_WoodRelease) ?? new CompProperties_AbilityWoodRelease());

        private static T Find<T>(AbilityDef def) where T : AbilityCompProperties
        {
            if (def?.comps == null) return null;
            foreach (AbilityCompProperties comp in def.comps)
                if (comp is T found) return found;
            return null;
        }
    }
}
