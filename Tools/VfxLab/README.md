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

## Animation clips

The third kind of entry, tagged `clip`. A clip is one of the json files Melee Animation plays: the
mod's own in `Animations/` (written by `make_throw_anim.py`, `make_gravity_anim.py` and
`make_shinra_anim.py`), and Melee Animation's 69 when that mod is installed. They are listed under
"Animations: RimArt" and "Animations: Melee Animation".

- `lab.py` writes the list to `recordings/animations.json` at start and again when a file in
  `Animations/` changes; the open page then fetches the changed clips again. Run the Python script,
  look at the page.
- Melee Animation is read from where it is installed and served under `/_am/`. Nothing of theirs is
  copied here. `lab.py` looks in the usual Steam workshop folders (item 2944488802); set
  `RIMART_MELEE_ANIMATION` to the mod folder if it is elsewhere. Without it, only our clips are
  listed and hands are drawn as soft discs.
- `<Name>North.json` and `<Name>South.json` are joined to `<Name>.json` as one entry. That entry
  has a "Target direction" slider, which picks the clip and the mirror the same way
  `ThrowAnimation.Aim` does and turns the throwing hand by what is left over, as
  `ThrowAimWorker` does. A clip with one file has a "Mirrored" checkbox.
- The timeline marks "Release": the first time a textured part that started visible is switched
  off, which is the thrown item leaving the hand. Clip events show as sound markers.
- Overlays: the path the main hand (`HandA`) takes over the whole clip, with the point it is at now
  and the release point, because a still frame cannot show motion; and the pivot of every part.

### Held weapons

