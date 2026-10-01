# Unlimited Blade Works

The Trace kit's ultimate, as a pocket map. The rules were proposed 2026-09-24 and taken as placeholders
2026-09-25; every number is an XML field (`UbwRules` on `AG_Trace_UnlimitedBladeWorks`). Built: the
pictures, the map, the ability (chant, take, world timer, return), granted by the Shirou Echo
(docs/hero-echo.md), and since 2026-10-01 the commands used inside the world (Full Open, Pin, Draw, Arm,
Intercept).

## What exists (2026-10-01)

| Piece | Where | State |
|---|---|---|
| Cast picture: the chant, the fire along its lines, the white, the ring left burning, the return | `Source/RimArt/Trace/UbwCastGraphics.cs`, `UbwTiming.cs` | ported, matches the sketch in the lab |
| World picture: the white, the fire running out, the field of swords, gear shadows, embers, the white closing in | `Source/RimArt/Trace/UbwWorldGraphics.cs`, `UbwGraphics.cs` | ported, matches the sketch in the lab |
| The standing field's layout and the blade geometry | `UbwField.cs`, `UbwBlade.cs`, `UbwWeapons.cs` | ported; `Tests/Ubw` checks 3,760 swords against the sketch's layout |
| Lab previews | RimArts debug window, Trace, "unlimited blade works: cast" / "world" | recorded by the lab; both play 2 cells north of the chosen cell, as the sketches do |
| The real pocket map: 40 x 33 (the caster 20 cells from the east, west and south edges, 13 from the north), earth terrain, roof, unseen warm lights, the world drawn over it | `Source/RimArt/Trace/Kit/`, `1.6/Defs/*/AG_UnlimitedBladeWorks_*.xml` | written, not yet built or run |
| The weapon atlas from the weapons' own textures (the lab's reference art never ships) | `Kit/UbwAtlasBuilder.cs` | written, not yet run |
| The commands (Full Open, Pin, Draw, Arm, Intercept) | rules `Kit/UbwFullOpen.cs`, `UbwPin.cs`, `UbwDraw.cs`, `UbwArm.cs`, `UbwIntercept.cs`, `UbwSwordHit.cs`, `UbwFieldState.cs` (the field's taken and stuck swords), `UbwCommands.cs` (the buttons); pictures `UbwCommandTiming.cs`, `UbwCommandGraphics.cs`; the field baked in rows (`UbwFieldBake.Banded`); previews `UbwCommandsPreview.cs` | ported 2026-10-01 from `trace-ubw-world-commands.js`; 7 game tests (`Kit/Tests_UbwCommands.cs`), which take stills of each command in the world and of each preview; the motion not watched; not played by hand |
| World v4, the one the ability opens (level plates, a sword crest past the north edge, a side-view sky with seven gears, ridges and rows of swords behind it that pan slower than the map, smoke and embers over the crest) | `UbwCrest.cs`, `UbwCrestGraphics.cs`, `UbwBackdropGraphics.cs`, `UbwTerrain.cs`, `UbwTerrainGraphics.cs`; preview "world v4"; "world map: open" | ported 2026-10-01 from `trace-ubw-world-v4.js`, replacing v2; `Tests/Ubw` checks its plates, swords and crest profile against the sketch; the lab recording matches the sketch (0.3 % of pixels differ, the sketch's stand-ins); the sketch's opening white fade is not ported: with the reveal shot off (or not watched) the fire still runs out, as v1 |
| The reveal shot: at the take, 4.6 s with the game paused, a camera at head height looks up at the sky and its gears, tilts down while the fire runs out to 130 cells and the swords rise ring by ring, cranes up and blends into the world v4's usual view; any key or click skips it | `UbwRevealTiming.cs`, `UbwRevealGraphics.cs`, `UbwRevealGround.cs`, `UbwRevealSky.cs`, `UbwShot.cs` (the frame as data); `Kit/UbwShotCamera.cs` (the camera), `Kit/UbwRevealWindow.cs` (pause, skip, UI, the world's clock, the pawns' portraits); setting in Options, Mod settings, RimArts; preview "reveal" | ported 2026-10-01 from `trace-ubw-reveal.js`; the lab replays the C# shot (the recorder records the camera and its 3D draws, at 12 frames a second) and it matches the sketch frame by frame (0.2 to 2.2 % of pixels differ: the turning parts are 1.15 s further on, as the game's world stands from 1.65 s); game test "reveal 1"; not run in game |
| The close shot (tilting back up to the gears) | sketch only (`trace-ubw-close.js`) | not ported |
| The ability: the chant, who is taken, the world's time, the return, the cooldown | `Kit/UbwCast.cs`, `GameComponent_UnlimitedBladeWorks.cs`, `CompAbilityEffect_UnlimitedBladeWorks.cs` (with the chant job and the Release/Close buttons), `1.6/Defs/AbilityDefs/AG_Trace_Abilities.xml` | written 2026-09-25; granted by the Shirou Echo since 2026-09-26; 7 game tests pass (`Kit/Tests_Ubw.cs`); not played by hand |

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
   the 40 x 33 world v4, 20 cells from its east, west and south edges and 13 from the north one. A caster downed or reverted between the release and
   the white stops the world opening; the cooldown and charge stay spent.
3. Hostile pawns get an assault lord of their faction in the world (no fleeing: the map edge is the
   world's edge). The home map keeps the low ring of fire, which blocks nothing.
4. The world stands 20 / 25 / 30 s of game time from the take, less 0.5 s for every sword the commands
   take out of the ground (The commands, below). It ends early when the caster is downed,
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

## The commands

Built 2026-10-01 from `Tools/VfxLab/web/sketches/trace-ubw-world-commands.js`. Every number is a
placeholder and an XML field of `UbwRules` (`AG_Trace_Abilities.xml`).

- **They exist only** while the cast stands, its close has not begun or been ordered, and the caster is in
  the world, fit and still Shirou (`UbwCast.CommandsOpen`). Full Open, Pin, Draw and Intercept are buttons
  after Close on the ability; Arm is a button on every other player colonist inside.
- **The cost** is the world's time, never charge: every sword a command takes out of the ground takes
  `swordCostSeconds` (0.5 s) off the world as it leaves. `UbwCast.spent` counts it (saved);
  `WorldSecondsLeft` and the close check subtract it, and the Close button shows the seconds left.
- **The swords are the field's own**: a command takes real swords from where they stand, nearest first, and
  each leaves its hole (the marks and both lips, no blade). Swords past the map edge are out of reach.
  `UbwFieldState` (on the world's map component, saved) keeps the taken seeds and the swords Full Open
  stuck in the ground; the layout itself is made again from the landing spots on first use, so the rules
  work with the world off screen and a loaded game shows the same holes. The field is baked in rows of 4
  cells (`UbwFieldBake.Banded`) and only the rows a command touched are built again, at most once a frame:
  the whole v4 field (about 490 swords, 47,000 vertices, 14 non-empty rows) bakes in 6 to 24 ms (most
  near 9 ms), a changed row is built again in 1.3 to 6.1 ms (most near 1.5 ms; dev log of 30 bakes and
  about 30 rebuilds in the game tests, Mac, 2026-10-01).

1. **Full Open** (caster). Pick a pawn in the world. The charge starts at once: the caster stands still
   facing it (`AG_UbwFullOpen` job) and every `fullOpenSwordSeconds` (0.1 s) one sword pulls out of its
   hole, nearest the target first, turns flat about its middle and hovers 1 cell up aimed at the target,
   following it, up to `fullOpenSwords` (30); then they wait. The buttons are **Release (n swords)** and
   **Cancel**. Release fires them in a shuffled order within `fullOpenVolleySeconds` (0.3 s) at
   `fullOpenSpeed` (16 cells/s): each hits the target as its own weapon and sticks in the ground 1 to 2
   cells past it, leaning 25 degrees back in, and 0.6 s later stands in the field again (it can be used
   again). Cancel, a move order, the caster going down, the target dying or leaving the world, or the close
   drop the hovering swords back into their own holes in 0.25 s; the time they cost stays spent. No
   cooldown; a new charge can start while a volley flies.
2. **Pin** (caster). Pick a pawn in the world. The `pinSwords` (4) swords nearest it leave together 0.1 s
   after the order and fly in low at `pinSpeed` (16 cells/s). The first goes through a trouser leg: the pawn
   gets `AG_UbwPinned` (Moving capped at 0, no crawling) for `pinSeconds` (12 s). Vanilla counts a pawn that
   cannot move as downed even awake, so it can be captured; the hediff is added with no DamageInfo, so no
   death-on-downed roll is made. The others arrive 0.1 s apart once it has fallen and pin the other leg and
   both sleeves. Each sword does `pinDamage` (2) Cut. The pinning swords are gone from the field; they are
   drawn in the pawn, leaning 15 degrees out, 60 % deep, while it stays pinned and in the world. There is
   no prisoner bed in the world, so a capture happens after the return, if the 12 s have not run out. Going down takes a
   pawn out of its lord (vanilla), so a hostile that gets up inside the world is put into a new assault lord.
3. **Draw** (caster). Pick a sword: the one nearest the clicked cell within `drawPickRadius` (1.5 cells);
   the targeter draws a line to it and a ring at it. It tears out 0.1 s after the order, turns flat and flies
   to the caster's hand spinning at `drawSpeed` (22 cells/s), following him if he moves. Every pawn hostile
   to the caster within half the blade's length of its path (W.Length x W.Image x Size / 2, about 0.7 cells
   for a longsword) takes one hit of that sword's weapon, once. The caster catches it as a traced copy: a
   real weapon in hand goes to the inventory, a copy in hand breaks. The catch is a Trace On copy: it stays
   in his hand after the world closes and breaks when it leaves it. Quality: Trace On's rule (`qualityBelow`
   under the best studied) if the weapon is in his library, else Normal; default stuff. A caster who cannot
   catch it lets it break into light.
4. **Arm** (every other player colonist inside). The sword nearest the colonist slides up out of its hole
   0.1 s after the order and arcs to their hand in 0.4 s, no damage. Their weapon goes to their inventory;
   the sword is a traced copy of its weapon (Normal, default stuff) marked as Arm's (`TraceCopy.ubwArm`): the
   colonist has no Trace On, so the rule that breaks a copy held without it does not apply. It breaks when
   it leaves the hand, and when the world closes (before anyone is moved home). Disabled for a downed pawn,
   one that cannot use weapons, and a Host whose Echo forces its hands.
5. **Intercept** (caster). A toggle, saved on the cast. While on, each shot fired by a pawn hostile to the
   caster inside the world is checked once, the tick it is first seen. Of the swords that can reach its
   line first (`interceptRiseSeconds` 0.1 s plus the distance at `interceptSpeed` 20 cells/s, no more than
   the shot needs to get there), the one nearest the line leaves the ground and meets it, no nearer than
   `interceptClearOfGun` (1.2 cells) to where it was fired and at least `interceptShortOfTarget` (1.5 cells)
   short of the pawn it was aimed at (a miss lands past it) and of where it lands, an explosive its blast
   radius further, so the burst does not reach the pawn. There a plain shot ends without hitting anything and an explosive one
   bursts; the sword breaks into light. Each stopped shot costs 0.5 s. A shot no sword can reach in time
   (fired from close by, or across bare ground) is not stopped and costs nothing. Only direct-flight rounds
   count, not mortar shells, and not a round Recursion holds. It is a prefix on `Projectile.TickInterval`,
   a no-op while no cast intercepts; while one does, its world's projectiles tick every tick.

A sword's hit (`UbwSwordHit`): the weapon def the sword is a picture of (`UbwWeapon.Name` is the defName),
its strongest Cut or Stab tool at default stuff and Normal quality (`Tool.AdjustedBaseMeleeDamageAmount`),
armour penetration by `VerbProperties.AdjustedArmorPenetration`'s rule, the weapon named on the DamageInfo,
the caster as instigator; no miss roll and no damage roll. A monosword's hit is Stab 25, AP 0.9. A weapon with
neither tool hits for `swordFallbackDamage` (10) Cut at `swordFallbackPenetration` (0.15).

## In game

To try the ability: RimArts debug window, Echo, **make Host (no trials)** with Shirou on a colonist,
**fill charge**, then Manifest and use the ability from the colonist's buttons. Trace, **unlimited blade
works: ready** clears its cooldown. The real way in: Kits, **Origin: Blade** on a colonist (the grant
awakens it), build the resonance device and tune it to Shirou and that colonist.

Game tests: `-quicktest -rimarttest=ubw`, 15 scenarios: 7 for the ability (the Trial, cast and return,
Stop chanting and a move order refund, revert during the chant, revert in the world, the reveal shot, empty
pool in the world) and 8 for the commands (`-rimarttest="Ubw: commands"`: Full Open released, with the field's
state through Scribe and the cast's save data; Full Open cancelled by a move order; Pin, with the raider back in
an assault lord when it gets up; Draw; Arm; Intercept of a revolver shot; Intercept of a triple rocket, met its
blast radius clear of the caster; the five previews), about 4.5 minutes in all.

RimArts debug window, Trace, pictures and the empty map:

- **unlimited blade works: cast**, **world** (the flat v1) and **world v4**: the pictures over the map on
  screen, no pocket map.
- **world map: open**: makes the world v4 beside the map on screen, as the ability does, and plays the fire
  running out. Nobody is taken; spawn a pawn in it with the game's own tools to see the swords at scale.
  **open (v1 flat)** makes it with the flat earth, to compare.
- **unlimited blade works: reveal**: the reveal shot over the map on screen, through the cutscene camera, with the
  lab's stand-in pawns and without pausing; the camera is moved to the world's framing first.
- **unlimited blade works: commands: full open**, **pin**, **draw**, **arm**, **intercept**: one command played on the
  standing world v4 over the map on screen, with the sketch's stand-ins at the sketch's places and times (Full Open's
  16 swords on a raider walking north, Pin on a raider walking in, Draw past two raiders on its lane and one off it,
  Arm by an ally, Intercept of two shots and a rocket from 9 cells), the XML's speeds and counts. No pocket map, nobody
  moved or hit.
- **world map: close**: the white closes in behind the wall of fire, then the map is removed.
  **close now** removes it at once.

The swords are drawings, not things: they block nothing and a pawn walks through them. The map has no
sun of its own (it is roofed and lit by unseen lights every 6 cells, `AG_UbwLight`), so the sword
shadows use the sketch's fixed low sun.

## Textures

`make_trace_textures.py` writes `Textures/RimArt/Trace/`: the flame, the earth tile, the fade gradient,
the terrain fallback, and for v4 the terrain atlas (the earth tile in four shades, the face gradient, the
swatches), the crest's face, the sky gradient, the plain behind the crest and the camera's haze, from the
lab's own formulas. The swords' pictures are not shipped: in the
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
- The frame rate: about 35 draws a frame for v1 while the world stands, about 80 for v4 (the gears,
  clouds, smoke and embers are rebuilt each frame), more while the fire runs out.
- v4 in game: the plates on the walkable map step by 0.08 cells and the hill by 0.18, so a pawn's feet
  stand a little off a raised plate; the backdrop follows `Find.Camera` (position and orthographic size),
  so check panning (it should open more sky going north), zooming out to 60 (the sky's top plane, the
  cover under the plates), and a pawn on the north row against the crest.
- The reveal shot in game, none of it seen yet: the cutscene camera over the map camera (culling mask 0, drawing from
  `OnPostRender` through a command buffer); every draw at one depth so the frame's far-to-near order decides what
  covers what (the game's shaders may write depth); the blend's vertices placed on the CPU (about 120,000 a frame for
  1.05 s; textures are not perspective-correct then); the window's pause, skip and screenshot mode; the hand-over at
  4.2 s to the map camera at the same framing (watch for a jump); the portraits standing among the swords (2 cells
  wide, cut at the feet); the fire's flames drawn over nearer swords (the lab hides them), and the camera's haze
  falling on the pawns during the blend (in v4 they are over it).
- v4 as sketched: the sketch's `clamp` is `Mathf.Clamp01`, so the field's clustering only thins it (no
  groves) and the sun stands at azimuth 1 degree, above the caster, not the 55 degrees the scene's shadows
  point from. The port does the same; changing either is a sketch change first.
- The ash and embers in front of everything also draw over the white while the fire runs out and closes
  in (the sketch's close does the same).

The commands (the game tests cover the rules: swords taken and stuck, the world's time spent, hits, Pin's
downed state and its end, the copies, a shot stopped and one not; their stills were looked at once: the hover, the
volley, the stuck swords, the pins on a lying pawn, Draw's lane and spin, Intercept's break, and the five previews):

- Every picture in motion: a sword pulled out turning flat, the hover 1 cell up and its glint, the volley
  sticking past the target and quivering, the hand-over from the one-by-one stuck sword to the rebuilt
  row 0.6 s later (a small jump in lean is possible), Pin's swords on a lying pawn (placed from the game's
  downed body angle, the head along it; check they sit on the legs and sleeves for every facing), Draw's
  spin and its two blur copies, Arm's arc, Intercept's spark and break, the crumbs where a sword leaves.
- The banded field: one draw per row and kind (14 rows x 3 draws instead of 3 for the whole
  field) and the step between rows (0.002 a row over Building); check that no row covers the one south of it.
- The buttons: Command_Target on pawns and cells inside the world, Draw's highlight line and ring while
  targeting, Release's count, Intercept's toggle, Arm on a colonist.
- By hand: Full Open on a raider who walks during the charge (the swords turn to follow), Release with 30
  swords, Pin and then a capture after the return, Intercept against a burst rifle and a rocket launcher
  (the rocket should burst where it is met), Arm on a Host whose Echo forces its hands (disabled).
- A save and load mid-world: the holes and stuck swords, a Full Open charging, Pin's swords in a pawn.
- Icons: Full Open, Release and Intercept use the ability's icon, Pin the vanilla attack icon, Draw and Arm
  Trace On's; none is its own yet.
