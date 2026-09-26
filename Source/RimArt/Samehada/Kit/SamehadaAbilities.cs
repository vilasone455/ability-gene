using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>What both Samehada abilities share: they need the blade in hand and enough charges on it.</summary>
    public abstract class CompAbilityEffect_Samehada : CompAbilityEffect
    {
        protected CompSamehada Blade => CompSamehada.HeldBy(parent.pawn);

        protected abstract int Needs { get; }

        public override bool CanCast => base.CanCast && Unavailable() == null;

        protected virtual string Unavailable()
        {
            CompSamehada blade = Blade;
            if (blade == null) return "Requires Samehada in hand.";
            if (blade.Charges < Needs) return "Needs " + Needs + " charges, has " + blade.Charges + ".";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }
    }

    public class CompProperties_SamehadaSharkSkin : CompProperties_AbilityEffect
    {
        /// <summary>Charges spent; the ability needs at least this many.</summary>
        public int chargeCost = 2;
        /// <summary>How long the bandage stays off, seconds.</summary>
        public float seconds = 10f;
        /// <summary>
        /// The hit area of each melee attack: cells within this radius of the wielder and within
        /// sweepHalfAngle degrees of the attacked cell's direction. 1.5 and 60 are the attacked cell and the
        /// two beside it.
        /// </summary>
        public float sweepRadius = 1.5f;
        public float sweepHalfAngle = 60f;

        public CompProperties_SamehadaSharkSkin()
        {
            compClass = typeof(CompAbilityEffect_SamehadaSharkSkin);
        }
    }

    /// <summary>
    /// Shark Skin. Spends chargeCost charges; for seconds the bandage is off and the scales stand up, and
    /// every melee attack with the blade also hits every hostile pawn in the cells in front of the wielder
    /// (SamehadaFeeding.Sweep). Each pawn hit is fed, so a sweep through several pawns puts charges back.
    /// </summary>
    public class CompAbilityEffect_SamehadaSharkSkin : CompAbilityEffect_Samehada
    {
        public new CompProperties_SamehadaSharkSkin Props => (CompProperties_SamehadaSharkSkin)props;

        protected override int Needs => Props.chargeCost;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompSamehada blade = Blade;
            if (caster?.Map == null || blade == null) return;
            caster.Map.GetComponent<MapComponent_Samehada>().Land(caster, parent, blade);
            blade.Spend(Props.chargeCost);
            blade.StartSharkSkin(UnityEngine.Mathf.RoundToInt(Props.seconds * 60f));
        }
    }

    public class CompProperties_SamehadaFusion : CompProperties_AbilityEffect
    {
        /// <summary>Charges the ability needs; it spends all the blade has.</summary>
        public int needsCharges = 5;
        /// <summary>How long the fusion lasts, seconds (AG_SamehadaFused: its move and regeneration are on the hediff).</summary>
        public float seconds = 15f;

        public CompProperties_SamehadaFusion()
        {
            compClass = typeof(CompAbilityEffect_SamehadaFusion);
        }
    }

    /// <summary>
    /// Fusion. Spends every charge: for seconds the wielder is fused with the blade (AG_SamehadaFused,
    /// faster and regenerating), still holding it, and Feed still works. When it ends the blade is at no
    /// charge (HediffComp_SamehadaFused).
    /// </summary>
    public class CompAbilityEffect_SamehadaFusion : CompAbilityEffect_Samehada
    {
        public new CompProperties_SamehadaFusion Props => (CompProperties_SamehadaFusion)props;

        protected override int Needs => Props.needsCharges;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompSamehada blade = Blade;
            if (caster?.Map == null || blade == null) return;
            caster.Map.GetComponent<MapComponent_Samehada>().Land(caster, parent, blade);
            blade.SetCharges(0);
            Hediff old = caster.health.hediffSet.GetFirstHediffOfDef(SamehadaDefOf.AG_SamehadaFused);
            if (old != null) caster.health.RemoveHediff(old);
            Hediff fused = HediffMaker.MakeHediff(SamehadaDefOf.AG_SamehadaFused, caster);
            caster.health.AddHediff(fused);
            fused.TryGetComp<HediffComp_Disappears>()?.SetDuration(UnityEngine.Mathf.RoundToInt(Props.seconds * 60f));
            caster.Map.GetComponent<MapComponent_Samehada>().Fused(caster);
        }
    }
}
