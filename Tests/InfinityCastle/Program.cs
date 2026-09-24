using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RimArt;

// Checks the C# castle generator (Source/RimArt/InfinityCastle/CastleLayout.cs) in two ways:
//   parity: castle by castle against layouts.json, written from the sketch's own generator by
//           dump-layouts.mjs, so seed N with M rooms is the same castle in game and in the lab;
//   rules:  the castle's rules from docs/infinity-castle-kit.md hold for every castle made here.
// Run: dotnet run --project Tests/InfinityCastle

int failures = 0;
void Check(bool condition, string message)
{
    if (condition) return;
    failures++;
    if (failures <= 40) Console.WriteLine("FAIL " + message);
}

static CastleKind KindOf(string name) => name switch
{
    "biwa" => CastleKind.Biwa, "tatami" => CastleKind.Tatami, "corridor" => CastleKind.Corridor,
    "hall" => CastleKind.Hall, "stair" => CastleKind.Stair, _ => throw new Exception("unknown kind " + name),
};
static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-9;
static int[] Ids(List<CastleRoom> rooms) => rooms.Select(r => r.Id).ToArray();
static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "layouts.json")));

// ---- parity with the sketch ------------------------------------------------------------------------
int castles = 0;
foreach (JsonElement expected in fixture.RootElement.GetProperty("layouts").EnumerateArray())
{
    int seed = expected.GetProperty("seed").GetInt32(), count = expected.GetProperty("count").GetInt32();
    string at = $"seed {seed}, {count} rooms";
    CastleLayout castle = CastleLayout.Generate(seed, count);
    castles++;

    var rooms = expected.GetProperty("rooms").EnumerateArray().ToList();
    Check(castle.Rooms.Count == rooms.Count, $"{at}: {castle.Rooms.Count} rooms, the sketch has {rooms.Count}");
    for (int i = 0; i < Math.Min(rooms.Count, castle.Rooms.Count); i++)
    {
        var e = rooms[i].EnumerateArray().ToList();
        CastleRoom r = castle.Rooms[i];
        Check(r.Id == i && r.Kind == KindOf(e[0].GetString()) && r.X == e[1].GetInt32() && r.Z == e[2].GetInt32() && r.W == e[3].GetInt32() && r.H == e[4].GetInt32(),
            $"{at}: room {i} is {r.Kind} {r.X},{r.Z} {r.W}x{r.H}, the sketch has {rooms[i]}");
    }

    var doors = expected.GetProperty("doors").EnumerateArray().ToList();
    Check(castle.Doorways.Count == doors.Count, $"{at}: {castle.Doorways.Count} doorways, the sketch has {doors.Count}");
    for (int i = 0; i < Math.Min(doors.Count, castle.Doorways.Count); i++)
    {
        var e = doors[i].EnumerateArray().ToList();
        CastleDoorway d = castle.Doorways[i];
        Check(d.A == e[0].GetInt32() && d.B == e[1].GetInt32() && d.AlongZ == (e[2].GetString() == "z")
            && d.Cells[0] == (e[3].GetInt32(), e[4].GetInt32()) && d.Cells[1] == (e[5].GetInt32(), e[6].GetInt32()),
            $"{at}: doorway {i} differs from the sketch's {doors[i]}");
    }

    int[] dist = Ints(expected.GetProperty("dist"));
    for (int i = 0; i < Math.Min(dist.Length, castle.Dist.Length); i++)
        Check((dist[i] < 0 ? CastleLayout.Unreachable : dist[i]) == castle.Dist[i], $"{at}: room {i} is {castle.Dist[i]} doorways out, the sketch says {dist[i]}");

    Check(Ids(castle.ArrivalRooms(6)).SequenceEqual(Ints(expected.GetProperty("arrival6"))), $"{at}: the 6 arrival rooms differ from the sketch's");
    Check(Ids(castle.ArrivalRooms(8)).SequenceEqual(Ints(expected.GetProperty("arrival8"))), $"{at}: the 8 arrival rooms differ from the sketch's");

    if (expected.GetProperty("lanterns").ValueKind == JsonValueKind.Array)
    {
        var lanterns = expected.GetProperty("lanterns").EnumerateArray().ToList();
        for (int i = 0; i < Math.Min(lanterns.Count, castle.Rooms.Count); i++)
        {
            var want = lanterns[i].EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble())).ToList();
            var got = CastleLayout.LanternsOf(castle.Rooms[i]);
            Check(got.Count == want.Count && got.Zip(want).All(q => Same(q.First.x, q.Second.Item1) && Same(q.First.z, q.Second.Item2)),
                $"{at}: room {i}'s lanterns differ from the sketch's");
        }
    }
}

