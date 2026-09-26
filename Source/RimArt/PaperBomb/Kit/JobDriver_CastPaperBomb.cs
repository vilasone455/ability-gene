using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The cast job of Tag Throw, Tag Line and Paper Shroud. It is JobDriver_CastAbility with the
    /// additions JobDriver_CastMark makes, for the same reasons: the clip starts with the warmup, so
    /// the hand has been to the roll by the time the warmup ends; and the job holds until the clip is
    /// done, because Melee Animation cancels a clip the moment its pawn is in any other job. For Tag
    /// Line that hold is the 2.5 s the strip takes to run out, which is the ability's cast time.
    ///
    /// The clips are throw clip sets (make_paper_bomb_anim.py), so they go through ThrowAnimation,
    /// which picks the facing and sets the aim; this job owns their clock. Without Melee Animation the
    /// job holds for the same length with no clip. Detonate's hand seal has no clip wired yet and uses
    /// Core's cast job.
    /// </summary>
    public class JobDriver_CastPaperBomb : JobDriver_CastAbility
    {
        /// <summary>RimArt_TagThrow, RimArt_TagFlick and RimArt_TagFan: release fraction, then length in seconds.</summary>
        private static readonly ThrowAnimation.Clips TagThrow = new ThrowAnimation.Clips("AG_TagThrow", 0.5f),
            TagFlick = new ThrowAnimation.Clips("AG_TagFlick", 0.1071f), TagFan = new ThrowAnimation.Clips("AG_TagFan", 0.2917f);
        internal const float ThrowLength = 0.6f, FlickLength = 2.8f, FanLength = 1.2f;

        private int startTick = -1;

        private ThrowAnimation.Clips Clips => job.ability.def == PaperBombDefOf.AG_PaperBomb_TagLine ? TagFlick
            : job.ability.def == PaperBombDefOf.AG_PaperBomb_Shroud ? TagFan : TagThrow;

        private float Length => job.ability.def == PaperBombDefOf.AG_PaperBomb_TagLine ? FlickLength
            : job.ability.def == PaperBombDefOf.AG_PaperBomb_Shroud ? FanLength : ThrowLength;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                ThrowAnimation.UpdateCustomThrow(pawn, job.def, 0f, true);
                pawn.MapHeld?.GetComponent<MapComponent_PaperBomb>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("PaperBombBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            cast.AddPreTickAction(Seek);
            yield return cast;

            Toil hold = ToilMaker.MakeToil("PaperBombHold");
            hold.tickAction = () =>
            {
                Seek();
                if ((Find.TickManager.TicksGame - startTick) / 60f >= Length) ReadyForNextToil();
            };
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pawn.Map?.GetComponent<MapComponent_PaperBomb>()?.Fired(pawn) ?? false;

        private void Begin()
        {
            pawn.pather.StopDead();
            if (startTick >= 0 || job.ability == null) return;
            startTick = Find.TickManager.TicksGame;
            ThrowAnimation.TryThrow(pawn, job.targetA.Cell, null, Clips, out _, job.def);
        }

        /// <summary>Moves the clip to now.</summary>
        private void Seek()
        {
            if (startTick >= 0) ThrowAnimation.UpdateCustomThrow(pawn, job.def, (Find.TickManager.TicksGame - startTick) / 60f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "paperBombStartTick", -1);
        }
    }
}
