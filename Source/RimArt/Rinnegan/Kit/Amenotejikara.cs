using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_Amenotejikara : CompProperties_EffectWithDest
    {
        /// <summary>Biggest pawn that can be swapped. Sasuke himself always can.</summary>
        public float maxBodySize = 2f;

        public CompProperties_Amenotejikara()
        {
            compClass = typeof(CompAbilityEffect_Amenotejikara);
            destination = AbilityEffectDestination.Selected;
        }
    }

    /// <summary>One end of a swap: a pawn, an item on the ground, or a weapon Amenoyodomi holds.</summary>
    public sealed class SwapEnd
    {
        public Pawn pawn;
        public Thing item;
        public HeldWeapon held;

        public IntVec3 Cell => pawn != null ? pawn.Position : item != null ? item.Position : held.at.ToIntVec3();

        /// <summary>The ground point the end stands on: a cell's middle, or a held weapon's own point.</summary>
        public Vector3 Ground => held != null ? held.at : SasukeKit.Flat(Cell.ToVector3Shifted());

        public bool Same(SwapEnd other) =>
            (pawn != null && pawn == other.pawn) || (item != null && item == other.item) || (held != null && held == other.held);
    }

    /// <summary>
    /// Amenotejikara (rinnegan-amenotejikara.js header): two picks, and the two change places at once. The second pick
    /// uses the game's own destination step, as Todo's Double Clap does. Ends: Sasuke, any pawn up to body size 2, an
    /// item on the ground, or a held weapon (picked by clicking where it hangs). One end must be Sasuke, an item or a
    /// held weapon: two other pawns is Todo's clap. Both ends within range of Sasuke and in his sight. A pawn is never
    /// put on a cell it cannot stand on.
    /// </summary>
    public class CompAbilityEffect_Amenotejikara : CompAbilityEffect_WithDest
    {
        public new CompProperties_Amenotejikara Props => (CompProperties_Amenotejikara)props;

        private float Range => parent.def.verbProperties.range;

        /// <summary>The second pick takes the same things as the first.</summary>
        public override TargetingParameters targetParams => parent.verb.targetParams;

        public SwapEnd End(LocalTargetInfo target, out string reason)
        {
            Pawn caster = parent.pawn;
            Map map = caster.Map;
            reason = "Target Sasuke, a pawn, an item on the ground, or a weapon Amenoyodomi holds.";
            if (map == null || !target.IsValid) return null;

            SwapEnd end = null;
            if (target.Thing is Pawn pawn) end = new SwapEnd { pawn = pawn };
            else if (target.Thing != null && target.Thing.def.category == ThingCategory.Item) end = new SwapEnd { item = target.Thing };
            else if (!target.HasThing && target.Cell.InBounds(map))
            {
                HeldWeapon held = GameComponent_Rinnegan.Instance?.HeldNear(caster, target.Cell);
                if (held != null) end = new SwapEnd { held = held };
                else if (target.Cell.GetFirstPawn(map) is Pawn standing) end = new SwapEnd { pawn = standing };
                else if (target.Cell.GetFirstItem(map) is Thing lying) end = new SwapEnd { item = lying };
            }
            if (end == null) return null;

            if (end.pawn != null)
            {
                if (!end.pawn.Spawned || end.pawn.Map != map) return null;
                if (end.pawn != caster && end.pawn.BodySize > Props.maxBodySize)
                {
                    reason = end.pawn.LabelShortCap + " is too big to swap (body size over " + Props.maxBodySize + ").";
                    return null;
                }
            }
            if (end.item != null && (!end.item.Spawned || end.item.Map != map)) return null;

            IntVec3 cell = end.Cell;
            if (end.pawn != caster)
            {
                if (caster.Position.DistanceTo(cell) > Range)
                {
                    reason = "Out of range.";
                    return null;
                }
                if (!GenSight.LineOfSight(caster.Position, cell, map, true))
                {
                    reason = "Sasuke cannot see it.";
                    return null;
                }
            }
            return end;
        }

        /// <summary>Whether two ends can change places. False with a reason when they cannot.</summary>
        public bool Pair(SwapEnd a, SwapEnd b, out string reason)
        {
            Pawn caster = parent.pawn;
            reason = null;
            if (a.Same(b))
            {
                reason = "Pick two different targets.";
                return false;
            }
            if (a.pawn != null && b.pawn != null && a.pawn != caster && b.pawn != caster)
            {
                reason = "One end must be Sasuke, an item or a held weapon.";
                return false;
            }
            if (!Fits(a.pawn, b.Cell, out reason) || !Fits(b.pawn, a.Cell, out reason)) return false;
            return true;
        }

        private bool Fits(Pawn pawn, IntVec3 cell, out string reason)
        {
            reason = null;
            if (pawn == null) return true;
            Map map = parent.pawn.Map;
            if (cell.InBounds(map) && cell.Standable(map) && cell.WalkableBy(map, pawn)) return true;
            reason = pawn.LabelShortCap + " cannot stand there.";
            return false;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (End(target, out string reason) == null)
            {
                if (throwMessages) Messages.Message(reason, parent.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        /// <summary>The second pick is checked from Sasuke, not from the first pick as the game's Skip does.</summary>
        public override bool CanHitTarget(LocalTargetInfo target) => End(target, out _) != null;

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            SwapEnd b = End(target, out string reason);
            SwapEnd a = b == null ? null : End(selectedTarget, out reason);
            if (a == null || b == null || !Pair(a, b, out reason))
            {
                if (showMessages && reason != null) Messages.Message(reason, parent.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Called for the first click with no destination yet (then the first end is enough), and again with both
        /// (see CompAbilityEffect_DoubleClap.CanApplyOn for why).
        /// </summary>
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            SwapEnd a = End(target, out _);
            if (a == null) return false;
            if (!dest.IsValid) return base.CanApplyOn(target, dest);
            SwapEnd b = End(dest, out _);
            if (b == null || !Pair(a, b, out _)) return false;
            return base.CanApplyOn(target, dest);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            SwapEnd a = End(target, out string reason);
            SwapEnd b = a == null ? null : End(dest, out reason);
            if (a == null || b == null || !Pair(a, b, out reason))
            {
                if (reason != null) Messages.Message(reason, parent.pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Swap(parent.pawn, a, b);
        }

        /// <summary>Exchanges the two ends. Only they move: the tiles and whatever else is on them stay.</summary>
        public static void Swap(Pawn caster, SwapEnd a, SwapEnd b)
        {
            Map map = caster.Map;
            Vector3 groundA = a.Ground, groundB = b.Ground;
            IntVec3 cellA = a.Cell, cellB = b.Cell;
            RinneganPictures.Swapped(caster, a, b, groundA, groundB);
            if (a.pawn != null && b.pawn != null)
            {
                AnchorSwap.Swap(a.pawn, b.pawn, caster);
                return;
            }
            Put(caster, a, cellB, groundB);
            Put(caster, b, cellA, groundA);
        }

        private static void Put(Pawn caster, SwapEnd end, IntVec3 cell, Vector3 ground)
        {
            if (end.pawn != null)
            {
                end.pawn.Position = cell;
                end.pawn.Notify_Teleported(end.pawn != caster, true);
            }
            else if (end.item != null)
            {
                end.item.Position = cell;
            }
            else if (end.held != null)
            {
                GameComponent_Rinnegan.Instance?.Place(end.held, ground);
            }
        }
    }
}
