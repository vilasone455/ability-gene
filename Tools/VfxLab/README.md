# RimArt VFX Lab

A browser page for watching, scrubbing and comparing power effects without starting RimWorld.

```bash
python3 Tools/VfxLab/lab.py
# open http://localhost:8765/Tools/VfxLab/web/
```

Needs the .NET 8 SDK and Python 3. It works from Windows Chrome while `lab.py` runs in WSL,
because WSL forwards `localhost`. A browser with WebGL2 is required; any current Chrome, Edge or
Firefox has it.

## Two kinds of effect

| | Recorded | Sketch |
|---|---|---|
| Comes from | the mod's own C# under `Source/RimArt`, run outside the game | a JavaScript file in `web/sketches/` |
| Tuned by | editing the C#; `lab.py` re-records within ~15 s and the page reloads it | sliders on the Params tab |
| Can drift from the game | no: nothing is copied | yes: it is a proposal |
| Use it for | checking what ships | trying an idea before writing C# |

Both kinds go through the same renderer, so the Compare tab puts a recorded effect and a sketch
side by side on one clock.

## What a recording is

`Recorder/` is a .NET 8 program that compiles the real drawing, timing and dev-action files, linked
from `Source/RimArt` in `Recorder.csproj`. It compiles them against small stand-ins for
UnityEngine and Verse in `Recorder/Engine/`. For every `[DebugAction("RimArts", ...)]` whose kit is
listed in `Recorder/Catalog.cs`, it does what the dev tool does:

1. Invoke the action on cell 60, 60 of a 120 × 120 map.
2. Call `MapComponentUpdate()` once per frame at 1/60 s.
3. Store every `Graphics.DrawMesh` call: mesh, position, altitude, rotation, scale, colour, texture and shader.
4. Store every `DoShake` and `PlayOneShot` as an event.

Recording stops when the preview switches itself off.
- A preview that draws the same frame 31 times from the start (the "frozen" actions) keeps one frame.
- The looping ring showcase stops after one full cycle of forms (6.64 s on its own clock).

Game values the recorder uses, read from Assembly-CSharp 1.6:

| Value | Source |
|---|---|
| altitude = layer × 0.36585367 | `Verse.Altitudes.LayerSpacing`, with the layer numbers of `AltitudeLayer` |
| camera shake decays 0.5 per second, caps at 0.2, oscillates at 24 Hz | `Verse.CameraShaker` |

The recorder checks itself on each run and fails, leaving the previous recordings in place, if:
- a recording has no draw calls;
- the Six Paths slam's shake is not within one frame of `SixPathsSlamTiming.LandAt`;
- Shinra's `AG_ShinraRelease` is not within one frame of `ShinraVfxTiming.ChargeEnd`;
- the slam's mid-fall block faces are not exactly the corners `SixPathsSlab.Project` gives for that frame.

Recorded now: Six Paths (7 actions), Gravity Well (2), Shinra Tensei (3).

## What the page cannot show

- **Vanilla textures and shaders.** Core art is inside Unity asset bundles, not loose PNGs. These
  paths are drawn from procedural stand-ins, and the Layers tab lists any that are on screen:
  `Things/Mote/PsychicDistortionCurrents`, `PsycastNoise`, `PsycastSkipFlash`,
  `SkipInnerDimension`, `Black`, `Other/ForceField`, and the skyfaller shadows.
  `MoteLargeDistortionWave` is an approximation. The shapes, sizes and timing under a stand-in
  are the kit's own; the pixels are not.
- **Tick-driven kits that need pawns:** Arc, Dispersal, Panoply, Mimic, TimeLattice and ToyCar.
  Their drawing reads pawns, the pawn renderer or game ticks, which the recorder does not stand in for.
- **Melee Animation clips.**
- **Sound.** Sounds appear as events on the timeline only.
- **The game.** The in-game check (build, `validate.py`, ApiChecks, deploy, clean log) is still what
  proves an effect works.

## Using the page

