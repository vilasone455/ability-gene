using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The genjutsu on one enemy: it sees its nearest ally as Itachi and attacks it, for up to the
    /// def's 10 s. A mental state rather than a retargeting, because the AI drops any target that is
    /// not hostile to it: <see cref="ForceHostileTo(Thing)"/> makes the false Itachi hostile in both
    /// directions (GenHostility asks both pawns' states), so the ally fights back on its own.
    /// <see cref="JobGiver_FalseFace"/>, reached through the MentalStateCritical think tree, gives
    /// the attack jobs. It breaks when the victim takes damage, when the false Itachi goes down,
    /// or when Itachi (causedByPawn) goes down.
    /// </summary>
    public class MentalState_FalseFace : MentalState
    {
        public Pawn falseItachi;
        private int startTick;

        public bool AllyValid =>
            falseItachi != null && falseItachi.Spawned && !falseItachi.Dead && !falseItachi.Downed
            && falseItachi.Map == pawn.Map;

        private bool ItachiDown => causedByPawn == null || causedByPawn.Dead || causedByPawn.Downed;

        /// <summary>Called right after TryStartMentalState, once the state object exists.</summary>
        public void Begin(Pawn ally)
        {
            falseItachi = ally;
            startTick = Find.TickManager.TicksGame;
            if (pawn.mindState != null)
            {
                pawn.mindState.enemyTarget = ally;
                pawn.mindState.meleeThreat = null;
            }
            if (pawn.jobs?.curJob != null) pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        public override bool ForceHostileTo(Thing t) => t != null && t == falseItachi;

        public override bool ForceHostileTo(Faction f) => false;

        public override void MentalStateTick(int delta)
        {
            if (!AllyValid || ItachiDown || (pawn.mindState != null && pawn.mindState.lastHarmTick > startTick))
            {
                RecoverFromState();
                return;
            }
            if (pawn.IsHashIntervalTick(30, delta)) FalseFaceFX.Mark(pawn);
            base.MentalStateTick(delta);
        }

        public override void PostEnd()
        {
            base.PostEnd();
            if (pawn.mindState != null && pawn.mindState.meleeThreat == falseItachi) pawn.mindState.meleeThreat = null;
            if (falseItachi != null && !falseItachi.Dead)
            {
                Pawn_MindState mind = falseItachi.mindState;
                if (mind != null)
                {
                    if (mind.enemyTarget == pawn) mind.enemyTarget = null;
                    if (mind.meleeThreat == pawn) mind.meleeThreat = null;
                }
                if (falseItachi.jobs?.curJob != null && falseItachi.CurJob.targetA.Thing == pawn)
                    falseItachi.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            FalseFaceFX.End(pawn);
        }

        public override string InspectLine =>
            "AG_ItachiFalseFaceInspect".Translate(falseItachi?.LabelShort ?? "?");

        public override RandomSocialMode SocialModeMax() => RandomSocialMode.Off;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref falseItachi, "falseItachi");
            Scribe_Values.Look(ref startTick, "startTick", 0);
        }
    }
}
