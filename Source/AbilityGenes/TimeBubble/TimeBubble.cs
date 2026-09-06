using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// One active sphere of stopped time. Everything spawned inside is skipped by
    /// <see cref="Verse.Thing.DoTick"/>, which is the single dispatch point for Tick,
    /// TickInterval, TickRare, TickLong and held-contents ticking. That means pawns,
    /// projectiles in flight, fire and gas all halt together.
    ///
    /// The bubble deliberately does not exempt the caster: they are at the centre and
    /// freeze along with everyone else. Lifetime is therefore counted by
    /// <see cref="MapComponent_TimeBubbles"/>, which is not a Thing and keeps ticking.
    /// </summary>
    public class TimeBubble : IExposable
    {
        public IntVec3 center;
        public float radius;
        public int ticksRemaining;
        public int totalTicks;
        public bool invulnerable = true;

        // Cached so the hot path compares squared distances without a sqrt.
        private float radiusSquared;
        private List<IntVec3> edgeCells;

        public Map Map { get; set; }

        public TimeBubble() { }

        public TimeBubble(Map map, IntVec3 center, float radius, int ticks, bool invulnerable)
        {
            Map = map;
            this.center = center;
            this.radius = radius;
            this.ticksRemaining = ticks;
            this.totalTicks = ticks;
            this.invulnerable = invulnerable;
            RecacheRadius();
        }

        public void RecacheRadius()
        {
            radiusSquared = radius * radius;
            edgeCells = null;
        }

        public bool Contains(IntVec3 cell)
        {
            return (cell - center).LengthHorizontalSquared <= radiusSquared;
        }

        public List<IntVec3> EdgeCells
        {
            get
            {
                if (edgeCells == null)
                {
                    edgeCells = new List<IntVec3>();
                    foreach (IntVec3 c in GenRadial.RadialCellsAround(center, radius, true))
                    {
                        if (c.InBounds(Map)) edgeCells.Add(c);
                    }
                }
                return edgeCells;
            }
        }

        /// <summary>
        /// Eases the dome in and out so it does not pop into existence. Steps once per
        /// tick rather than per frame, which is not noticeable at these durations.
        /// </summary>
        public float DrawAlpha()
        {
            const int FadeInTicks = 15;
            const int FadeOutTicks = 45;

            int elapsed = totalTicks - ticksRemaining;
            if (elapsed < FadeInTicks) return elapsed / (float)FadeInTicks;
            if (ticksRemaining < FadeOutTicks) return ticksRemaining / (float)FadeOutTicks;
            return 1f;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref radius, "radius", 6.9f);
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_Values.Look(ref totalTicks, "totalTicks", 0);
            Scribe_Values.Look(ref invulnerable, "invulnerable", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) RecacheRadius();
        }
    }
}
