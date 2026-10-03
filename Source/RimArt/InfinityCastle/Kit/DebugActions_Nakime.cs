using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>The Nakime kit's gameplay entries in the RimArts debug window; the castle's own entries are under Infinity Castle.</summary>
    public static class DebugActions_Nakime
    {
        public static EchoDef Echo => DefDatabase<EchoDef>.GetNamed("AG_Echo_Nakime");

        [RimArtDebug("Nakime", "make pawn Nakime's Host (manifested, full charge)", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn)
        {
            EchoRecord record = EchoUtility.ForceHost(Echo, pawn);
            if (record == null)
            {
                Messages.Message("Nakime already has a Host.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            EchoUtility.EnsureAwakenGenes(Echo, pawn);
            DebugActions_Echo.Fill();
            EchoUtility.Manifest(record);
        }

        [RimArtDebug("Nakime", "release the castle now (the Host's cast)", RimArtDebugKind.Pawn)]
        private static void Release(Pawn pawn)
        {
            InfinityCastleCast cast = GameComponent_InfinityCastle.Instance?.For(pawn);
            if (cast == null || !cast.Standing) { Messages.Message("That pawn has no castle standing.", MessageTypeDefOf.RejectInput, false); return; }
            cast.releaseOrdered = true;
        }

        [RimArtDebug("Nakime", "sunlight: burn once (pawn with the castle organ)", RimArtDebugKind.Pawn)]
        private static void Burn(Pawn pawn)
        {
            Gene_CastleOrgan gene = pawn.genes?.GetFirstGeneOfType<Gene_CastleOrgan>();
            if (gene == null) { Messages.Message("That pawn has no castle organ.", MessageTypeDefOf.RejectInput, false); return; }
            gene.Burn(Find.TickManager.TicksGame);
        }
    }
}
