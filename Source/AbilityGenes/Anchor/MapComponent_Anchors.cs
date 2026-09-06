using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Draws the carrier's marks and tells them when one fades.
    ///
    /// Only the player's own colonists are scanned. A hostile carrier's marks are deliberately
    /// invisible: the mark is the thing the ability is planned around, and being able to read
    /// an enemy's plan off the map would give the whole gene away.
    /// </summary>
    public class MapComponent_Anchors : MapComponent
    {
        /// <summary>Marks fade on a scale of in-game hours, so a sweep a second is plenty.</summary>
        private const int SweepInterval = 60;

        private static readonly List<Anchor> DroppedBuffer = new List<Anchor>();

        public MapComponent_Anchors(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager.TicksGame % SweepInterval != 0) return;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Gene_Anchors gene = AnchorUtility.GeneOf(colonists[i]);
                if (gene == null) continue;

                DroppedBuffer.Clear();
                gene.Prune(DroppedBuffer);
                for (int j = 0; j < DroppedBuffer.Count; j++)
                {
                    Messages.Message(
                        "AG_AnchorFaded".Translate(colonists[i].LabelShort, DroppedBuffer[j].Label),
                        colonists[i], MessageTypeDefOf.NeutralEvent, false);
                }
            }
            DroppedBuffer.Clear();
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists.Count == 0) return;

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.realtimeSinceStartup * 3f);

            for (int i = 0; i < colonists.Count; i++)
            {
                Gene_Anchors gene = AnchorUtility.GeneOf(colonists[i]);
                if (gene == null) continue;

                List<Anchor> anchors = gene.AnchorsRaw;
                for (int j = 0; j < anchors.Count; j++)
                {
                    if (!gene.Holds(anchors[j])) continue;
                    DrawMark(anchors[j].CurrentCell, pulse);
                }
            }
        }

        private void DrawMark(IntVec3 cell, float pulse)
        {
            if (!cell.InBounds(map) || cell.Fogged(map)) return;

            Color glow = AnchorGraphics.MarkColor;
            glow.a *= pulse;
            Color edge = AnchorGraphics.EdgeColor;
            edge.a *= pulse;

            GenDraw.DrawFieldEdges(new List<IntVec3> { cell }, edge, null, null);

            Vector3 drawPos = cell.ToVector3Shifted();
            drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            AnchorGraphics.PropertyBlock.SetColor(ShaderPropertyIDs.Color, glow);

            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(drawPos, Quaternion.identity,
                new Vector3(AnchorGraphics.MarkScale, 1f, AnchorGraphics.MarkScale));

            Graphics.DrawMesh(MeshPool.plane10, matrix, AnchorGraphics.MarkMat, 0, null, 0,
                AnchorGraphics.PropertyBlock);
        }
    }
}
