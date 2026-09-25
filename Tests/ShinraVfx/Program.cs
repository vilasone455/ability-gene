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

int releases = ShinraSound.Releases;
component.Preview(new IntVec3(10, 20), 0f, true);
for (int i = 0; i < 100; i++) component.MapComponentUpdate();
Check(Near(ShinraVfxGraphics.Time, ShinraVfxTiming.PeakTime), "Frozen peak must not expire");
Check(ShinraSound.Releases == releases, "A preview frozen past the release must stay silent");
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


var controller = new GameComponent_Shinra(Current.Game);
Current.Game.Shinra = controller;
var casts = new MapComponent_ShinraCasts(map);
var pawn = new Pawn { Map = map };
void Tick(int count = 1)
{ for (int i = 0; i < count; i++) { Find.TickManager.TicksGame++; controller.GameComponentTick(); } }
ShinraPawnState Begin(Pawn p)
{ casts.Begin(p, new CastClips.Handle()); return controller.For(p); }
var state = Begin(pawn);
Tick(16);
Check(state.charge.time < ShinraCharge.Hold, "Opening must play before holding");
Tick(1);
Check(Near(state.charge.time, 0.27f), "Hold marker must be exact");
Tick(18000);
Check(state.active && state.charge.Power == 1 && Near(state.charge.time, 0.27f), "Hold indefinitely without drift");
Check(ShinraCombat.Pushes == 0, "Holding must never push");
int booms = ShinraSound.Releases;
calls = ShinraVfxGraphics.Calls;
for (int i = 0; i < 50; i++) casts.MapComponentUpdate();
Check(ShinraVfxGraphics.Calls == calls && ShinraSound.Releases == booms, "No VFX or release sound while held");
Check(Near(state.charge.time, 0.27f), "Drawing while paused cannot advance either clock");
state.Release();
int cooldown = state.cooldownUntil;
Check(cooldown == Find.TickManager.TicksGame + 1200, "Release commits cooldown immediately");
Tick(6);
Check(!state.charge.burst, "Protection starts at burst, not release request");
Find.CurrentMap = new Map();
Tick();
Check(state.charge.burst && state.Protected && ShinraCombat.Pushes == 1, "Burst runs off selected map");
Tick(70);
Check(ShinraCombat.Pushes == 1 && ShinraSound.Releases == booms + 1, "Burst effects exactly once");
Check(!state.active && state.tail >= 0f, "Recovery releases pawn before VFX finish");
Check(!state.Protected && state.cooldownUntil == cooldown, "Defense expires without changing cooldown");
Tick(200);
Check(state.tail < 0f, "VFX tail expires");

var early = Begin(new Pawn { Map = map });
Tick(3);
early.Release();
float power = early.charge.Power;
Tick(30);
Check(early.charge.burst && early.charge.Power == power, "Early release freezes power and passes hold");
var cancelled = Begin(new Pawn { Map = map });
Tick(20);
cancelled.Cancel();
Tick();
Check(!cancelled.active && cancelled.cooldownUntil == 0, "Cancel costs no cooldown");
var interrupted = Begin(new Pawn { Map = map });
Tick(20);
interrupted.Release();
interrupted.animation.Valid = false;
Tick();
Check(!interrupted.active && !interrupted.charge.burst && interrupted.cooldownUntil > Find.TickManager.TicksGame,
    "Interrupted release costs cooldown without a burst");
foreach (string reason in new[] { "stun", "down", "dead", "map", "eye", "move", "job" })
{
    var p = new Pawn { Map = map };
    var c = Begin(p);
    Tick(18);
    switch (reason) {
      case "stun": p.stances.stunner.Stunned = true; break;
      case "down": p.Downed = true; break;
      case "dead": p.Dead = true; break;
      case "map": p.Map = new Map(); break;
      case "eye": p.health.hediffSet.hediffs.Clear(); break;
      case "move": p.Position = new IntVec3(1, 1); break;
      case "job": p.CurJobDef.defName = "Goto"; break;
    }
    Tick();
    Check(!c.active && c.cooldownUntil == 0, "Charging cancellation: " + reason);
}
var automatic = Begin(new Pawn { Map = map });
automatic.autoRelease = true;
ShinraCombat.Threat = true;
Tick(179);
Check(!automatic.charge.releasing, "Auto-release requires full charge");
Tick(2);
Check(automatic.charge.releasing, "Full charge releases on threat");
ShinraCombat.Threat = false;
var fast = Begin(new Pawn { Map = map });
ShinraCastAnimation.Clip.Speed = 2f;
Tick(9);
Check(fast.charge.Held && Near(fast.charge.Power, 9f / 180f), "Animation speed must not change charge rate");
fast.Release();
Tick(4);
Check(fast.charge.burst, "Animation setting advances release twice as quickly");
ShinraCastAnimation.Clip.Speed = 1f;

var saved = Begin(new Pawn { Map = map });
Tick(180);
saved.autoRelease = true;
Scribe.mode = LoadSaveMode.Saving; saved.ExposeData();
var loaded = new ShinraPawnState();
Scribe.mode = LoadSaveMode.LoadingVars; loaded.ExposeData();
Scribe.mode = LoadSaveMode.PostLoadInit; loaded.ExposeData();
Scribe.mode = LoadSaveMode.Inactive;
Check(loaded.active && loaded.charge.Held && loaded.charge.Power == 1 && loaded.autoRelease && loaded.restore,
    "Save/load restores charge, hold, toggle and schedules animation restoration");
saved.restore = true; saved.animation = null;
Tick();
Check(saved.active && saved.animation != null && Near(saved.animation.Time, 0.27f), "Held animation is reclaimed after load");
saved.Release();
Tick(7);
Scribe.mode = LoadSaveMode.Saving; saved.ExposeData();
var loadedRelease = new ShinraPawnState();
Scribe.mode = LoadSaveMode.LoadingVars; loadedRelease.ExposeData();
Scribe.mode = LoadSaveMode.PostLoadInit; loadedRelease.ExposeData();
Scribe.mode = LoadSaveMode.Inactive;
Check(loadedRelease.Protected && loadedRelease.charge.burst && loadedRelease.cooldownUntil == saved.cooldownUntil,
    "Defense and committed cooldown survive save/load");
saved.restore = true;
Tick();
Check(!saved.active && saved.Protected, "Loading interrupts release but preserves the active defense");

for (int ticks = 0; ticks <= 180; ticks++)
{
    var c = new ShinraCharge { ticks = ticks };
    Check(Near(c.Push, 3f + ticks / 45f) && Near(c.CollisionDamage, 8f + ticks / 15f)
        && Near(c.ProjectileLimit, 12f + ticks * 48f / 180f), "Continuous balance interpolation");
}
Console.WriteLine("Shinra charge lifecycle, cancellation, off-map burst, clocks, recovery and persistence passed.");
