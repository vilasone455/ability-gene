# Writing a VFX sketch

A guide for someone writing effect sketches for the RimArt VFX lab who has not worked on the mod.
The general lab manual is `README.md`, in the same folder.

A sketch is one JavaScript file that draws an effect for any time you ask it for. The lab shows
it with sliders, so an idea can be tried and tuned without starting RimWorld. A sketch is a
proposal. Nothing in it reaches the game until someone rewrites it in C#, so it is written with
the same calls the C# uses, which makes that rewrite close to line by line.

## 1. Running the lab

```bash
python3 Tools/VfxLab/lab.py
# open http://localhost:8765/Tools/VfxLab/web/
```

- Needs Python 3, the .NET 8 SDK and a browser with WebGL2 (any current Chrome, Edge or Firefox).
- The .NET SDK is needed even for sketches. `lab.py` records the game effects first, and the page
  lists no sketches until recordings exist.
- The page does not reload a sketch when you save it. Save the file, then reload the page
  (Ctrl+R). Slider values survive the reload.
- If the page says "The lab could not start" and names a file in `sketches/`, that file failed to
  load: it is listed in `index.js` but does not exist, or it has a syntax error. One bad sketch
  stops the whole page, not just that sketch.

## 2. Adding a sketch

1. Create `Tools/VfxLab/web/sketches/<kit>-<idea>.js`, for example `six-paths-laser.js`.
2. Add the file name to the list in `Tools/VfxLab/web/sketches/index.js`.
3. Reload the page. The sketch appears in the list under its `kit` heading.

Read `sketches/six-paths-rods.js` before writing your own. It is about 350 lines and uses most of
what this guide covers.

### Starting file

This file draws one glowing disc that grows over 1 second and fades over the next 0.5 s. It loads
and draws as written.

```js
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf, Matrix4x4,
  MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const props = new MaterialPropertyBlock();
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

export default {
  kit: 'Six Paths',                 // heading in the list
  label: 'Pulse (sketch)',          // must be unique; end it with "(sketch)"
  params: {
    grow: P('Grow', 1.0, 0.1, 3, 0.05, 'Timing (s)'),
    fade: P('Fade', 0.5, 0.1, 3, 0.05, 'Timing (s)'),
    size: P('Size (cells)', 3, 0.5, 8, 0.1, 'Shape'),
  },

  duration(p) { return p.grow + p.fade; },
  phases(p) { return [{ name: 'Grow', t: 0 }, { name: 'Fade', t: p.grow }]; },
  events(p) { return [{ t: p.grow, type: 'shake', value: 0.05 }]; },

  draw(seconds, p, { origin }) {
    const grown = Mathf.Smooth(seconds / p.grow);
    const alpha = 1 - Mathf.Smooth((seconds - p.grow) / p.fade);
    const size = p.size * grown;
    props.SetColor(ShaderPropertyIDs.Color, new Color(0.6, 0.4, 1.0, alpha));
    Graphics.DrawMesh(MeshPool.plane10,
      Matrix4x4.TRS(origin.WithY(AltitudeLayer.MoteOverhead.AltitudeFor()),
        Quaternion.Euler(0, 0, 0), new Vector3(size, 1, size)),
      glow, 0, null, 0, props);
  },
};
```

### The parts of the export

| Field | Required | What it is |
|---|---|---|
| `kit` | yes | List heading. Use an existing kit name (`Six Paths`, `Shinra Tensei`, `Gravity Well`) when the sketch belongs to one. |
| `label` | yes | Name in the list and in links. Must not match any other effect's label. |
| `params` | yes | The sliders. `{}` if there are none. |
| `duration(p)` | yes | Length in seconds. |
| `phases(p)` | yes | Named marks on the timeline, `[{ name, t }]`. `[]` if there are none. |
| `events(p)` | no | Timeline events: `{ t, type: 'shake', value }` shakes the camera; `{ t, type: 'sound', def: 'AG_Something' }` puts a marker on the timeline and plays nothing. |
| `draw(seconds, p, ctx)` | yes | Draws the frame at `seconds`. |
| `compareWith` | no | Label of a recorded effect to show beside this one on the Compare tab. |

