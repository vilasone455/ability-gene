using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Solar Flare and Instant Transmission: JobDriver_CastAbility that tells the
    /// picture when the warmup begins (the hands to the face, the fingers to the forehead), so the
    /// flash or the vanish lands on the tick the ability fires. No hold after the fire.
    /// </summary>
    public class JobDriver_CastGoku : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                GameComponent_Goku.Instance?.WarmupEnded(pawn);
            });

            Toil begin = ToilMaker.MakeToil("GokuBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
        }

        private bool Fired() => pictureTick >= 0 && (GameComponent_Goku.Instance?.FiredSince(pawn, pictureTick) ?? false);

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            GameComponent_Goku goku = GameComponent_Goku.Instance;
            if (goku == null) return;
            float warm = job.ability.def.verbProperties.warmupTime;
            if (job.ability.def == GokuDefOf.AG_GokuSolarFlare)
            {
                CompProperties_SolarFlare props = GokuBusy.Props<CompProperties_SolarFlare>(job.ability.def);
                goku.Begin(new SolarFlarePicture(pawn, pictureTick, warm, props?.stunSeconds ?? GokuSolarFlareTiming.ScriptStun, props?.radius ?? GokuSolarFlareTiming.ScriptRadius));
            }
            else if (job.ability.def == GokuDefOf.AG_GokuInstantTransmission)
            {
                Pawn passenger = job.targetA.Pawn == pawn ? null : job.targetA.Pawn;
                goku.Begin(new TransmissionPicture(pawn, pictureTick, warm, job.targetB.IsValid ? job.targetB.Cell : pawn.Position, passenger));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "gokuPictureTick", -1);
        }
    }

    /// <summary>
    /// The Kamehameha and Spirit Bomb channel: the caster stands still facing the aim until the cast
    /// lets it go. If the job ends first (a move order, downed, a mental break), the cast sees it and
    /// breaks.
    /// </summary>
    public class JobDriver_GokuChannel : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil channel = ToilMaker.MakeToil("GokuChannel");
            channel.initAction = () =>
            {
                pawn.pather.StopDead();
                GameComponent_Goku goku = GameComponent_Goku.Instance;
                if (goku?.ChannelStarted(pawn) != true)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                Face(goku.For(pawn));
            };
            channel.tickIntervalAction = delta =>
            {
                GokuCast cast = GameComponent_Goku.Instance?.For(pawn);
                if (cast == null) EndJobWith(JobCondition.Succeeded);
                else Face(cast);
            };
            channel.handlingFacing = true;
            channel.defaultCompleteMode = ToilCompleteMode.Never;
            yield return channel;
        }

        private void Face(GokuCast cast)
        {
            if (cast == null) return;
            IntVec3 at = cast.FacingCell;
            if (at.IsValid && at != pawn.Position) pawn.Rotation = Rot4.FromAngleFlat((at - pawn.Position).ToVector3().AngleFlat());
        }
    }

    /// <summary>
    /// Lending energy to a Spirit Bomb: the colonist stands facing the caster (targetA) with a hand up
    /// until the channel ends or Stop lending is pressed.
    /// </summary>
    public class JobDriver_GokuLend : JobDriver
    {
        private Pawn Caster => job.targetA.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil lend = ToilMaker.MakeToil("GokuLend");
            lend.initAction = () =>
            {
                pawn.pather.StopDead();
                if (!(GameComponent_Goku.Instance?.For(Caster) is SpiritBombCast bomb) || !bomb.Channelling) EndJobWith(JobCondition.Incompletable);
            };
            lend.tickIntervalAction = delta =>
            {
                if (!(GameComponent_Goku.Instance?.For(Caster) is SpiritBombCast bomb) || !bomb.Channelling)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                Pawn caster = Caster;
                if (caster != null && caster.Position != pawn.Position) pawn.Rotation = Rot4.FromAngleFlat((caster.Position - pawn.Position).ToVector3().AngleFlat());
            };
            lend.handlingFacing = true;
            lend.defaultCompleteMode = ToilCompleteMode.Never;
            yield return lend;
        }
    }
}