| Action | How |
|---|---|
| Play an effect | click it in the list |
| Put an effect on the right for comparison | shift-click it |
| Play / pause | Space |
| Step one frame (10 frames) | ← → (Shift + ← →) |
| Toggle compare | C |
| Toggle cell grid | G |
| Centre the camera on the effect | F |
| Move the effect | click a cell on the map |
| Pan / zoom | drag / wheel |
| Draw over a real game screenshot | Scene tab, then enter the screenshot's pixels per cell |
| Hide a group of draw calls | Layers tab, untick it |
| Save the sliders as a named preset | Params tab, Presets: name it, Save |
| Send a sketch's settings to someone | Params tab, Copy as a list, or Copy link |
| Write the effect out as PNGs | Export tab |

## Presets

The Params tab remembers each sketch's sliders by itself. A preset is for keeping a set of them
while trying another: name it and press Save, then click its name to load it back. Presets are
stored in this browser, per sketch file.

Export writes one as JSON, which is small enough to paste into a commit message or hand to
someone else; Import reads that file back. A file for another sketch is refused rather than
half-applied, and a preset saved before a slider existed simply leaves that slider alone.

## Exporting frames

The Export tab writes the selected effect out as PNGs. Frames are sampled from the effect's own
clock, not captured from playback, so the same settings give the same frames on any machine, and
a sketch is identical between runs because it is a pure function of time and its params.

| Setting | What it does |
|---|---|
| How many frames | Frames taken across the range, evenly spaced. The end is left open, so a loop does not repeat its first pose. |
| Layout | One image with the frames in a grid, one image with them in a row, or one image with them stacked. |
| From, To | The range in seconds. "Whole effect" resets it; the phase buttons beside it set one phase's span. |
| Pixels per frame | Each frame is square, this many pixels a side. |
| Cells across | How much map a frame covers, which together with the pixel size fixes the zoom. |
| Centre this far north | Frames centre on the effect's cell, pushed north, because height is drawn as a northward offset -- a tall effect sits up the screen. |
| Sheet columns | Grid layout only; 0 lays it out as square as it can. |
| Oldest frame's opacity | Stacked layout only: how faint the first frame is, so the motion reads in order. |
| Effect only, transparent background | Drops the generated terrain, trees and pawn and clears to nothing, leaving the effect alone on transparency. |
| Also write a JSON | The times, the settings and the sketch's params, next to the pictures. |

**Export image** writes one PNG in the chosen layout. **Export PNG sequence** writes the frames
numbered separately instead; the browser asks once to allow several downloads.

The stacked layout is a strobe photo: every frame in one frame-sized picture, the oldest faintest.
It needs transparent frames, since an opaque one would hide the frames under it, so the terrain is
drawn once as a bed and the effect stacked over it -- which happens whether or not "effect only"
is ticked. Stacking a whole effect mostly shows wherever it holds still; stack one phase instead,
with the phase buttons above, to see a movement.

The page's own drawing stops while an export runs, because the exporter is using the drawing
buffer at a different size and reads each frame straight back out of it.

A link can open an exact frame:

```
?effect=Six Paths: slam&t=2.0
?effect=Six Paths: slam&b=Slam v2 (sketch)&t=2.05
?effect=Slam v2 (sketch)&t=3.2&p.exit=orbs&p.seams=true
```

- `cell=x,z` places the effect.
- `ppc=` sets the zoom in pixels per cell.
- `debug` logs draw calls to the console.

Screenshot a frame from the command line, with `lab.py` running. This uses headless Chrome with
software WebGL, so no GPU is needed:

```bash
node Tools/VfxLab/shoot.mjs "Six Paths: slam" 2.0 slam.png --b "Slam v2 (sketch)"
```

## Adding a recorded kit

1. Link the kit's drawing, timing, preview component and DebugActions files in `Recorder/Recorder.csproj`.
2. Add a `Kit` to `Recorder/Catalog.cs`:
   - the label prefix, for example `"Gravity Well:"`;
   - the preview component type;
   - the name of its clock field;
   - its phases, read from the kit's timing class wherever one exists.
