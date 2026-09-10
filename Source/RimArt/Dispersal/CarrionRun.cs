using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Crows travel independently of their carrier. Only the return delivers recovery.</summary>
    public class CarrionRun : IExposable
    {
        private enum Phase { Outbound, Feeding, Returning, Finished }
        private Pawn carrier;
        private Corpse corpse;
        private Phase phase;
        private int ticks;
        private int flightTicks;
        private Vector3 origin;
        private Vector3 feedingPosition;
        private float nutrition;
        private Flock flock;
        private const int FeedingTicks = 120;
        private const float Lift = 0.6f;

        public Pawn Carrier => carrier;
        public Corpse Corpse => corpse;
        private Flock Birds => flock ?? (flock = new Flock(7, 0.85f, Lift));

        public CarrionRun() { }
        public CarrionRun(Pawn carrier, Corpse corpse)
        {
            this.carrier = carrier;
            this.corpse = corpse;
            origin = carrier.DrawPos;
            feedingPosition = corpse.DrawPos;
            flightTicks = Duration(origin, feedingPosition);
        }

        private static int Duration(Vector3 from, Vector3 to) =>
            Mathf.Max(24, Mathf.CeilToInt((to - from).MagnitudeHorizontal() * 6f));

        public static bool ValidCorpse(Corpse corpse, Map map) =>
            map != null && corpse != null && !corpse.Destroyed && corpse.Spawned
            && corpse.Map == map && corpse.InnerPawn != null && corpse.InnerPawn.RaceProps.IsFlesh
            && corpse.GetRotStage() == RotStage.Fresh;

        public bool Tick(Map map)
        {
            if (phase == Phase.Finished) return false;
            // A dead or departed recipient cannot receive recovery. No resurrection or remote healing.
            if (carrier == null || carrier.Dead || carrier.MapHeld != map)
                return false;
            if (phase != Phase.Returning && !ValidCorpse(corpse, map)) return false;
            // A concurrent Murder temporarily holds the pawn off-map; wait for its landing.
            if (!carrier.Spawned) return true;
            ticks++;

            if (phase == Phase.Outbound)
            {
                feedingPosition = corpse.DrawPos;
                if (ticks % 6 == 0) DispersalFX.Travel(origin, feedingPosition,
                    ticks / (float)flightTicks, Lift, map);
                if (ticks >= flightTicks) { phase = Phase.Feeding; ticks = 0; }
            }
            else if (phase == Phase.Feeding)
            {
                // If the body is hauled away or otherwise removed, feeding stops without recovery.
                if ((corpse.DrawPos - feedingPosition).MagnitudeHorizontalSquared() > 0.1f) return false;
                if (ticks % 18 == 0) DispersalFX.Feed(feedingPosition, map);
                if (ticks >= FeedingTicks)
                {
                    nutrition = Mathf.Clamp(corpse.InnerPawn.BodySize, 0f, 1f);
                    corpse.Destroy(DestroyMode.Vanish);
                    corpse = null;
                    phase = Phase.Returning;
                    ticks = 0;
                    flightTicks = Duration(feedingPosition, carrier.DrawPos);
                }
            }
            else if (phase == Phase.Returning)
            {
                if (ticks % 6 == 0) DispersalFX.Travel(feedingPosition, carrier.DrawPos,
                    ticks / (float)flightTicks, Lift, map);
                if (ticks >= flightTicks)
                {
                    // Mark completion before changing health, so this run can never pay out twice.
                    phase = Phase.Finished;
                    Recover();
                    DispersalFX.Arrive(carrier.DrawPos, map);
                    return false;
                }
            }
            return true;
        }

        private void Recover()
        {
            float budget = 20f * nutrition;
            // Snapshot: Heal can remove a hediff. Most actively bleeding wounds get help first.
            var injuries = carrier.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                .Where(h => !h.IsPermanent() && h.BleedRate > 0f)
                .OrderByDescending(h => h.BleedRate).ToList();
            foreach (Hediff_Injury injury in injuries)
            {
                float amount = Mathf.Min(budget, injury.Severity);
                if (amount <= 0f) break;
                injury.Heal(amount);
                budget -= amount;
            }
            Hediff bloodLoss = carrier.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null) bloodLoss.Severity = Mathf.Max(0f, bloodLoss.Severity - 0.2f * nutrition);
            Gene_Hemogen hemogen = carrier.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogen != null && hemogen.Active)
                GeneUtility.OffsetHemogen(carrier, 0.2f * nutrition, applyStatFactor: false);
        }

        public void Draw()
        {
            if (carrier == null || !carrier.Spawned || carrier.Dead) return;
            if (phase == Phase.Outbound)
                Birds.Draw(origin, feedingPosition, ticks / (float)flightTicks, ticks);
            else if (phase == Phase.Returning)
                Birds.Draw(feedingPosition, carrier.DrawPos, ticks / (float)flightTicks, ticks);
            else if (phase == Phase.Feeding)
            {
                for (int i = 0; i < 7; i++)
                {
                    float angle = i * Mathf.PI * 2f / 7f;
                    Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 0.42f;
                    int frame = ((ticks + i * 7) / 5) % 4;
                    // Closed wings and reaching beaks distinguish feeding from hovering.
                    DispersalGraphics.DrawFeedingCrow(feedingPosition + offset,
                        (-offset).AngleFlat(), frame, 1f);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref carrier, "carrier");
            Scribe_References.Look(ref corpse, "corpse");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref ticks, "ticks");
            Scribe_Values.Look(ref flightTicks, "flightTicks");
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref feedingPosition, "feedingPosition");
            Scribe_Values.Look(ref nutrition, "nutrition");
        }
    }
}
