using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityDisarm : CompProperties_AbilityEffect
    {
        /// <summary>How far the weapon is flung from the target.</summary>
        public float scatterRadius = 3f;

        /// <summary>Dropped weapons are forbidden, so the owner will not instantly re-equip.</summary>
        public bool forbidDropped = true;

        public CompProperties_AbilityDisarm()
        {
            compClass = typeof(CompAbilityEffect_Disarm);
        }
    }

    /// <summary>
    /// Strips the target's equipped weapon and throws it clear. Deliberately does no damage:
    /// the point is to neutralise a threat you would rather capture than kill.
    /// </summary>
    public class CompAbilityEffect_Disarm : CompAbilityEffect
    {
        public new CompProperties_AbilityDisarm Props => (CompProperties_AbilityDisarm)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn victim = target.Pawn;
            ThingWithComps weapon = victim?.equipment?.Primary;
            if (weapon == null || !victim.Spawned) return;

            IntVec3 landing = CellFinder.RandomClosewalkCellNear(
                victim.Position, victim.Map, GenMath.RoundRandom(Props.scatterRadius));

            ThingWithComps dropped;
            if (!victim.equipment.TryDropEquipment(weapon, out dropped, landing, false)) return;

            if (dropped != null && Props.forbidDropped)
            {
                dropped.SetForbidden(true, false);
            }

            Messages.Message(
                "AG_Disarmed".Translate(victim.LabelShort, weapon.LabelShort),
                victim, MessageTypeDefOf.NeutralEvent, false);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn victim = target.Pawn;
            if (victim == null)
            {
                if (throwMessages) Messages.Message("AG_DisarmNeedsPawn".Translate(), target.ToTargetInfo(parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (victim.equipment?.Primary == null)
            {
                if (throwMessages) Messages.Message("AG_DisarmNoWeapon".Translate(victim.LabelShort), victim, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }
    }
}
