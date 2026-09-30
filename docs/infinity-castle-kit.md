# Infinity Castle: Nakime, the castle and its commands

Agreed in outline on 2026-09-23. The castle's pictures and pocket map were ported on 2026-09-24 and
the four room commands the same day (see "Port notes"). Since 2026-09-30 the whole kit is Nakime's
hero kit in the Echo framework (`docs/hero-echo.md`, "Nakime"): the ability takes enemies in, the
commands are her buttons, and she brings everyone back. Every number below is a placeholder and an
XML field (the ability's comp properties, the castle map def's `InfinityCastleRules` or the gene's
`CastleOrganExtension`), not a C# constant. The pictures are the sketches under **Infinity Castle**
in the VFX lab (`Tools/VfxLab/web/sketches/infinity-castle-*.js`); the shared drawing and the room
generator are in `Tools/VfxLab/web/sketches/lib/infinity-castle.js`. Each sketch header carries the
same numbers as this page.

The source is Nakime from Demon Slayer: her biwa and the Infinity Castle.

## Source and roster row

Since 2026-09-27 the source is the Nakime Echo (hero), not a gene kit.

| Source | Kit | Abilities | Acquisition |
|---|---|---|---|
| Nakime Echo | Infinity Castle, the castle organ gene, the biwa | Infinity Castle; inside it Shift, Drop, Seal/Open, Crush, Summon, Release | Meet her Trials at the resonance device and awaken |

## The gene and the biwa

The castle organ (`AG_CastleOrgan`, name open) is given by her Echo on awakening and kept for life.
It lists no abilities and does nothing else but the sunlight rule.

