using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Trace On and Reinforcement (the Vergil shape: JobDriver_CastVergil). It starts the cast's
    /// picture when the job begins. Trace On first empties the hand: a held copy breaks at once and the warmup
    /// starts <see cref="TraceOnTiming.SwapGap"/> later; a real weapon is drawn sliding to the hip for
    /// <see cref="TraceOnTiming.StowTime"/> and then goes to the inventory, so it stays there if the cast is called
    /// off afterwards. During the warmup the weapon is carried, not aimed at the caster itself (the vanilla warmup of
    /// a self-cast points it north). After the fire the job keeps the pawn standing for as long as the cast says.
    /// alwaysShowWeapon on the job def shows the weapon while an undrafted pawn casts.
    /// </summary>
    public class JobDriver_CastTrace : JobDriver_CastAbility
    {
        private int traceTick = -1;

        private TraceCast Cast => traceTick < 0 ? null : GameComponent_Trace.Instance?.CastOf(pawn, job.ability?.def);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate { GameComponent_Trace.Instance?.JobEnded(pawn); });

            Toil begin = ToilMaker.MakeToil("TraceBegin");
            begin.initAction = () =>
            {
                Begin();
                if (!ended && (Cast == null || Cast.lead <= 0f)) ReadyForNextToil();
            };
            begin.tickAction = () =>
            {
                TraceCast cast = Cast;
                if (cast != null && Find.TickManager.TicksGame - traceTick < UnityEngine.Mathf.RoundToInt(cast.lead * 60f)) return;
                Stow(cast);
                ReadyForNextToil();
            };
            begin.defaultCompleteMode = ToilCompleteMode.Never;
            yield return begin;

            Toil castVerb = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            System.Action init = castVerb.initAction;
            castVerb.initAction = () =>
            {
                init?.Invoke();
                if (!(pawn.stances?.curStance is Stance_Warmup warmup)) return;
                warmup.neverAimWeapon = true;
                if (Cast != null) Cast.cast = warmup.ticksLeft / 60f;
            };
            yield return castVerb;

            Toil hold = ToilMaker.MakeToil("TraceHold");
            hold.tickAction = () =>
            {
                if (GameComponent_Trace.Instance?.Holding(pawn, Find.TickManager.TicksGame) == null) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => traceTick >= 0 && (GameComponent_Trace.Instance?.FiredSince(pawn, traceTick) ?? false);

        private void Begin()
        {
            pawn.pather.StopDead();
            if (traceTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            traceTick = Find.TickManager.TicksGame;
            var cast = new TraceCast { caster = pawn, def = job.ability.def, startTick = traceTick, cast = job.ability.def.verbProperties.warmupTime };
            if (cast.TraceOn)
            {
                var comp = job.ability.CompOfType<CompAbilityEffect_TraceOn>();
                TraceLibraryEntry entry = comp?.Chosen;
                if (entry == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                cast.pending = TraceLibrary.MakeCopy(entry, comp.Props.qualityBelow);
                ThingWithComps held = pawn.equipment?.Primary;
                if (held != null && TraceCopies.IsCopy(held))
                {
                    TraceCopies.Break(held, false);
                    cast.lead = TraceOnTiming.SwapGap;
                }
                else if (held != null)
                {
                    cast.stowing = held;
                    cast.lead = TraceOnTiming.StowTime;
                    cast.CaptureStow();
                }
            }
            GameComponent_Trace.Instance?.Begin(cast);
        }

        /// <summary>The lead is over: a real weapon being put away goes to the inventory (to the ground without one).</summary>
        private void Stow(TraceCast cast) => WeaponStow.Stow(pawn, cast?.stowing);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref traceTick, "traceStartTick", -1);
        }
    }

    /// <summary>Tells the cast its ability fired: on Reinforcement beside the vanilla give-hediff comp (Trace On's comp does it itself).</summary>
    public class CompProperties_TraceFired : CompProperties_AbilityEffect
    {
        public CompProperties_TraceFired() => compClass = typeof(CompAbilityEffect_TraceFired);
    }

    public class CompAbilityEffect_TraceFired : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            GameComponent_Trace.Instance?.Fired(parent.pawn, parent.def);
        }
    }
}