### Params

Each param becomes one control in the Params tab. `p.<name>` holds its current value.

| Kind | How to declare it | Value in `p` |
|---|---|---|
| Slider | `{ label, value, min, max, step, group }` | number |
| Dropdown | `{ label, value: 'fade', options: ['fade', 'sink'], group }` | string |
| Checkbox | `{ label, value: false, group }` | boolean |

- `group` is the heading the control sits under. Put times in seconds under `'Timing (s)'`.
- Put the unit in the label: `'Ring radius (cells)'`, `'Spin (degrees)'`.
- Every number someone might want changed should be a param. The "Copy as C# constants" button
  turns them into C# lines later.
- Renaming a param key resets that slider for anyone who had moved it.

## 3. What a sketch can do

### Coordinates

The game camera looks straight down at the map.

- `x` runs east, `z` runs north. One unit is one map cell.
- `y` is not height. It is altitude, which only decides what draws on top of what (see below).
- `ctx.origin` is the centre of the cell the effect is placed on. Clicking a cell on the map moves it.
- Height above the ground is drawn by moving a thing north. Six Paths uses 0.60 cells north per
  cell of height (`Lift = 0.60` in the rods sketch). A shadow stays on the ground position.
- `ctx.scene.shadowVector` is `{ x, z }`: where a shadow of something 1 cell tall ends, relative to
  its base. The Scene tab sets the sun. This is a lab setting, not something the game supplies.

### Drawing: `Graphics.DrawMesh`

Every visible thing is one call:

```js
props.SetColor(ShaderPropertyIDs.Color, colour);
Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.Euler(0, angle, 0), new Vector3(width, 1, depth)),
  material, 0, null, 0, props);
```

- `position`: `new Vector3(x, altitude, z)`.
- `angle`: degrees. A positive angle turns the mesh clockwise as seen on screen.
- `width` and `depth`: size in cells along the mesh's own x and z. The middle `1` is ignored.
- `colour`: `new Color(r, g, b, a)`, each 0 to 1. It multiplies the texture, so a white texture
  takes the colour exactly. `a` is opacity.
- Only the Y angle of the rotation is used. `Quaternion.Euler(30, 0, 0)` draws the same as `(0, 0, 0)`.

### Altitude: what draws on top

Calls are sorted by altitude, lowest first. Calls at the same altitude draw in the order they
were made. Take altitudes from the game's layers:

```js
AltitudeLayer.Filth.AltitudeFor()           // on the floor: marks, holes, scorch
AltitudeLayer.Shadows.AltitudeFor()         // shadows
AltitudeLayer.MoteOverheadLow.AltitudeFor() // effects behind a pawn
AltitudeLayer.MoteOverhead.AltitudeFor()    // effects in front of a pawn
```

Adjacent layers are 0.366 apart. To order several parts inside one layer, add small steps such
as `+ 0.004`, and keep the total below 0.3 so a part does not move into the next layer.

### Meshes

| Mesh | What it is |
|---|---|
| `MeshPool.plane10` | 1 × 1 square centred on its position, with texture coordinates. Scale it for sprites, flashes, puffs, shadows. |
| `Meshes.disc(segments, name)` | Filled circle, radius 1. No texture coordinates: use the `white` texture. |
| `Meshes.band(inner, outer, segments, name)` | Ring from radius `inner` to `outer`. No texture coordinates: use the `white` texture. |
| `new Mesh(name)` | Your own shape. |

Building your own shape:

```js
const m = new Mesh('beam');
m.setFlat([x0, z0, x1, z1, x2, z2, x3, z3],   // corners as x, z pairs
          [0, 1, 2, 0, 2, 3]);                 // triangles, three corner indices each
m.uv = new Float32Array([0, 0, 0, 1, 1, 1, 1, 0]); // only if a texture is used; one u, v pair per corner
```

