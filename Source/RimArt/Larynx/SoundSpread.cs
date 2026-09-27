using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where a word said at a cell can be heard, and how far the sound travelled to get there.
    ///
    /// The sound passes every cell that can be seen over (GenGrid.CanBeSeenOver: anything not
    /// full-fill, and open doors), so walls, natural rock and closed doors stop it while sandbags,
    /// furniture, trees and pawns do not. Entering an open door's cell costs
    /// <see cref="LarynxExtension.doorwayCost"/> extra, and the sound stops at the volume's reach.
    ///
    /// Distances are any-angle (Theta*): a cell takes the straight-line distance from the last
    /// point the sound could see it from - the speaker, a wall corner it bent round, or a doorway
    /// it came through - plus the distance to that point. So open ground is a circle, and past a
    /// door the sound spreads out from the doorway. Each cell's point is its source.
    /// </summary>
    public static class SoundSpread
    {
        private const float Diagonal = 1.41421356f;

        /// <summary>
        /// Distance the sound travelled to each reached cell, the origin at 0. When
        /// <paramref name="sources"/> is given it is filled with each cell's source: the point the
        /// distance is measured from (the picture draws its rings round it).
        /// </summary>
        public static Dictionary<IntVec3, float> Reach(Map map, IntVec3 origin, float reach, float doorwayCost,
            Dictionary<IntVec3, IntVec3> sources = null)
        {
            var dist = new Dictionary<IntVec3, float>();
            Dictionary<IntVec3, IntVec3> src = sources ?? new Dictionary<IntVec3, IntVec3>();
            src.Clear();
            if (map == null || !origin.InBounds(map)) return dist;
            var heap = new CellHeap();
            dist[origin] = 0f;
            src[origin] = origin;
            heap.Push(origin, 0f);
            while (heap.Count > 0)
            {
                heap.Pop(out IntVec3 cell, out float d);
                if (d > dist[cell]) continue;
                IntVec3 source = src[cell];
                for (int i = 0; i < 8; i++)
                {
                    IntVec3 next = cell + GenAdj.AdjacentCells[i];
                    if (!Carries(next, map)) continue;
                    bool diagonal = next.x != cell.x && next.z != cell.z;
                    if (diagonal && (!Carries(new IntVec3(next.x, 0, cell.z), map) || !Carries(new IntVec3(cell.x, 0, next.z), map)))
                        continue;
                    float step = diagonal ? Diagonal : 1f;
                    float nd;
                    IntVec3 nextSource;
                    if (IsDoor(next, map))
                    {
                        // A doorway costs its reach and becomes the source of everything past it.
                        nd = d + step + doorwayCost;
                        nextSource = next;
                    }
                    else if (source != cell && LineClear(map, source, next))
                    {
                        nd = dist[source] + (next - source).LengthHorizontal;
                        nextSource = source;
                    }
                    else
                    {
                        nd = d + step;
                        nextSource = cell;
                    }
                    if (nd > reach) continue;
                    if (dist.TryGetValue(next, out float old) && old <= nd) continue;
                    dist[next] = nd;
                    src[next] = nextSource;
                    heap.Push(next, nd);
                }
            }
            return dist;
        }

        public static bool Carries(IntVec3 cell, Map map) => cell.InBounds(map) && cell.CanBeSeenOverFast(map);

        private static bool IsDoor(IntVec3 cell, Map map) => cell.GetEdifice(map) is Building_Door;

        /// <summary>
        /// Whether the straight line between the two cell centres passes only cells that carry sound
        /// and are not doors (the start may be a door: a doorway is a source). Every cell the line
        /// touches is checked, and where it crosses exactly at a corner both side cells must carry,
        /// the same rule as a diagonal step, except on the way out of the start cell.
        /// </summary>
        private static bool LineClear(Map map, IntVec3 from, IntVec3 to)
        {
            int x = from.x, z = from.z;
            int dx = to.x - from.x, dz = to.z - from.z;
            int stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0, stepZ = dz > 0 ? 1 : dz < 0 ? -1 : 0;
            float tDeltaX = dx != 0 ? 1f / Mathf.Abs(dx) : float.MaxValue;
            float tDeltaZ = dz != 0 ? 1f / Mathf.Abs(dz) : float.MaxValue;
            float tMaxX = dx != 0 ? 0.5f * tDeltaX : float.MaxValue;
            float tMaxZ = dz != 0 ? 0.5f * tDeltaZ : float.MaxValue;
            const float Eps = 1e-5f;
            while (x != to.x || z != to.z)
            {
                if (Mathf.Abs(tMaxX - tMaxZ) < Eps)
                {
                    // Out of the source cell itself the sound may leave on a diagonal: a doorway or
                    // a corner spreads it out, it does not squeeze it into a straight line.
                    bool leaving = x == from.x && z == from.z;
                    if (!leaving && (!Passes(new IntVec3(x + stepX, 0, z), map) || !Passes(new IntVec3(x, 0, z + stepZ), map))) return false;
                    x += stepX;
                    z += stepZ;
                    tMaxX += tDeltaX;
                    tMaxZ += tDeltaZ;
                }
                else if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
                if (!Passes(new IntVec3(x, 0, z), map)) return false;
            }
            return true;
        }

        private static bool Passes(IntVec3 cell, Map map) => Carries(cell, map) && !IsDoor(cell, map);

        // One entry: the six word gizmos, their tooltips and the preview all ask for the same
        // speaker, cell and volume in the same tick, several times per frame.
        private static int cachedPawn = -1, cachedTick = -1;
        private static IntVec3 cachedCell;
        private static float cachedReach, cachedDoorway;
        private static Map cachedMap;
        private static Dictionary<IntVec3, float> cached;

        /// <summary><see cref="Reach"/> from the speaker's cell, reused within one tick.</summary>
        public static Dictionary<IntVec3, float> From(Pawn speaker, float reach, float doorwayCost)
        {
            Map map = speaker.Map;
            int tick = Find.TickManager.TicksGame;
            if (cached != null && cachedPawn == speaker.thingIDNumber && cachedTick == tick && cachedMap == map
                && cachedCell == speaker.Position && cachedReach == reach && cachedDoorway == doorwayCost)
                return cached;
            cached = Reach(map, speaker.Position, reach, doorwayCost);
            cachedPawn = speaker.thingIDNumber;
            cachedTick = tick;
            cachedMap = map;
            cachedCell = speaker.Position;
            cachedReach = reach;
            cachedDoorway = doorwayCost;
            return cached;
        }

        /// <summary>A binary min-heap of cells by distance. Stale entries are skipped by the caller.</summary>
        private sealed class CellHeap
        {
            private readonly List<IntVec3> cells = new List<IntVec3>();
            private readonly List<float> keys = new List<float>();

            public int Count => cells.Count;

            public void Push(IntVec3 cell, float key)
            {
                cells.Add(cell);
                keys.Add(key);
                int i = cells.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (keys[parent] <= keys[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public void Pop(out IntVec3 cell, out float key)
            {
                cell = cells[0];
                key = keys[0];
                int last = cells.Count - 1;
                cells[0] = cells[last];
                keys[0] = keys[last];
                cells.RemoveAt(last);
                keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1, right = left + 1, smallest = i;
                    if (left < cells.Count && keys[left] < keys[smallest]) smallest = left;
                    if (right < cells.Count && keys[right] < keys[smallest]) smallest = right;
                    if (smallest == i) break;
                    Swap(i, smallest);
                    i = smallest;
                }
            }

            private void Swap(int a, int b)
            {
                IntVec3 c = cells[a];
                cells[a] = cells[b];
                cells[b] = c;
                float k = keys[a];
                keys[a] = keys[b];
                keys[b] = k;
            }
        }
    }
}