| Field | Value | Note |
|---|---|---|
| Sunlight | Outdoors while the sky is lit (vanilla's InSunlight: unroofed, sky glow over 0.1): 4 burn damage a second through clothes, checked once a second, downed in about 20 s. Roofed cells, caravans and night are safe. In hero form or not. | Chosen over a hard indoor lock, which would need pathing patches and would stop her crossing between buildings at night |
| Metabolism, complexity | 0, 2 | An Echo gene: the Echo is the price |

The biwa (`AG_Biwa`) is her Echo's manifest weapon: it appears in her hands when she manifests,
vanishes when she reverts or lets go of it and comes back 10 s later while she is still manifested.
Blunt 9 (body) and 7 (neck). No stuff, no recipe, market value 0. Out of hero form she may hold any
weapon (the old gene's weapon ban is gone).

Sketch: `infinity-castle-sunlight.js` (sunlight burn; the biwa coming back). Its picture is not
ported: the burn shows as vanilla burns and a puff of smoke each second.

## Infinity Castle

Sketch: `infinity-castle-open.js` (the home map: taken in, brought back).

| Field | Value |
|---|---|
| Target | A cell within 35 cells; no line of sight needed |
| Warm-up | 1 s |
| Taken | Hostile pawns within 5.9 cells of the cell, nearest first, up to 8 pawns and a total body size of 8. Downed hostiles are not taken. |
| Carrier | Goes in after them |
| Castle | A new 100 x 100 pocket map for each cast, roofed, removed afterwards |
| Lasts | 60 s of game time from the move in, or until Release, or until the carrier is downed, dies, leaves or reverts |
| Return | Every pawn back to the cell it was taken from (a summoned colonist to where it was summoned from); corpses and items come up around the target cell |
| Cooldown | 3 days |
| Charge | 30 from the Echo pool; given back with the cooldown if nobody is left to take at the strum |

During the warm-up a ring shows the 5.9-cell radius and an outline marks the floor under each pawn
that will be taken. The strum opens a door in the floor under each of them and they drop in.

### The castle map

Sketch: `infinity-castle-castle.js` (arrival, the rooms at rest, Release).

- 30 to 45 rooms, 5 x 5 to 17 x 13 cells counting their walls, hanging in a void that nobody can
  walk. Each room has its own wall ring.
- Rooms grow off each other and keep 2 cells of void from every room except the one they grew
  from. The castle therefore starts as a tree: every room is reachable, and there is one way in to
  each. The generator in `lib/infinity-castle.js` (`generate`) is the rule set for the C# one.
- A doorway sits where two rooms' walls touch along 3 or more cells: one door in each wall.
- Room kinds are looks only: tatami room, corridor, great hall, stair hall.
- Castle walls and doors cannot be dug or broken.
- Drawn-only rooms and stair flights at two depths under the void give the endless look.
- The biwa room (9 x 9) is at the castle's west edge. The carrier lands on its dais and plays from
  there. She cannot walk or attack, and the room cannot be shifted, sealed or crushed, so enemies
  can reach her and down her.
- Each enemy lands in a different room, at least 3 doorways from the biwa room.

### Commands

Every command is one strum of the biwa, played from the biwa room. Strums are at least 1.5 s apart.

| Command | What it does | Sketch |
|---|---|---|
| Shift | Pick a room (not the biwa room) and one of four directions. It slides straight until its wall touches another room, at most 20 cells. Pawns, items and corpses in it ride along. Its doorways close as the rooms part; a new doorway opens where it now touches another room along 3 or more cells. A room pushed into open void has no doorways. | `infinity-castle-shift.js` |
| Drop | Pick one pawn, then a room. A door opens in the floor under the pawn; it comes up through a door in that room's floor. No damage. | `infinity-castle-drop.js` |
| Seal / Open | Pick one doorway. Seal: the leaves close and a bar locks it; nobody passes. Open: the bar comes off and the leaves open. | `infinity-castle-seal.js` |
| Crush | Pick a room of 7 x 7 or more (not the biwa room). The walls slam 2 cells in and draw back. Everyone in the outer 2 cells of the floor takes 15 blunt and is stunned 1 s, own pawns included. The only command that deals damage. Own cooldown 10 s. | `infinity-castle-crush.js` |
| Summon | Pick a room, then a colonist on the home map from a list. The colonist comes up through a door in that room's floor and goes home with everyone else. | `infinity-castle-drop.js` |
| Release | End the castle now. | `infinity-castle-castle.js` |

Passives:

- **Castle sight:** no fog; the carrier sees every room.
- **Void rule:** a pawn standing in a doorway that breaks (in either of its two door cells) is left
  over the void, drops, and comes up in a random room through a floor door, unharmed. Shown in the
  Shift sketch, scenario "raider in the doorway".

### Enemies taken in

- Being despawned ends the pawn's current job, as with Swallow, and the job never resumes.
- Before a pawn goes in, it drops whatever it carries where it stood on the home map. A kidnapped
  colonist or loot stays behind and can be rescued. Its own gear goes with it.
- The old lord gets `Notify_PawnLost(ExitedMap)`, as with Swallow. It is not a violent loss, so it
  should not break the rest of the raid (to confirm in game).
- Sappers and breachers stop; a dug tunnel stays. A siege crew leaves its posts; the camp stays.
- Mental states (manhunter, berserk) carry on inside.
- Inside, the pawns get a castle lord: attack anyone they can reach, else wait at a door.
- On return each pawn goes back to the cell it was taken from. It rejoins the old lord if that
  lord still exists (new duty, the raid plan continues); otherwise it gets a new lord that leaves
  the map. The save stores the old lord and the return cell for each pawn.

### Held for later

Rotate (same jobs as Shift plus Crush), Shuffle (every room at once, once per castle; an upgrade
candidate), Eject to a chosen home-map spot (cross-map targeting), Loop doors (pathing ignores
them), Door (move one pawn across the map; same job as the Anchor organ's rescue).

## Open

- The gene's name. The mod names genes as organs, glands and plexuses; "Castle organ" is a
  placeholder.
- The biwa room is fixed in the sketches (the earlier recommendation). A movable one is still
  possible.
- The Void rule as sketched: rooms own their walls, so a pawn is always inside one room. The rule
  covers a pawn standing in a door cell of a doorway that breaks.

## Port notes

- Pocket map: the Involute kit already calls `PocketMapUtility.GeneratePocketMap`
  (`Source/RimArt/Involute/InvoluteUtility.cs`) with a hand-written GenStep.
- Swallow drops the victim's lord and gives none, so returning enemies need a lord made for them.
- The void is a terrain. It is opaque near-black (`AG_CastleVoid`), and the mod draws the depth
  rooms and the void's fog just above it, under the rooms' floors, which the mod also draws
  (`CastleLayers.Pocket`). The sketch draws the depth rooms under a see-through void terrain instead;
  the picture is the same, and it needs no terrain transparency, which could not be tested.
- A sliding room is drawn with the same meshes as a room at rest while the real room is hidden,
  and its cells move at the stop. Drawing riding pawns needs `MimicRender` (not yet verified) or
  a fade.

### What is ported (2026-09-24, pictures and the pocket map, no mechanics)

- `CastleLayout.cs` is the sketch's generator, exact: the same Mulberry32 numbers in the same order,
  in double precision (JS rounding and stable sorts kept), so seed N with M rooms is the same castle
  in game and in the lab. `Tests/InfinityCastle` checks 80 castles and 4 depth-room lists against
  the JS (`dump-layouts.mjs` writes `layouts.json`) and the castle's rules on 600 castles.
- The rooms are the sketch's meshes drawn by the mod over real cells (final, not a stand-in: Shift
  and Crush need the same meshes). Rooms at rest, open doorways and lantern bodies are baked once
  into one mesh per height and colour (`CastleRoomGraphics.CastleBatch`), about 30 draws for a
  castle; lantern glows and the depth rooms are drawn each frame and culled to the view on the
  pocket map.
- Previews (RimArts debug window, Infinity Castle): "open (take)", "open (return)", "castle",
  "castle (biwa room)"; recorded by the lab and compared with the Open and Castle sketches with
  "Stand-in pawns" off. The pocket map: "castle map: open" (the lab's castle, seed 1 with 38 rooms),
  "castle map: open (random)", "castle map: release", "castle map: close now".
- The pocket map (`Kit/`, defs `AG_InfinityCastle*`): void terrain everywhere, castle floor under
  every room, walls in every wall cell but the doorways (open passages; no door things), a glower
  at every lantern, thick roof, no fog. Walls and lanterns carry flat or blank textures; the mod
  draws the look.
- Not ported: the stand-ins (pawns, Nakime with the biwa and the bachi, corpse, rifle), sounds,
  and the command sketches (Shift, Drop/Summon, Seal/Open, Crush, Sunlight).
- The castle is the largest system in the mod: generator, moving a group in and out, lords after
  return, moving rooms, the commands, cleanup, and save/load while the castle is open. Measure the
  hitch of generating the map mid-fight.
- The strum clip comes last, after the mechanics, like the other kits' clips.
- The lab's `scene: false` sketch option was added for these sketches: they draw their own ground
  (the pocket map) instead of the lab's grass and trees.

### The commands (2026-09-24)

Shift (PR #36), Seal/Open (#37), Crush (#38) and Drop (#39), each confirmed in game through the debug
window. The rules and pictures are in `MapComponent_InfinityCastle` and `InfinityCastleShift.cs`.

### The ability, the cast and Nakime (2026-09-30)

- `InfinityCastleCast` (one per cast, kept by `GameComponent_InfinityCastle`, the same shape as
  Unlimited Blade Works' `UbwCast`). The strum at the end of the 1 s warm-up chooses the pawns and
  makes the castle (`InfinityCastleMap.Make`, a random seed and room count). Everyone is held
  (stunned) over their door. Each pawn is hidden once it has sunk, and when the carrier has sunk
  everyone moves at once: each enemy to its own arrival room (`CastleLayout.ArrivalRooms`), Nakime
  to the dais. Release (60 s, the button, downed, dead, gone or reverted) plays the castle's Release
  over the real pawns. When the castle's fade is black, everyone goes home and the castle map is
  removed.
- Lords: each enemy leaves its lord with `Notify_PawnLost(ExitedMap)` before it moves, so a raid taken
  whole ends its lord. At the return a pawn rejoins its old lord if that still exists on the home map,
  else a pawn of another faction gets `LordJob_ExitMapBest` (jog). Inside, the hostile ones get
  `LordJob_AssaultColony` per faction; pawns in a mental state keep it and get no lord.
- The castle map's clock is real time and now runs whether or not the castle is on screen (it used to
  stop while you looked at the home map). A castle the ability made is `driven`: its arrival doors
  open under the real pawns (`Arrive`), its Release doors under whoever is leaving, and it holds at
  black (`Faded`) for the cast to bring everyone home. The debug window's castle still plays the
  sketch's arrival and closes itself; "castle map: close now" refuses a driven castle.
- The home-map picture (`InfinityCastleOpenGraphics`) takes a `CastleOpenPlan`: the preview's is the
  sketch's script, the ability's is built from the real pawns (door times from their distance to the
  target cell). The same plan draws the warm-up from the Host's warm-up stance. With a real carrier
  the shaft's dark also closes over her and lifts off her; the sketch drew its stand-in instead.
- Summon (new): the colonist leaves the home map at once and comes up in the chosen room through a
  floor door (a `CastleDrop` with `summoned` set); on the home map a floor door opens where it stood.
- Not built: the Nakime costume, sounds (strum, slide, thud, bar, crush), the strum clip, the sunlight
  picture, command icons beyond flat placeholders, pathing that keeps her indoors by day. The hitch
  of generating the castle mid-fight and save/load while the castle stands have not been measured
  or tested.
