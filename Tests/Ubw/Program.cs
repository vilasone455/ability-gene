using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RimArt;

// Checks the C# layout of Unlimited Blade Works' standing field (Source/RimArt/Trace/UbwField.cs and
// UbwBlade.cs) against fields.json, written from the sketches' own layout by dump-field.mjs: the same
// settings and landing spots give the same swords, in the same order, with the same poses and cuts, in
// game and in the lab. Run: dotnet run --project Tests/Ubw

int failures = 0;
void Check(bool condition, string message)
{
    if (condition) return;
    failures++;
    if (failures <= 40) Console.WriteLine("FAIL " + message);
}
static double[] Doubles(JsonElement e) => e.EnumerateArray().Select(x => x.GetDouble()).ToArray();
static bool Near(double a, double b, double tolerance = 1e-9) => Math.Abs(a - b) <= tolerance * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
static bool NearAll(double[] a, params double[] b) => a.Length == b.Length && a.Zip(b, (x, y) => Near(x, y)).All(x => x);

using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fields.json")));

int fields = 0, swords = 0;
foreach (JsonElement expected in fixture.RootElement.GetProperty("fields").EnumerateArray())
{
    JsonElement s = expected.GetProperty("settings");
    var settings = new UbwFieldSettings(s.GetProperty("density").GetDouble(), s.GetProperty("hill").GetDouble(), s.GetProperty("beyond").GetDouble(),
        s.GetProperty("size").GetDouble(), s.GetProperty("lean").GetDouble());
    List<UbwXZ> keep = expected.GetProperty("keep").EnumerateArray().Select(k => new UbwXZ(k.GetProperty("x").GetDouble(), k.GetProperty("z").GetDouble())).ToList();
    string at = $"density {settings.Density}, hill {settings.Hill}, beyond {settings.Beyond}, {keep.Count} kept";
    List<UbwSword> mine = UbwField.Make(settings, keep, UbwWeapons.Lab);
    var want = expected.GetProperty("swords").EnumerateArray().ToList();
    fields++;
    Check(mine.Count == want.Count, $"{at}: {mine.Count} swords, the sketch has {want.Count}");
    for (int i = 0; i < Math.Min(mine.Count, want.Count); i++)
    {
        UbwSword sw = mine[i];
        JsonElement e = want[i];
        swords++;
        string where = $"{at}: sword {i} (seed {sw.Seed})";
        Check(sw.Seed == e.GetProperty("seed").GetInt32(), $"{where}: the sketch has seed {e.GetProperty("seed").GetInt32()}");
        Check(sw.W.Name == e.GetProperty("name").GetString(), $"{where}: {sw.W.Name}, the sketch has {e.GetProperty("name").GetString()}");
        Check(sw.Far == e.GetProperty("far").GetBoolean(), $"{where}: far {sw.Far}, the sketch has {e.GetProperty("far").GetBoolean()}");
        Check(Near(sw.X, e.GetProperty("x").GetDouble()) && Near(sw.Z, e.GetProperty("z").GetDouble()) && Near(sw.D, e.GetProperty("d").GetDouble()),
            $"{where}: at {sw.X}, {sw.Z}, the sketch has {e.GetProperty("x").GetDouble()}, {e.GetProperty("z").GetDouble()}");
        Check(Near(sw.Lean, e.GetProperty("lean").GetDouble()) && Near(sw.Dir, e.GetProperty("dir").GetDouble()) && Near(sw.Turn, e.GetProperty("turn").GetDouble())
            && Near(sw.Sink, e.GetProperty("sink").GetDouble()) && Near(sw.Size, e.GetProperty("size").GetDouble()),
            $"{where}: lean {sw.Lean} dir {sw.Dir} turn {sw.Turn} sink {sw.Sink} size {sw.Size} differ from the sketch's");
        UbwPose b = sw.Pose;
        Check(NearAll(Doubles(e.GetProperty("tip")), b.Tip.X, b.Tip.Y, b.Tip.Z) && NearAll(Doubles(e.GetProperty("A")), b.A.X, b.A.Y, b.A.Z)
            && NearAll(Doubles(e.GetProperty("B")), b.B.X, b.B.Y, b.B.Z) && NearAll(Doubles(e.GetProperty("N")), b.N.X, b.N.Y, b.N.Z)
            && Near(b.L, e.GetProperty("L").GetDouble()) && Near(sw.Top, e.GetProperty("top").GetDouble()),
            $"{where}: the pose differs from the sketch's");
        UbwCut c = sw.Cut;
        Check(NearAll(Doubles(e.GetProperty("cut")), c.X, c.Z, c.Half, c.D.X, c.D.Z, c.F.X, c.F.Z), $"{where}: the cut differs from the sketch's");
    }
}

