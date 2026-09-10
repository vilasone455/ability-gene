using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The two questions the arc asks about the world: who is next, and where does the carrier
    /// have to be standing to hit them.
    /// </summary>
    public static class ArcUtility
    {
        private static readonly List<Pawn> candidates = new List<Pawn>();

        /// <summary>
        /// Whether the arc can land on this person at all.
        ///
        /// Downed is excluded, and that is Melee Animation's rule rather than a choice made
        /// here - their renderer refuses a downed pawn outright. It happens to be the right
        /// rule anyway: an arc is three people taken out of a fight, not a tour of the wounded.
        /// </summary>
        public static bool IsStrikeable(Pawn carrier, Pawn victim)
        {
            if (carrier == null || victim == null || victim == carrier) return false;
            if (victim.Dead || victim.Downed || !victim.Spawned) return false;
            if (victim.Map == null || victim.Map != carrier.Map) return false;
            if (!victim.HostileTo(carrier)) return false;
            return !MeleeAnimation.IsAnimating(victim);
        }

        /// <summary>
        /// The whole chain, decided at the moment of the cast.
        ///
        /// Each link is the nearest enemy still standing to the person the arc just picked, not
        /// to the carrier - which is what makes the shape of the chain the shape of the crowd
        /// rather than a circle around the caster. It is resolved up front on purpose: a chain
        /// that re-picked as it went would follow a fight that has already changed, and the
        /// player would have no way to read what they were about to get.
        ///
        /// Every link needs line of sight from the last one. The ability already requires it for
        /// the first target because an arc is a step and not a teleport past masonry, and a chain
        /// that ignored the rule after the first hop would let a carrier cross a wall by standing
        /// next to somebody near it - which is a different and much stronger gene than this one.
        /// </summary>
        public static List<Pawn> BuildChain(Pawn carrier, Pawn first, float chainRadius, int maxTargets)
        {
            List<Pawn> chain = new List<Pawn>();
            if (!IsStrikeable(carrier, first)) return chain;

            chain.Add(first);

            Map map = carrier.Map;
            float radiusSquared = chainRadius * chainRadius;

            while (chain.Count < maxTargets)
            {
                Pawn from = chain[chain.Count - 1];
                Pawn nearest = null;
                float nearestDistance = float.MaxValue;

                candidates.Clear();
                candidates.AddRange(map.mapPawns.AllPawnsSpawned);

                for (int i = 0; i < candidates.Count; i++)
                {
                    Pawn candidate = candidates[i];
                    if (chain.Contains(candidate)) continue;
                    if (!IsStrikeable(carrier, candidate)) continue;

                    float distance = (candidate.Position - from.Position).LengthHorizontalSquared;
                    if (distance > radiusSquared || distance >= nearestDistance) continue;
                    if (!GenSight.LineOfSight(from.Position, candidate.Position, map, true)) continue;

                    nearest = candidate;
                    nearestDistance = distance;
                }

                candidates.Clear();
                if (nearest == null) break;
                chain.Add(nearest);
            }

            return chain;
        }

        /// <summary>
        /// Where the carrier has to arrive to strike this person.
        ///
        /// Melee Animation lays an execution out with the attacker at (0,0) and the victim at
        /// (1,0), so the only two cells that can carry one are directly west of the victim, or
        /// directly east with the animation mirrored. The nearer of the two is preferred purely
        /// so the arc crosses the shortest distance it can.
        ///
        /// If neither is free the arc still happens - <paramref name="animated"/> comes back
        /// false, the carrier takes any adjacent cell, and the strike is an ordinary swing.
        /// A corridor fight is exactly where somebody presses this, and refusing it there
        /// because the geometry is tight would make the ability feel broken rather than
        /// constrained.
        /// </summary>
        public static bool TryFindLanding(Pawn carrier, Pawn victim, out IntVec3 cell, out bool flipX, out bool animated)
        {
            cell = IntVec3.Invalid;
            flipX = false;
            animated = false;

            Map map = victim?.Map;
            if (carrier == null || map == null) return false;

            IntVec3 west = victim.Position + IntVec3.West;
            IntVec3 east = victim.Position + IntVec3.East;
            bool westFirst = (carrier.Position - west).LengthHorizontalSquared <= (carrier.Position - east).LengthHorizontalSquared;

            IntVec3 firstChoice = westFirst ? west : east;
            IntVec3 secondChoice = westFirst ? east : west;

            if (CanLandOn(carrier, firstChoice, map))
            {
                cell = firstChoice;
                flipX = firstChoice == east;
                animated = true;
                return true;
            }

            if (CanLandOn(carrier, secondChoice, map))
            {
                cell = secondChoice;
                flipX = secondChoice == east;
                animated = true;
                return true;
            }

            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(victim))
            {
                if (!CanLandOn(carrier, adjacent, map)) continue;
                cell = adjacent;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The carrier's own cell always qualifies: they are already standing there, and a hop
        /// that does not move is still a strike.
        /// </summary>
        private static bool CanLandOn(Pawn carrier, IntVec3 cell, Map map)
        {
            if (!cell.IsValid || !cell.InBounds(map)) return false;
            if (cell == carrier.Position) return true;
            if (!cell.Standable(map)) return false;
            return cell.GetFirstPawn(map) == null;
        }
    }
}
