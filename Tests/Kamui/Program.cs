using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RimArt;

// Checks the C# generator of Kamui's dimension (Source/RimArt/Involute/Kamui/KamuiLayout.cs) in two ways:
//   parity: map by map against layouts.json, written from the sketch's own generator by dump-layouts.mjs,
//           so seed N with the same size, cover and islands is the same map in game and in the lab;
//   rules:  the map's rules hold for every map made here (walkable tops never overlap, islands keep a
//           cell of void round them, the main group is one walkable piece, lower blocks sit in void,
//           blocks past the edge stay outside and only rise where nothing in the map is behind them).
// Run: dotnet run --project Tests/Kamui

int failures = 0;
void Check(bool condition, string message)
{
    if (condition) return;
    failures++;
    if (failures <= 40) Console.WriteLine("FAIL " + message);
}
static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "layouts.json")));

// ---- parity with the sketch ------------------------------------------------------------------------
int maps = 0;
foreach (JsonElement expected in fixture.RootElement.GetProperty("layouts").EnumerateArray())
{
    int seed = expected.GetProperty("seed").GetInt32(), size = expected.GetProperty("size").GetInt32();
    double cover = expected.GetProperty("cover").GetDouble();
    int islands = expected.GetProperty("islands").GetInt32();
    string at = $"seed {seed}, size {size}, cover {cover}, islands {islands}";
    KamuiLayout map = KamuiLayout.Generate(seed, size, cover, islands);
    maps++;

    void Blocks(string name, List<KamuiBlock> got, bool tops)
    {
        var want = expected.GetProperty(name).EnumerateArray().Select(Ints).ToList();
        Check(got.Count == want.Count, $"{at}: {got.Count} {name}, the sketch has {want.Count}");
        for (int i = 0; i < Math.Min(got.Count, want.Count); i++)
        {
            KamuiBlock b = got[i];
            int[] e = want[i];
            bool same = b.X == e[0] && b.Z == e[1] && b.W == e[2] && b.H == e[3]
                && (tops ? b.Group == e[4] && b.Shade == e[5] && b.Level == 0 && b.Id == i : b.Level == e[4]);
            Check(same, $"{at}: {name} {i} is {b.X},{b.Z} {b.W}x{b.H} level {b.Level} group {b.Group} shade {b.Shade}, the sketch has [{string.Join(",", e)}]");
        }
    }
    Blocks("tops", map.Tops, true);
    Blocks("lower", map.Lower, false);
    Blocks("outside", map.Outside, false);

    int[] mouth = Ints(expected.GetProperty("mouth"));
    Check(map.Mouth == (mouth[0], mouth[1]), $"{at}: mouth {map.Mouth}, the sketch has {mouth[0]},{mouth[1]}");
    var landings = expected.GetProperty("landings").EnumerateArray().Select(Ints).ToList();
    Check(map.Landings.Count == landings.Count && map.Landings.Select((c, i) => c == (landings[i][0], landings[i][1])).All(x => x),
        $"{at}: landings differ from the sketch's");
    Check(map.Walkable == expected.GetProperty("walkable").GetInt32(), $"{at}: {map.Walkable} walkable cells, the sketch has {expected.GetProperty("walkable").GetInt32()}");
    Check(map.IslandCount == expected.GetProperty("islandCount").GetInt32(), $"{at}: {map.IslandCount} islands, the sketch has {expected.GetProperty("islandCount").GetInt32()}");

    if (expected.GetProperty("order").ValueKind == JsonValueKind.Array)
    {
        var order = expected.GetProperty("order").EnumerateArray().Select(Ints).ToList();
        List<KamuiBlock> mine = map.PaintOrder();
        Check(mine.Count == order.Count && mine.Select((b, i) => b.X == order[i][0] && b.Z == order[i][1] && b.Level == order[i][2]).All(x => x),
            $"{at}: paint order differs from the sketch's");
    }
}

