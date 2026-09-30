using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>A traced copy and the pawn whose hand it was put in.</summary>
    public sealed class TraceCopy : IExposable
    {
        public ThingWithComps thing;
        public Pawn holder;

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_References.Look(ref holder, "holder");
        }
    }

    /// <summary>
    /// The rules of Trace On's copies. A copy exists only in the hand of the pawn that traced it: it breaks into light
    /// the moment it leaves (dropped, disarmed, downed, killed, another weapon equipped, carried off by Vacuum) and
    /// when that pawn is no longer Shirou (Trace On taken away on revert). So a copy never lies on the ground and
    /// cannot be hauled, sold or given away; <see cref="StatPart_TraceCopy"/> sets its market value to 0 so it adds
    /// nothing to the colony's wealth while held.
    ///
    /// Every drop the game makes goes through <see cref="BoundWeapon"/>; <see cref="Check"/> catches a copy that
    /// left the hand another way, within <see cref="CheckTicks"/> ticks.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TraceCopies
    {
        public const int CheckTicks = 30;

        static TraceCopies()
        {
            BoundWeapon.Register(eq =>
            {
                if (!IsCopy(eq)) return false;
                Pawn holder = GameComponent_Trace.Instance?.CopyOf(eq)?.holder;
                Break(eq, holder != null && (holder.Downed || holder.Dead));
                return true;
            });
            StatDef stat = StatDefOf.MarketValue;
            if (stat.parts == null) stat.parts = new List<StatPart>();
            stat.parts.Add(new StatPart_TraceCopy { parentStat = stat });
        }

        /// <summary>Only melee weapons are looked up: the market value part asks this of every thing.</summary>
        public static bool IsCopy(Thing thing) => thing != null && thing.def.IsMeleeWeapon && GameComponent_Trace.Instance?.CopyOf(thing) != null;

        /// <summary>Puts the copy in the pawn's empty hand and keeps it on the list.</summary>
        public static void Give(Pawn pawn, ThingWithComps copy)
        {
            pawn.equipment.AddEquipment(copy);
            GameComponent_Trace.Instance?.AddCopy(new TraceCopy { thing = copy, holder = pawn });
        }

        /// <summary>
        /// Empties the hand for a copy: a held copy breaks, a real weapon goes to the inventory (or to the ground when
        /// there is no inventory). The cast job does this before the warmup; this catches a cast that skipped it.
        /// </summary>
        public static void ClearHands(Pawn pawn)
        {
            ThingWithComps held = pawn.equipment?.Primary;
            if (held == null) return;
            if (IsCopy(held))
            {
                Break(held, false);
                return;
            }
            if (pawn.inventory != null && pawn.equipment.TryTransferEquipmentToContainer(held, pawn.inventory.innerContainer)) return;
            if (pawn.SpawnedOrAnyParentSpawned) pawn.equipment.TryDropEquipment(held, out _, pawn.PositionHeld, forbid: false);
        }

        /// <summary>
        /// The copy breaks into light where it is drawn (with <paramref name="fall"/>, it slips from the hand, turns in
        /// the air and breaks before it lands) and is destroyed wherever it went.
        /// </summary>
        public static void Break(ThingWithComps copy, bool fall)
        {
            GameComponent_Trace trace = GameComponent_Trace.Instance;
            TraceCopy record = trace?.CopyOf(copy);
            trace?.RemoveCopy(copy);
            Pawn holder = record?.holder;
            if (holder != null && holder.Spawned && holder.equipment != null && holder.equipment.Contains(copy))
                trace?.AddBreak(holder, copy, fall);
            if (copy.Destroyed) return;
            if (holder?.equipment != null && holder.equipment.Contains(copy)) holder.equipment.DestroyEquipment(copy);
            else copy.Destroy();
        }

        /// <summary>
        /// Every <see cref="CheckTicks"/>: a copy destroyed elsewhere is forgotten; one out of its holder's hand, with a
        /// dead holder, or with a holder that no longer has Trace On (reverted) breaks.
        /// </summary>
        public static void Check(List<TraceCopy> copies)
        {
            for (int i = copies.Count - 1; i >= 0; i--)
            {
                if (i >= copies.Count) continue;
                TraceCopy c = copies[i];
                if (c.thing == null || c.thing.Destroyed)
                {
                    copies.RemoveAt(i);
                    continue;
                }
                Pawn holder = c.holder;
                bool inHand = holder?.equipment != null && holder.equipment.Contains(c.thing);
                if (!inHand || holder.Dead || holder.abilities?.GetAbility(TraceDefOf.AG_Trace_On) == null)
                    Break(c.thing, inHand && holder.Downed);
            }
        }
    }

    /// <summary>A traced copy is worth nothing: it cannot be sold and adds nothing to the colony's wealth.</summary>
    public class StatPart_TraceCopy : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TraceCopies.IsCopy(req.Thing)) val = 0f;
        }

        public override string ExplanationPart(StatRequest req) =>
            TraceCopies.IsCopy(req.Thing) ? "AG_TraceCopyValue".Translate().ToString() : null;
    }
}
