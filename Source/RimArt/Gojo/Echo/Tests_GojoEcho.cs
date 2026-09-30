using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Game tests for Gojo's Echo, The Strongest and Infinity's move to the Echo (run with -quicktest -rimarttest="Gojo: echo").</summary>
    public static class Tests_GojoEcho
    {
        private static EchoDef Gojo => DefDatabase<EchoDef>.GetNamed("AG_Echo_Gojo");
        private static EchoDef Accelerator => DefDatabase<EchoDef>.GetNamed("AG_Echo_Accelerator");
        private static RecordDef TimeDowned => DefDatabase<RecordDef>.GetNamed("TimeDowned");

        private static Pawn Colonist(RimArtTestContext t, int dx, float ageYears = -1f)
        {
            Pawn pawn = t.Colonist(t.center + new IntVec3(dx, 0, 0));
            pawn.drafter.Drafted = false;
            RimArtTestContext.Hold(pawn);
            if (ageYears > 0f) pawn.ageTracker.AgeBiologicalTicks = (long)(ageYears * GenDate.TicksPerYear);
            return pawn;
        }

        /// <summary>Time records refuse AddTo; the test writes the tracker's map directly.</summary>
        private static void SetRecord(Pawn pawn, RecordDef def, float value) =>
            ((DefMap<RecordDef, float>)AccessTools.Field(typeof(Pawn_RecordsTracker), "records").GetValue(pawn.records))[def] = value;

        [RimArtTest("Gojo", "echo 1 the time-downed trial counts hours")]
        private static IEnumerable<int> TrialHours(RimArtTestContext t)
        {
            t.ClearEchoes(null);
            Pawn pawn = Colonist(t, 0);
            Trial_Record trial = Gojo.trials.Find(x => x is Trial_Record) as Trial_Record;
            t.Check(trial != null && trial.record == TimeDowned, "Gojo has a TimeDowned trial");
            if (trial == null) yield break;
            t.Log("label: " + trial.Label);
            t.Check(trial.Label.Contains("24 h"), "the label shows 24 h");

            SetRecord(pawn, TimeDowned, 23f * GenDate.TicksPerHour);
            t.Log("23 h downed: " + trial.ProgressText(pawn) + ", met " + trial.Met(pawn));
            t.Check(!trial.Met(pawn), "23 h is not enough");
            SetRecord(pawn, TimeDowned, 24f * GenDate.TicksPerHour);
            t.Log("24 h downed: " + trial.ProgressText(pawn) + ", met " + trial.Met(pawn));
            t.Check(trial.Met(pawn), "24 h meets it");
            yield return 1;
        }

        [RimArtTest("Gojo", "echo 2 The Strongest likes the young and dislikes the old")]
        private static IEnumerable<int> Opinions(RimArtTestContext t)
        {
            t.ClearEchoes(null);
            Pawn gojo = Colonist(t, 0, 28f);
            EchoUtility.ForceHost(Gojo, gojo);
            t.Check(TheStrongest.Has(gojo), "awakening forced The Strongest");
            Pawn young = Colonist(t, 2, 20f);
            Pawn middle = Colonist(t, 4, 40f);
            Pawn old = Colonist(t, 6, 60f);

            ThoughtDef ofYoung = DefDatabase<ThoughtDef>.GetNamed("AG_StrongestOfYoung");
            ThoughtDef ofOld = DefDatabase<ThoughtDef>.GetNamed("AG_StrongestOfOld");
            ThoughtDef youngOf = DefDatabase<ThoughtDef>.GetNamed("AG_YoungOfStrongest");
            ThoughtDef oldOf = DefDatabase<ThoughtDef>.GetNamed("AG_OldOfStrongest");
            foreach (Pawn other in new[] { young, middle, old })
                t.Log(other.ageTracker.AgeBiologicalYears + " y: Gojo young " + ofYoung.Worker.CurrentSocialState(gojo, other).Active
                    + ", Gojo old " + ofOld.Worker.CurrentSocialState(gojo, other).Active
                    + ", them young " + youngOf.Worker.CurrentSocialState(other, gojo).Active
                    + ", them old " + oldOf.Worker.CurrentSocialState(other, gojo).Active);
            t.Check(ofYoung.Worker.CurrentSocialState(gojo, young).Active && !ofYoung.Worker.CurrentSocialState(gojo, middle).Active, "Gojo +20 only of the 20-year-old");
            t.Check(ofOld.Worker.CurrentSocialState(gojo, old).Active && !ofOld.Worker.CurrentSocialState(gojo, middle).Active, "Gojo -20 only of the 60-year-old");
            t.Check(youngOf.Worker.CurrentSocialState(young, gojo).Active && !youngOf.Worker.CurrentSocialState(middle, gojo).Active, "only the 20-year-old +10 of Gojo");
            t.Check(oldOf.Worker.CurrentSocialState(old, gojo).Active && !oldOf.Worker.CurrentSocialState(middle, gojo).Active, "only the 60-year-old -10 of Gojo");
            t.Check(!ofYoung.Worker.CurrentSocialState(young, middle).Active, "a pawn without the trait has none of it");

            int before = gojo.relations.OpinionOf(young) - gojo.relations.OpinionOf(middle);
            t.Log("Gojo's opinion: of the 20-year-old " + gojo.relations.OpinionOf(young) + ", of the 40-year-old " + gojo.relations.OpinionOf(middle)
                + ", of the 60-year-old " + gojo.relations.OpinionOf(old) + " (difference young - middle " + before + ")");
            yield return 1;
        }

        [RimArtTest("Gojo", "echo 3 The Strongest mood: alone -6, another Host +4")]
        private static IEnumerable<int> Mood(RimArtTestContext t)
        {
            t.ClearEchoes(null);
            Pawn gojo = Colonist(t, 0);
            EchoUtility.ForceHost(Gojo, gojo);
            ThoughtDef alone = DefDatabase<ThoughtDef>.GetNamed("AG_StrongestAlone");
            ThoughtState state = alone.Worker.CurrentState(gojo);
            t.Log("only Host: active " + state.Active + ", stage " + state.StageIndex);
            t.Check(state.Active && state.StageIndex == 0 && alone.stages[0].baseMoodEffect < 0f, "stage 0 (negative) while he is the only Host");

            Pawn other = Colonist(t, 3);
            EchoUtility.ForceHost(Accelerator, other);
            state = alone.Worker.CurrentState(gojo);
            t.Log("with Accelerator's Host: active " + state.Active + ", stage " + state.StageIndex);
            t.Check(state.Active && state.StageIndex == 1 && alone.stages[1].baseMoodEffect > 0f, "stage 1 (positive) once another Host is awakened");
            yield return 1;
        }

        [RimArtTest("Gojo", "echo 4 the Echo grants the five abilities, the phase barrier implant none")]
        private static IEnumerable<int> Abilities(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = t.ClearEchoes(null);
            Pawn gojo = Colonist(t, 0);
            EchoRecord record = EchoUtility.ForceHost(Gojo, gojo);
            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "Gojo manifested");
            yield return 2;
            foreach (AbilityDef ability in Gojo.abilities)
                t.Check(gojo.abilities.GetAbility(ability) != null, "manifested Gojo has " + ability.defName);

            Pawn implanted = Colonist(t, 3);
            implanted.health.AddHediff(DefDatabase<HediffDef>.GetNamed("AG_PhaseBarrier"), implanted.health.hediffSet.GetBrain());
            yield return 2;
            t.Check(implanted.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("AG_Recursion")) == null, "the phase barrier implant grants no Infinity");
            EchoUtility.Revert(record, collapse: false);
        }
    }
}
