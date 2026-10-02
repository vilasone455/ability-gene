using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Corrosion, rule 3: the wielder fires the corroded attack on purpose. It stands and fires the weapon's action
    /// overclockCount times, the first at once and then one every overclockInterval, each at the nearest hostile within
    /// overclockRange (an area action skips non-hostiles itself). The job lasts one interval past the last firing, so the
    /// look stays through it, and ends early when no hostile is left in range or the weapon leaves the wielder's hands.
    /// Overclock never rolls Corrosion. If at least one firing went off, the wielder gets AG_EgoOverclocked however the
    /// job ended, so cancelling it after the first firing does not avoid the cost.
    /// </summary>
    public class JobDriver_EgoOverclock : JobDriver
    {
        private int fired;
        private int nextFireTick;
        private int channelStartTick;
        /// <summary>The channel started, so the action's End is owed when the job finishes.</summary>
        private bool began;

        public int Fired => fired;

        public float Seconds => (Find.TickManager.TicksGame - channelStartTick) / 60f;

        /// <summary>The weapon in the wielder's hands; null once it has left them, which fails the job.</summary>
        public CompEgoWeapon Weapon => CompEgoWeapon.HeldBy(pawn);

        /// <summary>
        /// The weapon the job was ordered with (job.targetA), held or not: the cost is paid from its numbers even when the
        /// weapon has left the hands by the time the job ends, and after a load.
        /// </summary>
        private CompEgoWeapon Ordered => (job.targetA.Thing as ThingWithComps)?.GetComp<CompEgoWeapon>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Weapon == null);
            Toil channel = ToilMaker.MakeToil("EgoOverclock");
            channel.initAction = () =>
            {
                pawn.pather.StopDead();
                channelStartTick = Find.TickManager.TicksGame;
                nextFireTick = channelStartTick;
                CompEgoWeapon weapon = Weapon;
                weapon?.Props.Action.Begin(pawn, weapon, overclock: true);
                began = true;
            };
            channel.tickAction = () =>
            {
                CompEgoWeapon weapon = Weapon;
                if (weapon == null || Find.TickManager.TicksGame < nextFireTick) return;
                CompProperties_EgoWeapon props = weapon.Props;
                Pawn target = EgoCorrosion.NearestHostile(pawn, props.overclockRange);
                if (fired >= props.overclockCount || target == null)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (EgoCorrosion.Fire(pawn, weapon, target, hostilesOnly: true)) fired++;
                nextFireTick = Find.TickManager.TicksGame + props.OverclockIntervalTicks;
            };
            channel.handlingFacing = true;
            channel.defaultCompleteMode = ToilCompleteMode.Never;
            AddFinishAction(condition =>
            {
                CompEgoWeapon weapon = Ordered ?? Weapon;
                if (began && weapon != null) weapon.Props.Action.End(pawn, weapon, overclock: true);
                if (fired > 0 && weapon != null) EgoCorrosion.PayOverclock(pawn, weapon.Props);
            });
            yield return channel;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fired, "fired");
            Scribe_Values.Look(ref nextFireTick, "nextFireTick");
            Scribe_Values.Look(ref channelStartTick, "channelStartTick");
            Scribe_Values.Look(ref began, "began");
        }
    }

    /// <summary>
    /// The Overclock button on an E.G.O. weapon's wielder (a colonist the player controls). Off while the wielder is
    /// already overclocking or no hostile is within overclockRange. The weapon rides on the job as targetA.
    /// </summary>
    public class Command_EgoOverclock : Command_Action
    {
        public Command_EgoOverclock(CompEgoWeapon weapon, Pawn wielder)
        {
            CompProperties_EgoWeapon p = weapon.Props;
            defaultLabel = "Overclock";
            defaultDesc = "Fire " + weapon.parent.LabelNoCount + "'s corroded attack on purpose: " + p.overclockCount
                + " times, one every " + p.overclockInterval.ToString("0.#") + " s, at the nearest hostile within "
                + p.overclockRange.ToString("0.#") + " cells. Allies are never hit. Costs " + p.overclockMood
                + " mood for " + p.overclockMoodDays.ToString("0.#") + " days.";
            icon = weapon.parent.def.uiIcon;
            action = () => wielder.jobs.TryTakeOrderedJob(JobMaker.MakeJob(EgoDefOf.AG_EgoOverclock, weapon.parent), JobTag.Misc);
            if (wielder.CurJobDef == EgoDefOf.AG_EgoOverclock) Disable("Already overclocking.");
            else if (!EgoCorrosion.HostileInRange(wielder, p.overclockRange)) Disable("No hostile within " + p.overclockRange.ToString("0.#") + " cells.");
        }
    }
}
