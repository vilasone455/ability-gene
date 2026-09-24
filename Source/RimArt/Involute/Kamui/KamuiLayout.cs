using System;
using System.Collections.Generic;
using System.Linq;

namespace RimArt
{
    /// <summary>
    /// One block of Kamui's dimension in whole cells: X..X+W-1, Z..Z+H-1. Level 0 is the walkable level
    /// (a top pawns stand on); below 0 is that many cells further down, above 0 that many higher. Only
    /// level 0 blocks are walkable; the others are drawn.
    /// </summary>
    internal sealed class KamuiBlock
    {
        public int Id = -1, X, Z, W, H, Level, Group = -1, Shade;

        public bool Contains(int x, int z) => x >= X && x < X + W && z >= Z && z < Z + H;
    }

    /// <summary>
    /// The layout of Kamui's dimension: walkable tops (a main top in the middle, tops grown off it and
    /// islands nobody can walk to), lower blocks in the void between them, and blocks past the map edge.
    /// The port of generate() in Tools/VfxLab/web/sketches/lib/kamui.js (the Kamui dimension sketch).
    ///
    /// It is exact, not only the same rules: the sketch's Mulberry32 numbers are drawn in the same order
    /// (short-circuits included) and the maths is the sketch's, so seed N at a given size, cover and
    /// island count is the same map in game and in the lab. Tests/Kamui checks it against the JS.
    /// System only, so the test links it without stubs.
    /// </summary>
    internal sealed class KamuiLayout
    {
        // The rules, as the sketch's Rule. Size, cover and islands are the generator def's; the rest is
        // the shape of the place.
        public const int DefaultSize = 48, Margin = 2, MainW = 14, MainH = 10;
        public const int Min = 3, Max = 9, IslandMax = 6, GapMax = 2;
        public const double Touch = 0.65, MainParent = 0.8, DefaultCover = 0.55;
        public const int DefaultIslands = 6, IslandsMax = 10, IslandCells = 45;
        public const double GapFill = 0.4;
        public const int GapDepth = 3;
        public const int Band = 20, TallMax = 8, DeepMax = 6;
        public const double BandFill = 0.8, Tall = 0.3;
        private const int GrowTries = 6000, IslandTries = 3000;

        public readonly int Seed, Size;
        /// <summary>Walkable tops, by id. Group 0 is the main top's; every other group is an island.</summary>
        public readonly List<KamuiBlock> Tops = new List<KamuiBlock>();
        /// <summary>Lower blocks in the void inside the map, 1 to <see cref="GapDepth"/> cells down.</summary>
        public readonly List<KamuiBlock> Lower = new List<KamuiBlock>();
        /// <summary>Blocks past the map edge, up to <see cref="Band"/> cells out.</summary>
        public readonly List<KamuiBlock> Outside = new List<KamuiBlock>();
        /// <summary>Top id per cell (z * Size + x), -1 for void.</summary>
        public readonly int[] Occ;
        /// <summary>Where Obito and allies arrive: the middle cell of the main top.</summary>
        public (int x, int z) Mouth;
        /// <summary>One cell per island (the middle of its first top), in island order: where held enemies land.</summary>
        public readonly List<(int x, int z)> Landings = new List<(int x, int z)>();
        public int Walkable, IslandCount;

        private KamuiLayout(int seed, int size)
        {
            Seed = seed;
            Size = size;
            Occ = Enumerable.Repeat(-1, size * size).ToArray();
        }

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Size && z < Size;
        public bool IsWalkable(int x, int z) => InBounds(x, z) && Occ[z * Size + x] >= 0;
        /// <summary>The walkable top at a cell, or null.</summary>
        public KamuiBlock TopAt(int x, int z) => IsWalkable(x, z) ? Tops[Occ[z * Size + x]] : null;

        /// <summary>
        /// Every block to draw, in the order to draw it: north first; on a tie, lower first, then west
        /// first. Blocks never overlap on the ground, so this order is the whole cover rule.
        /// </summary>
        public List<KamuiBlock> PaintOrder(bool lower = true, bool outside = true)
        {
            IEnumerable<KamuiBlock> all = Tops;
            if (lower) all = all.Concat(Lower);
            if (outside) all = all.Concat(Outside);
            return all.OrderByDescending(b => b.Z).ThenBy(b => b.Level).ThenBy(b => b.X).ToList();
        }

