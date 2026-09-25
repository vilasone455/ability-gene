using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real pipe. The drawing previews are in DebugActions_BubblePipe; the pipe itself is granted by "Kits: grant kit...".</summary>
    public static class DebugActions_BubblePipeKit
    {
        /// <summary>Empties the jar of the clicked pawn's pipe, to test the no-soap button and the refill at water.</summary>
        [RimArtDebug("Bubble Pipe", "empty the soap jar", RimArtDebugKind.Pawn)]
        private static void Empty(Pawn pawn) => Set(pawn, 0f);

        /// <summary>Fills the jar of the clicked pawn's pipe.</summary>
        [RimArtDebug("Bubble Pipe", "fill the soap jar", RimArtDebugKind.Pawn)]
        private static void Fill(Pawn pawn) => Set(pawn, float.MaxValue);

        private static void Set(Pawn pawn, float blows)
        {
            CompBubblePipe pipe = CompBubblePipe.HeldBy(pawn);
            if (pipe == null)
            {
                Messages.Message("this pawn is not holding a bubble pipe", MessageTypeDefOf.RejectInput, false);
                return;
            }
            pipe.SetBlows(blows);
        }
    }
}
