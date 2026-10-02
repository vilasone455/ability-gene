using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Corrosion, rule 2: the E.G.O. weapon has the pawn. It ignores orders and drafting and stands where it is
    /// (<see cref="JobGiver_EgoCorroded"/>, reached through the MentalStateCritical think tree), and every
    /// corrodedInterval the weapon's action fires at the nearest pawn of any faction, or round the pawn when the action
    /// takes no target. The first firing comes one interval after the start. Faction, stats, gear and relations are
    /// unchanged; a colonist the action hurts gives the vanilla "harmed me" opinion through the damage itself.
    ///
    /// Ends after corrodedDuration (forceRecoverAfterTicks, checked every 30 ticks; the def's own recovery times are a
    /// day, so they never come first), when the pawn goes down or falls asleep (the def's recoverFrom flags), or when
    /// the weapon leaves its hands. Then AG_EgoExhausted for exhaustionHours. Not hostile to anyone by itself: whether
    /// colonists may attack a corroded pawn without a prompt is open in the doc.
    /// </summary>
    public class MentalState_EgoCorroded : MentalState
    {
        /// <summary>The weapon <see cref="EgoCorrosion.Corrode"/> is starting the state for; read in PreStart, before the letter goes out.</summary>
        internal static CompEgoWeapon starting;

        public ThingWithComps weapon;
        private int startTick;
        private int nextFireTick;

        public CompEgoWeapon Comp => weapon?.GetComp<CompEgoWeapon>();

        public float Seconds => (Find.TickManager.TicksGame - startTick) / 60f;

        public override void PreStart()
        {
            base.PreStart();
            startTick = Find.TickManager.TicksGame;
            if (starting == null) return;
            weapon = starting.parent;
            forceRecoverAfterTicks = starting.Props.DurationTicks;
            nextFireTick = startTick + starting.Props.IntervalTicks;
        }

        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            CompEgoWeapon comp = Comp;
            comp?.Props.Action.Begin(pawn, comp, overclock: false);
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (pawn.MentalState != this) return;
            CompEgoWeapon comp = Comp;
            if (comp == null || pawn.equipment?.Primary != weapon)
            {
                RecoverFromState();
                return;
            }
            if (!pawn.Spawned || Find.TickManager.TicksGame < nextFireTick) return;
            nextFireTick = Find.TickManager.TicksGame + comp.Props.IntervalTicks;
            EgoCorrosion.Fire(pawn, comp, hostilesOnly: false);
        }

        public override void PostEnd()
        {
            base.PostEnd();
            CompEgoWeapon comp = Comp;
            if (comp == null) return;
            comp.Props.Action.End(pawn, comp, overclock: false);
            if (!pawn.Dead) EgoCorrosion.Exhaust(pawn, comp.Props.exhaustionHours);
        }

        public override string InspectLine
        {
            get
            {
                int left = forceRecoverAfterTicks - age;
                return "Corroded by " + (weapon?.LabelNoCount ?? "an E.G.O. weapon")
                    + (forceRecoverAfterTicks > 0 ? ": " + left.ToStringSecondsFromTicks() + " left" : "");
            }
        }

        public override RandomSocialMode SocialModeMax() => RandomSocialMode.Off;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref weapon, "weapon");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref nextFireTick, "nextFireTick");
        }
    }

    /// <summary>
    /// The corroded pawn's job: stand, or with a weapon whose action <see cref="EgoCorrosionAction.WalksToNearest"/>, walk
    /// to the nearest living pawn of any faction (downed ones too) and stand next to it. The firing belongs to the state.
    /// </summary>
    public class JobGiver_EgoCorroded : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn.MentalState is MentalState_EgoCorroded state)) return null;
            if (state.Comp?.Props.Action.WalksToNearest == true)
            {
                Pawn nearest = EgoCorrosion.Nearest(pawn);
                if (nearest != null && !pawn.Position.AdjacentTo8WayOrInside(nearest.Position)
                    && pawn.CanReach(nearest, PathEndMode.Touch, Danger.Deadly))
                {
                    Job walk = JobMaker.MakeJob(EgoDefOf.AG_EgoCorrodedWalk, nearest);
                    // Asked again every second, so the walk turns to whoever is nearest now.
                    walk.expiryInterval = 60;
                    walk.checkOverrideOnExpire = true;
                    return walk;
                }
            }
            return JobMaker.MakeJob(EgoDefOf.AG_EgoCorrodedHold);
        }
    }

    /// <summary>
    /// Stands still for 2 s and ends, so the think tree is asked again. Not vanilla Wait: its auto-attack punches an
    /// adjacent hostile, and a corroded pawn attacks only through its weapon's action.
    /// </summary>
    public class JobDriver_EgoCorrodedHold : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil hold = ToilMaker.MakeToil("EgoCorrodedHold");
            hold.initAction = () => pawn.pather.StopDead();
            hold.defaultCompleteMode = ToilCompleteMode.Delay;
            hold.defaultDuration = 120;
            hold.handlingFacing = true;
            yield return hold;
        }
    }

    /// <summary>
    /// Walks to the pawn in job.targetA until touching it. Not vanilla Goto, which ends on the target's cell. A walking
    /// pawn that steps next to its target holds the next time the think tree asks (<see cref="JobGiver_EgoCorroded"/>);
    /// the job expires every second so the target can change. The hold job's stand stops the pawn there.
    /// </summary>
    public class JobDriver_EgoCorrodedWalk : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        }
    }
}
