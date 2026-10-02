using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Butterfly's numbers (docs/ego-weapons.md, Weapon 2), on the AG_EgoButterfly hediff def.</summary>
    public class EgoButterflyExtension : DefModExtension
    {
        /// <summary>Stacks at which the pawn goes down; the guns and Overclock never put on more.</summary>
        public int cap = 10;
        /// <summary>Consciousness lost per stack, up to the cap: at 0.075 an unhurt pawn goes under the game's 30 % at 10 stacks and stands at 9.</summary>
        public float consciousnessPerStack = 0.075f;
        /// <summary>Seconds per stack lost, whatever the count.</summary>
        public float fadeSeconds = 10f;

        public static EgoButterflyExtension Of => EgoDefOf.AG_EgoButterfly.GetModExtension<EgoButterflyExtension>() ?? Fallback;
        private static readonly EgoButterflyExtension Fallback = new EgoButterflyExtension();
    }

    /// <summary>
    /// Solemn Lament's Butterfly: one stack per point of severity. Each stack up to the cap takes consciousness
    /// (<see cref="EgoButterflyExtension.consciousnessPerStack"/>), so the cap is the down point; stacks past the cap
    /// (only the corroded coffin puts them on) take no more, or the pawn would die of consciousness 0 before the
    /// funeral's count. One stack fades every fadeSeconds. The stage is made from the numbers, one per stack count, so
    /// the game re-reads capacities when the count changes.
    /// </summary>
    public class Hediff_EgoButterfly : HediffWithComps
    {
        private static readonly List<HediffStage> stages = new List<HediffStage>();
        private int fadeTicks;

        public int Stacks => Mathf.RoundToInt(Severity);

        public override int CurStageIndex => Stacks;

        public override HediffStage CurStage => Stage(Stacks);

        public override string LabelInBrackets => Stacks + " / " + EgoButterflyExtension.Of.cap;

        private static HediffStage Stage(int stacks)
        {
            EgoButterflyExtension x = EgoButterflyExtension.Of;
            while (stages.Count <= stacks)
            {
                int k = stages.Count;
                var stage = new HediffStage();
                stage.capMods.Add(new PawnCapacityModifier { capacity = PawnCapacityDefOf.Consciousness, offset = -x.consciousnessPerStack * Mathf.Min(k, x.cap) });
                stages.Add(stage);
            }
            return stages[stacks];
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            fadeTicks += delta;
            int every = Mathf.Max(1, EgoButterflyExtension.Of.fadeSeconds.SecondsToTicks());
            if (fadeTicks < every) return;
            fadeTicks -= every;
            Severity -= 1f;
            GameComponent_EgoSolemnLament.Instance?.Faded(pawn, Stacks);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fadeTicks, "fadeTicks");
        }
    }

    /// <summary>Putting Butterfly stacks on a pawn, and the funeral.</summary>
    public static class EgoButterfly
    {
        public static int Stacks(Pawn pawn) => (pawn?.health?.hediffSet.GetFirstHediffOfDef(EgoDefOf.AG_EgoButterfly) as Hediff_EgoButterfly)?.Stacks ?? 0;

        /// <summary>
        /// Up to <paramref name="stacks"/> stacks on <paramref name="pawn"/>, never past <paramref name="limit"/> (the cap
        /// for the guns and Overclock, the funeral's count for the corroded coffin). Returns how many went on. At the
        /// funeral's count (<paramref name="funeral"/> above 0) the pawn dies of Butterfly.
        /// </summary>
        public static int Add(Pawn pawn, int stacks, int limit, int funeral = 0)
        {
            if (pawn == null || pawn.Dead || pawn.health == null || stacks <= 0) return 0;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(EgoDefOf.AG_EgoButterfly) as Hediff_EgoButterfly;
            int had = hediff?.Stacks ?? 0, add = Mathf.Min(stacks, limit - had);
            if (add <= 0) return 0;
            if (hediff == null)
            {
                hediff = (Hediff_EgoButterfly)HediffMaker.MakeHediff(EgoDefOf.AG_EgoButterfly, pawn);
                hediff.Severity = add;
                pawn.health.AddHediff(hediff);
            }
            else hediff.Severity = had + add;
            if (funeral > 0 && had + add >= funeral && !pawn.Dead) pawn.Kill(null, hediff);
            return add;
        }
    }
}
