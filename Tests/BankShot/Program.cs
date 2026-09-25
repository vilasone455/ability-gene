using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using RimArt;

// Checks the C# ricochet rule (Source/RimArt/BankShot/BankShotPath.cs) against paths.json, written from the
// sketch's own rule by dump-paths.mjs: same layouts and aims give the same corners, bounces (point, wall cell,
// face normal) and end, to 1e-6 cells.
// Run: dotnet run --project Tests/BankShot

int failures = 0, compared = 0;
void Check(bool condition, string message)
{
    if (condition) return;
    failures++;
    if (failures <= 40) Console.WriteLine("FAIL " + message);
}
static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;

using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "paths.json")));
int maxBounces = fixture.RootElement.GetProperty("maxBounces").GetInt32();
double range = fixture.RootElement.GetProperty("range").GetDouble();

foreach (JsonElement want in fixture.RootElement.GetProperty("paths").EnumerateArray())
{
    string scene = want.GetProperty("scene").GetString();
    double offset = want.GetProperty("offset").GetDouble();
    BankShotPath.Layout layout = scene == "corridor" ? BankShotPath.Corridor : scene == "room" ? BankShotPath.Room : BankShotPath.Corner;
    BankShotPath got = layout.Shot(offset, 0.5, maxBounces, range);
    string at = $"{scene} aim {layout.Aim + offset}";
    compared++;

    var pts = want.GetProperty("pts").EnumerateArray().Select(p => p.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToList();
    Check(got.Points.Count == pts.Count, $"{at}: {got.Points.Count} corners, the sketch has {pts.Count}");
    for (int i = 0; i < Math.Min(got.Points.Count, pts.Count); i++)
        Check(Near(got.Points[i].X, pts[i][0]) && Near(got.Points[i].Z, pts[i][1]) && Near(got.Points[i].D, pts[i][2]),
            $"{at}: corner {i} is {got.Points[i].X:F6},{got.Points[i].Z:F6} at {got.Points[i].D:F6}, the sketch has {pts[i][0]:F6},{pts[i][1]:F6} at {pts[i][2]:F6}");

    var bounces = want.GetProperty("bounces").EnumerateArray().Select(p => p.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToList();
    Check(got.Bounces.Count == bounces.Count, $"{at}: {got.Bounces.Count} bounces, the sketch has {bounces.Count}");
    for (int i = 0; i < Math.Min(got.Bounces.Count, bounces.Count); i++)
    {
        BankShotBounce b = got.Bounces[i];
        double[] e = bounces[i];
        Check(Near(b.X, e[0]) && Near(b.Z, e[1]) && Near(b.D, e[2]) && b.CellX == (int)e[3] && b.CellZ == (int)e[4]
              && b.NormalX == (int)e[5] && b.NormalZ == (int)e[6] && b.N == (int)e[7],
            $"{at}: bounce {i} differs: {b.X:F6},{b.Z:F6} cell {b.CellX},{b.CellZ} normal {b.NormalX},{b.NormalZ}; the sketch has [{string.Join(",", e)}]");
    }

    JsonElement end = want.GetProperty("end");
    string kind = end[0].GetString();
    string gotKind = got.End == BankShotEnd.Hit ? "hit" : got.End == BankShotEnd.Embed ? "embed" : "range";
    Check(gotKind == kind, $"{at}: ends with {gotKind}, the sketch with {kind}");
    Check(Near(got.EndPoint.X, end[1].GetDouble()) && Near(got.EndPoint.Z, end[2].GetDouble()) && Near(got.Length, want.GetProperty("length").GetDouble()),
        $"{at}: ends at {got.EndPoint.X:F6},{got.EndPoint.Z:F6} after {got.Length:F6}, the sketch at {end[1].GetDouble():F6},{end[2].GetDouble():F6} after {want.GetProperty("length").GetDouble():F6}");
}

// The layouts' default aims give what the sketch's header says: corner 1 bounce then the hit, corridor 2, room 3.
Check(BankShotPath.Corner.Shot(0, 0.5, 3, 30) is { End: BankShotEnd.Hit, Bounces.Count: 1 }, "corner: not a hit after 1 bounce");
Check(BankShotPath.Corridor.Shot(0, 0.5, 3, 30) is { End: BankShotEnd.Hit, Bounces.Count: 2 }, "corridor: not a hit after 2 bounces");
Check(BankShotPath.Room.Shot(0, 0.5, 3, 30) is { End: BankShotEnd.Hit, Bounces.Count: 3 }, "room: not a hit after 3 bounces");

Console.WriteLine(failures == 0 ? $"OK: {compared} paths match the sketch" : $"{failures} failures in {compared} paths");
return failures == 0 ? 0 : 1;
