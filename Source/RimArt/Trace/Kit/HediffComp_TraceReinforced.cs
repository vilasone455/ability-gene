using UnityEngine;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_TraceReinforced : HediffCompProperties
    {
        public HediffCompProperties_TraceReinforced() => compClass = typeof(HediffComp_TraceReinforced);
    }

    /// <summary>
    /// Reinforcement's picture while the buff lasts: it keeps the pawn on <see cref="GameComponent_Trace"/>'s list
    /// (the weapon's glow, the streaks at the heels), leaves a footprint every
    /// <see cref="TraceReinforcementTiming.Step"/> s while the pawn moves, and when the buff ends the circuit runs
    /// back into the chest. The numbers of the buff itself are on the hediff and the ability defs.
    /// </summary>
    public class HediffComp_TraceReinforced : HediffComp
    {
        private static readonly int StepTicks = Mathf.RoundToInt(TraceReinforcementTiming.Step * 60f);
        private int steps;

        public override void CompPostTick(ref float severityAdjustment)
        {
            GameComponent_Trace trace = GameComponent_Trace.Instance;
            if (trace == null) return;
            trace.Reinforced(Pawn);
            if (!Pawn.Spawned || !Pawn.IsHashIntervalTick(StepTicks)) return;
            Vector2 heading = TraceHands.Heading(Pawn);
            if (heading != Vector2.zero) trace.AddPrint(Pawn, heading, steps++);
        }

        public override void CompPostPostRemoved() => GameComponent_Trace.Instance?.ReinforceEnded(Pawn);
    }
}
