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

        public int Fired => fired;

        public float Seconds => (Find.TickManager.TicksGame - channelStartTick) / 60f;

        public CompEgoWeapon Weapon => CompEgoWeapon.HeldBy(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Weapon == null);
            CompProperties_EgoWeapon paid = null;
            Toil channel = ToilMaker.MakeToil("EgoOverclock");
            channel.initAction = () =>
            {
                pawn.pather.StopDead();
                channelStartTick = Find.TickManager.TicksGame;
                nextFireTick = channelStartTick;
            };
            channel.tickAction = () =>
            {
                CompEgoWeapon weapon = Weapon;
                if (weapon == null || Find.TickManager.TicksGame < nextFireTick) return;
                CompProperties_EgoWeapon props = weapon.Props;
                if (fired >= props.overclockCount || !EgoCorrosion.HostileInRange(pawn, props.overclockRange))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (EgoCorrosion.Fire(pawn, weapon, hostilesOnly: true))
                {
                    fired++;
                    paid = props;
                }
                nextFireTick = Find.TickManager.TicksGame + props.OverclockIntervalTicks;
            };
            channel.handlingFacing = true;
            channel.defaultCompleteMode = ToilCompleteMode.Never;
            AddFinishAction(condition =>
            {
                CompProperties_EgoWeapon props = paid ?? Weapon?.Props;
                if (fired > 0 && props != null) EgoCorrosion.PayOverclock(pawn, props);
            });
            yield return channel;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fired, "fired");
            Scribe_Values.Look(ref nextFireTick, "nextFireTick");
            Scribe_Values.Look(ref channelStartTick, "channelStartTick");
        }
    }

    /// <summary>
    /// The Overclock button on an E.G.O. weapon's wielder (a colonist the player controls). Off while the wielder is
    /// already overclocking or no hostile is within overclockRange.
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
            action = () => wielder.jobs.TryTakeOrderedJob(JobMaker.MakeJob(EgoDefOf.AG_EgoOverclock), JobTag.Misc);
            if (wielder.CurJobDef == EgoDefOf.AG_EgoOverclock) Disable("Already overclocking.");
            else if (!EgoCorrosion.HostileInRange(wielder, p.overclockRange)) Disable("No hostile within " + p.overclockRange.ToString("0.#") + " cells.");
        }
    }
}
