using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of the pole's three abilities. It is JobDriver_CastAbility with two additions, the
    /// ones JobDriver_CastMark makes: the picture is told the cast has begun when the warmup starts,
    /// because the pole slides back and is planted during the warmup; and the job holds until the
    /// pole is back to its carried length, so the wielder does not walk off holding 12 tiles of it.
    ///
    /// With Melee Animation the wielder's hands and body follow the pole (PowerPoleCastAnimation).
    /// The clip starts with the warmup and this job owns its clock. Vault Strike's clip is only the
    /// plant: the wielder then leaves the map inside the flyer, where no clip can follow.
    /// </summary>
    public class JobDriver_CastPowerPole : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                PowerPoleCastAnimation.Stop(pawn, job.def);
                pawn.MapHeld?.GetComponent<MapComponent_PowerPoleCasts>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("PowerPoleBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            cast.FailOn(() => !job.ability.CanCast && !job.ability.Casting);
            cast.AddPreTickAction(Seek);
            yield return cast;

            Toil hold = ToilMaker.MakeToil("PowerPoleHold");
            hold.tickAction = () =>
            {
                Seek();
                MapComponent_PowerPoleCasts casts = pawn.Map?.GetComponent<MapComponent_PowerPoleCasts>();
                if (casts == null || !casts.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private void Begin()
        {
            pawn.pather.StopDead();
            // A job resumed after the vault's flight finds the ability on cooldown and ends; it must not begin a second picture.
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            MapComponent_PowerPoleCasts casts = pawn.Map.GetComponent<MapComponent_PowerPoleCasts>();
            casts.Begin(pawn, job.ability, job.targetA);
            if (casts.TryGetCast(pawn, out PowerPoleCastKind kind, out Vector2 toward)) PowerPoleCastAnimation.TryStart(pawn, kind, toward, job.def);
        }

        /// <summary>Moves the clip to now.</summary>
        private void Seek()
        {
            if (pictureTick >= 0) PowerPoleCastAnimation.Seek(pawn, job.def, (Find.TickManager.TicksGame - pictureTick) / 60f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "powerPolePictureTick", -1);
        }
    }
}
