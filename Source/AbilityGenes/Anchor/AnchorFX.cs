using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// The two effecters vanilla plays at each end of a skip. Both live in Core rather than in
    /// Royalty, so they resolve with only Biotech installed.
    ///
    /// Looked up by name and cached rather than through a DefOf, so a missing def leaves the
    /// teleport silent instead of throwing during startup.
    /// </summary>
    public static class AnchorFX
    {
        private static EffecterDef entryDef;
        private static EffecterDef exitDef;
        private static bool resolved;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            entryDef = DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_EntryNoDelay");
            exitDef = DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_ExitNoDelay");
        }

        public static void Entry(IntVec3 cell, Map map)
        {
            Play(EntryDef, cell, map);
        }

        public static void Exit(IntVec3 cell, Map map)
        {
            Play(ExitDef, cell, map);
        }

        private static EffecterDef EntryDef
        {
            get { Resolve(); return entryDef; }
        }

        private static EffecterDef ExitDef
        {
            get { Resolve(); return exitDef; }
        }

        private static void Play(EffecterDef def, IntVec3 cell, Map map)
        {
            if (def == null || map == null || !cell.InBounds(map)) return;

            Effecter effecter = def.Spawn(cell, map);
            effecter.Cleanup();
        }
    }
}
