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

Console.WriteLine(failures == 0
    ? $"OK: {fields} fields, {swords} swords identical to the sketch's layout"
    : $"{failures} failures over {fields} fields, {swords} swords");
return failures == 0 ? 0 : 1;
