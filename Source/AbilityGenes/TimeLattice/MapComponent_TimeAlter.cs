using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Drives acceleration and draws afterimages.
    ///
    /// Acceleration needs no Harmony patch: this component ticks once per game tick and simply
    /// calls Pawn.DoTick() the extra times a multiplier owes. Stagnation is the opposite problem
    /// and is handled by the Thing.DoTick prefix the stasis field already installs.
    ///
    /// A pawn caught in a stasis field gets its DoTick prefixed to false, so the extra calls
    /// below are swallowed too - stopped beats accelerated with no special case.
    /// </summary>
    public class MapComponent_TimeAlter : MapComponent
    {
        private static readonly List<HediffComp_TimeAlter> working = new List<HediffComp_TimeAlter>();

        public MapComponent_TimeAlter(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (TimeAlterRegistry.ActiveCount == 0) return;

            working.Clear();
            working.AddRange(TimeAlterRegistry.Active);

            for (int i = 0; i < working.Count; i++)
            {
                HediffComp_TimeAlter comp = working[i];
                Pawn pawn = comp.Pawn;

                // Prune anything that has gone away, died, or left this map.
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map)
                {
                    if (pawn == null || pawn.Dead || !pawn.Spawned) TimeAlterRegistry.Drop(comp);
                    continue;
                }

                if (comp.parent == null || !pawn.health.hediffSet.hediffs.Contains(comp.parent))
                {
                    TimeAlterRegistry.Drop(comp);
                    continue;
                }

                int extra = comp.ExtraTicks;
                for (int t = 0; t < extra; t++)
                {
                    if (pawn.Dead || !pawn.Spawned) break;
                    pawn.DoTick();
                }
            }

            working.Clear();
        }

        public override void MapComponentUpdate()
        {
            if (TimeAlterRegistry.ActiveCount == 0) return;

            List<HediffComp_TimeAlter> active = TimeAlterRegistry.Active;
            for (int i = 0; i < active.Count; i++)
            {
                HediffComp_TimeAlter comp = active[i];
                Pawn pawn = comp.Pawn;
                if (pawn == null || !pawn.Spawned || pawn.Map != map) continue;
                if (!comp.Props.drawAfterimages || comp.IsStagnating) continue;

                DrawAfterimages(pawn, comp);
            }
        }

        /// <summary>
        /// One trailing copy per step of the multiplier, spaced back through the recorded
        /// positions so the tier is readable at a glance - two ghosts at x2, three at x4.
        ///
        /// RenderPawnAt exposes no alpha, so these are solid copies rather than fading ghosts.
        /// Drawn tightly spaced they read as a motion trail. If it looks wrong in play, turn
        /// drawAfterimages off in XML rather than rebuilding.
        /// </summary>
        private static void DrawAfterimages(Pawn pawn, HediffComp_TimeAlter comp)
        {
            IReadOnlyList<Vector3> trail = comp.Trail;
            if (trail.Count < 3) return;

            int ghosts = Mathf.Min(comp.ExtraTicks, 3);
            for (int g = 1; g <= ghosts; g++)
            {
                int index = trail.Count - 1 - (g * 2);
                if (index < 0) break;

                Vector3 pos = trail[index];
                if ((pos - pawn.DrawPos).sqrMagnitude < 0.02f) continue;

                pos.y = AltitudeLayer.Pawn.AltitudeFor() - 0.01f * g;
                pawn.Drawer.renderer.RenderPawnAt(pos, null, true);
            }
        }
    }
}
