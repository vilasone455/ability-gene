using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Two selected targets, the same way the Skip psycast picks a thing and then a place.
    /// CompAbilityEffect_WithDest is what makes the targeter ask twice; the destination is
    /// Selected so the second pick is the player's rather than derived from the first.
    /// </summary>
    public class CompProperties_AbilityDoubleClap : CompProperties_AbilityTeleport
    {
        public CompProperties_AbilityDoubleClap()
        {
            compClass = typeof(CompAbilityEffect_DoubleClap);
            destination = AbilityEffectDestination.Selected;
        }
    }

    /// <summary>
    /// Any two ends change places with each other, chosen at cast time: pawns in sight within
    /// range, or the carrier's stones anywhere. The carrier does not move and is not one of the
    /// ends, which is what separates this from the clap: it is the version that puts somebody else
    /// somewhere dangerous.
    ///
    /// At least one end has to be a pawn. Costs one clap from the shared charges, or all of them
    /// for a long-range swap (see <see cref="ClapTargets"/>).
    /// </summary>
    public class CompAbilityEffect_DoubleClap : CompAbilityEffect_WithDest
    {
        public new CompProperties_AbilityDoubleClap Props => (CompProperties_AbilityDoubleClap)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            var teleports = caster.Map.GetComponent<MapComponent_ClapTeleports>();
            if (!Pair(caster, gene, target, dest, out Anchor a, out Anchor b, out int cost, out _))
            {
                teleports.Ended(caster);
                return;
            }

            ClapEnds.For(caster, a, b, false, out ClapEnd first, out ClapEnd second);
            if (!AnchorSwap.Resolve(a, b))
            {
                teleports.Ended(caster);
                return;
            }
            ClapTargets.Pay(gene, cost, a, b);
            ClapTargets.Stun(caster, gene, a);
            ClapTargets.Stun(caster, gene, b);
            teleports.Land(caster, first, second, parent.def.verbProperties.warmupTime, true);
        }

        /// <summary>Both ends, and what the swap costs. False with a reason when the pair cannot be clapped.</summary>
        public static bool Pair(Pawn caster, Gene_Anchors gene, LocalTargetInfo target, LocalTargetInfo dest,
            out Anchor a, out Anchor b, out int cost, out string reason)
        {
            b = null;
            cost = 0;
            a = ClapTargets.EndFor(caster, gene, target, out reason);
            if (a == null) return false;
            b = ClapTargets.EndFor(caster, gene, dest, out reason);
            if (b == null) return false;

            if ((a.IsOnPawn && a.pawn == b.pawn) || (a.IsStone && a.stone == b.stone))
            {
                reason = "AG_AnchorSameEnd".Translate().ToString();
                return false;
            }
            if (a.IsStone && b.IsStone)
            {
                reason = "AG_AnchorTwoTiles".Translate().ToString();
                return false;
            }

            cost = ClapTargets.Cost(caster, gene, a, b);
            return ClapTargets.Affordable(gene, cost, out reason);
        }

        /// <summary>
        /// Called twice with different meanings, which is the whole subtlety here.
        ///
        /// The first call is the targeter validating the first click, and it passes
        /// LocalTargetInfo.Invalid as the destination because the player has not been offered a
        /// second pick yet. Demanding a valid far end at that point refuses the first click and
        /// the destination step never opens at all - the ability silently does nothing.
        ///
        /// The second call is the real one, after CompAbilityEffect_WithDest has collected a
        /// destination. Only then is there a pair to check. There is no throwMessages here, so
        /// an invalid pair is refused by the targeter rather than explained; the rules that make
        /// a pair invalid are stated in the ability description instead.
        /// </summary>
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Gene_Anchors gene = AnchorUtility.GeneOf(parent.pawn);
            if (gene == null) return false;
            if (!Valid(target)) return false;

            // No destination chosen yet: this is the first click, and the first end is enough.
            if (!dest.IsValid) return base.CanApplyOn(target, dest);

            if (!Pair(parent.pawn, gene, target, dest, out _, out _, out _, out _)) return false;
            return base.CanApplyOn(target, dest);
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

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            if (gene == null) return false;

            if (!AnchorClapCheck.HandsFree(caster, throwMessages)) return false;

            if (ClapTargets.EndFor(caster, gene, target, out string reason) == null)
            {
                if (throwMessages) Messages.Message(reason, caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
