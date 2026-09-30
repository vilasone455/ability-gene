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
    ///
    /// Instant Transmission's warmup is its lock channel, up to 12 s, so it has its own job def
    /// (AG_GokuTransmit) that a move order can interrupt. A vanilla stun only pauses a warmup; here it
    /// ends the channel. A channel broken by a stun, a downing or death spends the cooldown; Cancel or
    /// a move order spends nothing. The charge is paid only when the ability fires (EchoCastPayment).
    /// </summary>
    public class JobDriver_CastGoku : JobDriver_CastAbility
    {
        private int pictureTick = -1;
        private bool lockBroken;

        private bool Transmission => job.ability?.def == GokuDefOf.AG_GokuInstantTransmission;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            this.FailOn(StunnedInLock);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                else if (job.ability != null && Transmission && !Fired() && (lockBroken || pawn.Downed || pawn.Dead))
                {
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                    if (pawn.Spawned)
                        Messages.Message("Instant Transmission: the lock broke. The cooldown is spent, the charge is not.", pawn, MessageTypeDefOf.NegativeEvent, false);
                }
                GameComponent_Goku.Instance?.WarmupEnded(pawn);
            });

            Toil begin = ToilMaker.MakeToil("GokuBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
        }

        private bool Fired() => pictureTick >= 0 && (GameComponent_Goku.Instance?.FiredSince(pawn, pictureTick) ?? false);

        /// <summary>A stun during Instant Transmission's channel ends it (jobs tick while stunned; the warmup does not).</summary>
        private bool StunnedInLock()
        {
            if (!Transmission || Fired() || !(pawn.stances?.stunner?.Stunned ?? false)) return false;
            lockBroken = true;
            return true;
        }

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
                IntVec3 dest = job.targetB.IsValid ? job.targetB.Cell : pawn.Position;
                // The same number the verb's warmup is about to take (Verb_GokuTransmission.WarmupTime).
                warm = GokuTransmissionLock.Seconds(pawn, pawn.Position, dest, passenger, out _, out _);
                goku.Begin(new TransmissionPicture(pawn, pictureTick, warm, dest, passenger));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "gokuPictureTick", -1);
            Scribe_Values.Look(ref lockBroken, "gokuLockBroken");
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
    /// until the channel ends, the ball is full, its Rest falls below the bomb's stopRest, or Stop
    /// lending is pressed. The cast takes the Rest (<see cref="SpiritBombCast"/>).
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
                if (!(GameComponent_Goku.Instance?.For(Caster) is SpiritBombCast bomb) || !bomb.Channelling || bomb.Full || SpiritBombCast.TooTired(pawn))
                    EndJobWith(JobCondition.Incompletable);
            };
            lend.tickIntervalAction = delta =>
            {
                if (!(GameComponent_Goku.Instance?.For(Caster) is SpiritBombCast bomb) || !bomb.Channelling || bomb.Full)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (SpiritBombCast.TooTired(pawn))
                {
                    Messages.Message(pawn.LabelShortCap + " is too tired to lend more ki.", pawn, MessageTypeDefOf.NeutralEvent, false);
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
