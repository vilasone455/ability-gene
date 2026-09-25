using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityMark : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityMark()
        {
            compClass = typeof(CompAbilityEffect_Mark);
        }
    }

    /// <summary>
    /// Throws a stone onto a tile, or takes one back. The stone is a real item lying there (the
    /// gene's stoneDef), spawned forbidden so nobody hauls it away. It is what the carrier claps
    /// with when the other end is out of sight or out of range; pawns in sight need no stone, so
    /// Mark never targets a pawn.
    ///
    /// Targeting one of the carrier's own stones takes it back, so a slot can be freed without
    /// waiting out the duration.
    /// </summary>
    public class CompAbilityEffect_Mark : CompAbilityEffect
    {
        public new CompProperties_AbilityMark Props => (CompProperties_AbilityMark)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            Map map = caster.Map;
            if (gene == null || map == null) return;

            var flicks = map.GetComponent<MapComponent_MarkFlicks>();
            Anchor existing = gene.AnchorFor(target);
            if (existing != null)
            {
                flicks?.Lifted(caster, existing);
                gene.Remove(existing);
                Messages.Message("AG_AnchorLifted".Translate(caster.LabelShort, existing.Label),
                    caster, MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            if (gene.StoneDef == null || !CanHoldStone(map, target.Cell) || gene.LiveCount >= gene.MaxAnchors)
            {
                flicks?.Ended(caster);
                return;
            }

            Thing stone = ThingMaker.MakeThing(gene.StoneDef);
            if (stone is ClapStone owned) owned.owner = caster;
            GenSpawn.Spawn(stone, target.Cell, map);
            stone.SetForbidden(true, false);

            Anchor anchor = new Anchor(stone, Find.TickManager.TicksGame);
            gene.Add(anchor);
            flicks?.Placed(caster, anchor);
        }

        private static bool CanHoldStone(Map map, IntVec3 cell) =>
            cell.IsValid && cell.InBounds(map) && cell.Standable(map);

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target) && base.CanApplyOn(target, dest);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Anchors gene = AnchorUtility.GeneOf(caster);
            if (gene == null || caster.Map == null) return false;

            // Taking a stone back is always allowed, including when the carrier is full.
            if (gene.IsMarked(target)) return base.Valid(target, throwMessages);

            if (!CanHoldStone(caster.Map, target.Cell))
            {
                if (throwMessages)
                {
                    Messages.Message("AG_AnchorBadTile".Translate(),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (gene.LiveCount >= gene.MaxAnchors)
            {
                if (throwMessages)
                {
                    Messages.Message("AG_AnchorFull".Translate(caster.LabelShort, gene.MaxAnchors),
                        caster, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
