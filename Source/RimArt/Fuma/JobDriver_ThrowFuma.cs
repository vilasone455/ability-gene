using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimArt
{
    public class JobDriver_ThrowFuma : JobDriver
    {
        private bool released;
        private int windupStartTick = -1;
        public bool Animated { get; private set; }
        private ThingWithComps Weapon => job.targetB.Thing as ThingWithComps;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(_ => ThrowAnimation.UpdateCustomThrow(pawn, FumaDefOf.AG_ThrowFuma, 0f, true));
            var windup = ToilMaker.MakeToil("FumaWindup");
            windup.initAction = () =>
            {
                pawn.pather.StopDead();
                if (windupStartTick < 0) windupStartTick = Find.TickManager.TicksGame;
                ThrowAnimation.TryThrow(pawn, TargetA.Cell, null, ThrowAnimation.Fuma, out _, FumaDefOf.AG_ThrowFuma);
            };
            windup.tickAction = () =>
            {
                pawn.rotationTracker.FaceCell(TargetA.Cell);
                int elapsed = Find.TickManager.TicksGame - windupStartTick;
                Animated = ThrowAnimation.UpdateCustomThrow(pawn, FumaDefOf.AG_ThrowFuma, elapsed / 60f);
                if (!released)
                {
                    CompFuma comp = Weapon?.TryGetComp<CompFuma>();
                    string refusal = comp == null ? "The weapon is gone." : comp.Refusal(pawn, TargetA.Cell);
                    if (refusal != null) { EndJobWith(JobCondition.Incompletable); return; }
                    if (elapsed >= FumaRules.WarmupTicks)
                    {
                        if (!Projectile_Fuma.Release(pawn, Weapon, TargetA.Cell))
                        { EndJobWith(JobCondition.Incompletable); return; }
                        comp.Released();
                        released = true;
                        DefDatabase<SoundDef>.GetNamed("ThrowGrenade").PlayOneShot(pawn);
                    }
                }
                if (elapsed >= FumaRules.AnimationTicks) ReadyForNextToil();
            };
            windup.defaultCompleteMode = ToilCompleteMode.Never;
            yield return windup;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref released, "released");
            Scribe_Values.Look(ref windupStartTick, "windupStartTick", -1);
        }
    }
}