// ---- the world v2's ground: plates, heights, the ridge, the field standing on it -------------------------
using JsonDocument grounds = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "terrains.json")));
int terrains = 0, plates = 0;
foreach (JsonElement expected in grounds.RootElement.GetProperty("terrains").EnumerateArray())
{
    JsonElement r = expected.GetProperty("rules");
    UbwGround rules = UbwGround.Default;
    rules.Plate = r.GetProperty("plate").GetDouble(); rules.HillPlate = r.GetProperty("hillPlate").GetDouble(); rules.Outer = r.GetProperty("outer").GetDouble();
    rules.Drop = r.GetProperty("drop").GetDouble(); rules.Gap = r.GetProperty("gap").GetDouble(); rules.TierStep = r.GetProperty("tierStep").GetDouble();
    rules.Tiers = r.GetProperty("tiers").GetInt32(); rules.SouthTiers = r.GetProperty("southTiers").GetInt32(); rules.Jitter = r.GetProperty("jitter").GetDouble();
    rules.RidgeBand = r.GetProperty("ridgeBand").GetDouble(); rules.RidgeMax = r.GetProperty("ridgeMax").GetDouble(); rules.HillStep = r.GetProperty("hillStep").GetDouble();
    rules.PlateStep = r.GetProperty("plateStep").GetDouble(); rules.HillLevels = r.GetProperty("hillLevels").GetInt32();
    rules.Hill = r.GetProperty("hill").GetDouble(); rules.Beyond = r.GetProperty("beyond").GetDouble();
    int seed = expected.GetProperty("seed").GetInt32();
    string at = $"terrain seed {seed}, plate {rules.Plate}, tiers {rules.Tiers}/{rules.SouthTiers}";
    UbwTerrain T = UbwTerrain.Make(rules, seed);
    terrains++;

    var seeds = expected.GetProperty("seeds").EnumerateArray().Select(Doubles).ToList();
    Check(T.Seeds.Count == seeds.Count, $"{at}: {T.Seeds.Count} seeds, the sketch has {seeds.Count}");
    for (int i = 0; i < Math.Min(T.Seeds.Count, seeds.Count); i++)
        Check(NearAll(seeds[i], T.Seeds[i].X, T.Seeds[i].Z, T.Seeds[i].Sp), $"{at}: seed {i} differs from the sketch's");
    var want = expected.GetProperty("plates").EnumerateArray().ToList();
    Check(T.Plates.Length == want.Count, $"{at}: {T.Plates.Length} plates, the sketch has {want.Count}");
    for (int i = 0; i < Math.Min(T.Plates.Length, want.Count); i++)
    {
        UbwPlate p = T.Plates[i];
        JsonElement e = want[i];
        bool kept = e.ValueKind != JsonValueKind.Null;
        Check((p != null) == kept, $"{at}: plate {i} is {(p == null ? "dropped" : "kept")}, the sketch {(kept ? "keeps" : "drops")} it");
        if (p == null || !kept) continue;
        plates++;
        var poly = e.GetProperty("poly").EnumerateArray().Select(Doubles).ToList();
        bool same = p.Poly.Count == poly.Count;
        for (int k = 0; same && k < poly.Count; k++)
            same = Near(p.Poly[k].X, poly[k][0]) && Near(p.Poly[k].Z, poly[k][1]) && p.Poly[k].Nb == (int)poly[k][2] && Near(p.Poly[k].G, poly[k][3]);
        Check(same, $"{at}: plate {i} has {p.Poly.Count} corners, the sketch's differ");
        Check(p.Sgn == e.GetProperty("sgn").GetInt32() && p.Tier == e.GetProperty("tier").GetInt32() && p.Shade == e.GetProperty("shade").GetInt32()
            && Near(p.H, e.GetProperty("h").GetDouble()) && Near(p.MinX, e.GetProperty("minX").GetDouble()) && Near(p.MaxX, e.GetProperty("maxX").GetDouble())
            && Near(p.MinZ, e.GetProperty("minZ").GetDouble()) && Near(p.MaxZ, e.GetProperty("maxZ").GetDouble()),
            $"{at}: plate {i} height {p.H} tier {p.Tier} shade {p.Shade}, the sketch has {e.GetProperty("h").GetDouble()} {e.GetProperty("tier").GetInt32()} {e.GetProperty("shade").GetInt32()}");
    }
    Check(Near(T.Bottom, expected.GetProperty("bottom").GetDouble()) && Near(T.Reach, expected.GetProperty("reach").GetDouble()) && Near(T.EdgeAt, expected.GetProperty("edge").GetDouble()),
        $"{at}: bottom, reach or edge differ from the sketch's");
    JsonElement ridge = expected.GetProperty("ridge");
    double[] zs = T.RidgeSamples().Select(z => (double)z).ToArray();
    double[] wantZs = Doubles(ridge.GetProperty("zs"));
    Check(Near(T.RidgeX0, ridge.GetProperty("x0").GetDouble()) && zs.Length == wantZs.Length && zs.Zip(wantZs, (a, b) => Near(a, b)).All(x => x),
        $"{at}: the ridge line differs from the sketch's ({zs.Length} samples, the sketch has {wantZs.Length})");
    double[] heights = Doubles(expected.GetProperty("heights"));
    for (int k = 0; k < heights.Length; k++)
        Check(Near(T.HeightAt(-40 + k * 2, 7 - k * .35), heights[k]), $"{at}: heightAt sample {k} differs from the sketch's");
    List<UbwXZ> keep = expected.GetProperty("keep").EnumerateArray().Select(k => new UbwXZ(k.GetProperty("x").GetDouble(), k.GetProperty("z").GetDouble())).ToList();
    var settings = new UbwFieldSettings(UbwField.Look.Density, rules.Hill, rules.Beyond, UbwField.Look.Size, UbwField.Look.Lean);
    List<UbwSword> lifted = UbwField.Make(settings, keep, UbwWeapons.Lab, T.HeightAt);
    var wantSwords = expected.GetProperty("swords").EnumerateArray().Select(Doubles).ToList();
    Check(lifted.Count == wantSwords.Count, $"{at}: {lifted.Count} swords on the ground, the sketch has {wantSwords.Count}");
    for (int i = 0; i < Math.Min(lifted.Count, wantSwords.Count); i++)
        Check(lifted[i].Seed == (int)wantSwords[i][0] && Near(lifted[i].X, wantSwords[i][1]) && Near(lifted[i].Z, wantSwords[i][2]) && Near(lifted[i].Lift, wantSwords[i][3]),
            $"{at}: sword {i} (seed {lifted[i].Seed}) differs from the sketch's (seed {(int)wantSwords[i][0]}, lift {wantSwords[i][3]})");
}

Console.WriteLine(failures == 0
    ? $"OK: {fields} fields, {swords} swords identical to the sketch's layout; {terrains} terrains, {plates} plates identical to the sketch's ground"
    : $"{failures} failures over {fields} fields, {swords} swords, {terrains} terrains, {plates} plates");
return failures == 0 ? 0 : 1;