Rules for meshes:

- Create a mesh once, outside `draw`, or once per key in a `Map` as the rods sketch does with
  `mesh(key)`. A new mesh on every frame is uploaded to the GPU again on every frame and slows
  the page down.
- One mesh per thing drawn in a frame. The renderer reads meshes after `draw` returns, so if two
  draws share a mesh that was rebuilt between them, both show the last shape.
- A mesh without `uv` samples a single texel. With the `white` texture that is fine. With a soft
  texture such as `SoftDisc` that texel is in a transparent corner, and the shape does not appear
  at all.

### Materials and blend modes

```js
const mat = MaterialPool.MatFrom('<texture path>', ShaderDatabase.Transparent);
```

Create materials once, at the top of the file.

| Shader | How it draws | Use for |
|---|---|---|
| `ShaderDatabase.Transparent` | Normal see-through blending | Dark shapes, dust, shadows, anything that is not light |
| `ShaderDatabase.Mote` | Same as Transparent in the lab | |
| `ShaderDatabase.MoteGlow` | Additive: adds its colour to what is under it and never darkens | Glows, flashes, energy |
| `ShaderDatabase.Cutout` | A pixel is either drawn fully or not at all, at 50 % alpha | Hard-edged sprites |
| `InvertShader` (from `engine.js`) | Negative of what is under it, by alpha: draw with colour `(a, a, a, a)`. Not in ShaderDatabase: in C# it is `Hidden/Internal-Colored` with `_SrcBlend` OneMinusDstColor and `_DstBlend` OneMinusSrcAlpha | Screen-negative flashes |

### Random numbers

`draw` is called for whatever time is on the timeline, in any order, and export relies on the same
time giving the same picture. So:

- Do not use `Math.random()`. Use `hash` from `../js/standins.js`:
  `hash(i, k, seed)` gives a fixed number from 0 to 1 for integers `i`, `k` and `seed`. For
  example `hash(chunkIndex, 3, 6006)` is always the same value for that chunk.
- Do not store anything between frames: no counters, no particle lists that are updated each frame.
  Work out every position from `seconds`. A thrown particle is `height = rise * t - 0.5 * gravity * t * t`,
  not a velocity added each frame.

### Helpers in `engine.js`

- `Mathf.Clamp01`, `Mathf.Lerp`, `Mathf.Smooth` (0 to 1 ease in and out), `Mathf.Repeat`, `Mathf.Deg2Rad`.
- `Vector3`: `plus`, `minus`, `times`, `WithY`, `magnitude`, `normalized`. JavaScript has no
  operator overloading, so the C# `a + b * 2` is `a.plus(b.times(2))`.
- `Color.Lerp(a, b, t)`, `colour.withAlpha(a)`.

### A caster that plays a real animation clip

