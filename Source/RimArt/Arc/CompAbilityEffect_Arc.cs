using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityArc : CompProperties_AbilityEffect
    {
        /// <summary>How many people one cast can reach, the first one included.</summary>
        public int maxTargets = 3;

        /// <summary>How far the arc will look from one target to find the next.</summary>
        public float chainRadius = 9.9f;

        public CompProperties_AbilityArc()
        {
            compClass = typeof(CompAbilityEffect_Arc);
        }
    }

    /// <summary>
    /// The cast, and almost nothing else. It picks the chain, hands it to the map and stops
    /// existing - everything that looks like the ability happens over the next few seconds in
    /// <see cref="ArcRun"/>.
    ///
    /// The reason the chain is built here rather than there is that the player is owed a cast
    /// that means something: what the arc will do is decided in the tick the button is pressed,
    /// against the fight the player is looking at.
    /// </summary>
    public class CompAbilityEffect_Arc : CompAbilityEffect
    {
        public new CompProperties_AbilityArc Props => (CompProperties_AbilityArc)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn carrier = parent.pawn;
            Pawn first = target.Pawn;
            if (carrier == null || first == null || carrier.Map == null) return;

            List<Pawn> chain = ArcUtility.BuildChain(carrier, first, Props.chainRadius, Props.maxTargets);
            if (chain.Count == 0) return;

            MapComponent_Arc.Begin(carrier, chain);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn carrier = parent.pawn;
            if (carrier == null || carrier.Map == null) return false;

            Pawn victim = target.Pawn;
            if (victim == null)
            {
                if (throwMessages)
                    Messages.Message("AG_ArcNeedsPawn".Translate(), carrier, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!ArcUtility.IsStrikeable(carrier, victim))
            {
                if (throwMessages)
                    Messages.Message("AG_ArcBadTarget".Translate(victim.LabelShortCap), carrier, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (MapComponent_Arc.Running(carrier))
            {
                if (throwMessages)
                    Messages.Message("AG_ArcRunning".Translate(carrier.LabelShortCap), carrier, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Refused rather than silently downgraded to a bare swing: an arc with no animation
            // at the end of it is the ability with its point removed, and a player who pressed
            // this deserves to be told what is missing rather than shown a worse version of it.
            if (!MeleeAnimation.CanStrikeWith(carrier))
            {
                if (throwMessages)
                    Messages.Message("AG_ArcNoWeapon".Translate(carrier.LabelShortCap), carrier, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
