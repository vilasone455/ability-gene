# Unlimited Blade Works

The Trace kit's ultimate, as a pocket map. The rules were proposed 2026-09-24 and taken as placeholders
2026-09-25; every number is an XML field (`UbwRules` on `AG_Trace_UnlimitedBladeWorks`). Built: the
pictures, the map, and the ability (chant, take, world timer, return), granted by the Shirou Echo
(docs/hero-echo.md). Not built: the commands.

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
| The ability: the chant, who is taken, the world's time, the return, the cooldown | `Kit/UbwCast.cs`, `GameComponent_UnlimitedBladeWorks.cs`, `CompAbilityEffect_UnlimitedBladeWorks.cs` (with the chant job and the Release/Close buttons), `1.6/Defs/AbilityDefs/AG_Trace_Abilities.xml` | written 2026-09-25; granted by the Shirou Echo since 2026-09-26; 6 game tests pass (`Kit/Tests_Ubw.cs`); not played by hand |

## The ability

Granted by the Shirou Echo (`AG_Echo_Shirou` in `AG_Echoes.xml`) while its Host is manifested. The
Echo's one Trial is the Origin: Blade trait, so the way in is still the blade study; the trait itself
grants nothing. Numbers from `UbwRules` on the ability def; cooldown 2 days (`cooldownTicksRange`
120000); cast cost 40 charge from the shared pool (the Echo's `castCosts`), taken when the ability
fires. Shirou's upkeep is 12 charge an hour while manifested.

1. Cast (0.25 s warmup): the caster stands facing south and chants, 2 s a verse. Buttons: **Release**
   (the world opens when the verse being said ends) and **Stop chanting**. After verse 3 it opens by
   itself. A move order, going down, a mental break or the Host reverting breaks the chant. A stopped
   or broken chant gives the cooldown and the 40 charge back.
2. Released after verse V: the fire runs along the chant's lines, the ring closes, white. At full white
   everyone standing within 6 / 9 / 12 cells of the chant's cell is taken: allies, enemies, animals and
   the caster; downed pawns stay. Each lands at its offset from the caster, who lands in the middle of
   the 40 x 40 world (v2 by default, `worldV2`). A caster downed or reverted between the release and
   the white stops the world opening; the cooldown and charge stay spent.
3. Hostile pawns get an assault lord of their faction in the world (no fleeing: the map edge is the
   world's edge). The home map keeps the low ring of fire, which blocks nothing.
4. The world stands 20 / 25 / 30 s of game time from the take. It ends early when the caster is downed,
   dies, is no longer in it or stops being Shirou (reverted by hand, or the pool ran dry and every Host
   reverted), or on the caster's **Close (N s)** button.
5. The close: the white comes in from the edge in 1.2 s. At full white everyone alive goes back to the
   cell they were taken from (nearest free cell), downed or not, drafted if they were; into their old
   lord if it still exists, else hostiles get a new assault lord and other non-player pawns that had a
   lord leave the map. Corpses and every item in the world drop round the chant's cell. The world is
   removed. On the home map the fire runs back in along the lines and its white peaks on the same tick.

Both sides run on game time, so pausing stops them; the pictures move smoothly between ticks
(`UbwClock`). Saving and loading keeps a cast at any step (the game component saves the casts; the world
map saves its own clock).

## In game

To try the ability: RimArts debug window, Echo, **make Host (no trials)** with Shirou on a colonist,
**fill charge**, then Manifest and use the ability from the colonist's buttons. Trace, **unlimited blade
works: ready** clears its cooldown. The real way in: Kits, **Origin: Blade** on a colonist (the grant
awakens it), build the resonance device and tune it to Shirou and that colonist.

Game tests: `-quicktest -rimarttest=ubw`, 6 scenarios (the Trial, cast and return, Stop chanting and a
move order refund, revert during the chant, revert in the world, empty pool in the world), about 30 s.

RimArts debug window, Trace, pictures and the empty map:

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

The ability (the game tests cover the chant starting, the take and the return with lords and drafted
state, the refunds, and the world's removal):

- By hand: selection and camera on the take and the return; raiders fighting inside for the full
  20 to 30 s; the hitch while the world generates inside a game tick at the release.
- A save and load while the world stands, and while chanting.

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
