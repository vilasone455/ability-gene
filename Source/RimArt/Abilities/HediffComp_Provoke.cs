using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    public class HediffCompProperties_Provoke : HediffCompProperties
    {
        public float radius = 12.9f;
        public int intervalTicks = 60;

        public HediffCompProperties_Provoke()
        {
            compClass = typeof(HediffComp_Provoke);
        }
    }

    /// <summary>
    /// Drags every nearby hostile's attention onto the carrier. RimWorld has no aggro
    /// system, so this works by rewriting each hostile's enemyTarget and — once, on the
    /// first pass — interrupting their current job so their AI re-acquires. Re-pointing
    /// enemyTarget every second afterwards keeps it sticky without thrashing their jobs,
    /// and leaves ranged pawns free to shoot rather than forcing everyone into melee.
    /// </summary>
    public class HediffComp_Provoke : HediffComp
    {
        private int ticksToNext;
        private bool interrupted;

        public HediffCompProperties_Provoke Props => (HediffCompProperties_Provoke)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn taunter = Pawn;
            if (taunter == null || taunter.Dead || !taunter.Spawned) return;

            ticksToNext -= delta;
            if (ticksToNext > 0) return;
            ticksToNext = Props.intervalTicks;

            float radiusSquared = Props.radius * Props.radius;
            IReadOnlyList<Pawn> everyone = taunter.Map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < everyone.Count; i++)
            {
                Pawn other = everyone[i];
                if (other == taunter || other.Dead || other.Downed) continue;
                if (other.mindState == null) continue;
                if (!other.HostileTo(taunter)) continue;
                if ((other.Position - taunter.Position).LengthHorizontalSquared > radiusSquared) continue;

                other.mindState.enemyTarget = taunter;

                if (!interrupted && other.jobs != null)
                {
                    other.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }

            interrupted = true;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksToNext, "ticksToNext", 0);
            Scribe_Values.Look(ref interrupted, "interrupted", false);
        }
    }
}
