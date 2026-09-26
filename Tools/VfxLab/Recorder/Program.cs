using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using RimArt;
using RimArt.VfxLab;
using UnityEngine;
using Verse;

// Plays every recordable [RimArtDebug] entry the way the mod's debug window would -- invoke it on the centre
// cell, then call MapComponentUpdate once per 60 fps frame -- and writes what was drawn.
//
//   dotnet run --project Tools/VfxLab/Recorder -- [--out <dir>]

const float Fps = 60f;
const int StillFrames = 30;
const float SafetySeconds = 120f;

string outDir = args.SkipWhile(a => a != "--out").Skip(1).FirstOrDefault() ?? FindOut();
var failures = new List<string>();
var written = new List<object>();

var actions = typeof(SixPathsSlab).Assembly.GetTypes()
    .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
    .Select(m => (method: m, attr: m.GetCustomAttribute<RimArtDebugAttribute>()))
    .Where(a => a.attr != null && a.attr.kind == RimArtDebugKind.Cell)
    .Select(a => (a.method, label: a.attr.FullLabel, kit: Kit.For(a.attr.FullLabel)))
    .Where(a => a.kit != null)
    .OrderBy(a => a.kit.Name).ThenBy(a => a.label)
    .ToList();

foreach (var (method, label, kit) in actions)
{
    var recording = Record(method, label, kit);
    string file = $"{Slug(kit.Name)}/{Slug(label.Substring(kit.Prefix.Length))}.json";
    recording.Write(Path.Combine(outDir, file));
    int calls = recording.frames.Sum(f => f.calls.Count);
    Console.WriteLine($"  {label,-38} {recording.frames.Count,5} frames {recording.Seconds,6:0.00} s {calls,7} draws{(recording.still ? "  (still)" : "")}");
    if (calls == 0) failures.Add($"{label}: recorded no draw calls");
    written.Add(new
    {
        file, label, kit = kit.Name, frames = recording.frames.Count, seconds = recording.Seconds,
        still = recording.still,
    });
    Check(recording, failures);
}

foreach (Kit kit in Kit.All)
    if (!actions.Any(a => a.kit == kit))
        failures.Add($"{kit.Name}: no [RimArtDebug] entry starting with \"{kit.Prefix}\" was found");

Directory.CreateDirectory(outDir);
var index = new
{
    stamp = DateTime.UtcNow.ToString("o"),
    ok = failures.Count == 0,
    failures,
    recordings = written,
};
File.WriteAllText(Path.Combine(outDir, "index.json"), JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }));

if (failures.Count > 0)
{
    Console.Error.WriteLine("Recorder self-checks failed:");
    foreach (string f in failures) Console.Error.WriteLine("  " + f);
    return 1;
}
Console.WriteLine($"Recorded {written.Count} previews into {Path.GetFullPath(outDir)}");
return 0;

static Recording Record(MethodInfo method, string label, Kit kit)
{
    Find.CurrentMap = new Map();
    Tap.BeginFrame();
    Time.unscaledDeltaTime = Time.deltaTime = 1f / Fps;
    method.Invoke(null, null);

    var recording = new Recording { label = label, kit = kit.Name };
    var component = Find.CurrentMap.components.FirstOrDefault(c => kit.Component.IsInstanceOfType(c))
        ?? throw new InvalidOperationException($"{label}: the action did not create {kit.Component.Name}");
    FieldInfo active = Field(kit.Component, "active");
    FieldInfo clock = Field(kit.Component, kit.Clock);
    float? loop = kit.LoopSeconds(label);

    for (int i = 0; i < SafetySeconds * Fps; i++)
    {
        Tap.BeginFrame();
        Time.realtimeSinceStartup = i / Fps;
        Time.frameCount = i;
        foreach (MapComponent c in Find.CurrentMap.components.ToList()) c.MapComponentUpdate();

        bool running = active == null || (bool)active.GetValue(component);
        float? seconds = clock == null ? null : (float)clock.GetValue(component);
        // A preview that switches itself off before drawing leaves an empty last frame; drop it.
        if (!running && Tap.calls.Count == 0) break;
        // A frame is stamped with the time it shows, which is the clock after this update: the
        // previews advance before they draw, so the first frame shows 1/60 s, not 0.
        recording.Capture((i + 1) / Fps, seconds);
        if (!running) break;
        if (loop.HasValue && seconds >= loop.Value) break;

        if (!kit.StartsStill(label) && recording.frames.Count == StillFrames + 1 && recording.frames.All(f => f.hash == recording.frames[0].hash))
        {
            // Frozen previews draw the same frame forever: keep one.
            recording.frames.RemoveRange(1, recording.frames.Count - 1);
            recording.still = true;
            break;
        }
    }

    if (!recording.still)
        foreach (Phase phase in kit.Phases(label))
        {
            Frame at = recording.frames.FirstOrDefault(f => f.clock >= phase.Seconds - 0.0001f);
            if (at != null) recording.phases.Add((phase.Name, at.wall));
        }
    return recording;
}

