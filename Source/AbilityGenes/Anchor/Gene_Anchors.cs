using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Holds the carrier's marks. Storage lives on the gene rather than on a hediff or a map
    /// component because marks belong to a person: they save and load with the pawn, they
    /// travel with them between maps, and they are gone the moment the gene is.
    ///
    /// Marks are pruned lazily rather than ticked. Every read filters out anything that has
    /// faded or become unreachable, so nothing has to run per tick to keep the list honest;
    /// <see cref="MapComponent_Anchors"/> only sweeps periodically to tell the player.
    /// </summary>
    public class Gene_Anchors : Gene
    {
        private List<Anchor> anchors = new List<Anchor>();

        private AnchorGeneExtension Ext => def.GetModExtension<AnchorGeneExtension>();

        public int MaxAnchors
        {
            get
            {
                AnchorGeneExtension ext = Ext;
                return ext != null ? ext.maxAnchors : 3;
            }
        }

        public int MarkDurationTicks
        {
            get
            {
                AnchorGeneExtension ext = Ext;
                return ext != null ? ext.markDurationTicks : 60000;
            }
        }

        public bool BansWeapons
        {
            get
            {
                AnchorGeneExtension ext = Ext;
                return ext == null || ext.banWeapons;
            }
        }

        /// <summary>
        /// The raw list, including anything that has quietly stopped holding. Only the sweep in
        /// <see cref="MapComponent_Anchors"/> is allowed to remove from it - every other reader
        /// filters without mutating, because the draw path and the gizmo checks both run every
        /// frame and pruning there would drop marks before anyone could be told they had gone.
        /// </summary>
        public List<Anchor> AnchorsRaw => anchors;

        private Map HeldMap => pawn != null ? pawn.MapHeld : null;

        public bool Holds(Anchor anchor)
        {
            return anchor.StillHolds(HeldMap, MarkDurationTicks);
        }

        public int LiveCount
        {
            get
            {
                Map map = HeldMap;
                int duration = MarkDurationTicks;
                int count = 0;
                for (int i = 0; i < anchors.Count; i++)
                {
                    if (anchors[i].StillHolds(map, duration)) count++;
                }
                return count;
            }
        }

        public bool IsMarked(LocalTargetInfo target)
        {
            return AnchorFor(target) != null;
        }

        public Anchor AnchorFor(LocalTargetInfo target)
        {
            Map map = HeldMap;
            int duration = MarkDurationTicks;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].Marks(target) && anchors[i].StillHolds(map, duration)) return anchors[i];
            }
            return null;
        }

        /// <summary>The two oldest marks that still hold, in the order they were placed.</summary>
        public bool TryGetPair(out Anchor first, out Anchor second)
        {
            first = null;
            second = null;

            Map map = HeldMap;
            int duration = MarkDurationTicks;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (!anchors[i].StillHolds(map, duration)) continue;

                if (first == null) first = anchors[i];
                else { second = anchors[i]; return true; }
            }
            return false;
        }

        public void Add(Anchor anchor)
        {
            anchors.Add(anchor);
        }

        public void Remove(Anchor anchor)
        {
            anchors.Remove(anchor);
        }

        /// <summary>
        /// Drops marks that no longer hold and collects what was dropped so the sweep can say
        /// so. This is the only place anchors are removed for expiry.
        /// </summary>
        public void Prune(List<Anchor> dropped)
        {
            Map map = HeldMap;
            int duration = MarkDurationTicks;
            for (int i = anchors.Count - 1; i >= 0; i--)
            {
                if (anchors[i].StillHolds(map, duration)) continue;

                if (dropped != null) dropped.Add(anchors[i]);
                anchors.RemoveAt(i);
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            anchors.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref anchors, "anchors", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && anchors == null)
            {
                anchors = new List<Anchor>();
            }
        }
    }
}
