using System;
using RimArt;
using UnityEngine;
using Verse;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;

float lastRadius = 0f;
for (float time = 0f; time <= ShinraVfxTiming.Duration + 0.1f; time += 0.005f)
{
    float radius = ShinraVfxTiming.ShellRadius(time);
    Check(float.IsFinite(radius) && radius >= lastRadius && radius <= ShinraVfxTiming.Radius,
        "Expansion must be finite, outward only, and bounded");
    lastRadius = radius;
    foreach (float alpha in new[] { ShinraVfxTiming.ShellAlpha(time), ShinraVfxTiming.DustAlpha(time) })
        Check(float.IsFinite(alpha) && alpha >= 0f && alpha <= 1f, "Opacity must stay valid");
}
Check(ShinraVfxTiming.ShellAlpha(0f) == 0f, "No shell before the charge");
Check(ShinraVfxTiming.ShellAlpha(ShinraVfxTiming.PeakTime) > 0.95f, "Frozen peak must show the shell");
Check(ShinraVfxTiming.ShellAlpha(ShinraVfxTiming.ShellEnd) == 0f, "Shell must finish before the dust");
Check(ShinraVfxTiming.DustAlpha(ShinraVfxTiming.ShellEnd) > 0f, "Dust must linger");
Check(ShinraVfxTiming.DustAlpha(ShinraVfxTiming.Duration) == 0f, "Dust must disappear before removal");
Check(ShinraVfxTiming.DustAlpha(ShinraVfxTiming.Duration, 0.23f) == 0f,
    "Delayed dust must also fade completely before the preview is removed");

var map = new Map();
var component = new MapComponent_ShinraVfx(map);
Find.CurrentMap = map;
Time.unscaledDeltaTime = 0.1f;
component.MapComponentUpdate();
Check(ShinraVfxGraphics.Calls == 0, "Idle component must not draw");

component.Preview(new IntVec3(10, 20), 1f);
component.MapComponentUpdate();
Check(Near(ShinraVfxGraphics.Time, 0.1f), "Normal preview uses the frame clock");
Check(Near(ShinraVfxGraphics.Centre.x, 10.5f), "Centre must be cell centred");
component.Preview(new IntVec3(30, 40), 0.25f);
component.MapComponentUpdate();
Check(Near(ShinraVfxGraphics.Time, 0.025f) && Near(ShinraVfxGraphics.Centre.z, 40.5f),
    "A new preview replaces and resets the previous one, at quarter speed");

int calls = ShinraVfxGraphics.Calls;
Find.CurrentMap = new Map();
component.MapComponentUpdate();
Check(ShinraVfxGraphics.Calls == calls, "Other maps must not draw or advance this preview");
Find.CurrentMap = map;
component.MapComponentUpdate();
Check(Near(ShinraVfxGraphics.Time, 0.05f), "Switching back resumes without a time jump");

component.Preview(new IntVec3(10, 20), 0f, true);
for (int i = 0; i < 100; i++) component.MapComponentUpdate();
Check(Near(ShinraVfxGraphics.Time, ShinraVfxTiming.PeakTime), "Frozen peak must not expire");
component.Clear();
calls = ShinraVfxGraphics.Calls;
component.MapComponentUpdate();
Check(ShinraVfxGraphics.Calls == calls, "Clear must remove the frozen preview");

component.Preview(new IntVec3(-1, 0), 1f);
component.MapComponentUpdate();
Check(ShinraVfxGraphics.Calls == calls, "Do not start outside the map");
map.Fogged = true;
component.Preview(new IntVec3(10, 20), 1f);
component.MapComponentUpdate();
Check(ShinraVfxGraphics.Calls == calls, "Do not start inside fog");
map.Fogged = false;
component.Preview(new IntVec3(10, 20), 1f);
Time.unscaledDeltaTime = ShinraVfxTiming.Duration;
component.MapComponentUpdate();
Check(ShinraVfxGraphics.Calls == calls, "Large frame delta must expire cleanly without drawing stale effects");

Time.unscaledDeltaTime = 0.1f;
component.Preview(new IntVec3(10, 20), 1f);
map.Fogged = true;
component.MapComponentUpdate();
map.Fogged = false;
component.MapComponentUpdate();
Check(ShinraVfxGraphics.Calls == calls, "Fogging the centre cancels the preview");
Console.WriteLine("Shinra VFX envelopes and preview lifecycle passed.");
