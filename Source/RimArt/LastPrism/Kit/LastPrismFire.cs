using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>The beam's balance, as XML fields on AG_LastPrism_Fire. The range is the ability verb's.</summary>
    public class CompProperties_LastPrismFire : CompProperties_AbilityEffect
    {
        /// <summary>Seconds of fan before the six beams join.</summary>
        public float joinSeconds = 3f;
        /// <summary>The fan's half angle: the beams sweep up to this many degrees either side of the aim.</summary>
        public float fanDegrees = 35f;
        /// <summary>The joined beam's width, which is the lane it hits (cells).</summary>
        public float width = 1f;
        /// <summary>A pawn this close to a fan beam's line (cells) is crossed by it.</summary>
        public float fanReach = 0.3f;
        /// <summary>How fast the prism turns after its target or a Retarget.</summary>
        public float turnDegreesPerSecond = 45f;

        public DamageDef damageDef;
        /// <summary>Fan: the burn to a pawn any beam crosses and the least gap between two on one pawn.</summary>
        public float fanDamage = 3f;
        public float fanArmorPenetration = 0.1f;
        public float fanEverySeconds = 0.5f;
        /// <summary>Joined: the burn to each pawn in the lane and the least gap between two on one pawn (8 each 0.25 s = 32/s).</summary>
        public float joinedDamage = 8f;
        public float joinedArmorPenetration = 0.45f;
        public float joinedEverySeconds = 0.25f;
        /// <summary>A beam started this soon after the last one from the same prism stopped starts joined: the fan only comes back after a pause.</summary>
        public float rejoinSeconds = 1f;

        public CompProperties_LastPrismFire()
        {
            compClass = typeof(CompAbilityEffect_LastPrismFire);
        }

        /// <summary>The ability's range, which is the beam's: one number in the XML for the targeting, the hits and the picture.</summary>
        public float Range => LastPrismDefOf.AG_LastPrism_Fire.verbProperties.range;

        public static CompProperties_LastPrismFire Of => LastPrismDefOf.AG_LastPrism_Fire.comps.Find(c => c is CompProperties_LastPrismFire) as CompProperties_LastPrismFire
                                                         ?? new CompProperties_LastPrismFire();
    }

    /// <summary>
    /// Fire. Needs the prism in hand with some charge; hidden while its beam is running, when Retarget and Stop stand
    /// in for it. On the fire tick the channel starts (<see cref="LastPrismCast.MarkFired"/>); from then the cast's own
    /// clock decides the hits, the turning and when it stops.
    /// </summary>
    public class CompAbilityEffect_LastPrismFire : CompAbilityEffect
    {
        public new CompProperties_LastPrismFire Props => (CompProperties_LastPrismFire)props;

        private CompLastPrism Prism => CompLastPrism.HeldBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        public override bool ShouldHideGizmo => GameComponent_LastPrism.Instance?.FiringBy(parent.pawn) != null;

        private string Unavailable()
        {
            CompLastPrism prism = Prism;
            if (prism == null) return "Needs the last prism in hand.";
            if (prism.Level < 1f / 60f) return "The prism is empty: it fills in sunlight.";
            if (GameComponent_LastPrism.Instance?.FiringBy(parent.pawn) != null) return "The prism is already firing.";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            CompLastPrism prism = Prism;
            return prism == null ? null : prism.Level.ToString("0.0") + " s of beam";
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            LastPrismCast cast = GameComponent_LastPrism.For(parent, target);
            cast?.MarkFired(Find.TickManager.TicksGame);
        }
    }
}
