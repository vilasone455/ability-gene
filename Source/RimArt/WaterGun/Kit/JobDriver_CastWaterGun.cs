using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Stream Shot and Hydro Pump. It is JobDriver_CastAbility with the two additions
    /// JobDriver_CastPowerPole makes: the picture is told the cast has begun when the warmup starts,
    /// because the gun comes up (and the bag is pumped) during the warmup; and the job holds until the
    /// gun is back at rest, so the caster does not walk off with the picture's gun still raised where
    /// it stood. No animation clip: the picture draws the gun, the hose and the bag.
    /// </summary>
    public class JobDriver_CastWaterGun : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_WaterGun>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("WaterGunBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("WaterGunHold");
            hold.tickAction = () =>
            {
                MapComponent_WaterGun guns = pawn.Map?.GetComponent<MapComponent_WaterGun>();
                if (guns == null || !guns.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pawn.Map?.GetComponent<MapComponent_WaterGun>()?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_WaterGun>().Begin(pawn, job.ability, job.targetA);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "waterGunPictureTick", -1);
        }
    }
}