// ---- the rules --------------------------------------------------------------------------------------
int ruled = 0;
double coverLow = 1, coverHigh = 0;
int islandsLow = int.MaxValue, islandsHigh = 0;
foreach (int size in new[] { 32, 48, 64 })
    for (int seed = 1; seed <= 200; seed++)
    {
        KamuiLayout map = KamuiLayout.Generate(seed, size);
        string at = $"seed {seed}, size {size}";
        ruled++;
        int n = map.Size;

        // Walkable tops: inside the margin, never overlapping (the occupancy grid names each cell once).
        int cells = 0;
        foreach (KamuiBlock t in map.Tops)
        {
            Check(t.X >= KamuiLayout.Margin && t.Z >= KamuiLayout.Margin && t.X + t.W <= n - KamuiLayout.Margin && t.Z + t.H <= n - KamuiLayout.Margin,
                $"{at}: top {t.Id} leaves the margin");
            for (int x = t.X; x < t.X + t.W; x++)
                for (int z = t.Z; z < t.Z + t.H; z++)
                    Check(map.Occ[z * n + x] == t.Id, $"{at}: cell {x},{z} of top {t.Id} is marked {map.Occ[z * n + x]}");
            cells += t.W * t.H;
        }
        Check(cells == map.Walkable, $"{at}: tops cover {cells} cells, the layout says {map.Walkable}");

        // Groups: no cell of one group within one cell (corners too) of another group's cell.
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                KamuiBlock t = map.TopAt(x, z);
                if (t == null) continue;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        KamuiBlock o = map.TopAt(x + dx, z + dz);
                        Check(o == null || o.Group == t.Group, $"{at}: groups {t.Group} and {o?.Group} touch at {x},{z}");
                    }
            }

        // The main group is one walkable piece reached from the mouth; no island is reached from it.
        var seen = new bool[n * n];
        var queue = new Queue<(int x, int z)>();
        queue.Enqueue(map.Mouth);
        seen[map.Mouth.z * n + map.Mouth.x] = true;
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            Check(map.TopAt(x, z).Group == 0, $"{at}: the walk from the mouth reaches group {map.TopAt(x, z).Group} at {x},{z}");
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, nz = z + dz;
                if (!map.IsWalkable(nx, nz) || seen[nz * n + nx]) continue;
                seen[nz * n + nx] = true;
                queue.Enqueue((nx, nz));
            }
        }
        foreach (KamuiBlock t in map.Tops.Where(t => t.Group == 0))
            Check(seen[t.Z * n + t.X], $"{at}: main-group top {t.Id} is not reached from the mouth");

        // Islands: one landing each, on its own island.
        Check(map.Landings.Count == map.IslandCount, $"{at}: {map.Landings.Count} landings for {map.IslandCount} islands");
        for (int i = 0; i < map.Landings.Count; i++)
        {
            KamuiBlock t = map.TopAt(map.Landings[i].x, map.Landings[i].z);
            Check(t != null && t.Group == i + 1, $"{at}: landing {i} is not on island {i + 1}");
        }

        // Lower blocks: in the map, on void only, never overlapping each other, 1 to 3 down.
        var low = new bool[n * n];
        foreach (KamuiBlock b in map.Lower)
        {
            Check(b.Level <= -1 && b.Level >= -KamuiLayout.GapDepth && b.W >= 1 && b.H >= 1, $"{at}: lower block at {b.X},{b.Z} has level {b.Level}, size {b.W}x{b.H}");
            for (int x = b.X; x < b.X + b.W; x++)
                for (int z = b.Z; z < b.Z + b.H; z++)
                {
                    Check(map.InBounds(x, z) && !map.IsWalkable(x, z) && !low[z * n + x], $"{at}: lower block at {b.X},{b.Z} covers {x},{z} badly");
                    if (map.InBounds(x, z)) low[z * n + x] = true;
                }
        }

        // Past the edge: outside the map, in the band, never overlapping; above the walkable level only
        // where the block is not south of the map.
        int band = KamuiLayout.Band, span = n + 2 * band;
        var taken = new bool[span * span];
        foreach (KamuiBlock b in map.Outside)
        {
            bool southOfMap = b.X < n && b.X + b.W > 0 && b.Z < n;
            Check(b.Level != 0 && (b.Level < 0 || !southOfMap) && b.Level <= KamuiLayout.TallMax && b.Level >= -KamuiLayout.DeepMax,
                $"{at}: outside block at {b.X},{b.Z} has level {b.Level}");
            for (int x = b.X; x < b.X + b.W; x++)
                for (int z = b.Z; z < b.Z + b.H; z++)
                {
                    bool inBand = x >= -band && z >= -band && x < n + band && z < n + band;
                    Check(inBand && !map.InBounds(x, z) && !taken[(z + band) * span + x + band], $"{at}: outside block at {b.X},{b.Z} covers {x},{z} badly");
                    if (inBand) taken[(z + band) * span + x + band] = true;
                }
        }

        if (size == 48)
        {
            double c = map.Walkable / (double)(n * n);
            coverLow = Math.Min(coverLow, c); coverHigh = Math.Max(coverHigh, c);
            islandsLow = Math.Min(islandsLow, map.IslandCount); islandsHigh = Math.Max(islandsHigh, map.IslandCount);
        }
    }

Console.WriteLine($"{maps} maps compared with the sketch, {ruled} maps checked against the rules.");
Console.WriteLine($"Size 48, seeds 1-200: walkable {coverLow:P1} to {coverHigh:P1}, islands {islandsLow} to {islandsHigh}.");
Console.WriteLine(failures == 0 ? "All checks pass." : $"{failures} checks failed.");
return failures == 0 ? 0 : 1;
