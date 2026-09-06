using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Owns bubble lifetime and drawing. A MapComponent is not a Thing, so it keeps
    /// ticking while everything inside the bubble is frozen — including the caster.
    /// Without this the bubble could never expire.
    /// </summary>
    public class MapComponent_TimeBubbles : MapComponent
    {
        private List<TimeBubble> bubbles = new List<TimeBubble>();

        /// <summary>
        /// The ground outline under the dome is drawn at this fraction of the dome's own
        /// alpha. The field freezes your own pawns too, so the exact cell boundary has to
        /// be readable, not merely suggested.
        /// </summary>
        private const float EdgeAlphaFactor = 0.8f;

        public MapComponent_TimeBubbles(Map map) : base(map) { }

        public void AddBubble(IntVec3 center, float radius, int ticks, bool invulnerable, Color color)
        {
            TimeBubble bubble = new TimeBubble(map, center, radius, ticks, invulnerable, color);
            bubbles.Add(bubble);
            TimeBubbleRegistry.Register(bubble);
        }

        public override void MapComponentTick()
        {
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                TimeBubble bubble = bubbles[i];
                bubble.ticksRemaining--;
                if (bubble.ticksRemaining <= 0)
                {
                    TimeBubbleRegistry.Deregister(bubble);
                    bubbles.RemoveAt(i);
                }
            }
        }

        public override void MapComponentUpdate()
        {
            for (int i = 0; i < bubbles.Count; i++)
            {
                TimeBubble bubble = bubbles[i];

                float alpha = bubble.DrawAlpha();
                if (alpha <= 0f) continue;

                Color tint = bubble.color;
                tint.a *= alpha;

                Color edge = tint;
                edge.a *= EdgeAlphaFactor;
                GenDraw.DrawFieldEdges(bubble.EdgeCells, edge, null, null);

                Vector3 drawPos = bubble.center.ToVector3Shifted();
                drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                TimeBubbleGraphics.PropertyBlock.SetColor(ShaderPropertyIDs.Color, tint);

                float diameter = bubble.radius * 2f * TimeBubbleGraphics.TextureRingSizeFactor;
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(diameter, 1f, diameter));

                Graphics.DrawMesh(MeshPool.plane10, matrix, TimeBubbleGraphics.FieldMat, 0, null, 0,
                    TimeBubbleGraphics.PropertyBlock);
            }
        }

        public override void FinalizeInit()
        {
            // Rebuild the static registry after a load.
            for (int i = 0; i < bubbles.Count; i++)
            {
                bubbles[i].Map = map;
                bubbles[i].RecacheRadius();
                TimeBubbleRegistry.Register(bubbles[i]);
            }
        }

        public override void MapRemoved()
        {
            TimeBubbleRegistry.DeregisterMap(map);
            bubbles.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref bubbles, "bubbles", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && bubbles == null)
            {
                bubbles = new List<TimeBubble>();
            }
        }
    }
}
