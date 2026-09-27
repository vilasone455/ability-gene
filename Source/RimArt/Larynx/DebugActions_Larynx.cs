using Verse;

namespace RimArt
{
    /// <summary>Inumaki's throat, for play tests. Make him a Host from the "Echo" kit first.</summary>
    public static class DebugActions_Larynx
    {
        [RimArtDebug("Inumaki", "throat +30 %", RimArtDebugKind.Pawn)]
        private static void AddWear(Pawn pawn) => LarynxUtility.ApplyWear(pawn, 0.3f);

        [RimArtDebug("Inumaki", "clear throat", RimArtDebugKind.Pawn)]
        private static void ClearWear(Pawn pawn)
        {
            Hediff wear = pawn.health?.hediffSet?.GetFirstHediffOfDef(LarynxDefOf.AG_LarynxWear);
            if (wear != null) pawn.health.RemoveHediff(wear);
        }
    }
}
