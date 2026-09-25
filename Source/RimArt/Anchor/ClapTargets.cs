using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What a click can be the end of a clap, and what the clap costs.
    ///
    /// An end is a living flesh pawn in sight within directSwapRange, which needs no stone, or one of
    /// the carrier's stones anywhere on the map. A pawn wins over a stone under it: the stone is
    /// only needed for what the carrier cannot reach directly.
    ///
    /// A swap with any end farther than longSwapRange from the carrier is a long-range swap. It needs
    /// every charge, spends every charge, and the stones it used are gone. Pawn ends are never that
    /// far, so only a stone makes a swap long-range.
    /// </summary>
    public static class ClapTargets
    {
        /// <summary>
        /// The end at a target, or null with the reason. The pawn end is built fresh; a stone end is
        /// the gene's own record, so the mark card drawn for it can follow the cast.
        /// </summary>
        public static Anchor EndFor(Pawn caster, Gene_Anchors gene, LocalTargetInfo target, out string reason)
        {
            reason = null;
            if (caster == null || gene == null || caster.Map == null || !target.IsValid) return null;

            Pawn pawn = target.Pawn ?? (target.Cell.IsValid && target.Cell.InBounds(caster.Map) ? target.Cell.GetFirstPawn(caster.Map) : null);
            string pawnReason = null;
            if (pawn != null && Reachable(caster, gene, pawn, out pawnReason))
                return new Anchor(pawn, Find.TickManager.TicksGame);

            Anchor stone = gene.AnchorFor(target);
            if (stone != null && stone.Usable) return stone;

            reason = pawnReason ?? "AG_AnchorNotMarked".Translate().ToString();
            return null;
        }

        /// <summary>A pawn the carrier can clap with directly: alive, flesh, not the carrier, in sight and in range.</summary>
        public static bool Reachable(Pawn caster, Gene_Anchors gene, Pawn pawn, out string reason)
        {
            reason = null;
            if (pawn == caster || pawn.Dead || !pawn.Spawned || pawn.Map != caster.Map || !pawn.RaceProps.IsFlesh)
            {
                reason = "AG_AnchorNotReachable".Translate(pawn.LabelShort).ToString();
                return false;
            }
            if (pawn.Position.DistanceTo(caster.Position) > gene.DirectSwapRange)
            {
                reason = "AG_AnchorTooFar".Translate(pawn.LabelShort, gene.DirectSwapRange.ToString("F0")).ToString();
                return false;
            }
            if (!GenSight.LineOfSight(caster.Position, pawn.Position, caster.Map, true))
            {
                reason = "AG_AnchorNoSight".Translate(pawn.LabelShort).ToString();
                return false;
            }
            return true;
        }

        public static bool IsLong(Pawn caster, Gene_Anchors gene, Anchor end) =>
            end != null && end.IsStone && end.CurrentCell.DistanceTo(caster.Position) > gene.LongSwapRange;

        /// <summary>Charges a swap with these ends costs: one, or all of them for a long-range swap.</summary>
        public static int Cost(Pawn caster, Gene_Anchors gene, Anchor a, Anchor b) =>
            IsLong(caster, gene, a) || IsLong(caster, gene, b) ? gene.MaxCharges : 1;

        /// <summary>Whether the carrier holds enough claps for this swap, with the reason if not.</summary>
        public static bool Affordable(Gene_Anchors gene, int cost, out string reason)
        {
            reason = null;
            if (gene.Charges >= cost) return true;
            reason = cost > 1
                ? "AG_AnchorLongNeedsAll".Translate(gene.MaxCharges, gene.Charges).ToString()
                : "AG_AnchorNoClaps".Translate(gene.SecondsToNext.ToString("F0")).ToString();
            return false;
        }

        /// <summary>
        /// Pays for a landed swap. A long-range swap also removes the stones it used, which is the
        /// point of it: a planned move, once, and then the stone has to be thrown again.
        /// </summary>
        public static void Pay(Gene_Anchors gene, int cost, Anchor a, Anchor b)
        {
            gene.Spend(cost);
            if (cost <= 1) return;
            if (a != null && a.IsStone) gene.Remove(a);
            if (b != null && b.IsStone) gene.Remove(b);
        }

        /// <summary>A hostile pawn that was swapped has lost track of where it is. Its job was already dropped by the teleport.</summary>
        public static void Stun(Pawn caster, Gene_Anchors gene, Anchor end)
        {
            Pawn pawn = end?.pawn;
            if (pawn == null || pawn == caster || pawn.Dead || !pawn.HostileTo(caster)) return;
            if (gene.SwapStunTicks > 0) pawn.stances?.stunner?.StunFor(gene.SwapStunTicks, caster, false);
        }
    }
}
