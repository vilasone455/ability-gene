using System;
using RimArt;
using UnityEngine;
using Verse;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;

// The dome holds one size: it arrives with a punch and settles, and never swells from nothing.
float peakRadius = ShinraVfxTiming.ShellRadius(ShinraVfxTiming.ChargeEnd + ShinraVfxTiming.PopIn);
float ringReach = ShinraVfxTiming.Radius * ShinraVfxTiming.RingSpan;
float lastRing = 0f;
for (float time = 0f; time <= ShinraVfxTiming.Duration + 0.1f; time += 0.005f)
{
    float radius = ShinraVfxTiming.ShellRadius(time);
    Check(float.IsFinite(radius) && radius >= 0f && radius <= peakRadius + 0.0001f,
        "The dome must be finite and never exceed its punch");
    Check(time < ShinraVfxTiming.ChargeEnd
        ? radius == 0f
        : radius >= ShinraVfxTiming.Radius * ShinraVfxTiming.PopScale - 0.0001f,
        "The dome must appear at its mid scale rather than growing into it");

    // The outward travel moved off the dome and onto the ground ring, which still only expands.
    float ring = ShinraVfxTiming.RingRadius(time);
    Check(float.IsFinite(ring) && ring >= lastRing - 0.0001f && ring <= ringReach + 0.0001f,
        "Ring travel must be finite, outward only, and bounded");
    lastRing = ring;

    foreach (float alpha in new[] { ShinraVfxTiming.ShellAlpha(time), ShinraVfxTiming.DustAlpha(time),
        ShinraVfxTiming.ReleaseFlash(time), ShinraVfxTiming.GroundRingAlpha(time),
        ShinraVfxTiming.DistortionIntensity(time) })
        Check(float.IsFinite(alpha) && alpha >= 0f && alpha <= 1f, "Opacity must stay valid");

    for (int pulse = 0; pulse < ShinraVfxTiming.ImpactPulses; pulse++)
    {
        float alpha = ShinraVfxTiming.ImpactAlpha(pulse, time);
        Check(float.IsFinite(alpha) && alpha >= 0f && alpha <= 1f, "Impact opacity must stay valid");
        Check(ShinraVfxTiming.ImpactRadius(pulse, time) <= ShinraVfxTiming.Radius + 0.0001f,
            "An impact wave must die against the shell, not pass through it");
    }
}
Check(ShinraVfxTiming.ShellRadius(ShinraVfxTiming.ChargeEnd - 0.01f) == 0f, "No dome before the thrust");
Check(Near(ShinraVfxTiming.ShellRadius(ShinraVfxTiming.ChargeEnd),
    ShinraVfxTiming.Radius * ShinraVfxTiming.PopScale), "The dome arrives at its pop-in scale");
Check(peakRadius > ShinraVfxTiming.Radius, "The release must punch past the dome's held size");
Check(Near(ShinraVfxTiming.ShellRadius(ShinraVfxTiming.ChargeEnd + ShinraVfxTiming.PopIn
    + ShinraVfxTiming.Rebound), ShinraVfxTiming.Radius), "The dome must settle to exactly its held size");
Check(Near(ShinraVfxTiming.ShellRadius(ShinraVfxTiming.Duration), ShinraVfxTiming.Radius),
    "and hold that size for the rest of the effect");
Check(Near(ShinraVfxTiming.RingRadius(ShinraVfxTiming.ExpansionEnd), ringReach),
    "The ground ring must travel its full reach, past the dome");
Check(ShinraVfxTiming.ShellAlpha(0f) == 0f, "No shell before the charge");
Check(ShinraVfxTiming.ShellAlpha(ShinraVfxTiming.PeakTime) > 0.95f, "Frozen peak must show the shell");
Check(ShinraVfxTiming.ShellAlpha(ShinraVfxTiming.ShellEnd) == 0f, "Shell must finish before the dust");
Check(ShinraVfxTiming.DustAlpha(ShinraVfxTiming.ShellEnd) > 0f, "Dust must linger");
Check(ShinraVfxTiming.DustAlpha(ShinraVfxTiming.Duration) == 0f, "Dust must disappear before removal");
Check(ShinraVfxTiming.DustAlpha(ShinraVfxTiming.Duration, 0.23f) == 0f,
    "Delayed dust must also fade completely before the preview is removed");