        public static KamuiLayout Generate(int seed, int size = DefaultSize, double cover = DefaultCover, int islands = DefaultIslands)
        {
            var layout = new KamuiLayout(seed, size);
            var r = new Mulberry32(seed);
            int n = size, m = Margin;
            int[] occ = layout.Occ;
            List<KamuiBlock> tops = layout.Tops;
            var groupCells = new Dictionary<int, int>();

            bool Inside(int x, int z, int w, int h) => x >= m && z >= m && x + w <= n - m && z + h <= n - m;
            bool Free(int x, int z, int w, int h)
            {
                for (int cz = z; cz < z + h; cz++)
                    for (int cx = x; cx < x + w; cx++)
                        if (occ[cz * n + cx] >= 0) return false;
                return true;
            }
            // Groups of the tops in the ring of cells around the rectangle, corners included.
            HashSet<int> RingGroups(int x, int z, int w, int h)
            {
                var s = new HashSet<int>();
                for (int cz = z - 1; cz <= z + h; cz++)
                    for (int cx = x - 1; cx <= x + w; cx++)
                    {
                        if (cx < 0 || cz < 0 || cx >= n || cz >= n) continue;
                        if (cx >= x && cx < x + w && cz >= z && cz < z + h) continue;
                        int id = occ[cz * n + cx];
                        if (id >= 0) s.Add(tops[id].Group);
                    }
                return s;
            }
            void Place(KamuiBlock b, int group)
            {
                b.Id = tops.Count;
                b.Group = group;
                b.Level = 0;
                b.Shade = (int)Math.Floor(LabHash(b.Id, 5, seed) * 4);
                tops.Add(b);
                for (int cz = b.Z; cz < b.Z + b.H; cz++)
                    for (int cx = b.X; cx < b.X + b.W; cx++)
                        occ[cz * n + cx] = b.Id;
                groupCells.TryGetValue(group, out int cells);
                groupCells[group] = cells + b.W * b.H;
            }

            var main = new KamuiBlock { X = (n - MainW) / 2, Z = (n - MainH) / 2, W = MainW, H = MainH };
            Place(main, 0);
            int walk = MainW * MainH, nextGroup = 1, islandCount = 0;
            double target = cover * n * n;
            for (int tries = 0; tries < GrowTries && walk < target; tries++)
            {
                bool fromMain = islandCount == 0 || r.Next() < MainParent;
                List<KamuiBlock> pool = tops.Where(t => (t.Group == 0) == fromMain).ToList();
                KamuiBlock parent = pool[(int)Math.Floor(r.Next() * pool.Count)];
                int side = r.Int(0, 3);                                    // 0 east, 1 north, 2 west, 3 south
                int gap = r.Next() < Touch ? 0 : r.Int(1, GapMax);
                int hi = gap > 0 ? IslandMax : Max;
                int w = r.Int(Min, hi), h = r.Int(Min, hi);
                int ov = gap == 0 ? 2 : 1;                                 // a touching top shares 2+ cells of edge
                int x, z;
                if (side % 2 == 0)
                {
                    x = side == 0 ? parent.X + parent.W + gap : parent.X - gap - w;
                    z = r.Int(parent.Z - h + ov, parent.Z + parent.H - ov);
                }
                else
                {
                    z = side == 1 ? parent.Z + parent.H + gap : parent.Z - gap - h;
                    x = r.Int(parent.X - w + ov, parent.X + parent.W - ov);
                }
                if (!Inside(x, z, w, h) || !Free(x, z, w, h)) continue;
                HashSet<int> ring = RingGroups(x, z, w, h);
                var b = new KamuiBlock { X = x, Z = z, W = w, H = h };
                if (gap > 0)
                {
                    // A new island: nothing within one cell of it.
                    if (ring.Count > 0 || islandCount >= IslandsMax) continue;
                    Place(b, nextGroup++);
                    islandCount++;
                }
                else
                {
                    // Grows its parent's group and touches no other group, so islands stay islands.
                    if (ring.Count != 1 || !ring.Contains(parent.Group)) continue;
                    if (parent.Group != 0 && groupCells[parent.Group] + w * h > IslandCells) continue;
                    Place(b, parent.Group);
                }
                walk += w * h;
            }
            // Too few islands on a crowded seed: small ones in whatever void is left.
            for (int tries = 0; tries < IslandTries && islandCount < islands; tries++)
            {
                int w = r.Int(Min, IslandMax - 1), h = r.Int(Min, IslandMax - 1);
                int x = r.Int(m, n - m - w), z = r.Int(m, n - m - h);
                if (!Free(x, z, w, h) || RingGroups(x, z, w, h).Count > 0) continue;
                Place(new KamuiBlock { X = x, Z = z, W = w, H = h }, nextGroup++);
                islandCount++;
                walk += w * h;
            }

            // Lower blocks in the void inside the map: north to south, west to east, each growing east then
            // south over void no block covers yet.
            var low = new bool[n * n];
            for (int z = n - 1; z >= 0; z--)
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    if (occ[i] >= 0 || low[i] || r.Next() > GapFill) continue;
                    int wMax = r.Int(2, 6), hMax = r.Int(2, 6);
                    int w = 0;
                    while (w < wMax && x + w < n && occ[z * n + x + w] < 0 && !low[z * n + x + w]) w++;
                    int h = 0;
                    for (; h < hMax && z - h >= 0; h++)
                    {
                        bool ok = true;
                        for (int k = 0; k < w; k++)
                        {
                            int j = (z - h) * n + x + k;
                            if (occ[j] >= 0 || low[j]) { ok = false; break; }
                        }
                        if (!ok) break;
                    }
                    var b = new KamuiBlock { X = x, Z = z - h + 1, W = w, H = h };
                    b.Level = -r.Int(1, GapDepth);
                    for (int cz = b.Z; cz < b.Z + b.H; cz++)
                        for (int cx = b.X; cx < b.X + b.W; cx++)
                            low[cz * n + cx] = true;
                    layout.Lower.Add(b);
                }

