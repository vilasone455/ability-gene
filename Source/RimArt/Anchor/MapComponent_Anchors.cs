using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
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
            if (Find.CurrentMap != map) return;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists.Count == 0) return;

            var teleports = map.GetComponent<MapComponent_ClapTeleports>();
            for (int i = 0; i < colonists.Count; i++)
            {
                Gene_Anchors gene = AnchorUtility.GeneOf(colonists[i]);
                if (gene == null) continue;

                List<Anchor> anchors = gene.AnchorsRaw;
                for (int j = 0; j < anchors.Count; j++)
                {
                    if (!gene.Holds(anchors[j])) continue;
                    DrawMark(anchors[j], teleports);
                }
            }
        }

        /// <summary>
        /// The mark as a playing card: over the head of a marked pawn, flat on a marked tile inside
        /// a gold outline. While a clap against it is in its warmup the card flips with the rising
        /// ring, and the ring's drawing has the tile's outline.
        /// </summary>
        private void DrawMark(Anchor anchor, MapComponent_ClapTeleports teleports)
        {
            IntVec3 cell = anchor.CurrentCell;
            if (!cell.InBounds(map) || cell.Fogged(map)) return;

            float rising = -1f;
            bool clapping = teleports != null && teleports.Rising(anchor, out rising);
            Vector2 ground = ClapEnds.Ground(anchor, true);
            int suit = Mathf.Clamp(anchor.suit, 0, 2);
            float placed = (Find.TickManager.TicksGame - anchor.placedTick) / 60f;
            ClapTeleportGraphics.Mark(ground, ClapEnds.Kind(anchor), suit, suit, Time.realtimeSinceStartup, rising, placed);
            if (!anchor.IsOnPawn && !clapping) ClapTeleportGraphics.TileOutline(ground, 0.5f * SixPathsSlamTiming.Smooth(placed / MarkFlick.Settle));
        }
    }
}