int depthLists = 0;
foreach (JsonElement expected in fixture.RootElement.GetProperty("depth").EnumerateArray())
{
    int seed = expected.GetProperty("seed").GetInt32();
    double reach = expected.GetProperty("reach").GetDouble();
    List<CastleDepthItem> items = CastleLayout.DepthItems(seed, reach);
    var want = expected.GetProperty("items").EnumerateArray().ToList();
    depthLists++;
    Check(items.Count == want.Count, $"depth seed {seed}: {items.Count} items, the sketch has {want.Count}");
    for (int i = 0; i < Math.Min(items.Count, want.Count); i++)
    {
        var e = want[i].EnumerateArray().ToList();
        CastleDepthItem it = items[i];
        bool room = it.Flight == 0;
        Check(it.Id == e[0].GetInt32() && it.Flight == e[1].GetInt32() && it.Level == e[5].GetInt32()
            && (!room || (it.Kind == KindOf(e[2].GetString()) && it.W == e[3].GetInt32() && it.H == e[4].GetInt32()))
            && Same(it.X, e[6].GetDouble()) && Same(it.Z, e[7].GetDouble()) && Same(it.Rot, e[8].GetDouble())
            && Same(it.DriftA, e[9].GetDouble()) && Same(it.DriftP, e[10].GetDouble()) && Same(it.Spin, e[11].GetDouble()),
            $"depth seed {seed}: item {i} differs from the sketch's {want[i]}");
    }
}

// ---- the castle's rules -------------------------------------------------------------------------------
int ruled = 0;
foreach (int count in new[] { 30, 38, 45 })
    for (int seed = 1; seed <= 200; seed++)
    {
        CastleLayout c = CastleLayout.Generate(seed, count);
        string at = $"seed {seed}, {count} rooms";
        ruled++;
        Check(c.Rooms.Count == count, $"{at}: only {c.Rooms.Count} rooms were placed");

        CastleRoom biwa = c.Biwa;
        Check(biwa.Id == 0 && biwa.Kind == CastleKind.Biwa && biwa.X == CastleLayout.Margin && biwa.W == 9 && biwa.H == 9, $"{at}: the biwa room is not 9 x 9 at the west edge");

        foreach (CastleRoom r in c.Rooms)
        {
            Check(r.X >= CastleLayout.Margin && r.Z >= CastleLayout.Margin && r.X + r.W <= CastleLayout.Size - CastleLayout.Margin
                && r.Z + r.H <= CastleLayout.Size - CastleLayout.Margin, $"{at}: room {r.Id} leaves the map's margin");
            int small = Math.Min(r.W, r.H), large = Math.Max(r.W, r.H);
            Check(small >= 5 && large <= 17 && small <= 13, $"{at}: room {r.Id} is {r.W} x {r.H}, outside 5 x 5 to 17 x 13");
            if (r.Kind == CastleKind.Corridor) Check(small == 5, $"{at}: corridor {r.Id} is not 5 wide");
            Check(CastleLayout.LanternsOf(r).Count >= 1, $"{at}: room {r.Id} has no lantern");
        }

        for (int i = 0; i < c.Rooms.Count; i++)
            for (int j = i + 1; j < c.Rooms.Count; j++)
            {
                CastleRoom a = c.Rooms[i], b = c.Rooms[j];
                bool overlap = a.X < b.X + b.W && b.X < a.X + a.W && a.Z < b.Z + b.H && b.Z < a.Z + a.H;
                Check(!overlap, $"{at}: rooms {a.Id} and {b.Id} overlap");
                bool joined = c.Doorways.Any(d => (d.A == a.Id && d.B == b.Id) || (d.A == b.Id && d.B == a.Id));
                bool near = a.X - CastleLayout.Gap < b.X + b.W && b.X - CastleLayout.Gap < a.X + a.W
                    && a.Z - CastleLayout.Gap < b.Z + b.H && b.Z - CastleLayout.Gap < a.Z + a.H;
                Check(joined || !near, $"{at}: rooms {a.Id} and {b.Id} are closer than {CastleLayout.Gap} cells without a doorway");
            }

        // A tree: every room reachable, one doorway fewer than rooms.
        Check(c.Doorways.Count == c.Rooms.Count - 1, $"{at}: {c.Doorways.Count} doorways for {c.Rooms.Count} rooms");
        Check(c.Dist.All(d => d != CastleLayout.Unreachable), $"{at}: a room no doorway reaches");
        foreach (CastleDoorway d in c.Doorways)
        {
            CastleRoom a = c.Rooms[d.A], b = c.Rooms[d.B];
            var (ax, az) = d.Cells[0];
            var (bx, bz) = d.Cells[1];
            bool corner(CastleRoom r, int x, int z) => (x == r.X || x == r.X + r.W - 1) && (z == r.Z || z == r.Z + r.H - 1);
            Check(a.IsWall(ax, az) && b.IsWall(bx, bz) && !corner(a, ax, az) && !corner(b, bx, bz) && Math.Abs(ax - bx) + Math.Abs(az - bz) == 1,
                $"{at}: doorway {d.Key} is not two side-by-side wall cells");
        }

        List<CastleRoom> arrivals = c.ArrivalRooms(6);
        Check(arrivals.Count == 6 && arrivals.Select(r => r.Id).Distinct().Count() == 6 && arrivals.All(r => r.Kind != CastleKind.Biwa),
            $"{at}: not 6 different arrival rooms");
        if (c.Rooms.Count(r => c.Dist[r.Id] >= 3) >= 6) Check(arrivals.All(r => c.Dist[r.Id] >= 3), $"{at}: an arrival room is under 3 doorways from the biwa room");
    }