            // Past the map edge. Blocks rise above the walkable level only where nothing in the map is
            // behind them: north of it, or beside it east and west. South of it they only go down.
            int band = Band, span = n + 2 * band;
            var taken = new bool[span * span];
            bool InMap(int x, int z) => x >= 0 && z >= 0 && x < n && z < n;
            bool IsTaken(int x, int z) => InMap(x, z) || taken[(z + band) * span + x + band];
            for (int z = n + band - 1; z >= -band; z--)
                for (int x = -band; x < n + band; x++)
                {
                    if (IsTaken(x, z) || r.Next() > BandFill) continue;
                    int wMax = r.Int(3, 9), hMax = r.Int(3, 9);
                    int w = 0;
                    while (w < wMax && x + w < n + band && !IsTaken(x + w, z)) w++;
                    int h = 0;
                    for (; h < hMax && z - h >= -band; h++)
                    {
                        bool ok = true;
                        for (int k = 0; k < w; k++)
                            if (IsTaken(x + k, z - h)) { ok = false; break; }
                        if (!ok) break;
                    }
                    var b = new KamuiBlock { X = x, Z = z - h + 1, W = w, H = h };
                    bool southOfMap = b.X < n && b.X + b.W > 0 && b.Z < n;
                    b.Level = !southOfMap && r.Next() < Tall ? r.Int(1, TallMax) : -r.Int(1, DeepMax);
                    for (int cz = b.Z; cz < b.Z + b.H; cz++)
                        for (int cx = b.X; cx < b.X + b.W; cx++)
                            taken[(cz + band) * span + cx + band] = true;
                    layout.Outside.Add(b);
                }

            layout.Mouth = (main.X + MainW / 2, main.Z + MainH / 2);
            for (int g = 1; g < nextGroup; g++)
            {
                KamuiBlock first = tops.First(t => t.Group == g);
                layout.Landings.Add((first.X + first.W / 2, first.Z + first.H / 2));
            }
            layout.Walkable = walk;
            layout.IslandCount = islandCount;
            return layout;
        }

        /// <summary>Mulberry32, as the sketch's rng(seed): the same 32-bit steps, the same doubles out.</summary>
        internal sealed class Mulberry32
        {
            private uint a;

            public Mulberry32(int seed) { a = unchecked((uint)seed * 2654435761u) ^ 0x9e3779b9u; }

            public double Next()
            {
                unchecked
                {
                    a += 0x6D2B79F5u;
                    uint t = a;
                    t = (t ^ (t >> 15)) * (t | 1u);
                    t ^= t + (t ^ (t >> 7)) * (t | 61u);
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            }

            /// <summary>The sketch's int(R, lo, hi): lo to hi inclusive.</summary>
            public int Int(int lo, int hi) => lo + (int)Math.Floor(Next() * (hi - lo + 1));
        }

        /// <summary>
        /// The lab's hash(x, y, seed) from Tools/VfxLab/web/js/standins.js, 0..1: the sum wraps to 32 bits
        /// as JavaScript's "| 0" does, then two 32-bit mixing steps.
        /// </summary>
        public static double LabHash(int x, int y, int seed)
        {
            unchecked
            {
                int h = (int)((long)x * 374761393L + (long)y * 668265263L + (long)seed * 144665L);
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967295.0;
            }
        }
    }
}