3. Run `python3 Tools/VfxLab/lab.py --record-only`. When the build names a Unity or Verse member
   that `Engine/` lacks, add that member to `Engine/` with the game's signature. Never change a mod
   file to suit the lab.

A kit is recordable if its preview draws from `MapComponentUpdate` on a clock it advances itself,
as `MapComponent_ShinraVfx`, `MapComponent_GravityPreview` and `MapComponent_SixPathsPreview` do.

## Adding a sketch

A full guide for someone new to the mod, including textures and what a sketch cannot do, is
`SKETCHING.md`.


1. Create `web/sketches/<name>.js` whose default export is:

   ```js
   {
     kit: 'Six Paths',                       // list heading
     label: 'Slam v2 (sketch)',
     compareWith: 'Six Paths: slam',         // optional: the Compare tab's default partner
     params: { fall: { label: 'Fall', value: 0.22, min: 0.08, max: 1, step: 0.01, group: 'Timing (s)' },
               exit: { label: 'Exit', value: 'fade', options: ['fade', 'sink'], group: 'Exit' },
               seams: { label: 'Seams', value: false, group: 'Faces' } },
     duration(p) { return 4.6; },
     phases(p) { return [{ name: 'Fall', t: 1.68 }]; },
     events(p) { return [{ t: 1.9, type: 'shake', value: 0.18 }]; },
     draw(seconds, p, { origin, scene }) { /* Graphics.DrawMesh(...) */ },
   }
   ```

2. Add the file name to `web/sketches/index.js`.

`draw` must be a pure function of `seconds` and `p`: the page calls it for whatever time you
scrub to.

Draw with `web/js/engine.js`. It uses the same names and argument order as the C#:
`Graphics.DrawMesh(mesh, Matrix4x4.TRS(pos, Quaternion.Euler(0, a, 0), new Vector3(w, 1, d)), material, 0, null, 0, props)`,
`AltitudeLayer.MoteOverhead.AltitudeFor()`, `MeshPool.plane10`, `MaterialPool.MatFrom(path, shader)`.
Vector arithmetic is methods (`a.plus(b)`, `a.times(f)`), because JavaScript has no operator overloading.

Keep one `Mesh` per thing drawn in a frame. The renderer reads meshes after the frame is drawn,
as Unity does, so two draws sharing a rebuilt mesh both show its last shape.

## Sending a sketch's settings

Three buttons at the top of the Params tab copy every parameter the sketch declares, in
declaration order, under the same group headings the panel shows.

| Button | Gives you | For |
|---|---|---|
| Copy as C# constants | `public const float Sink = 0.35f;   // Orbs sink into the floor`, grouped by `// Timing (s)` comments | pasting into a timing class |
| Copy as a list | `Timing (s)` then `  Orbs sink into the floor: 0.35` | sending to a person to read |
| Copy link | the page's URL with `p.<param>=` for every value and the current time | someone opening the exact configuration in their own lab |

The C# lines are named as the C# will name them, with the panel's own label after each one when
it differs. A choice between options comes out as a comment rather than a constant, because what
the C# should do with a switch is a decision. The link writes every parameter out, not only the
ones that differ from the defaults, so it keeps working when a sketch's defaults change.

For a whole configuration as a file rather than as text, use Presets: Export.

## Files

```
lab.py                 record, serve, watch Source/ and re-record
shoot.mjs              screenshot a frame from the command line
Recorder/              the C# recorder (Engine/ = UnityEngine and Verse stand-ins)
recordings/            generated; git-ignored
web/index.html         the page
web/js/gl.js           WebGL2 renderer: altitude order, blend per shader, distortion pass
web/js/engine.js       the sketch drawing API
web/js/player.js       recorded and sketch sources, the clock
web/js/scene.js        generated terrain, trees, rocks, a pawn for scale, sun shadows
web/js/camera.js       pan, zoom, camera shake
web/js/standins.js     procedural textures for vanilla paths
web/js/presets.js      named parameter sets, and their JSON files
web/js/export.js       frame sampling, sprite sheets, downloads
web/js/ui.js           the page controller
web/sketches/          sketches, listed in index.js
```
