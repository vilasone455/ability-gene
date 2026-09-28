using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What Minato can jump to: his marks. A pawn is marked while it holds one of his sealed kunai
    /// (<see cref="Hediff_EmbeddedKunai.sealedByMinato"/>) or his sealing touch (<see cref="Hediff_MinatoSeal"/>);
    /// a kunai item on the ground is a mark while it is sealed. Every Flying Thunder God ability reads marks
    /// through here, and where he lands next to one.
    /// </summary>
    public static class ThunderGodMarks
    {
        public static bool HasSealedKunai(Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return false;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i] is Hediff_EmbeddedKunai kunai && kunai.Active && kunai.sealedByMinato) return true;
            return false;
        }

        public static Hediff_MinatoSeal Seal(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(MinatoDefOf.AG_MinatoSeal) as Hediff_MinatoSeal;

        /// <summary>A living pawn on a map that holds one of his kunai or his seal.</summary>
        public static bool Marked(Pawn pawn) =>
            pawn != null && pawn.Spawned && !pawn.Dead && (HasSealedKunai(pawn) || Seal(pawn) != null);

        /// <summary>A sealed kunai lying (or standing planted) on this cell, or null.</summary>
        public static KunaiItem GroundKunaiAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map)) return null;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is KunaiItem kunai && kunai.sealedByMinato && kunai.Spawned) return kunai;
            return null;
        }

        /// <summary>
        /// The mark a click means: a marked pawn, a sealed kunai item, or a cell holding either (a marked pawn
        /// first). False when there is none.
        /// </summary>
        public static bool TryResolve(LocalTargetInfo target, Map map, out Pawn pawn, out KunaiItem item)
        {
            pawn = null;
            item = null;
            if (!target.IsValid || map == null) return false;
            if (target.Thing is Pawn p && Marked(p))
            {
                pawn = p;
                return true;
            }
            if (target.Thing is KunaiItem k && k.sealedByMinato && k.Spawned && k.Map == map)
            {
                item = k;
                return true;
            }
            IntVec3 cell = target.Cell;
            if (!cell.InBounds(map)) return false;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn other && Marked(other))
                {
                    pawn = other;
                    return true;
                }
            item = GroundKunaiAt(cell, map);
            return item != null;
        }

        /// <summary>
        /// Why <paramref name="caster"/> cannot jump to <paramref name="target"/>, or null. Out-of-range is left to
        /// the ability's own verb range.
        /// </summary>
        public static string Problem(Pawn caster, LocalTargetInfo target, out Pawn pawn, out KunaiItem item)
        {
            if (!TryResolve(target, caster?.Map, out pawn, out item)) return "Needs one of Minato's kunai: in a pawn, sealed by his touch, or on the ground.";
            if (pawn == caster) return "Minato cannot jump to himself.";
            IntVec3 landing = pawn != null ? Behind(caster, caster.Position, pawn) : OnKunai(caster, item);
            if (!landing.IsValid) return "No room to land there.";
            return null;
        }

        /// <summary>
        /// The free cell next to <paramref name="target"/> that is most behind it, seen from <paramref name="from"/>:
        /// straight behind if it is open, otherwise the open neighbour nearest that side. Invalid when every
        /// neighbour is blocked or taken.
        /// </summary>
        public static IntVec3 Behind(Pawn caster, IntVec3 from, Pawn target)
        {
            Map map = target?.Map;
            if (map == null) return IntVec3.Invalid;
            IntVec3 at = target.Position;
            Vector2 look = new Vector2(at.x - from.x, at.z - from.z);
            if (look.sqrMagnitude > 1e-4f) look.Normalize();
            IntVec3 best = IntVec3.Invalid;
            float bestScore = float.MinValue;
            foreach (IntVec3 cell in GenAdjFast.AdjacentCells8Way(at))
            {
                if (!Free(caster, cell, map)) continue;
                Vector2 side = new Vector2(cell.x - at.x, cell.z - at.z).normalized;
                // Straight lines first: a diagonal with the same lean scores a little lower.
                float score = Vector2.Dot(side, look) - (cell.x != at.x && cell.z != at.z ? 0.01f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cell;
                }
            }
            return best;
        }

        /// <summary>Where he lands to pick up a ground kunai: its own cell, or the nearest free neighbour if someone stands there.</summary>
        public static IntVec3 OnKunai(Pawn caster, KunaiItem item)
        {
            Map map = item?.Map;
            if (map == null) return IntVec3.Invalid;
            IntVec3 at = item.Position;
            if (Free(caster, at, map)) return at;
            foreach (IntVec3 cell in GenAdjFast.AdjacentCells8Way(at))
                if (Free(caster, cell, map)) return cell;
            return IntVec3.Invalid;
        }

        /// <summary>He can stand here: standable, seen, and no other pawn in it.</summary>
        public static bool Free(Pawn caster, IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)) return false;
            Pawn standing = cell.GetFirstPawn(map);
            return standing == null || standing == caster;
        }

        /// <summary>
        /// A ground kunai he jumped to comes back: into the belt as far as it has room (the seal goes, the belt's
        /// charges are only a count), the rest of the stack into his inventory, still sealed.
        /// </summary>
        public static void TakeKunai(Pawn caster, KunaiItem item)
        {
            if (caster == null || item == null || item.Destroyed) return;
            CompApparelReloadable belt = KunaiBelt.WornBy(caster);
            if (belt != null && belt.NeedsReload(true)) belt.ReloadFrom(item);
            if (item.Destroyed || item.stackCount <= 0) return;
            if (item.Spawned) item.DeSpawn();
            if (caster.inventory != null && caster.inventory.innerContainer.TryAdd(item)) return;
            if (caster.Spawned) GenPlace.TryPlaceThing(item, caster.Position, caster.Map, ThingPlaceMode.Near);
        }

        /// <summary>
        /// Hostile marked pawns within <paramref name="range"/> cells of <paramref name="caster"/> that are still
        /// standing, in the order the chain takes them: the nearest to where he is, then the nearest to that one,
        /// and so on, up to <paramref name="most"/>.
        /// </summary>
        public static List<Pawn> ChainRoute(Pawn caster, float range, int most)
        {
            var route = new List<Pawn>();
            Map map = caster?.Map;
            if (map == null) return route;
            var left = new List<Pawn>();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!MinatoKit.Foe(caster, pawn) || pawn.Downed || !Marked(pawn)) continue;
                if (pawn.Position.DistanceTo(caster.Position) > range) continue;
                left.Add(pawn);
            }
            IntVec3 at = caster.Position;
            while (route.Count < most && left.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < left.Count; i++)
                    if (left[i].Position.DistanceToSquared(at) < left[best].Position.DistanceToSquared(at)) best = i;
                route.Add(left[best]);
                at = left[best].Position;
                left.RemoveAt(best);
            }
            return route;
        }
    }
}
