using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityVectorShove : CompProperties_AbilityEffect
    {
        public float pushCells = 6f;
        public int stunTicks = 60;

        /// <summary>Blunt damage per cell actually travelled.</summary>
        public float damagePerCell = 1.5f;

        /// <summary>Extra blunt damage when something stopped the throw early.</summary>
        public float slamDamage = 8f;

        /// <summary>Heavier bodies carry more momentum and move less.</summary>
        public bool scaleByBodySize = true;

        public CompProperties_AbilityVectorShove()
        {
            compClass = typeof(CompAbilityEffect_VectorShove);
        }
    }

    /// <summary>
    /// Seizes one target's momentum and spends it away from the caster. Cheap and short, so the
    /// kit is not purely a wall: it breaks a melee grip, throws a sapper off a wall, or puts a
    /// raider back out in the open.
    /// </summary>
    public class CompAbilityEffect_VectorShove : CompAbilityEffect
    {
        public new CompProperties_AbilityVectorShove Props => (CompProperties_AbilityVectorShove)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn victim = target.Pawn;
            if (victim == null || !victim.Spawned) return;

            VectorPush.Shove(victim, parent.pawn.Position, Props.pushCells, Props.stunTicks,
                Props.damagePerCell, Props.slamDamage, parent.pawn, Props.scaleByBodySize);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Pawn == null)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_ShoveNeedsPawn".Translate(),
                        target.ToTargetInfo(parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.Valid(target, throwMessages);
        }
    }
}
