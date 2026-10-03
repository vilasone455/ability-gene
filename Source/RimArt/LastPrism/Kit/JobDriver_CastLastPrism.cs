using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Fire (JobDriver_CastVergil's shape): it starts the cast when the warmup begins, so the idle prism
    /// turns to the target, and after the fire holds the wielder, facing the aim, for as long as the beam fires. The job
    /// def is player-interruptible, so a move or draft order ends it and the finish action stops the beam, as Stop does.
    /// The job def has neverShowWeapon: the picture draws the prism.
    ///
    /// The vanilla fail conditions apply only until the ability fires (CastJobFail.FailBeforeFired).
    /// </summary>
    public class JobDriver_CastLastPrism : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate { GameComponent_LastPrism.Instance?.Stop(pawn); });

            Toil begin = ToilMaker.MakeToil("LastPrismBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("LastPrismHold");
            hold.initAction = () => pawn.pather.StopDead();
            hold.tickAction = () =>
            {
                LastPrismCast cast = GameComponent_LastPrism.Instance?.FiringBy(pawn);
                if (cast == null)
                {
                    ReadyForNextToil();
                    return;
                }
                pawn.Rotation = LastPrismBeam.Facing(cast.aim);
            };
            hold.handlingFacing = true;
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pictureTick >= 0 && (GameComponent_LastPrism.Instance?.FiredSince(pawn, pictureTick) ?? false);

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            LastPrismCast cast = GameComponent_LastPrism.Make(pawn, job.targetA, pictureTick);
            if (cast != null) GameComponent_LastPrism.Instance?.Begin(cast);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "lastPrismPictureTick", -1);
        }
    }
}
