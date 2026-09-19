using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using T = RimArt.ClapTeleport;

namespace RimArt
{
    /// <summary>
    /// The cast job of the clap and the double clap. It is JobDriver_CastAbility with three
    /// additions: the clap clip starts with the warmup, so the palms meet as the warmup ends and
    /// the swap happens under them; the teleport picture is told the cast has begun, so the cards
    /// can rise before the contact; and the job holds until the clip's open-hands pose is done,
    /// because Melee Animation cancels a clip the moment its pawn is in any other job.
    ///
    /// The toils are restated rather than taken from the base class. The base fails the whole job
    /// once the ability can no longer be cast, which is true the moment it has been cast, and that
    /// would end the hold on its first tick. Here that condition is on the cast toil alone.
    /// </summary>
    public class JobDriver_CastClap : JobDriver_CastAbility
    {
        private int clapStartTick = -1;

        private bool Twice => job.ability?.CompOfType<CompAbilityEffect_DoubleClap>() != null;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                ClapCastAnimation.Stop(pawn, job.def);
                pawn.MapHeld?.GetComponent<MapComponent_ClapTeleports>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("ClapBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            cast.FailOn(() => !job.ability.CanCast && !job.ability.Casting);
            cast.AddPreTickAction(() => Seek());
            yield return cast;

            Toil hold = ToilMaker.MakeToil("ClapHold");
            hold.tickAction = () => { if (!Seek()) ReadyForNextToil(); };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private void Begin()
        {
            pawn.pather.StopDead();
            if (clapStartTick >= 0) return;
            clapStartTick = Find.TickManager.TicksGame;
            bool animated = ClapCastAnimation.TryStart(pawn, Twice, job.def);

            Gene_Anchors gene = AnchorUtility.GeneOf(pawn);
            Anchor first = gene?.AnchorFor(job.targetA), second = Twice ? gene?.AnchorFor(job.targetB) : null;
            if (first == null || (Twice && second == null)) return;
            pawn.Map.GetComponent<MapComponent_ClapTeleports>()
                .Begin(pawn, first, second, job.ability.def.verbProperties.warmupTime, Twice, animated);
        }

        /// <summary>Moves the clip to now. False once the clip is over, or when there is none.</summary>
        private bool Seek()
        {
            float seconds = (Find.TickManager.TicksGame - clapStartTick) / 60f;
            return seconds < (Twice ? T.ClipTwiceLength : T.ClipLength) && ClapCastAnimation.Seek(pawn, job.def, seconds);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref clapStartTick, "clapStartTick", -1);
        }
    }
}
