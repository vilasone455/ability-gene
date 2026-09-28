using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>The Todo kit's gameplay entries in the RimArts debug window; the picture previews are in DebugActions_TodoPictures.</summary>
    public static class DebugActions_Todo
    {
        public static EchoDef Echo => DefDatabase<EchoDef>.GetNamed("AG_Echo_Todo");

        [RimArtDebug("Todo", "make pawn Todo's Host (manifested, full charge)", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn)
        {
            EchoRecord record = EchoUtility.ForceHost(Echo, pawn);
            if (record == null)
            {
                Messages.Message("Todo already has a Host.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            DebugActions_Echo.Fill();
            EchoUtility.Manifest(record);
        }

        [RimArtDebug("Todo", "open the Black Flash window (as if a clap just landed)", RimArtDebugKind.Pawn)]
        private static void OpenWindow(Pawn pawn) => AnchorUtility.GeneOf(pawn)?.NoteSwap();
    }
}