static FieldInfo Field(Type type, string name) =>
    name == null ? null : type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

// Checks that the recording says what the kit's own timing says. They catch a broken stub, not
// a broken effect: the effects have their own tests under Tests/.
static void Check(Recording recording, List<string> failures)
{
    string label = recording.label;
    var clocked = recording.frames.Where(f => f.clock.HasValue).ToList();

    if (label == "Six Paths: slam")
    {
        Frame shake = recording.frames.FirstOrDefault(f => f.events.Any(e => e.type == "shake"));
        if (shake == null || Math.Abs(shake.clock.Value - SixPathsSlamTiming.LandAt) > 1.01f / Fps)
            failures.Add($"{label}: expected one camera shake at LandAt {SixPathsSlamTiming.LandAt:0.000}s, got {shake?.clock?.ToString("0.000") ?? "none"}");

        // Mid-fall, the block's faces must sit exactly on SixPathsSlab.Project for the pose the
        // timing class gives at that frame's clock.
        float target = SixPathsSlamTiming.FallAt + SixPathsSlamTiming.Fall * 0.5f;
        Frame mid = clocked.OrderBy(f => Math.Abs(f.clock.Value - target)).First();
        SlabPose pose = SixPathsSlamTiming.Slab(mid.clock.Value);
        var corners = new Vector2[SixPathsSlab.CornerCount];
        SixPathsSlab.Project(corners, pose.yaw, pose.height, SixPathsSlamTiming.DrawScale(pose));
        var faces = mid.calls.Where(c => recording.meshes[c.mesh].name.StartsWith("Six Paths block face")
            && Math.Abs(c.call.colour.a - pose.alpha) < 0.0001f).ToList();
        int visible = Enumerable.Range(0, SixPathsSlab.FaceCount).Count(f => SixPathsSlab.Visible(corners, f));
        if (faces.Count != visible)
            failures.Add($"{label}: mid-fall frame drew {faces.Count} block faces, SixPathsSlab says {visible}");
        foreach (var (key, _) in faces)
            foreach (Vector3 v in recording.meshes[key].v)
                if (!corners.Any(c => Math.Abs(c.x - v.x) < 0.001f && Math.Abs(c.y - v.z) < 0.001f))
                {
                    failures.Add($"{label}: a recorded face vertex ({v.x:0.000}, {v.z:0.000}) is not a projected corner");
                    goto doneFaces;
                }
        doneFaces:;
    }

    if (label == "Shinra Tensei: VFX preview")
    {
        Frame release = recording.frames.FirstOrDefault(f => f.events.Any(e => e.def == "AG_ShinraRelease"));
        if (release == null || Math.Abs(release.clock.Value - ShinraVfxTiming.ChargeEnd) > 1.01f / Fps)
            failures.Add($"{label}: expected AG_ShinraRelease at ChargeEnd {ShinraVfxTiming.ChargeEnd:0.000}s, got {release?.clock?.ToString("0.000") ?? "none"}");
    }
}

static string Slug(string text) =>
    string.Join("-", new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));

static string FindOut()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (Directory.Exists(Path.Combine(dir.FullName, "Tools", "VfxLab")))
            return Path.Combine(dir.FullName, "Tools", "VfxLab", "recordings");
    throw new DirectoryNotFoundException("Run from inside the repository, or pass --out.");
}
