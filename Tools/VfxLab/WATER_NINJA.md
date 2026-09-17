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

Choose **Water Ninja → Undertow Dash (sketch)** for the movement companion.
Default timing: gather at the feet (0–0.18 s), dash (0.18–0.46 s),
low arrival splash (0.46–0.64 s), wake settling into puddles (0.64–1.00 s).
Two translucent afterimages follow a forward water crescent along a five-cell wake.
The proxy illustrates movement; the lab scene pawn does not move. Both the proxy
and the afterimages can be toggled independently in Params.

Timing, distance, aim, wake width, arrival radius and foam are adjustable.
The shared drawing helpers live in `web/sketches/lib/water-ninja-water.js`;
both sketches use the same procedural water texture, which still needs baking for a game port.

### Sprite wake

Undertow's trail layers three rounded water-sheet PNGs with broad curved foam
highlights. Highlights move faster than the sheets to suggest internal flow;
the lab does not support UV scrolling. Sheets shrink as they age, releasing larger
droplets instead of retaining a solid silhouette while fading.

The front is a curved bow wave. Side spray mixes tiny mist drops with heavier blobs;
sprites stretch and rotate along their projected velocity. Gravity brings them down,
and expanding ground rings replace them at contact. Soft shadows sit beneath the
stream and droplets, while dark wet patches persist into the settling phase.

Regenerate the nine white-alpha sprites with `python3 make_water_ninja_textures.py`.
They live in `Textures/RimArt/WaterNinja/` and can be used directly in a C# port.
The shadow reuses `RimArt/SixPaths/SoftDisc`. The gather and arrival splash
still use the shared lab-only procedural texture.
