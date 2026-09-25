using Verse;

namespace RimArt
{
    /// <summary>
    /// The stone Mark throws. It is an ordinary item on the map; what makes it an end of a clap is
    /// the owner's gene holding it (<see cref="Gene_Anchors"/>). The stone checks that itself every
    /// rare tick and vanishes when nobody holds it any more - faded, taken back, used by a
    /// long-range swap, or its owner's gene gone - so no stone is ever left lying about that
    /// nobody can clap with.
    /// </summary>
    public class ClapStone : ThingWithComps
    {
        public Pawn owner;

        public override void TickRare()
        {
            base.TickRare();
            if (Destroyed) return;

            Gene_Anchors gene = AnchorUtility.GeneOf(owner);
            if (gene == null || !gene.HoldsStone(this)) Destroy(DestroyMode.Vanish);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
        }
    }
}
