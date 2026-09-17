# Water Ninja — Tidecutter

A stylized water-ninja VFX proposal inspired by Greninja's water-shuriken theme.
It is a lab sketch, not an implemented RimWorld ability.

Run `python3 Tools/VfxLab/lab.py`, then open:

<http://localhost:8765/Tools/VfxLab/web/?effect=Tidecutter%20showcase%20(sketch)>

Choose **Water Ninja → Tidecutter showcase (sketch)**. Space plays/pauses;
the timeline scrubs, and Params tunes timing, aim, distance, blade size, splash,
spin, foam and droplets. Use Export to make a sheet or PNG sequence.

## Choreography

| Default time | Action |
|---|---|
| 0–0.85 s | Broken water ribbons gather from a shallow pool. |
| 0.85–1.30 s | Four curved blades hold as a spinning water shuriken. |
| 1.30–1.72 s | The shuriken flies forward with three translucent, broken water trails. |
| 1.72–2.10 s | A foamy pressure burst opens into water lobes and a broken splash crown. |
| 2.10–3.35 s | Droplets fall away and thin ground ripples dissolve. |

Deep navy outlines, cobalt bodies, turquoise bands and narrow white foam edges
keep the water readable on terrain. The effect uses flat meshes and normal
transparency rather than bloom. Geometry uses the lab's game-compatible drawing API. Trails and impact lobes use
a procedural `lab/tidecutter-water` alpha texture with soft edges, striations and holes.
This lab-only texture must be baked into a PNG for a game port; no custom shader is needed.
Positions are calculated directly from time, so scrubbing and exports repeat exactly.

Implementation: `web/sketches/water-ninja.js`. No existing ability is replaced.
Porting still requires C# timing/drawing and an in-game check.

## Undertow Dash

Choose **Water Ninja → Undertow Dash (sketch)** for a compact traveling splash.
Default timing: pawn collapses into water (0–0.18 s), a rolling surge moves forward
(0.18–0.46 s), pawn emerges through a larger splash (0.46–0.64 s), wet ground
and contact ripples settle (0.64–1.00 s).

Most water stays around the moving front. Only a short, broken wake follows it;
there is no connecting beam or repeated crescent afterimage. Spray is thrown to
the sides, falls under gravity, and becomes expanding ground rings. Subtle dark
wet patches remain along the route after the water has passed.

The simple transformation pawn is a preview stand-in, toggled in Params. The lab's
scene pawn does not move. Timing, aim, distance, moving splash size, arrival splash
size and foam are adjustable. The preview pawn fades at the end for clean looping.

### Sprites

Rounded water-sheet sprites and separate foam layers overlap into the rolling surge.
Sprite motion suggests internal flow; the lab does not support UV scrolling.
Regenerate the seven white-alpha assets with `python3 make_water_ninja_textures.py`.
They live in `Textures/RimArt/WaterNinja/` and can be used directly in a C# port.
Shadows reuse `RimArt/SixPaths/SoftDisc`. Undertow no longer draws the procedural
Tidecutter ribbon texture; Tidecutter itself is unchanged.
