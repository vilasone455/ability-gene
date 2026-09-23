# Infinity Castle: the gene, the castle and its commands

Agreed in outline on 2026-09-23. Nothing is built in `Source/RimArt` yet. Every number below is a
placeholder and will be an XML field (the ability's comp properties or the gene's
`DefModExtension`), not a C# constant. The pictures are the sketches under **Infinity Castle** in
the VFX lab (`Tools/VfxLab/web/sketches/infinity-castle-*.js`); the shared drawing and the room
generator are in `Tools/VfxLab/web/sketches/lib/infinity-castle.js`. Each sketch header carries the
same numbers as this page.

The source is Nakime from Demon Slayer: her biwa and the Infinity Castle.

## Source and roster row

One source, as `REVAMP.md` requires.

| Source | Kit | Abilities | Acquisition |
|---|---|---|---|
| Gene | **Castle organ** (name open) | Infinity Castle; inside it Shift, Drop, Seal/Open, Crush, Summon, Release | Acquire and implant the archite gene |

## The gene

| Field | Value | Note |
|---|---|---|
| Archites | 2 | Fold organ: 1 |
| Complexity | 8 | Fold organ: 7 |
| Metabolism | 0 | The drawbacks are the price. It was -5 before they were added. Fold organ: -4 |
| Grants | the bound biwa, the Infinity Castle ability | |
| Sunlight | Outdoors in daylight: about 4 burn damage a second, checked once a second, downed in about 20 s. Roofed cells and night are safe. | Chosen over a hard indoor lock, which would need pathing patches and would stop her crossing between buildings at night |
| No other weapons | She cannot equip any weapon but the biwa | Same hook as the Anchor organ's `banWeapons` (`EquipmentUtility.CanEquip`), with the biwa excepted |

Sketch: `infinity-castle-sunlight.js` (sunlight burn; the biwa coming back).

### The bound biwa

- Appears in her hands. Destroyed when dropped (she is downed), given back when she is up again.
- Not craftable and not tradeable, so no one-of-a-kind code is needed.
- Weak blunt melee, about 9.

A plain biwa item was rejected: it is dropped when downed, can be stolen, needs one-of-a-kind code,
and invites swapping to a gun.

## Infinity Castle

Sketch: `infinity-castle-open.js` (the home map: taken in, brought back).

| Field | Value |
|---|---|
| Target | A cell within 35 cells; no line of sight needed |
| Warm-up | 1 s |
| Taken | Hostile pawns within 5.9 cells of the cell, nearest first, up to 8 pawns and a total body size of 8. Downed hostiles are not taken. |
| Carrier | Goes in after them |
| Castle | A new 100 x 100 pocket map for each cast, roofed, removed afterwards |
| Lasts | 60 s, or until Release, or until the carrier is downed |
| Return | Every pawn back to the cell it was taken from; corpses and items come up around the target cell |
| Cooldown | 3 days |

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
- The void is a terrain. For the depth rooms to show under it, the void terrain needs a see-through
  texture and the depth rooms are drawn at `BelowTerrain`, where real floors cover them. They can
  be baked into one mesh per depth.
- A sliding room is drawn with the same meshes as a room at rest while the real room is hidden,
  and its cells move at the stop. Drawing riding pawns needs `MimicRender` (not yet verified) or
  a fade.
- The castle is the largest system in the mod: generator, moving a group in and out, lords after
  return, moving rooms, the commands, cleanup, and save/load while the castle is open. Measure the
  hitch of generating the map mid-fight.
- The strum clip comes last, after the mechanics, like the other kits' clips.
- The lab's `scene: false` sketch option was added for these sketches: they draw their own ground
  (the pocket map) instead of the lab's grass and trees.