Check(ShinraVfxTiming.ReleaseFlash(0f) == 0f, "No release flash while the hands draw back");
Check(Near(ShinraVfxTiming.ReleaseFlash(ShinraVfxTiming.ChargeEnd), 1f), "The flash is full at the thrust");
Check(ShinraVfxTiming.ReleaseFlash(ShinraVfxTiming.FlashEnd) == 0f, "The flash must be brief");
Check(ShinraVfxTiming.GroundRingAlpha(0f) == 0f, "No ground ring before the thrust");
Check(ShinraVfxTiming.GroundRingAlpha(ShinraVfxTiming.ChargeEnd + 0.1f) > 0.5f, "The ring leads the dome");
Check(ShinraVfxTiming.GroundRingAlpha(ShinraVfxTiming.ShellEnd) == 0f, "The ring clears before the shell");
Check(ShinraVfxTiming.DistortionIntensity(0f) == 0f, "No warp before the thrust");
Check(ShinraVfxTiming.DistortionIntensity(ShinraVfxTiming.ShellEnd) == 0f,
    "The warp must clear with the shell it belongs to");
// Each pulse is a separate beat: they must not all fire at the release.
Check(ShinraVfxTiming.ImpactAlpha(0, ShinraVfxTiming.ChargeEnd + 0.02f) > 0f, "The first impact fires at the release");
for (int pulse = 1; pulse < ShinraVfxTiming.ImpactPulses; pulse++)
    Check(ShinraVfxTiming.ImpactAlpha(pulse, ShinraVfxTiming.ChargeEnd + 0.02f) == 0f,
        "Later impacts must be staggered, not simultaneous");
Check(ShinraVfxTiming.ImpactAlpha(ShinraVfxTiming.ImpactPulses - 1, ShinraVfxTiming.ShellEnd) == 0f,
    "Every impact must finish inside the shell's life");

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

var casts = new MapComponent_ShinraCasts(map);
var pawn = new Pawn { Map = map };
var animation = new ShinraCastAnimation.Handle { Time = ShinraVfxTiming.ChargeEnd };
casts.Begin(pawn, animation);
casts.MapComponentUpdate();
Check(casts.Running(pawn) && Near(ShinraVfxGraphics.Time, animation.Time),
    "Cast must sample the real animation clock at hand release");
Time.unscaledDeltaTime = 10f;
casts.MapComponentUpdate();
Check(Near(ShinraVfxGraphics.Time, animation.Time), "Paused animation must not drift with wall time");
animation.Time = 0.8f;
casts.MapComponentUpdate();
Check(Near(ShinraVfxGraphics.Time, 0.8f), "Animation speed changes must immediately carry the wave with them");
animation.Time = 1.35f;
animation.Finished = true;
Find.TickManager.TicksGame = 500;
casts.MapComponentUpdate();
Find.TickManager.TicksGame = 530;
casts.MapComponentUpdate();
Check(Near(ShinraVfxGraphics.Time, 1.85f), "After hand recovery, remaining VFX use game time");
Find.TickManager.TicksGame = 750;
casts.MapComponentUpdate();
Check(!casts.Running(pawn), "Finished VFX must release the cast button");

animation = new ShinraCastAnimation.Handle { Time = 0.2f, Valid = false };
casts.Begin(pawn, animation);
calls = ShinraVfxGraphics.Calls;
casts.MapComponentUpdate();
Check(!casts.Running(pawn) && ShinraVfxGraphics.Calls == calls,
    "An interrupted animation must not release a wave later");

var otherPawn = new Pawn { Map = map };
animation = new ShinraCastAnimation.Handle { Time = 0.5f };
casts.Begin(pawn, animation);
casts.Begin(otherPawn, new ShinraCastAnimation.Handle { Time = 0.6f });
pawn.Downed = true;
casts.MapComponentUpdate();
Check(!casts.Running(pawn) && casts.Running(otherPawn), "One cancelled caster must not cancel another");
otherPawn.Spawned = false;
casts.MapComponentUpdate();
Check(!casts.Running(otherPawn), "Leaving the map cancels the cast");
Console.WriteLine("Shinra pawn casts: animation synchronization, interruption, completion and independent casters passed.");
