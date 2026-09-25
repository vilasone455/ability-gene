using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Devour and Release: JobDriver_CastChainSickle's shape. The picture is told the
    /// cast has begun when the warmup starts (the arm comes forward and the palm opens, or the glow
    /// slides into the fist); the job holds the wearer in place until the result (the last fire is in
    /// the palm, or the wave has stopped). The picture draws the gauntlet, so the job hides the held
    /// weapon (neverShowWeapon).
    ///
    /// The vanilla fail conditions (target gone, ability can no longer be cast) apply only until the
    /// ability is applied: after it the ability is on cooldown (Ability.PreActivate starts it) and
    /// would end the job at once. The target is a cell for both abilities, so "gone" only applies
    /// when a thing was clicked.
    /// </summary>
    public class JobDriver_CastFlameGauntlet : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !Applied() && (TargetGone() || !job.ability.CanCast && !job.ability.Casting));
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_FlameGauntlet>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("FlameGauntletBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("FlameGauntletHold");
            hold.tickAction = () =>
            {
                MapComponent_FlameGauntlet casts = pawn.Map?.GetComponent<MapComponent_FlameGauntlet>();
                if (casts == null || !casts.Holds(pawn)) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Applied() => pawn.Map?.GetComponent<MapComponent_FlameGauntlet>()?.Applied(pawn) ?? false;

        private bool TargetGone()
        {
            if (!job.targetA.HasThing) return !job.targetA.Cell.IsValid;
            Thing target = job.targetA.Thing;
            return target == null || !target.Spawned || target.Map != pawn.Map;
        }

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_FlameGauntlet>().Begin(pawn, job.ability, job.targetA);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "flameGauntletPictureTick", -1);
        }
    }
}
