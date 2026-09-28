using Verse;

namespace RimArt
{
    /// <summary>
    /// The render node of a stuck kunai: Minato's (<see cref="Hediff_EmbeddedKunai.sealedByMinato"/>) is
    /// drawn with his three-pronged kunai instead of the ordinary one. Placement and facing rules are in
    /// <see cref="PawnRenderNodeWorker_EmbeddedKunai"/>.
    /// </summary>
    public class PawnRenderNode_EmbeddedKunai : PawnRenderNode
    {
        public PawnRenderNode_EmbeddedKunai(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        protected override string TexPathFor(Pawn pawn) =>
            hediff is Hediff_EmbeddedKunai kunai && kunai.sealedByMinato ? KunaiDefaults.MinatoEmbeddedTexture : base.TexPathFor(pawn);
    }
}
