using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Why a Reset started. Only Explosion destroys the body at once; Headshot has its own delay.</summary>
    public enum AjinCause { Killed, Explosion, Headshot, Downed }

    /// <summary>
    /// The Ajin trait's passive (docs/hero-echo.md "Satō"). He never dies: whatever would kill him starts a
    /// Reset instead (<see cref="Hediff_AjinReset"/>), he lies downed until it ends, then stands up whole.
    ///
    /// Manifested and able to pay: the delay is the trait's resetSeconds and he rises at his biggest piece,
    /// paying that piece's cost. While the body lies on the map it is the biggest piece and he rises where it
    /// lies. When the body is destroyed (an explosion, or too much damage while it lies there) it is taken
    /// off the map and held by his biggest anchor, or by remains on the spot when he has none; his gear drops
    /// where the body was. Otherwise the Reset takes the slow delay and happens where he fell, anchors ignored.
    ///
    /// Pawns are moved and despawned only from <see cref="GameComponent_Sato"/>'s tick, never from inside a
    /// damage or death call, so vanilla code in the middle of an explosion or a kill never sees him vanish.
    /// </summary>
    public static class AjinReset
    {
        public static bool IsAjin(Pawn pawn) => pawn?.story?.traits != null && pawn.story.traits.HasTrait(SatoDefOf.AG_Ajin);

        public static Hediff_AjinReset Resetting(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(SatoDefOf.AG_AjinReset) as Hediff_AjinReset;

        public static bool Manifested(Pawn pawn)
        {
            EchoRecord record = GameComponent_Echoes.Get?.HostRecord(pawn);
            return record != null && record.manifested;
        }

        /// <summary>
        /// Starts a Reset, or, for a pawn already in one, lets an explosion destroy the lying body. A delay
        /// override (Headshot Reset) applies only when the Reset is paid.
        /// </summary>
        public static Hediff_AjinReset Start(Pawn pawn, AjinCause cause, int delayTicks = -1)
        {
            Hediff_AjinReset reset = Resetting(pawn);
            if (reset != null)
            {
                if (cause == AjinCause.Explosion && reset.holder == null) reset.destroyPending = true;
                return reset;
            }
            AjinExtension ext = AjinExtension.Get;
            int now = Find.TickManager.TicksGame;
            reset = (Hediff_AjinReset)HediffMaker.MakeHediff(SatoDefOf.AG_AjinReset, pawn);
            reset.cause = cause;
            reset.startTick = now;
            bool destroyed = cause == AjinCause.Explosion;
            AjinPiece piece = destroyed ? BiggestAnchor(pawn, pawn.MapHeld)?.piece ?? AjinPiece.Body : AjinPiece.Body;
            float cost = ext.Cost(piece);
            GameComponent_Echoes pool = GameComponent_Echoes.Get;
            if (Manifested(pawn) && pool != null && pool.TrySpend(cost))
            {
                reset.fast = true;
                reset.paid = cost;
                reset.riseTick = now + (delayTicks >= 0 ? delayTicks : ext.resetSeconds.SecondsToTicks());
            }
            else reset.riseTick = now + ext.slowResetTicks;
            reset.destroyPending = destroyed;
            reset.destroyAtDamage = ext.destroyedDamageShare * BodyHealth(pawn);
            pawn.health.AddHediff(reset);
            if (pawn.Spawned)
            {
                if (pawn.carryTracker?.CarriedThing != null) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                pawn.pather?.StopDead();
            }
            GameComponent_Sato.Instance?.Register(pawn);
            return reset;
        }

        /// <summary>His anchors on a map, oldest first. Remains are not anchors.</summary>
        public static List<AjinAnchor> Anchors(Pawn pawn, Map map)
        {
            var list = new List<AjinAnchor>();
            if (pawn == null || map == null) return list;
            foreach (Thing thing in map.listerThings.ThingsOfDef(SatoDefOf.AG_AjinAnchor))
                if (thing is AjinAnchor anchor && anchor.owner == pawn && !anchor.Crumbling) list.Add(anchor);
            list.SortBy(a => a.madeTick);
            return list;
        }

        /// <summary>The anchor he would rise from: the biggest piece, the newest of equals. Null when he has none free.</summary>
        public static AjinAnchor BiggestAnchor(Pawn pawn, Map map)
        {
            AjinExtension ext = AjinExtension.Get;
            AjinAnchor best = null;
            foreach (AjinAnchor anchor in Anchors(pawn, map))
            {
                if (anchor.HeldPawn != null || anchor.Flying) continue;
                if (best == null || ext.Size(anchor.piece) >= ext.Size(best.piece)) best = anchor;
            }
            return best;
        }

        /// <summary>
        /// The body is gone: his gear drops where it lay and he is held by his biggest anchor (a paid Reset that
        /// can also pay that piece's extra cost) or by remains on the spot. Called from the game component's tick.
        /// </summary>
        internal static void DestroyBody(Pawn pawn, Hediff_AjinReset reset)
        {
            reset.destroyPending = false;
            if (reset.holder != null) return;
            if (pawn.ParentHolder is Pawn_CarryTracker carry)
                carry.TryDropCarriedThing(carry.pawn.Position, ThingPlaceMode.Near, out _);
            // In a caravan, a pod or a bed-like container: nothing to blow apart on a map, he stays where he is.
            if (!pawn.Spawned) return;
            Map map = pawn.Map;
            IntVec3 spot = pawn.Position;
            AjinAnchor holder = null;
            if (reset.fast)
            {
                holder = BiggestAnchor(pawn, map);
                if (holder != null)
                {
                    float extra = AjinExtension.Get.Cost(holder.piece) - reset.paid;
                    if (extra <= 0f || GameComponent_Echoes.Get.TrySpend(extra)) reset.paid += Mathf.Max(0f, extra);
                    else
                    {
                        // The pool cannot pay for the smaller piece: the slow Reset on the spot.
                        holder = null;
                        reset.fast = false;
                        reset.riseTick = Find.TickManager.TicksGame + AjinExtension.Get.slowResetTicks;
                    }
                }
            }
            DropGear(pawn, spot);
            Splatter(spot, map, pawn);
            SatoPictures.BodyDestroyed(pawn, spot, map);
            pawn.DeSpawn(DestroyMode.Vanish);
            if (holder == null) holder = AjinAnchor.MakeRemains(pawn, spot, map);
            holder.Hold(pawn);
            reset.holder = holder;
            reset.bodyDestroyed = true;
        }

        /// <summary>A holder was lost with him inside: he moves to his next anchor, or to remains on its spot.</summary>
        internal static void HolderLost(Pawn pawn, AjinAnchor lost, IntVec3 spot, Map map)
        {
            Hediff_AjinReset reset = Resetting(pawn);
            if (reset == null || map == null) return;
            AjinAnchor next = reset.fast ? BiggestAnchor(pawn, map) : null;
            if (next == null || next == lost) next = AjinAnchor.MakeRemains(pawn, spot, map);
            next.Hold(pawn);
            reset.holder = next;
        }

        /// <summary>
        /// The end of the Reset: he stands up whole where the body lies, or at the piece that held him. Rising at
        /// an anchor uses it up and his other anchors crumble; rising in place leaves them.
        /// </summary>
        internal static void Rise(Pawn pawn, Hediff_AjinReset reset)
        {
            AjinAnchor holder = reset.holder;
            bool atAnchor = holder != null && holder.def == SatoDefOf.AG_AjinAnchor;
            SatoPictures.Rising(pawn, reset, holder);
            if (holder != null)
            {
                Map map = holder.MapHeld;
                IntVec3 cell = holder.PositionHeld;
                holder.Release(pawn);
                if (map != null)
                {
                    if (!cell.Standable(map)) CellFinder.TryFindRandomCellNear(cell, map, 3, c => c.Standable(map), out cell);
                    GenSpawn.Spawn(pawn, cell, map);
                    if (atAnchor)
                        foreach (AjinAnchor other in Anchors(pawn, map))
                            if (other != holder) other.Crumble();
                }
                holder.UsedUp();
                reset.holder = null;
            }
            Heal(pawn);
            pawn.health.RemoveHediff(reset);
        }

        /// <summary>
        /// What a Reset clears: injuries, missing natural parts, infections, chronic illness, drug highs, the
        /// trait's clearHediffs (blood loss, toxic buildup, heatstroke, hypothermia), mental states and fire. Bionics,
        /// addictions, genes, needs and memories stay.
        /// </summary>
        public static void Heal(Pawn pawn)
        {
            AjinExtension ext = AjinExtension.Get;
            HediffSet set = pawn.health.hediffSet;
            foreach (Hediff hediff in set.hediffs.ToList())
            {
                if (hediff is Hediff_AjinReset || !set.hediffs.Contains(hediff)) continue;
                bool clear = hediff is Hediff_Injury
                    || hediff is Hediff_MissingPart missing && !set.PartOrAnyAncestorHasDirectlyAddedParts(missing.Part)
                    || ext.clearHediffs.Contains(hediff.def)
                    || hediff.TryGetComp<HediffComp_Immunizable>() != null
                    || hediff.def.chronic
                    || hediff is Hediff_High;
                if (clear) HealthUtility.Cure(hediff);
            }
            pawn.MentalState?.RecoverFromState();
            pawn.GetAttachment(ThingDefOf.Fire)?.Destroy();
        }

        /// <summary>Everything he wore, held and carried drops on the spot. Hero weapons vanish as they always do.</summary>
        private static void DropGear(Pawn pawn, IntVec3 spot)
        {
            pawn.equipment?.DropAllEquipment(spot, forbid: false);
            pawn.apparel?.DropAll(spot, forbid: false);
            pawn.inventory?.DropAllNearPawn(spot);
        }

        /// <summary>Blood and ash round the spot where the body came apart.</summary>
        private static void Splatter(IntVec3 spot, Map map, Pawn pawn)
        {
            ThingDef blood = pawn.RaceProps.BloodDef;
            for (int i = 0; i < 6; i++)
            {
                IntVec3 cell = spot + GenRadial.RadialPattern[Rand.Range(0, 9)];
                if (!cell.InBounds(map)) continue;
                if (blood != null) FilthMaker.TryMakeFilth(cell, map, blood, pawn.LabelIndefinite());
                if (i % 2 == 0) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Ash);
            }
        }

        /// <summary>Total health of the parts he still has: a Reset destroys the body past the trait's share of it.</summary>
        public static float BodyHealth(Pawn pawn)
        {
            float total = 0f;
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
                total += part.def.GetMaxHealth(pawn);
            return total;
        }
    }
}
