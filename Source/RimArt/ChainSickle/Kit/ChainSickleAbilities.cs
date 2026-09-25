using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>What both chain abilities share: they need the chain sickle in hand and a free hand.</summary>
    public abstract class CompAbilityEffect_ChainSickle : CompAbilityEffect
    {
        protected CompChainSickle Sickle => CompChainSickle.HeldBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        protected virtual string Unavailable()
        {
            if (Sickle == null) return "Requires a chain sickle in hand.";
            if (!parent.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return parent.pawn.LabelShortCap + " cannot manipulate.";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        /// <summary>"ratio 1.15 (75 kg carry / 65 kg)", for tooltips.</summary>
        protected static string RatioText(Pawn holder, Pawn target, in ChainSickleWeight w) =>
            "weight ratio " + w.Ratio.ToString("0.00") + " (" + ChainSickleCombat.Carry(holder).ToString("0") + " kg carry / "
            + ChainSickleCombat.Mass(target).ToString("0") + " kg)";
    }

    public class CompProperties_ChainSickleSnag : CompProperties_AbilityEffect
    {
        /// <summary>The weight's hit when it wraps the target.</summary>
        public float damage = 4f;
        public float armorPenetration = 0f;

        public CompProperties_ChainSickleSnag()
        {
            compClass = typeof(CompAbilityEffect_ChainSickleSnag);
        }
    }

    /// <summary>
    /// Snag. The weight is spun up during the warmup, thrown at a hostile pawn or wild animal, wraps
    /// it (blunt hit) and the holder reels it in, or is dragged toward it when it is too heavy
    /// (CompProperties_ChainSickle's weight rule). The target stays snagged afterwards, which Stake
    /// needs. The hit and the reel happen when the picture shows them (MapComponent_ChainSickle).
    /// </summary>
    public class CompAbilityEffect_ChainSickleSnag : CompAbilityEffect_ChainSickle
    {
        public new CompProperties_ChainSickleSnag Props => (CompProperties_ChainSickleSnag)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string refusal = ChainSickleCombat.SnagRefusal(parent.pawn, target.Thing);
            if (refusal != null)
            {
                if (throwMessages) Messages.Message(refusal, target.Thing, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            CompChainSickle sickle = Sickle;
            if (sickle == null || !(target.Thing is Pawn pawn) || ChainSickleCombat.SnagRefusal(parent.pawn, pawn) != null) return null;
            ChainSickleWeight w = ChainSickleCombat.Weigh(parent.pawn, pawn, sickle.Props);
            string result = w.Dragged
                ? "too heavy: " + parent.pawn.LabelShort + " is dragged " + sickle.Props.dragCells.ToString("0.#") + " cells"
                : "pulls " + w.Pull.ToString("0.#") + " cells in " + w.Reel.ToString("0.#") + " s";
            return RatioText(parent.pawn, pawn, w) + "\n" + result;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !(target.Thing is Pawn pawn) || Sickle == null) return;
            caster.Map.GetComponent<MapComponent_ChainSickle>().LandSnag(caster, pawn, parent.def.verbProperties.warmupTime, Props);
        }
    }

    public class CompProperties_ChainSickleStake : CompProperties_AbilityEffect
    {
        /// <summary>The staked pawn drops the weapon it holds.</summary>
        public bool dropWeapon = true;

        public CompProperties_ChainSickleStake()
        {
            compClass = typeof(CompAbilityEffect_ChainSickleStake);
        }
    }

    /// <summary>
    /// Stake. Only on the pawn this holder has snagged: the coil tightens, the weight drives into the
    /// floor, the pawn drops its weapon and is pinned (stunned) for the rule's pin while the holder
    /// stays within the chain's reach. Refused when the pawn is too heavy (ratio under the drag ratio).
    /// </summary>
    public class CompAbilityEffect_ChainSickleStake : CompAbilityEffect_ChainSickle
    {
        public new CompProperties_ChainSickleStake Props => (CompProperties_ChainSickleStake)props;

        protected override string Unavailable()
        {
            string basic = base.Unavailable();
            if (basic != null) return basic;
            CompChainSickle sickle = Sickle;
            Pawn target = sickle.snagged;
            if (target == null) return "Not snagged: Snag a pawn first.";
            if (sickle.staked) return target.LabelShortCap + " is already staked.";
            if (!target.Spawned || target.Map != parent.pawn.Map) return "Not snagged: " + target.LabelShortCap + " is still being reeled in.";
            ChainSickleWeight w = ChainSickleCombat.Weigh(parent.pawn, target, sickle.Props);
            if (w.Dragged) return "Too heavy: " + RatioText(parent.pawn, target, w) + " is under " + sickle.Props.dragRatio.ToString("0.0#") + ".";
            if (parent.pawn.Position.DistanceTo(target.Position) > sickle.Props.chainLength)
                return "Too far: " + target.LabelShortCap + " is more than " + sickle.Props.chainLength.ToString("0") + " cells away.";
            return null;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            CompChainSickle sickle = Sickle;
            if (sickle == null || !(target.Thing is Pawn pawn) || !sickle.Snags(pawn))
            {
                if (throwMessages) Messages.Message("Stake works only on the pawn at the end of the chain.", target.Thing, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            CompChainSickle sickle = Sickle;
            if (sickle == null || !(target.Thing is Pawn pawn) || !sickle.Snags(pawn)) return null;
            ChainSickleWeight w = ChainSickleCombat.Weigh(parent.pawn, pawn, sickle.Props);
            return RatioText(parent.pawn, pawn, w) + "\npin " + w.Pin.ToString("0.#") + " s";
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !(target.Thing is Pawn pawn) || Sickle == null) return;
            caster.Map.GetComponent<MapComponent_ChainSickle>().LandStake(caster, pawn, parent.def.verbProperties.warmupTime, Props);
        }
    }
}
