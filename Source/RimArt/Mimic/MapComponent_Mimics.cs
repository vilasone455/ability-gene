using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws the decoys on this map, and enforces one per thrower.
    ///
    /// It holds no state of its own and saves nothing. The decoys are ordinary spawned things and
    /// the map already lists them, so a registry here would be a second copy of a list that can
    /// go stale. What this component is actually for is the frame hook: the three-phase renderer
    /// trick in <see cref="MimicRender"/> is only known to work from MapComponentUpdate, which is
    /// where <see cref="MapComponent_Arc"/> drives the same call.
    /// </summary>
    public class MapComponent_Mimics : MapComponent
    {
        private readonly List<Thing> drawing = new List<Thing>();

        public MapComponent_Mimics(Map map) : base(map) { }

        /// <summary>
        /// Ends any decoy this pawn already has standing.
        ///
        /// One per thrower, which is a design rule and a rendering one at the same time. Two
        /// decoys sharing a source would run that pawn's renderer twice in a frame and push the
        /// results back once, so the second would be drawn from the first one's position.
        /// </summary>
        public static void ClearFor(Pawn source, Map map)
        {
            if (source == null || map == null) return;

            List<Thing> existing = map.listerThings.ThingsOfDef(MimicDefOf.AG_MimicDecoy);
            for (int i = existing.Count - 1; i >= 0; i--)
            {
                if (existing[i] is MimicDecoy decoy && decoy.Source == source && !decoy.Destroyed)
                {
                    decoy.Destroy(DestroyMode.Vanish);
                }
            }
        }

        public override void MapComponentUpdate()
        {
            List<Thing> decoys = map.listerThings.ThingsOfDef(MimicDefOf.AG_MimicDecoy);
            if (decoys.Count == 0) return;

            // Copied out first: drawing a decoy cannot change the list, but the lister hands back
            // its own storage and a stale enumerator is not worth risking for one allocation-free
            // loop a frame.
            drawing.Clear();
            drawing.AddRange(decoys);

            for (int i = 0; i < drawing.Count; i++)
            {
                if (drawing[i] is MimicDecoy decoy && decoy.Spawned && !decoy.Position.Fogged(map))
                {
                    MimicRender.Draw(decoy);
                }
            }

            drawing.Clear();
        }
    }
}
