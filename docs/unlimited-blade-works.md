# Unlimited Blade Works

The Trace kit's ultimate, as a pocket map. The design is proposed and not agreed; every number is a
placeholder (the sketch headers in `Tools/VfxLab/web/sketches/trace-ubw-*.js` carry the mechanic). What
is built is the pictures and the map, with no ability behind them yet.

## What exists (2026-09-25)

| Piece | Where | State |
|---|---|---|
| Cast picture: the chant, the fire along its lines, the white, the ring left burning, the return | `Source/RimArt/Trace/UbwCastGraphics.cs`, `UbwTiming.cs` | ported, matches the sketch in the lab |
| World picture: the white, the fire running out, the field of swords, gear shadows, embers, the white closing in | `Source/RimArt/Trace/UbwWorldGraphics.cs`, `UbwGraphics.cs` | ported, matches the sketch in the lab |
| The standing field's layout and the blade geometry | `UbwField.cs`, `UbwBlade.cs`, `UbwWeapons.cs` | ported; `Tests/Ubw` checks 3,760 swords against the sketch's layout |
| Lab previews | RimArts debug window, Trace, "unlimited blade works: cast" / "world" | recorded by the lab; both play 2 cells north of the chosen cell, as the sketches do |
| The real pocket map: 40 x 40, earth terrain, roof, unseen warm lights, the world drawn over it | `Source/RimArt/Trace/Kit/`, `1.6/Defs/*/AG_UnlimitedBladeWorks_*.xml` | written, not yet built or run |
| The weapon atlas from the weapons' own textures (the lab's reference art never ships) | `Kit/UbwAtlasBuilder.cs` | written, not yet run |
| The commands (Full Open, Pin, Draw, Arm, Intercept) | sketches only | not ported |
| World v2 (plates with depth, tiers past the edge, the north sky with gears) | `UbwTerrain.cs`, `UbwTerrainGraphics.cs`; preview "world v2"; "world map: open (v2 depth)" | ported as a second world picture on the same field and timing; `Tests/Ubw` checks 1,327 plates against the sketch's ground; an experiment, not agreed |
| The ability: the verse, who is taken, the return, the cooldown | nothing | not started; the numbers sit in `UbwRules` on the generator def |

## In game

RimArts debug window, Trace:

- **unlimited blade works: cast** and **world**: the pictures over the map on screen, no pocket map.
- **world map: open**: makes the pocket map beside the map on screen and plays the fire running out.
  Nobody is taken; spawn a pawn in it with the game's own tools to see the swords at scale.
  **open (v2 depth)** makes it on the plate ground with the sky (seed 1, the sketch's), to compare.
- **world map: close**: the white closes in behind the wall of fire, then the map is removed.
  **close now** removes it at once.

The swords are drawings, not things: they block nothing and a pawn walks through them. The map has no
sun of its own (it is roofed and lit by unseen lights every 6 cells, `AG_UbwLight`), so the sword
shadows use the sketch's fixed low sun.

## Textures

`make_trace_textures.py` writes `Textures/RimArt/Trace/`: the flame, the earth tile, the fade gradient,
the terrain fallback, and for v2 the terrain atlas (the earth tile in four shades, the face gradient, the
swatches) and the sky gradient, from the lab's own formulas. The swords' pictures are not shipped: in the
lab they are the local reference atlas of `make_trace_trial_textures.py` (git-excluded); in game
`UbwAtlasBuilder` reads each weapon's texture at first use, measures its axis the way that script
does, and builds the same atlas and the outline and silhouette textures for the trace look. Until an
ability names the studied blades it uses Core's knife, longsword, spear, gladius and ikwa, and the
monosword when Royalty is present.

## Still to check in game

- The atlas builder: whether `Graphics.Blit` and `ReadPixels` give the pictures the right way up, and
  what the field looks like with Core's art (the lab was tuned on six reference weapons).
- Backface culling on the baked field and the batches (every triangle is written clockwise on
  screen; a failure looks like missing blades, lips or flames).
- Draw order against real pawns: the haze at Building + 0.6 must sit under pawns and over the swords;
  the field's shadows at Shadows + 0.002 under a pawn's own shadow.
- The weather tint and the glowers' colour: placeholders, to be tuned by eye.
- The frame rate with about 930 swords baked (about 35 draws a frame while the world stands, about 65
  for v2 with its sky, more while the fire runs out).
- v2 in game: the plates on the walkable map step by 0.08 cells and the hill by 0.18, so a pawn's feet
  stand a little off a raised plate; whether that reads; the sky's top plane (800 x 400 cells north of
  the map) against the map's own edge at full zoom-out.