A clip with an `ItemA` or `ItemB` part (67 of Melee Animation's; none of ours yet) has a "Weapon"
group. The dropdown lists our own melee weapons (every `ThingDef` in `1.6/Defs` with `<tools>` and
a single PNG) and the weapons of installed workshop mods that Melee Animation ships tweak data for
and whose texture is a loose PNG. Vanilla and DLC weapons are not listed: their art is inside asset
bundles. The installed-mod scan reads a lot of XML over `/mnt/c`, so `lab.py` runs it once on a
thread (about 90 s) and keeps it in `recordings/weapons-cache.json`; reload the page when it says it
is done, and delete that file to scan again.

The weapon is placed the way Melee Animation places it: by `WeaponTweakData/<def>_<packageId>.json`
(`OffX`, `OffY`, `Rotation`, `ScaleX`, `ScaleY`, `FlipX`, `FlipY`, `HandsMode`), looked for in this
repository first and then in Melee Animation's folder. A weapon with no tweak file sits centred on
the hand, and Melee Animation does not animate such a weapon in game at all. To write one, tick
"Place it with the sliders", move them until the grip is in the hand, and press "Copy tweak JSON";
the first line of what is copied says where to save it. `lab.py` lists the new file within a second;
reload the page to use it. Blade start and end, weapon type and sweep trails are not shown; set those in
Melee Animation's own tweak editor in game.

### Clips inside sketches

`playClip(name, seconds, position, options)` from `web/js/animation.js` draws a clip as a sketch's
caster and returns where the hand and the thrown item are, so a throw and its projectile can be
checked on one timeline. `SKETCHING.md` has the call; `sketches/throw-kunai.js` ("Kunai throw
(sketch)") uses it, and shows the projectile starting at the pawn's centre as `PendingThrow` does,
or at the hand, to see the gap between the two.

`web/js/animation.js` follows Melee Animation's renderer as read from its decompiled
`zAnimationMod.dll`: a part is a 1 x 1 quad drawn with its composed position, y rotation and scale,
world y is draw depth, hands are `Textures/AM/Hand.png` tinted with skin colour. What it cannot
show: the pawn is a stand-in (RimWorld draws the real one), and so is a melee weapon on `ItemA`
(the game takes it from the pawn's equipment). A clip is wrapped as a sketch, so scrubbing, export,
`shoot.mjs` and links work on it unchanged: `shoot.mjs "ThrowKunai (clip)" 0.28 out.png p.aim=200`.

## What a recording is

`Recorder/` is a .NET 8 program that compiles the real drawing, timing and dev-action files, linked
from `Source/RimArt` in `Recorder.csproj`. It compiles them against small stand-ins for
UnityEngine and Verse in `Recorder/Engine/`. For every `[RimArtDebug(...)]` entry of kind `Cell` whose kit is
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
- **Sound as the game mixes it.** Sound markers play (see Sound below), but at one loudness:
  the game also lowers a sound with the camera's distance, and a one-shot does not slow down with
  the lab's 0.5× and slower speeds.
- **The game.** The in-game check (build, `validate.py`, ApiChecks, deploy, clean log) is still what
  proves an effect works.

## Using the page

| Action | How |
|---|---|
| Play an effect | click it in the list |
| Put an effect on the right for comparison | shift-click it |
| Find an effect | press `/` and type; every word must be in the kit, the name or the type (`shadow grasp`, `pole recorded`). Enter plays the first match, Esc clears |
| Show only sketches, recordings or clips | the chips under the filter box; none pressed shows all. The number is how many match the typed words |
| Fold a kit | click its heading; Fold all / Unfold all does every kit. Typed words show their matches inside folded kits too |
| Play / pause | Space |
| Step one frame (10 frames) | ← → (Shift + ← →) |
| Toggle compare | C |
| Pick A and B from short lists | Compare tab: both lists are grouped by kit, and B starts with the kit A is from. Type in "Filter both lists" to narrow them (`sweep` leaves 8 of 193); Esc clears. The effect a side already shows stays in its list |
| Toggle cell grid | G |
| Centre the camera on the effect | F |
| Move the effect | click a cell on the map |
| Pan / zoom | drag / wheel |
| Draw over a real game screenshot | Scene tab, then enter the screenshot's pixels per cell |
| Hide a group of draw calls | Layers tab, untick it |
| Save the sliders as a named preset | Params tab, Presets: name it, Save |
| Send a sketch's settings to someone | Params tab, Copy as a list, or Copy link |
| Write the effect out as PNGs | Export tab |
| Hear the sound markers | the Sound box under the stage, or M |
| Choose what a marker sounds like | Sound tab: click its name |

## Sound

A sound marker is an event `{ t, type: 'sound', def: '<SoundDef>' }`: from a sketch's `events()`, a
recording's `PlayOneShot`, or an animation clip's events. A sustainer the ability's code ends adds
`lasts: <seconds>`, and the lab cuts that marker's sound then with a 0.2 s fade (Gojo's Blue pull,
Purple's travel). The lab plays each one as the clock passes it, using the sound lab's audio (`Tools/SoundLab/web/sound.js`) and its endpoints, which
`lab.py` serves under `/soundlab/` (`Tools/SoundLab/soundlab.py`). The sound lab page itself is at
`/Tools/SoundLab/web/` on the same port. The game's clips come from
`python3 Tools/SoundLab/extract.py`, run once; without it only the mod's own `Sounds/` play.

A marker plays the first of these that exists:

1. the mix open in the Sound tab for that name, while it differs from what is saved;
2. a pick saved for it, `"sound:<SoundDef>"` in `Tools/SoundLab/picks.json`;
3. the mod's SoundDef of that name, else the game's;
4. nothing: the marker is grey on the timeline and tagged "none".

Playback: markers of the effect on the left (A) play, also while comparing. Pausing, seeking and
picking another effect stop what is playing; a layer marked "loop" stops when the effect loops back
to its start. A hidden page (another tab, a minimized window, a closed browser pane) is silent:
some hosts keep drawing a hidden page, and a looping effect would go on playing out of sight. A browser starts no audio before the first click or key on the page, so markers
before that are skipped.

The Sound tab lists the effect's markers by name, with their times and what plays for each. Click a
name to open it:

- **Watch from** plays the effect from 0.6 s before the marker's first time; it plays on to the end and
  loops whole. **Hear it alone** plays the mix without the picture.
- **Options from the sound lab** are the candidate moments in `Tools/SoundLab/candidates.json` of
  the hero whose name the effect's kit starts with (kit "Satō (Ajin)", hero "Satō"), and any moment
  with `"defName": "<SoundDef>"`, which is listed first and open. An option's ▶ puts it in the mix
  and plays the effect as Watch from does; ♪ hears it alone. The option in the mix is highlighted.
- **Every marker keeps what you tried for it.** Opening another marker does not drop the last one's
  mix: it stays "trying" and plays on the timeline, so you can choose a sound for every marker and then
  play the whole effect with all of them. **Pick all N tried** saves them together. Tried mixes live in
  the page only; a reload drops what was not picked.
- **Mix**: one layer per subSound, with pitch, volume, delay, loop and mute. The search box adds a
  folder or clip (`+`) or loads a whole SoundDef (`Use`).
- **Pick** saves the mix as `sound:<SoundDef>` with the note; Claude writes the SoundDef XML from
  it. **Copy SoundDef XML** gives that XML now.

The sound lab page and this tab read and write the same `picks.json`, and each picks up the other's
changes within 3 seconds.

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

The recorder links the picture folder (`<Kit>/`), never `<Kit>/Kit/`, so a preview must not name a rules
type. A preview that reads its balance numbers from the defs reads them from a plain class on the picture
side, as Last Prism does: `LastPrismNumbers` has the XML fields' names, Kit fills its `Of` from the defs at
startup (`LastPrismNumbersFromDefs`), and `Recorder/Program.cs` fills it from the same def XML with
`ModDefs.Fill`.

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
lab.py                 record, serve, watch Source/ and re-record; list animation clips
shoot.mjs              screenshot a frame from the command line
Recorder/              the C# recorder (Engine/ = UnityEngine and Verse stand-ins)
recordings/            generated; git-ignored
web/index.html         the page
web/js/gl.js           WebGL2 renderer: altitude order, blend per shader, distortion pass
web/js/engine.js       the sketch drawing API
web/js/player.js       recorded and sketch sources, the clock
web/js/animation.js    Melee Animation clips: curves, part tree, stand-in pawn, hand path
web/js/scene.js        generated terrain, trees, rocks, a pawn for scale, sun shadows
web/js/camera.js       pan, zoom, camera shake
web/js/standins.js     procedural textures for vanilla paths
web/js/presets.js      named parameter sets, and their JSON files
web/js/export.js       frame sampling, sprite sheets, downloads
web/js/ui.js           the page controller
web/js/sounds.js       sound markers and the Sound tab (audio from Tools/SoundLab/web/sound.js)
web/sketches/          sketches, listed in index.js
```
