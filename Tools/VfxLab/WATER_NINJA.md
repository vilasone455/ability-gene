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
| 1.30–1.72 s | The shuriken flies forward with three curling water trails. |
| 1.72–2.10 s | Two staggered crossing cuts open a broken splash crown. |
| 2.10–3.35 s | Droplets fall away and thin ground ripples dissolve. |

Deep navy outlines, cobalt bodies, turquoise bands and narrow white foam edges
keep the water readable on terrain. The effect uses flat meshes and normal
transparency rather than bloom. All geometry uses the lab's game-compatible drawing
API and the built-in white texture; there are no new PNGs or shader dependencies.
Positions are calculated directly from time, so scrubbing and exports repeat exactly.

Implementation: `web/sketches/water-ninja.js`. No existing ability is replaced.
Porting still requires C# timing/drawing and an in-game check.
