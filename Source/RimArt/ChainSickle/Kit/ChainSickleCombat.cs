using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class ChainSickleDefOf
    {
        public static ThingDef AG_ChainSickle;
        /// <summary>Slides a reeled pawn, or a dragged holder, over the weight rule's reel time.</summary>
        public static ThingDef AG_ChainSickleReeled;
        public static HediffDef AG_ChainStaked;
        public static AbilityDef AG_ChainSickle_Snag;
        public static AbilityDef AG_ChainSickle_Stake;

        static ChainSickleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ChainSickleDefOf));
        }
    }

    /// <summary>The weight rule with the game's numbers, who can be snagged, and where a reel ends.</summary>
    public static class ChainSickleCombat
    {
        /// <summary>
        /// What the rule weighs: the pawn's Mass stat (its body) plus everything it carries: worn
        /// apparel, equipment and inventory (MassUtility.GearAndInventoryMass), in kg.
        /// </summary>
        public static float Mass(Pawn pawn) =>
            pawn.GetStatValue(StatDefOf.Mass) + MassUtility.GearAndInventoryMass(pawn);

        /// <summary>The holder's Carrying Capacity stat, kg.</summary>
        public static float Carry(Pawn holder) => holder.GetStatValue(StatDefOf.CarryingCapacity);

        public static ChainSickleWeight Weigh(Pawn holder, Pawn target, CompProperties_ChainSickle props) =>
            ChainSickleRule.Of(Carry(holder), Mass(target), props.Numbers);

        /// <summary>
        /// Why Snag cannot take this pawn, or null. Hostile pawns (people, animals, mechanoids) and
        /// wild animals, standing, not the holder. Allies and downed pawns are refused: rescuing a
        /// downed ally is the belt items' job.
        /// </summary>
        public static string SnagRefusal(Pawn holder, Thing thing)
        {
            if (!(thing is Pawn pawn) || pawn == holder) return "Snag needs a pawn.";
            if (pawn.Dead) return pawn.LabelShortCap + " is dead.";
            if (pawn.Downed) return pawn.LabelShortCap + " is down.";
            bool wild = pawn.RaceProps.Animal && pawn.Faction == null;
            if (!wild && !pawn.HostileTo(holder)) return "Only hostile pawns and wild animals can be snagged.";
            return null;
        }

        /// <summary>
        /// Where a pawn moved from <paramref name="from"/> toward <paramref name="toward"/> by up to
        /// <paramref name="cells"/> cells ends: walked one cell at a time along the line, stopping at
        /// the first cell it cannot stand in, that holds another pawn, or that is closer than 1.5
        /// cells to <paramref name="keepAway"/> (the other end of the chain). The sketch's
        /// "distance - 1.5" is the same stop.
        /// </summary>
        public static IntVec3 ReelCell(IntVec3 from, IntVec3 toward, float cells, IntVec3 keepAway, Pawn mover, Map map)
        {
            IntVec3 dest = from;
            Vector3 start = from.ToVector3Shifted(), run = (toward.ToVector3Shifted() - start);
            run.y = 0f;
            float length = run.magnitude;
            if (length < 0.01f) return dest;
            Vector3 step = run / length;
            int most = Mathf.FloorToInt(Mathf.Min(cells, length) + 0.5f);
            for (int i = 1; i <= most; i++)
            {
                IntVec3 c = (start + step * i).ToIntVec3();
                if (c == dest) continue;
                if (!c.InBounds(map) || !c.Standable(map) || c.DistanceTo(keepAway) < 1.5f) break;
                Pawn there = c.GetFirstPawn(map);
                if (there != null && there != mover) break;
                if (!GenSight.LineOfSight(from, c, map, true)) break;
                dest = c;
            }
            return dest;
        }

        /// <summary>A pawn's drawn ground point, in or out of a flyer. Null when it is nowhere on <paramref name="map"/>.</summary>
        public static Vector2? Ground(Pawn pawn, Map map)
        {
            if (pawn == null || pawn.Destroyed) return null;
            if (pawn.Spawned) return pawn.Map == map ? new Vector2(pawn.DrawPos.x, pawn.DrawPos.z) : (Vector2?)null;
            if (pawn.ParentHolder is PawnFlyer flyer && flyer.Spawned && flyer.Map == map) return new Vector2(flyer.DrawPos.x, flyer.DrawPos.z);
            return null;
        }

        /// <summary>The weight's blunt hit on Snag. It has no tool, so the staked bonus never applies to it.</summary>
        public static void Hit(Pawn victim, Pawn holder, float damage, float armorPenetration, Vector2 toward)
        {
            if (damage < 1f) return;
            float angle = new Vector3(toward.x, 0f, toward.y).AngleFlat();
            victim.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, armorPenetration, angle, holder, null, ChainSickleDefOf.AG_ChainSickle,
                DamageInfo.SourceCategory.ThingOrUnknown, victim));
        }
    }
}
