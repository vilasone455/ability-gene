using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityBladeGrasp : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityBladeGrasp()
        {
            compClass = typeof(CompAbilityEffect_BladeGrasp);
        }
    }

    /// <summary>
    /// One blade out of the ground and into a hand.
    ///
    /// It is the smallest ability in the gene and the one that decides what the gene is for. A
    /// planted blade cannot be picked up by anybody - it is not an item - so the field is not a
    /// pile of free weapons lying in front of a raid. Grasp is the only door out of that, it
    /// costs a cast, and what comes through it is one ordinary steel longsword in one named
    /// hand.
    ///
    /// The hand has to be empty. Emptying it here would mean a colonist's rifle hitting the
    /// floor in the middle of a firefight because somebody pressed a button about swords.
    /// </summary>
    public class CompAbilityEffect_BladeGrasp : CompAbilityEffect
    {
        public new CompProperties_AbilityBladeGrasp Props => (CompProperties_AbilityBladeGrasp)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return;

            Pawn recipient = target.Pawn ?? caster;
            PlantedBlade blade = PanoplyUtility.NearestBlade(caster, map, recipient.Position);
            if (blade == null) return;

            Vector3 from = blade.DrawPos;
            IntVec3 cell = blade.Position;
            blade.PullOut();

            Thing spawned = GenSpawn.Spawn(PanoplyDefOf.AG_PanoplyBladeFlying, cell, map);
            BladeFlight flight = spawned as BladeFlight;
            if (flight == null)
            {
                if (spawned != null && !spawned.Destroyed) spawned.Destroy();
                return;
            }

            flight.Configure(caster, recipient, from);
            PanoplyUtility.ImpactEffect(cell, map);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return false;

            Pawn recipient = target.Pawn ?? caster;

            if (recipient.HostileTo(caster))
            {
                if (throwMessages)
                {
                    Messages.Message("AG_PanoplyNotToEnemy".Translate(recipient.LabelShortCap),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (!PanoplyGrasp.CanTakeBlade(recipient))
            {
                if (throwMessages)
                {
                    Messages.Message("AG_PanoplyHandFull".Translate(recipient.LabelShortCap),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (PanoplyRegistry.CountOf(caster) == 0)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_PanoplyNoBlades".Translate(caster.LabelShortCap),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }

    /// <summary>
    /// The one question grasp asks about a hand, kept where both the ability and the blade in
    /// flight can ask it - the check has to give the same answer at targeting time and at
    /// arrival, because a hand can be filled while a blade is in the air.
    /// </summary>
    public static class PanoplyGrasp
    {
        public static bool CanTakeBlade(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || !pawn.Spawned) return false;
            if (pawn.equipment == null) return false;
            if (pawn.equipment.Primary != null) return false;
            if (pawn.RaceProps == null || !pawn.RaceProps.ToolUser) return false;
            if (pawn.WorkTagIsDisabled(WorkTags.Violent)) return false;
            return true;
        }
    }
}
