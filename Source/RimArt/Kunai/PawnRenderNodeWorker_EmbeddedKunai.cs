using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws a stuck kunai on the pawn at the wound anchor of its body part.
    ///
    /// The HediffDef gives two render nodes, one under the body and one under the head, because
    /// head anchors are offsets from the head and body anchors are offsets from the body. Each node
    /// draws only when its part is on its side, and only for a facing where the part has a usable
    /// anchor (a kunai in the back of the left arm is not drawn while the pawn faces east). Pawns
    /// without a body type - animals, mechs - have no anchors, so nothing is drawn on them.
    ///
    /// Several kunai near one anchor are spread by the hediff's rolled lean and a small offset from
    /// its load ID.
    /// </summary>
    public class PawnRenderNodeWorker_EmbeddedKunai : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms)) return false;
            if (!(node.hediff is Hediff_EmbeddedKunai kunai) || !kunai.Visible || node.bodyPart == null) return false;

            bool headPart = node.bodyPart.IsInGroup(BodyPartGroupDefOf.FullHead);
            bool headNode = node.Props.parentTagDef != null && node.Props.parentTagDef.defName == "Head";
            if (headPart != headNode) return false;

            foreach (BodyTypeDef.WoundAnchor anchor in PawnDrawUtility.FindAnchors(parms.pawn, node.bodyPart))
                if (PawnDrawUtility.AnchorUsable(parms.pawn, anchor, parms.facing)) return true;
            return false;
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            int id = node.hediff?.loadID ?? 0;
            offset.x += ((id * 37) % 9 - 4) * 0.012f;
            offset.z += ((id * 53) % 9 - 4) * 0.012f;
            return offset;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            float lean = (node.hediff as Hediff_EmbeddedKunai)?.drawAngle ?? 0f;
            if (parms.facing == Rot4.West) lean = -lean;
            return base.RotationFor(node, parms) * Quaternion.AngleAxis(lean, Vector3.up);
        }
    }
}
