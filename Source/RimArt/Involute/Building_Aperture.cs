using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The hole, standing on the ground where the carrier left it.
    ///
    /// It cannot be brought down. Damage aimed at it is not resisted, absorbed or reduced - it
    /// is passed on to the far side, which is the same thing the hole does when it is riding on
    /// a body. One rule, two places it can be.
    ///
    /// That makes hiding in the volume a decision rather than an escape: the door is open, and
    /// anything fired into it arrives in the room the carrier is standing in.
    /// </summary>
    public class Building_Aperture : Building
    {
        /// <summary>
        /// Whose hole this is. The pawn is stored rather than the gene because a Gene is not a
        /// reference the save system will hand back, and the gene is one lookup off the pawn.
        /// </summary>
        private Pawn owner;

        public Pawn Owner => owner;

        public Gene_Involute Gene => InvoluteUtility.GeneOf(owner);

        public void SetOwner(Pawn value)
        {
            owner = value;
        }

        /// <summary>
        /// The choke point, and it sits in front of everything - PreApplyDamage runs before the
        /// damage worker is even chosen, so a round, a swing, a blast and a fire all arrive
        /// here and all leave the same way.
        ///
        /// Reporting the damage as absorbed is what makes the aperture indestructible, and
        /// "absorbed" is doing no work of its own: nothing is resisted or reduced, it is simply
        /// no longer on this side.
        /// </summary>
        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PreApplyDamage(ref dinfo, out absorbed);
            if (absorbed) return;

            Gene_Involute gene = Gene;
            if (gene == null) return;

            InvoluteUtility.PassThrough(gene, dinfo, this);
            absorbed = true;
        }

        /// <summary>
        /// Drawn rather than left to graphicData, because a hole needs three layers and a
        /// ThingDef graphic is one. See <see cref="InvoluteGraphics"/> for why the first
        /// single-layer attempt was nearly invisible.
        /// </summary>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            InvoluteGraphics.Draw(drawLoc, InvoluteDefaults.ApertureScale, 1f);
        }

        /// <summary>
        /// An aperture outlives the carrier stepping away from it, but not the carrier. With no
        /// gene there is nothing on the far side to pass anything to, and an indestructible
        /// object with no owner would sit on the map forever.
        /// </summary>
        public override void TickRare()
        {
            base.TickRare();

            if (owner != null && !owner.Destroyed && Gene != null) return;

            InvoluteUtility.CloseFlash(Position, Map);
            Destroy(DestroyMode.Vanish);
        }

        public override string GetInspectString()
        {
            Gene_Involute gene = Gene;
            string owned = owner != null
                ? "AG_InvoluteApertureOwner".Translate(owner.LabelShortCap).Resolve()
                : null;

            string inside = gene != null && gene.Inside
                ? "AG_InvoluteApertureOccupied".Translate().Resolve()
                : "AG_InvoluteApertureIdle".Translate().Resolve();

            string basic = base.GetInspectString();
            return string.IsNullOrEmpty(basic) ? owned + "\n" + inside : basic + "\n" + owned + "\n" + inside;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
        }
    }
}
