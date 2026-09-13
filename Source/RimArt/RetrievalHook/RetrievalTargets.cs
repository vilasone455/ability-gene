using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Reads a target out of the game into <see cref="RetrievalRules"/> facts. The same checks
    /// run while aiming, when the net connects, and on every tick of the drag.
    /// </summary>
    public static class RetrievalTargets
    {
        /// <summary>
        /// Why <paramref name="caster"/> cannot pull <paramref name="target"/>, or null when they
        /// can. <paramref name="take"/> is the number of item units the pull would move (the whole
        /// pawn counts as 1). <paramref name="self"/> is the pull asking, so its own claim on the
        /// target is not counted; during a drag the pull already holds the reservation, so
        /// <paramref name="checkReservation"/> is false there.
        /// </summary>
        public static string Refusal(Pawn caster, Thing target, out int take, RetrievalPull self = null,
            bool checkReservation = true)
        {
            take = 0;
            if (target == null || target.Destroyed) return "Target is gone.";

            bool claimed = MapComponent_RetrievalHooks.IsTargeted(target, self);
            bool reservable = !checkReservation || caster?.Map == null || !target.Spawned
                              || caster.Map.reservationManager.CanReserve(caster, target);

            if (target is Pawn pawn)
            {
                take = 1;
                HookPawnFacts facts = new HookPawnFacts
                {
                    IsCaster = pawn == caster,
                    Spawned = pawn.Spawned,
                    Dead = pawn.Dead,
                    Downed = pawn.Downed,
                    Humanlike = pawn.RaceProps.Humanlike,
                    Prisoner = pawn.IsPrisoner,
                    ColonyMember = pawn.Faction == Faction.OfPlayer || pawn.IsSlaveOfColony,
                    AlliedFaction = pawn.Faction != null && !pawn.Faction.IsPlayer
                                    && pawn.Faction.PlayerRelationKind == FactionRelationKind.Ally,
                    AlreadyRetrieved = claimed,
                    Reservable = reservable,
                };
                return RetrievalRules.PawnRefusal(facts);
            }

            ThingDef def = target.def;
            HookItemFacts item = new HookItemFacts
            {
                Spawned = target.Spawned,
                IsItem = def.category == ThingCategory.Item && !(target is Corpse),
                Haulable = def.EverHaulable,
                Corpse = target is Corpse,
                Chunk = def.IsWithinCategory(ThingCategoryDefOf.Chunks),
                Kind = KindOf(target),
                StackCount = target.stackCount,
                MassPerUnit = target.GetStatValue(StatDefOf.Mass),
                AlreadyRetrieved = claimed,
                Reservable = reservable,
            };
            return RetrievalRules.ItemRefusal(item, out take);
        }

        public static HookItemKind KindOf(Thing thing)
        {
            ThingDef def = thing.def;
            if (thing is MinifiedThing) return HookItemKind.Minified;
            if (def.IsWeapon) return HookItemKind.Weapon;
            if (def.IsApparel) return HookItemKind.Apparel;
            if (def.IsMedicine) return HookItemKind.Medicine;
            if (def.IsIngestible) return HookItemKind.Food;
            if (def.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw)
                || def.IsWithinCategory(ThingCategoryDefOf.Manufactured)) return HookItemKind.Resource;
            return HookItemKind.Other;
        }

        /// <summary>Range and sight, checked again when the net connects.</summary>
        public static string ReachRefusal(Pawn caster, Thing target)
        {
            if (caster.Position.DistanceTo(target.Position) > RetrievalHookDefaults.Range + 0.001f)
                return "Target moved out of range.";
            if (!GenSight.LineOfSight(caster.Position, target.Position, caster.Map, true))
                return "Line of sight to the target was blocked.";
            return null;
        }
    }
}
