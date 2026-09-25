using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityClap : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityClap()
        {
            compClass = typeof(CompAbilityEffect_Clap);
        }
    }

    /// <summary>
    /// The carrier and one end change places. A pawn in sight within range needs no stone; a stone
    /// can be anywhere. A pawn arrives where the carrier was standing, which is the whole cost of
    /// the ability and the reason it is not simply an escape; a stone lands there, which is the way
    /// back.
    ///
    /// Costs one clap from the shared charges, or all of them for a long-range swap (see
    /// <see cref="ClapTargets"/>).
    /// </summary>
    public class CompAbilityEffect_Clap : CompAbilityEffect
    {
        public new CompProperties_AbilityClap Props => (CompProperties_AbilityClap)props;

        /// <summary>DrawRadiusRing has a precalculated cell list; past this it logs an error every frame.</summary>
        private const float MaxRing = 50f;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            var teleports = caster.Map.GetComponent<MapComponent_ClapTeleports>();
            Anchor end = ClapTargets.EndFor(caster, gene, target, out _);
            int cost = end != null ? ClapTargets.Cost(caster, gene, end, null) : 0;
            if (end == null || !ClapTargets.Affordable(gene, cost, out _))
            {
                teleports.Ended(caster);
                return;
            }

            // The ends are read before anyone moves: afterwards the carrier's cell is the end's.
            ClapEnds.For(caster, end, null, false, out ClapEnd from, out ClapEnd to);

            if (!AnchorSwap.Resolve(caster, end))
            {
                teleports.Ended(caster);
                return;
            }
            ClapTargets.Pay(gene, cost, end, null);
            ClapTargets.Stun(caster, gene, end);
            teleports.Land(caster, from, to, parent.def.verbProperties.warmupTime, false);
            ClapCastAnimation.MoveTo(caster, caster.CurJobDef);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool GizmoDisabled(out string reason)
        {
            Gene_Anchors gene = AnchorUtility.GeneOf(parent.pawn);
            if (gene != null && gene.Charges <= 0)
            {
                reason = "AG_AnchorNoClaps".Translate(gene.SecondsToNext.ToString("F0"));
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Gene_Anchors gene = AnchorUtility.GeneOf(parent.pawn);
            if (gene != null && gene.DirectSwapRange <= MaxRing) GenDraw.DrawRadiusRing(parent.pawn.Position, gene.DirectSwapRange);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            if (gene == null) return false;

            if (!AnchorClapCheck.HandsFree(caster, throwMessages)) return false;

            Anchor end = ClapTargets.EndFor(caster, gene, target, out string reason);
            if (end == null || !ClapTargets.Affordable(gene, ClapTargets.Cost(caster, gene, end, null), out reason))
            {
                if (throwMessages) Messages.Message(reason, caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
