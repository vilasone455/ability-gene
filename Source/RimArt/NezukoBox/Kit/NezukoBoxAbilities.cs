using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    public class CompProperties_NezukoBoxGoIn : CompProperties_AbilityEffect
    {
        /// <summary>How close the wearer must be to the pawn when the ability fires, cells (touch).</summary>
        public float touchRange = 1.5f;

        public CompProperties_NezukoBoxGoIn()
        {
            compClass = typeof(CompAbilityEffect_NezukoBoxGoIn);
        }
    }

    /// <summary>
    /// Go in: the wearer walks up to the pawn (JobDriver_CastNezukoBox), the door opens over the warmup,
    /// and the pawn goes into the box at the picture's moment (MapComponent_NezukoBox). Who may go in is
    /// <see cref="CompNezukoBox.CannotTake"/>.
    /// </summary>
    public class CompAbilityEffect_NezukoBoxGoIn : CompAbilityEffect
    {
        public new CompProperties_NezukoBoxGoIn Props => (CompProperties_NezukoBoxGoIn)props;

        private CompNezukoBox Box => CompNezukoBox.WornBy(parent.pawn);

        public override bool CanCast => base.CanCast && Box != null && !Box.Full;

        public override bool GizmoDisabled(out string reason)
        {
            CompNezukoBox box = Box;
            if (box == null) reason = "Requires the box on the back.";
            else if (box.Full) reason = "The box is full: " + box.Sleeper.LabelShort + " is inside.";
            else reason = null;
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string why = Box == null ? "Requires the box on the back." : Box.CannotTake(target.Pawn, parent.pawn);
            if (why != null)
            {
                if (throwMessages) Messages.Message(why, target.Pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target) && base.CanApplyOn(target, dest);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn wearer = parent.pawn, pawn = target.Pawn;
            if (wearer?.Map == null || pawn == null) return;
            wearer.Map.GetComponent<MapComponent_NezukoBox>().Fire(wearer, pawn, parent, Props.touchRange);
        }
    }

    /// <summary>
    /// The cast job of Go in: walk to the pawn, then the warmup (the door opening, which the picture is
    /// told has begun), then hold the wearer in place until the latch. The pawn is asked to wait for it.
    /// The vanilla fail conditions apply only until the ability fires (CastJobFail.FailBeforeFired).
    /// </summary>
    public class JobDriver_CastNezukoBox : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_NezukoBox>()?.Ended(pawn);
            });

            Toil walk = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            walk.FailOn(() => job.ability?.CompOfType<CompAbilityEffect_NezukoBoxGoIn>()?.Valid(job.targetA) == false);
            yield return walk;

            Toil begin = ToilMaker.MakeToil("NezukoBoxBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("NezukoBoxHold");
            hold.tickAction = () =>
            {
                MapComponent_NezukoBox box = pawn.Map?.GetComponent<MapComponent_NezukoBox>();
                if (box == null || !box.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pawn.Map?.GetComponent<MapComponent_NezukoBox>()?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            Pawn target = job.targetA.Pawn;
            if (target != null) pawn.rotationTracker.FaceTarget(target);
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast || target == null) return;
            pictureTick = Find.TickManager.TicksGame;
            int warmup = Mathf.RoundToInt(job.ability.def.verbProperties.warmupTime * 60f);
            pawn.Map.GetComponent<MapComponent_NezukoBox>().Begin(pawn, target, warmup);
            // An awake pawn waits for the door rather than walking off.
            if (!target.Downed && target.jobs != null && target.Spawned)
                PawnUtility.ForceWait(target, warmup + 30, pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "nezukoBoxPictureTick", -1);
        }
    }
}