A sketch can draw one of the Melee Animation clips (`Animations/*.json`, or that mod's own) as its
caster, so the pawn's motion and the effect run on one clock. `sketches/throw-kunai.js` is the
worked example.

```js
import { playClip } from '../js/animation.js';

const clip = playClip('RimArt_ThrowKunai', seconds - lead, casterPosition, { aim: p.aim, scene });
if (!clip) return;                       // still loading: nothing was drawn
if (seconds - lead >= clip.release) { /* the item has left the hand: start the projectile */ }
```

- The name is the json file's name without `.json`. `position` is `{ x, z }` in cells.
- `aim` is degrees to the target (0 east, 90 north). It picks the East, North or South clip and
  the west mirror as `ThrowAnimation.Aim` does, and turns the throwing hand by what is left, as
  `ThrowAimWorker` does. Leave it out and the East clip plays; `mirror: true` faces it west.
- `seconds` is clamped to the clip, so before 0 the pawn holds the first pose and after the end
  the last. Shift it (`seconds - lead`) to start the clip late.
- It returns `{ length, release, facing, mirror, hand, item, itemAtRelease }`. `hand` and `item`
  are `{ x, z, rot, held }` in world cells for the main hand and the thrown item at this time;
  `itemAtRelease` is the item one frame before it is switched off. `release` is null for a clip
  that throws nothing.
- The pawn is the clip player's stand-in, not the game's pawn. See "Animation clips" in `README.md`.
- In game the thrown projectile starts at the pawn's `DrawPos`, not at the hand
  (`PendingThrow.cs`), so `itemAtRelease` is for judging that gap, not the real launch point.

## 4. What a sketch cannot do

The lab only offers what the game's drawing calls can do, so that a sketch can be ported. Anything
outside this list needs a different plan, not a workaround in JavaScript.

| Cannot | Why, and what to do instead |
|---|---|
| Custom GLSL or shaders | The mod uses RimWorld's built-in shaders only. Use the four in the table above. |
| True 3D, perspective, lighting | The game camera is flat and straight down. Fake height by moving things north; fake shading with darker and lighter shapes. |
| Tilt a sprite on x or z | Only the Y angle is used. Draw the tilted shape as your own mesh. |
| Per-corner colours or gradients inside a mesh | One colour per draw call. Use a texture with the gradient in its alpha, or stack several draws. |
| Tiling or scrolling a texture | The lab clamps textures at their edges and has no texture offset. Use several draws, or move the mesh. |
| `Math.random()`, stored state, timers | See "Random numbers". The frame must depend only on `seconds` and `p`. |
| DOM, `fetch`, canvas drawing inside `draw` | `draw` only makes `Graphics.DrawMesh` calls. |
| Read pawns, buildings, terrain or the tick | The scene is a generated picture with no game data behind it. |
| Vanilla textures (`Things/...`, `Other/...`) | Vanilla art is inside Unity asset bundles. The lab draws a rough stand-in and lists it on the Layers tab; the real pixels will differ. |
| Distortion (`MoteLargeDistortionWave`) | Approximated only. Do not tune fine details of a distortion in the lab. |
| Sound | Sound events show on the timeline and play nothing. |
| Prove that it works in game | Only building the mod and checking it in RimWorld does that. |

Also keep effects on the ground: new effects in this mod rise out of the floor, and designs that
drop things from the sky have been rejected.

## 5. Textures

A texture is a PNG. It controls the shape and softness of a draw. The draw call's colour controls
the hue.

### How the lab finds a texture

The path given to `MaterialPool.MatFrom` decides where it comes from:

| Path | Comes from |
|---|---|
| `white` | Solid white. For flat shapes. |
| `lab/soft-disc` | Round, soft-edged white spot, made in the browser. |
| `lab/puff` | Round white spot with a noisy edge, for dust and smoke. |
| `lab/<anything else>` | A texture you generate in your sketch file (below). |
| `RimArt/<Kit>/<Name>` | `Textures/RimArt/<Kit>/<Name>.png` in the repository. Same path the C# uses, without `.png`. |
| `Things/...`, `Other/...` | Vanilla art; always a stand-in. |
| Anything not found | Magenta and dark purple checkerboard. If you see this, the path is wrong or the PNG is missing. |

The mod already ships two soft textures that can be used as they are:

- `RimArt/SixPaths/SoftDisc`: white, alpha falls from 1 at the centre to 0 at the edge.
- `RimArt/SixPaths/Puff`: white, the same falloff broken up by noise.

### Rules for a texture

- Draw it white (RGB 255, 255, 255) and put the shape in the alpha channel. Then any colour can be
  applied with `SetColor`, and one texture serves many colours. Only use colour in the PNG when a
  sprite genuinely needs several colours at once.
- Square, power of two: 64, 128, 256 or 512 pixels. 128 is enough for most soft shapes.
- Leave the outermost pixels fully transparent, or the edge of the quad shows as a hard line.
- The top of the image points north on the map.
- `MeshPool.plane10` shows the whole image across its 1 × 1 cell square before scaling.

### Way 1: generate it in the sketch (for trying shapes)

Register a generator at the top of the sketch file, then use `lab/<name>` as the path. `pixels`
calls your function once per pixel with `u` and `v` from 0 to 1 (`v = 0` is the top row) and takes
back `[r, g, b, a]`, each 0 to 1.

```js
import { registerLabTexture, pixels, fbm } from '../js/standins.js';

// A thin soft ring: alpha 1 at radius 0.8 of the image, 0 at 0.12 either side of it.
registerLabTexture('lab/thin-ring', () => pixels(128, (u, v) => {
  const r = Math.hypot(u - 0.5, v - 0.5) * 2;
  return [1, 1, 1, Math.max(0, 1 - Math.abs(r - 0.8) / 0.12)];
}));

const ringGlow = MaterialPool.MatFrom('lab/thin-ring', ShaderDatabase.MoteGlow);
```

- `fbm(x, y, seed, octaves, period)` from the same file gives smooth noise from 0 to 1 for rough
  edges. `lab/puff` uses `fbm(u * 4, v * 4, 71, 3, 4)`.
- The page builds each texture once. After changing a generator, reload the page.
- A `lab/` texture exists only in the lab. The game cannot load it. Once the shape is right, go to
  Way 2.

### Way 2: a PNG in `Textures/` (what the game uses)

Put the file at `Textures/RimArt/<Kit>/<Name>.png` and use `RimArt/<Kit>/<Name>` as the path in
the sketch. The lab loads it on the next page reload, and the C# port uses the same path string:

```cs
MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
```

Mod textures are made by Python scripts in the repository root, not painted by hand, so they can be
regenerated and adjusted by changing a number. `make_six_paths_textures.py` is the example to copy:

- It uses Pillow (`pip install Pillow`).
- It contains the same `hash`, `noise` and `fbm` as `standins.js`, so a formula tuned in Way 1 gives
  the same pixels when copied into Python.
- It writes white pixels with the formula as alpha: `(255, 255, 255, int(a * 255))`.
- Note that Python's image rows count from the top, the same as `v` in `pixels`, so a formula can
  be copied without flipping.

Steps to turn a `lab/` texture into a game texture:

1. Copy the generator formula into a `make_<kit>_textures.py` script (or add it to the existing
   one for that kit) and run it: `python3 make_<kit>_textures.py`.
2. In the sketch, change the path from `lab/<name>` to `RimArt/<Kit>/<Name>`.
3. Reload the page and check the effect looks the same as before.
4. If the checkerboard appears, the path or file name does not match. Paths are case-sensitive.

A hand-painted PNG also works in the lab. Mention it when handing the sketch over, because the
PNG is shipped with the mod as it is.

## 6. Checking and handing over

Before handing a sketch over:

- [ ] The page loads, and the sketch plays from 0 to the end without errors in the browser
      console (F12).
- [ ] Scrub the timeline backwards and jump around: the picture at a given time never changes.
- [ ] The Layers tab lists no stand-in textures you did not expect, and there is no checkerboard.
- [ ] The first lines of the file are a comment that says what the effect is, in order, with its
      timings, and which game effect it replaces or extends if any.
- [ ] Any texture the sketch needs is either a `Textures/RimArt/...` PNG plus the script that
      makes it, or a `lab/` generator that still has to be ported (say which).

To send your tuned values, use the Params tab:

| Button | Sends |
|---|---|
| Copy link | A URL that opens the sketch with every current value and the current time |
| Copy as a list | The values as readable text |
| Presets: Export | The values as a JSON file |

To show a picture, use the Export tab (PNG sheet, strip, or all frames stacked in one image), or from
a terminal while `lab.py` runs:

```bash
node Tools/VfxLab/shoot.mjs "Pulse (sketch)" 0.8 pulse.png p.size=4
```
