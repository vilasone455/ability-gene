using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Draws the hole while it is connected.
    ///
    /// It is drawn beside the pawn rather than on the part it is actually in. PawnRenderNode
    /// does not hand out a screen position for a left hand, and chasing one across every body
    /// type, rotation and posture would be a great deal of work for a dot less than half a cell
    /// across. The health tab is where the part is named; this only has to say the hole is open.
    ///
    /// Only the player's own colonists are drawn, for the same reason the anchor organ only
    /// draws theirs: a hostile carrier's open hole would tell the player exactly which body part
    /// not to shoot at.
    /// </summary>
    public class MapComponent_Involute : MapComponent
    {
        public MapComponent_Involute(Map map) : base(map) { }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (InvoluteRegistry.ActiveCount == 0) return;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists.Count == 0) return;

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (!InvoluteRegistry.IsVented(pawn)) continue;
                if (pawn.Position.Fogged(map)) continue;

                DrawHole(pawn);
            }
        }

        private void DrawHole(Pawn pawn)
        {
            InvoluteGraphics.Draw(pawn.DrawPos + InvoluteDefaults.BodyHoleOffset,
                InvoluteDefaults.BodyHoleScale, 1f);
        }
    }
}
