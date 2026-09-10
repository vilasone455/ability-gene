using System.Collections.Generic;
using Verse;

namespace AbilityGenes
{
    /// <summary>Saved feeding flights. Claims are derived from the runs, including after reload.</summary>
    public class MapComponent_Carrion : MapComponent
    {
        private List<CarrionRun> runs = new List<CarrionRun>();
        public MapComponent_Carrion(Map map) : base(map) { }

        public bool Running(Pawn pawn) => runs.Exists(run => run.Carrier == pawn);
        public bool Claimed(Corpse corpse) => runs.Exists(run => run.Corpse == corpse);

        public bool Begin(Pawn pawn, Corpse corpse)
        {
            Gene_Dispersal gene = pawn.genes?.GetFirstGeneOfType<Gene_Dispersal>();
            if (gene == null || !gene.Active || !gene.HasCharge || Running(pawn)
                || Claimed(corpse) || !CarrionRun.ValidCorpse(corpse, map)) return false;
            runs.Add(new CarrionRun(pawn, corpse));
            gene.Spend();
            DispersalFX.Arrive(pawn.DrawPos, map);
            return true;
        }

        public override void MapComponentTick()
        {
            for (int i = runs.Count - 1; i >= 0; i--)
                if (!runs[i].Tick(map)) runs.RemoveAt(i);
        }

        public override void MapComponentUpdate()
        {
            foreach (CarrionRun run in runs) run.Draw();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref runs, "carrionRuns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && runs == null)
                runs = new List<CarrionRun>();
        }
    }
}
