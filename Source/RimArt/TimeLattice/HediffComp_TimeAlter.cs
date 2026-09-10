using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_TimeAlter : HediffCompProperties
    {
        /// <summary>
        /// How fast this pawn's own time runs. Above 1 the pawn is ticked extra times per game
        /// tick; below 1 its ticks are skipped. Only whole multipliers accelerate cleanly, so
        /// 2 and 4 are the intended values, and 0.333 is the stagnate case.
        /// </summary>
        public float rateMultiplier = 2f;

        /// <summary>Temporal strain added per game second while this is active.</summary>
        public float strainPerSecond = 0.02f;

        /// <summary>
        /// Lifetime in game ticks, counted by this comp rather than by
        /// HediffCompProperties_Disappears. An accelerated pawn ticks its own hediffs N times
        /// per game tick, so a Disappears comp would expire N times too early - Square Accel
        /// would last a quarter of its stated duration.
        /// </summary>
        public int durationTicks = 1200;

        /// <summary>Trailing copies of the pawn, one fewer than the multiplier.</summary>
        public bool drawAfterimages = true;

        public HediffDef strainHediff;

        public HediffCompProperties_TimeAlter()
        {
            compClass = typeof(HediffComp_TimeAlter);
        }
    }

    public class HediffComp_TimeAlter : HediffComp
    {
        private int ticksLeft = -1;
        private int lastGameTickProcessed = -1;

        /// <summary>Recent draw positions, newest last. Sampled per pawn-tick, so a faster pawn leaves a denser trail.</summary>
        private readonly List<Vector3> trail = new List<Vector3>();
        private const int MaxTrail = 12;

        public HediffCompProperties_TimeAlter Props => (HediffCompProperties_TimeAlter)props;

        public bool IsStagnating => Props.rateMultiplier < 1f;

        /// <summary>Extra DoTick calls owed per game tick. Zero when stagnating.</summary>
        public int ExtraTicks => IsStagnating ? 0 : Mathf.Max(0, Mathf.RoundToInt(Props.rateMultiplier) - 1);

        public IReadOnlyList<Vector3> Trail => trail;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead) return;

            TimeAlterRegistry.Report(this);

            if (pawn.Spawned && Props.drawAfterimages && !IsStagnating)
            {
                trail.Add(pawn.DrawPos);
                if (trail.Count > MaxTrail) trail.RemoveAt(0);
            }

            // Everything below must happen once per *game* tick, not once per pawn tick.
            // While accelerated this comp runs several times inside a single game tick, which
            // would otherwise multiply both the strain rate and the countdown by the multiplier.
            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            if (ticksLeft < 0) ticksLeft = Props.durationTicks;

            AccrueStrain(pawn);

            ticksLeft--;
            if (ticksLeft <= 0)
            {
                TimeAlterRegistry.Drop(this);
                pawn.health.RemoveHediff(parent);
            }
        }

        private void AccrueStrain(Pawn pawn)
        {
            if (Props.strainHediff == null || Props.strainPerSecond <= 0f) return;

            Hediff strain = pawn.health.hediffSet.GetFirstHediffOfDef(Props.strainHediff);
            if (strain == null)
            {
                strain = pawn.health.AddHediff(Props.strainHediff);
                if (strain == null) return;
                strain.Severity = 0f;
            }
            strain.Severity += Props.strainPerSecond / 60f;
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            TimeAlterRegistry.Drop(this);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
        }
    }
}
