using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Holds the carrier's stones and claps. Storage lives on the gene rather than on a hediff or a
    /// map component because both belong to a person: they save and load with the pawn and they are
    /// gone the moment the gene is.
    ///
    /// Stones are pruned lazily rather than ticked. Every read filters out anything that has faded
    /// or gone, so nothing has to run per tick to keep the list honest;
    /// <see cref="MapComponent_Anchors"/> sweeps periodically to tell the player and to clear the
    /// stone items off the map. Claps are ticked, the same way Gene_Dispersal grows its charges.
    /// </summary>
    public class Gene_Anchors : Gene
    {
        private List<Anchor> anchors = new List<Anchor>();

        private int charges = -1;
        private int rechargeProgress;

        /// <summary>Guards the recharge against a carrier running accelerated ticks; see Gene_Dispersal.</summary>
        private int lastGameTickProcessed = -1;

        private AnchorGeneExtension Ext => def.GetModExtension<AnchorGeneExtension>();

        public int MaxAnchors => Ext?.maxAnchors ?? 3;

        public int MarkDurationTicks => Ext?.markDurationTicks ?? 60000;

        public bool BansWeapons => Ext == null || Ext.banWeapons;

        public ThingDef StoneDef => Ext?.stoneDef;

        public float DirectSwapRange => Ext?.directSwapRange ?? 15f;

        public float LongSwapRange => Ext?.longSwapRange ?? 25f;

        public int MaxCharges => Mathf.Max(1, Ext?.clapCharges ?? 3);

        public int RechargeTicks => Ext?.chargeRechargeTicks ?? 600;

        public int SwapStunTicks => Ext?.swapStunTicks ?? 30;

        /// <summary>Claps in hand. Starts full; the field is -1 until the first read because the def is not attached before.</summary>
        public int Charges
        {
            get
            {
                if (charges < 0) charges = MaxCharges;
                return charges;
            }
        }

        public bool Full => Charges >= MaxCharges;

        /// <summary>Seconds until the next clap grows back. Zero when full.</summary>
        public float SecondsToNext => Full ? 0f : Mathf.Max(0, RechargeTicks - rechargeProgress) / 60f;

        public void Spend(int count)
        {
            charges = Mathf.Max(0, Charges - count);
        }

        /// <summary>Back to full. Only the debug window calls this.</summary>
        public void Refill()
        {
            charges = MaxCharges;
            rechargeProgress = 0;
        }

        public override void PostAdd()
        {
            base.PostAdd();
            charges = MaxCharges;
            rechargeProgress = 0;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn == null || pawn.Dead || !Active) return;

            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            if (Full)
            {
                rechargeProgress = 0;
                return;
            }

            rechargeProgress += Mathf.Max(1, delta);
            if (rechargeProgress < RechargeTicks) return;
            rechargeProgress = 0;
            charges = Charges + 1;
        }

        /// <summary>
        /// The raw list, including anything that has quietly stopped holding. Only the sweep in
        /// <see cref="MapComponent_Anchors"/> is allowed to remove from it - every other reader
        /// filters without mutating, because the draw path and the gizmo checks both run every
        /// frame and pruning there would drop stones before anyone could be told they had gone.
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

        /// <summary>The held stone at the target, or null.</summary>
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

        /// <summary>Whether this stone item is one of the carrier's and still holds. The sweep clears the rest off the map.</summary>
        public bool HoldsStone(Thing stone)
        {
            Map map = HeldMap;
            int duration = MarkDurationTicks;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].stone == stone) return anchors[i].StillHolds(map, duration);
            }
            return false;
        }

        public void Add(Anchor anchor)
        {
            anchor.suit = FreeSuit();
            anchors.Add(anchor);
        }

        /// <summary>
        /// The lowest suit no held stone is using. With more than three stones allowed (maxAnchors is
        /// XML) suits repeat, least used first.
        /// </summary>
        private int FreeSuit()
        {
            int best = 0, fewest = int.MaxValue;
            for (int suit = 0; suit < Suits; suit++)
            {
                int used = 0;
                for (int i = 0; i < anchors.Count; i++) if (anchors[i].suit == suit) used++;
                if (used < fewest) { fewest = used; best = suit; }
            }
            return best;
        }

        private const int Suits = 3;

        /// <summary>Forgets the stone and takes the item off the map.</summary>
        public void Remove(Anchor anchor)
        {
            anchors.Remove(anchor);
            DestroyStone(anchor);
        }

        /// <summary>
        /// Drops stones that no longer hold and collects what was dropped so the sweep can say so.
        /// This is the only place stones are removed for expiry.
        /// </summary>
        public void Prune(List<Anchor> dropped)
        {
            Map map = HeldMap;
            int duration = MarkDurationTicks;
            for (int i = anchors.Count - 1; i >= 0; i--)
            {
                if (anchors[i].StillHolds(map, duration)) continue;

                if (dropped != null) dropped.Add(anchors[i]);
                DestroyStone(anchors[i]);
                anchors.RemoveAt(i);
            }
        }

        private static void DestroyStone(Anchor anchor)
        {
            if (anchor.stone != null && !anchor.stone.Destroyed) anchor.stone.Destroy(DestroyMode.Vanish);
        }

        public override void PostRemove()
        {
            base.PostRemove();
            for (int i = 0; i < anchors.Count; i++) DestroyStone(anchors[i]);
            anchors.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref anchors, "anchors", LookMode.Deep);
            Scribe_Values.Look(ref charges, "clapCharges", -1);
            Scribe_Values.Look(ref rechargeProgress, "clapRechargeProgress", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (anchors == null) anchors = new List<Anchor>();
                // Marks on pawns and invisible tile marks are from before the stones. Neither exists
                // any more: pawns in sight need no mark, and a tile mark is a stone now.
                anchors.RemoveAll(a => a == null || a.stone == null);
                for (int i = 0; i < anchors.Count; i++) if (anchors[i].suit < 0) anchors[i].suit = FreeSuit();
            }
        }
    }
}
