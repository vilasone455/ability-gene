using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of the five Shadow plexus abilities: JobDriver_CastChainSickle's shape. The map
    /// component is told the cast has begun when the warmup starts, because the shadow line runs out
    /// during the warmup; the job then holds the caster only for a Neck bind channel (the other four
    /// end at the fire, and the hold or seam lives on in the map component). The channel ends when the
    /// bind lets go or the caster's cell changes; AG_CastShadowNeckBind is player-interruptible, so a
    /// move order ends it too and the hands fall off (<see cref="MapComponent_ShadowPlexus.Ended"/>).
    ///
    /// The vanilla fail conditions apply only until the ability fires (CastJobFail.FailBeforeFired).
    /// </summary>
    public class JobDriver_CastShadowPlexus : JobDriver_CastAbility
    {
        private int pictureTick = -1;
        private IntVec3 stands = IntVec3.Invalid;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                MapComponent_ShadowPlexus.Of(pawn.MapHeld)?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("ShadowPlexusBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("ShadowPlexusHold");
            hold.initAction = () => stands = pawn.Position;
            hold.tickAction = () =>
            {
                MapComponent_ShadowPlexus plexus = MapComponent_ShadowPlexus.Of(pawn.Map);
                if (plexus == null || !plexus.Holds(pawn) || pawn.Position != stands) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => MapComponent_ShadowPlexus.Of(pawn.Map)?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            MapComponent_ShadowPlexus.Of(pawn.Map)?.Begin(pawn, job.ability, job.targetA, job.targetB);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "shadowPlexusPictureTick", -1);
            Scribe_Values.Look(ref stands, "shadowPlexusStands", IntVec3.Invalid);
        }
    }
}