Console.WriteLine($"{castles} castles and {depthLists} depth lists checked against the sketch; rules checked on {ruled} castles.");
// ---- Shift: slide distance and doorway changes against the sketch --------------------------------
int shifts = 0;
foreach (JsonElement expected in fixture.RootElement.GetProperty("shifts").EnumerateArray())
{
    int seed = expected.GetProperty("seed").GetInt32(), id = expected.GetProperty("id").GetInt32();
    int dx = expected.GetProperty("dx").GetInt32(), dz = expected.GetProperty("dz").GetInt32();
    string at = $"seed {seed} room {id} slid ({dx},{dz})";
    CastleLayout castle = CastleLayout.Generate(seed, 38);
    int d = castle.SlideDistance(id, dx, dz, 20, out bool blocked);
    Check(d == expected.GetProperty("d").GetInt32() && blocked == expected.GetProperty("blocked").GetBoolean(),
        $"{at}: {d} cells, blocked {blocked}; the sketch has {expected.GetProperty("d")} and {expected.GetProperty("blocked")}");
    CastleLayout after = castle.Moved(id, dx * d, dz * d);
    CastleLayout.DoorChanges(castle, after, out var kept, out var broken, out var made);
    bool SameList(List<CastleDoorway> got, JsonElement want) =>
        got.Count == want.GetArrayLength() && got.Zip(want.EnumerateArray()).All(q =>
        {
            int[] e = Ints(q.Second);
            return q.First.A == e[0] && q.First.B == e[1] && q.First.Cells[0] == (e[2], e[3]) && q.First.Cells[1] == (e[4], e[5]);
        });
    Check(SameList(kept, expected.GetProperty("kept")), $"{at}: the kept doorways differ from the sketch's");
    Check(SameList(broken, expected.GetProperty("broken")), $"{at}: the broken doorways differ from the sketch's");
    Check(SameList(made, expected.GetProperty("made")), $"{at}: the made doorways differ from the sketch's");
    Check(after.Rooms[id].X == castle.Rooms[id].X + dx * d && after.Rooms[id].Z == castle.Rooms[id].Z + dz * d, $"{at}: the room did not move");
    Check(after.Rooms.Count == castle.Rooms.Count && after.Rooms.Where(r => r.Id != id).All(r => r.X == castle.Rooms[r.Id].X && r.Z == castle.Rooms[r.Id].Z),
        $"{at}: another room moved");
    Check(!after.Rooms.Any(a => after.Rooms.Any(b => a.Id < b.Id && CastleLayout.Overlaps(a, b))), $"{at}: rooms overlap after the slide");
    shifts++;
}
Console.WriteLine($"{shifts} shifts checked against the sketch.");

if (failures > 0)
{
    Console.WriteLine($"{failures} checks failed.");
    return 1;
}
Console.WriteLine("Infinity Castle layout checks passed.");
return 0;
