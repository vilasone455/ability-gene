using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws the carrier's stones and tells them when one fades.
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

            var flicks = map.GetComponent<MapComponent_MarkFlicks>();
            float seconds = Time.realtimeSinceStartup;
            for (int i = 0; i < colonists.Count; i++)
            {
                Gene_Anchors gene = AnchorUtility.GeneOf(colonists[i]);
                if (gene == null) continue;

                List<Anchor> anchors = gene.AnchorsRaw;
                for (int j = 0; j < anchors.Count; j++)
                {
                    // A carried stone has no glow; it is back when the stone is on the ground.
                    if (!gene.Holds(anchors[j]) || !anchors[j].Usable) continue;
                    DrawStone(anchors[j], flicks, seconds);
                }
            }
        }

        /// <summary>
        /// The charged stone at rest (StoneThrowGraphics.Resting): a teal glow that breathes and a glint
        /// now and then; the item draws the stone itself. It flares while a cast is taking it back. A
        /// stone still skidding in and settling is the throw's to draw.
        /// </summary>
        private void DrawStone(Anchor anchor, MapComponent_MarkFlicks flicks, float seconds)
        {
            IntVec3 cell = anchor.CurrentCell;
            if (!cell.InBounds(map) || cell.Fogged(map)) return;
            if (flicks != null && flicks.Landing(anchor)) return;
            StoneThrowGraphics.Resting(ClapEnds.Ground(anchor, true), seconds, 1f, flicks?.Flare(anchor) ?? 0f,
                anchor.stone.thingIDNumber % 997, false);
        }
    }
}
