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

        private static readonly Color EdgeColor = new Color(0.45f, 0.85f, 1f, 0.7f);

        public MapComponent_TimeBubbles(Map map) : base(map) { }

        public void AddBubble(IntVec3 center, float radius, int ticks, bool invulnerable)
        {
            TimeBubble bubble = new TimeBubble(map, center, radius, ticks, invulnerable);
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
                GenDraw.DrawFieldEdges(bubbles[i].EdgeCells, EdgeColor, null, null);
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
