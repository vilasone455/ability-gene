using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The colony's Echo state: the one shared charge pool, each Echo's record, and the deeds
    /// Trials count. Every Host draws on the same pool; working resonance devices refill it.
    /// </summary>
    public class GameComponent_Echoes : GameComponent
    {
        /// <summary>Pool and upkeep run every this many ticks.</summary>
        public const int PoolInterval = 60;
        private const int TrialInterval = 250;

        public float charge;
        private List<EchoRecord> records = new List<EchoRecord>();
        private List<PawnDeeds> deeds = new List<PawnDeeds>();
        private bool deviceWorking;

        public GameComponent_Echoes(Game game) { }

        public static GameComponent_Echoes Get => Current.Game?.GetComponent<GameComponent_Echoes>();

        public float MaxCharge => EchoDevice.CurrentTier.maxCharge;
        public float RefillPerHour => EchoDevice.CurrentTier.refillPerHour;
        public int HeroCap => EchoDevice.CurrentTier.heroCap;
        public bool DeviceWorking => deviceWorking;
        public int HostCount => records.Count(r => r.state == EchoState.Awakened);
        public EchoRecord Tuned => records.Find(r => r.state == EchoState.Tracking);
        public IEnumerable<EchoRecord> Manifested => records.Where(r => r.manifested);

        public float DrainPerHour
        {
            get
            {
                float drain = 0f;
                for (int i = 0; i < records.Count; i++)
                    if (records[i].manifested) drain += records[i].def.upkeepPerHour;
                return drain;
            }
        }

        public EchoRecord RecordFor(EchoDef def)
        {
            EchoRecord record = records.Find(r => r.def == def);
            if (record != null) return record;
            record = new EchoRecord { def = def };
            records.Add(record);
            return record;
        }

        public EchoRecord HostRecord(Pawn pawn) =>
            pawn == null ? null : records.Find(r => r.state == EchoState.Awakened && r.host == pawn);

        public EchoRecord CandidateRecord(Pawn pawn) =>
            pawn == null ? null : records.Find(r => r.state == EchoState.Tracking && r.candidate == pawn);

        public PawnDeeds DeedsFor(Pawn pawn, bool create)
        {
            PawnDeeds found = deeds.Find(d => d.pawn == pawn);
            if (found != null || !create) return found;
            found = new PawnDeeds { pawn = pawn };
            deeds.Add(found);
            return found;
        }

        /// <summary>Takes charge if there is enough. False, and nothing taken, otherwise.</summary>
        public bool TrySpend(float amount)
        {
            if (amount <= 0f) return true;
            if (charge < amount) return false;
            charge -= amount;
            return true;
        }

        /// <summary>Gives back charge a cast took and did not use, up to the device's maximum.</summary>
        public void Refund(float amount)
        {
            if (amount > 0f) charge = Mathf.Min(MaxCharge, charge + amount);
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick % PoolInterval == 0) TickPool();
            if (tick % TrialInterval == 0) TickRecords();
        }

        private void TickPool()
        {
            deviceWorking = EchoDevice.AnyWorking();
            float hours = PoolInterval / (float)GenDate.TicksPerHour;
            if (deviceWorking) charge += RefillPerHour * hours;

            bool anyManifested = false;
            for (int i = 0; i < records.Count; i++)
            {
                if (!records[i].manifested) continue;
                anyManifested = true;
                charge -= records[i].def.upkeepPerHour * hours;
            }

            if (charge > MaxCharge) charge = MaxCharge;
            if (charge <= 0f)
            {
                charge = 0f;
                if (anyManifested) EchoUtility.EmptyPool();
            }
        }

        private void TickRecords()
        {
            for (int i = 0; i < records.Count; i++)
            {
                EchoRecord record = records[i];
                switch (record.state)
                {
                    case EchoState.Tracking:
                        if (record.candidate == null || record.candidate.Dead || record.candidate.Destroyed
                            || !EchoUtility.CanBeCandidate(record.candidate, out _))
                        {
                            EchoUtility.StopTuning(record, lost: true);
                            break;
                        }
                        EchoUtility.CheckTrials(record);
                        break;
                    case EchoState.Awakened:
                        if (record.host == null || record.host.Dead || record.host.Destroyed)
                            EchoUtility.CloseSeat(record);
                        break;
                }
            }
        }

        /// <summary>For game tests: forgets every record, deed and charge. Hosts revert first.</summary>
        internal void ResetForTests()
        {
            foreach (EchoRecord record in records.ToList())
                if (record.manifested) EchoUtility.Revert(record, collapse: false);
            records.Clear();
            deeds.Clear();
            charge = 0f;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref charge, "charge");
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            Scribe_Collections.Look(ref deeds, "deeds", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (records == null) records = new List<EchoRecord>();
                records.RemoveAll(r => r?.def == null);
                if (deeds == null) deeds = new List<PawnDeeds>();
                deeds.RemoveAll(d => d?.pawn == null);
            }
        }
    }
}
