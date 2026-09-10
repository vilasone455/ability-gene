using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AbilityGenes
{
    /// <summary>
    /// One dispersal, from the blow that was not taken to the body standing somewhere else.
    ///
    /// The order here is the whole design. The damage is cancelled by the prefix *before* this
    /// runs, so by the time anything in this file executes the hit has already not happened;
    /// what follows is only the question of where the carrier ends up and what it cost them.
    /// There is no interception, no reduction and no saving throw. Either the plexus answers or
    /// it does not, and <see cref="Applies"/> is the entire list of reasons it might not.
    ///
    /// The carrier arrives instantly. The crows are decoration flying a route that has already
    /// been taken - see <see cref="DispersalDefaults.FlightTicks"/> for why that is the honest
    /// way round rather than the pretty one.
    /// </summary>
    public static class Scatter
    {
        /// <summary>
        /// Whether this particular damage instance is one the plexus comes apart for.
        ///
        /// Read this as the cost of the exemption. Everything the halving membrane's notes warn
        /// about - that a hard "this does not apply" stops being a mechanic and becomes
        /// invulnerability - is true, and the answer is that this returns false far more often
        /// than it returns true.
        /// </summary>
        public static bool Applies(Pawn pawn, Gene_Dispersal gene, DamageInfo dinfo)
        {
            if (gene == null || !gene.AutoScatter || !gene.HasCharge) return false;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Downed) return false;
            if (pawn.Map == null) return false;

            // A clone is a trick played on somebody who is looking. Asleep, anaesthetised or
            // otherwise not present, the carrier takes the hit like anyone else - which is
            // also what stops this being a way to make a downed colonist unkillable.
            if (!pawn.Awake()) return false;
            if (pawn.health == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Consciousness)) return false;

            if (dinfo.Amount < gene.MinimumDamage) return false;

            // There has to be a direction to be absent from. Damage with no live instigator -
            // a collapsing roof, toxic buildup, a fall - is not aimed at anything, so there is
            // no place the body could have not been.
            Thing instigator = dinfo.Instigator;
            if (instigator == null || instigator == pawn) return false;
            if (instigator.Destroyed || !instigator.Spawned || instigator.Map != pawn.Map) return false;

            // Fire already on the body is the same case wearing a different hat: its instigator
            // is the Fire, and the Fire is attached to the carrier. Coming apart does not put
            // any distance between them, so it does not get to try.
            if (instigator is Fire) return false;

            return true;
        }

        /// <summary>
        /// Comes apart, moves, and puts itself back together.
        ///
        /// Assumes <see cref="Applies"/> has already said yes and <see cref="TryFindLanding"/>
        /// has already found somewhere. Both of those run before the prefix cancels anything,
        /// which is the ordering that matters: a carrier the flock cannot put down anywhere -
        /// cornered in a one-cell closet, or on a map where nothing within range is reachable -
        /// takes the hit normally rather than having it quietly deleted.
        /// </summary>
        public static void Disperse(Pawn pawn, Gene_Dispersal gene, IntVec3 to)
        {
            Map map = pawn.Map;
            Vector3 fromVec = pawn.DrawPos;

            DispersalFX.Depart(fromVec, map);

            pawn.Position = to;
            pawn.Notify_Teleported(true, true);

            MapComponent_Dispersal.Begin(map, fromVec, pawn.DrawPos);
            DispersalFX.Arrive(pawn.DrawPos, map);

            Bleed(pawn, gene.BloodLossPerScatter);
            pawn.health.GetOrAddHediff(DispersalDefOf.AG_Reassembling);

            gene.Spend();

            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message(
                    "AG_DispersalScattered".Translate(pawn.LabelShort, gene.Charges.ToString()),
                    pawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        /// <summary>
        /// The price, charged by both halves of the gene. Severity is added to Core's own BloodLoss rather than to a hediff of this
        /// mod's, so it stacks with every other thing that has bled this pawn and is read by
        /// everything that already knows how to read blood loss - the health tab, the doctor's
        /// priorities, the downed threshold.
        /// </summary>
        public static void Bleed(Pawn pawn, float severity)
        {
            if (pawn == null || pawn.health == null) return;

            Hediff bloodLoss = pawn.health.GetOrAddHediff(HediffDefOf.BloodLoss);
            if (bloodLoss != null) bloodLoss.Severity += severity;
        }

        /// <summary>
        /// Where the flock puts them down.
        ///
        /// Scored rather than random, because a scatter that lands the carrier in front of the
        /// person who just shot at them is worse than no scatter at all - it spends a charge and
        /// blood to move them somewhere they still have to run from. Breaking line of sight is
        /// worth far more than raw distance, which is why it outweighs it by an order of
        /// magnitude below.
        /// </summary>
        public static bool TryFindLanding(Pawn pawn, Gene_Dispersal gene, Thing threat, out IntVec3 result)
        {
            result = IntVec3.Invalid;

            Map map = pawn.Map;
            if (map == null) return false;

            IntVec3 from = pawn.Position;
            int minRange = gene.MinRange;

            // Gathered once rather than per candidate cell. This whole method runs inside
            // Thing.TakeDamage, and the naive version - a radial scan around every cell in a
            // ten-cell radius - is several thousand thing lookups on a path that is already
            // doing a reachability query per cell.
            List<Pawn> threats = HostilesNear(pawn, map, gene.MaxRange + 4);

            float best = float.MinValue;
            try
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(from, gene.MaxRange, true))
                {
                    if (!cell.InBounds(map)) continue;
                    if ((cell - from).LengthHorizontalSquared < minRange * minRange) continue;
                    if (!cell.Standable(map)) continue;
                    if (cell.Fogged(map)) continue;
                    if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) continue;

                    float score = Score(map, cell, threat, threats);
                    if (score <= best) continue;

                    best = score;
                    result = cell;
                }
            }
            finally
            {
                threats.Clear();
                scratch.Clear();
            }

            return result.IsValid;
        }

        /// <summary>Reused between scatters; nothing holds a reference past TryFindLanding.</summary>
        private static readonly List<Pawn> scratch = new List<Pawn>();

        private static List<Pawn> HostilesNear(Pawn pawn, Map map, float radius)
        {
            scratch.Clear();

            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            float radiusSquared = radius * radius;

            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == pawn || other.Dead || other.Downed) continue;
                if (!other.HostileTo(pawn)) continue;
                if ((other.Position - pawn.Position).LengthHorizontalSquared > radiusSquared) continue;

                scratch.Add(other);
            }
            return scratch;
        }

        private static float Score(Map map, IntVec3 cell, Thing threat, List<Pawn> threats)
        {
            float score = 0f;

            if (threat != null && threat.Spawned)
            {
                score += (cell - threat.Position).LengthHorizontal;

                // The one thing worth more than distance. Ten cells in the open is a worse
                // place to be standing than four cells behind a wall.
                if (!GenSight.LineOfSight(threat.Position, cell, map, true)) score += 40f;
            }

            // Landing at somebody's elbow is landing in a melee, which is the fight the carrier
            // just declined. Anyone hostile within two cells is charged against the cell.
            for (int i = 0; i < threats.Count; i++)
            {
                if ((threats[i].Position - cell).LengthHorizontalSquared <= 4) score -= 25f;
            }

            // A nudge, not a rule: seeded on the cell so the choice is stable within one scatter
            // but not identical between two of them from the same doorway.
            score += Rand.ValueSeeded(cell.GetHashCode()) * 2f;

            return score;
        }
    }
}
