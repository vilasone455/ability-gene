using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Chain Arc. It is JobDriver_CastAbility with the two additions the Water Gun's
    /// cast job makes: the picture is told the cast has begun when the warmup starts, because the coil
    /// charges at the muzzle during the warmup; and the job holds until the chain has run out (about
    /// 0.45 s for four pawns), so the bolts leave the muzzle where the caster still stands. The gun is
    /// Core's own, drawn aimed by the warmup stance; the job does not hide it.
    /// </summary>
    public class JobDriver_CastCoilGun : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_CoilGun>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("CoilGunBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("CoilGunHold");
            hold.tickAction = () =>
            {
                MapComponent_CoilGun guns = pawn.Map?.GetComponent<MapComponent_CoilGun>();
                if (guns == null || !guns.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pawn.Map?.GetComponent<MapComponent_CoilGun>()?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_CoilGun>().Begin(pawn, job.ability, job.targetA);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "coilGunPictureTick", -1);
        }
    }
}
