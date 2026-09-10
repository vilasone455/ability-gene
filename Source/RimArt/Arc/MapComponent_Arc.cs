using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Holds the arcs currently in flight on this map and ticks them.
    ///
    /// A map component rather than a hediff comp because a run is not a state the carrier is in
    /// - it is a sequence being played at them, three seconds long, that has to survive them
    /// being knocked about mid-way. There is no patch here and nothing else in the mod reads it.
    /// </summary>
    public class MapComponent_Arc : MapComponent
    {
        private List<ArcRun> runs = new List<ArcRun>();

        public MapComponent_Arc(Map map) : base(map) { }

        /// <summary>Starts one arc, replacing any the same carrier already had running.</summary>
        public static void Begin(Pawn carrier, List<Pawn> chain)
        {
            Map map = carrier?.Map;
            if (map == null || chain == null || chain.Count == 0) return;

            MapComponent_Arc component = map.GetComponent<MapComponent_Arc>();
            if (component == null) return;

            component.runs.RemoveAll(run => run.Carrier == carrier);
            component.runs.Add(new ArcRun(carrier, chain));
        }

        /// <summary>Whether this pawn is in the middle of an arc right now.</summary>
        public static bool Running(Pawn pawn)
        {
            MapComponent_Arc component = pawn?.Map?.GetComponent<MapComponent_Arc>();
            if (component == null) return false;

            for (int i = 0; i < component.runs.Count; i++)
            {
                if (component.runs[i].Carrier == pawn) return true;
            }

            return false;
        }

        public override void MapComponentTick()
        {
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                if (!runs[i].Tick(map)) runs.RemoveAt(i);
            }
        }

        public override void MapComponentUpdate()
        {
            for (int i = 0; i < runs.Count; i++)
            {
                runs[i].Draw();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref runs, "runs", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && runs == null)
                runs = new List<ArcRun>();
        }
    }
}
