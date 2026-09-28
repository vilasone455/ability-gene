# Chibaku Tensei: handoff for building the real ability

Written 2026-09-28 at the end of the preview work; updated the same day after PR #96 was merged. The next
conversation turns the preview into Pain's ultimate, replacing Gravity Well in Pain's Echo.

## Where things are

- **main a8ae401 has everything**: the Pain kit
  ([PR #96](https://github.com/vilasone455/ability-gene/pull/96), merged as a8ae401) and the Chibaku preview
  ([PR #97](https://github.com/vilasone455/ability-gene/pull/97), merged as d72a888). Start the real ability
  on a new branch from main.
- The preview's commits (branch `feature/pain-chibaku-sketch`, kept), oldest first:

| Commit | What |
|---|---|
| 4f29825 | Lab sketch `Tools/VfxLab/web/sketches/pain-chibaku-tensei.js` (kit Pain, "Chibaku Tensei (sketch)") |
| baf1515 | In-game ground plates test: capture of the real terrain, Voronoi cut |
| 43eebf3 | Ball preview made of the captured ground |
| 53c2422 | Pawns taken into the ball and dropped hurt |
| b915de3 | Items, corpses, plants, trees, filth go with their plate |
| 1228807 | Pulled soil becomes stony soil; biggest rocks land as real chunks |
| 5caca5d | Ball hangs 5 cells up (user's choice) |
| 4145b5b | This handoff |

- The Pain kit pieces the ability hooks into, all on main: `Source/RimArt/Pain/Kit/GameComponent_Pain.cs`
  (`PainCast` base class, `GameComponent_Pain`, `JobDriver_CastPain`, `PainCasts.Make` / `For`),
  `PainDefOf.cs` (`PainKit.Unmovable` for the Black Receiver pin, the Deva gap `PainKit.DevaGapLeft` /
  `StartDevaGap`), `PainLook.cs` / `PainPictures.cs` (Pain's pose and the drawn arm),
  `Source/RimArt/Pain/PainGraphics.cs` (the Banshō palm core), `BanshoCast.cs` and `BlackReceiverCast.cs` as
  worked examples of a `PainCast`, and `1.6/Defs/EchoDefs/AG_Echo_Pain.xml`, which still lists
  `AG_GravityWell` with cast cost 20.
- Before merging #96, main was merged into `feature/pain-port` (17d4949) and tested: pain 8/8, Gravity Well
  10/10, echo 20/20, chibaku 5/5.

## What the code does now (preview only)

`Source/RimArt/Chibaku/`:

| File | What it does |
|---|---|
| `ChibakuCut.cs` | Voronoi plates of a disc (jittered hex seeds, edges bent the same way from both sides). Same hash and numbers as the sketch; seed 0 gives the lab layout |
| `ChibakuGround.cs` | Captures the map's ground layers under the circle (Terrain, TerrainEdges, TerrainScatter, Sand, Snow section layers, plus small plants laid out like `Plant.Print`) into a RenderTexture with a CommandBuffer and an overhead ortho view, 64 px per cell. Terrain meshes have no UVs, so plate meshes sample this picture instead. Cuts the plates; a plate is anchored (stays) if a covered cell has any Building, a built floor (`TerrainDef.IsFloor`) or a roof. Also the plates-only lift test timeline |
| `ChibakuBall.cs` | The picture: core, area circle, cracks, heaving and tumbling plates, crater (dark ribs + holes), ball of 84 surface patches cut from the capture, hold (turn 14°/s, squeeze each second), seams, burst rocks. Draws the taken pawns/items/trees as pictures. Timeline constants |
| `ChibakuPull.cs` | The rules on the map: catching pawns, clearing plates (items, plants, trees, filth), lift, hold, burst, landing damage and stun, lords, soil change, real chunks, rubble |
| `MapComponent_ChibakuPlates.cs` | Drives the previews; `IThingHolder` holding taken things in a `ThingOwner<Thing>` (the map counts it); save/load; debug entries |
| `Tests_ChibakuPlates.cs` | 5 game tests, `-rimarttest=chibaku`, all pass (about 60 s) |

Debug window entries (kit "Pain"): Chibaku ground plates, ... held up, Chibaku ball, Chibaku ball held,
Chibaku ground plates clear. The live "Chibaku ball" is the full rule set on game time; "held" is a frozen
picture that takes nothing.

### Timeline (seconds from the core's arrival; `ChibakuBall` constants)

| Pull | Formed | Crack | Burst | End |
|---|---|---|---|---|
| 0.25 | 3.25 | 15.25 | 15.65 | 18.65 |

Pull 3 s, hold 12 s, crack 0.4 s, tail 3 s. Ball radius 2, centre 5 cells up (bottom 3 up, drawn 3 cells
north), falls take 0.89 s. There is no Pain and no launch yet: the preview starts with the core already over
the cell.

### Rules as built (placeholders; the ones marked * were my defaults and the user never answered)

- Radius 6. A pawn on a plate that is pulled is caught from the start of the pull until 0.75 s before the
  ball forms: stunned, lifted 0.12 s before its plate tears free, taken off the map into the ball, leaves
  its lord. Colonists are caught too*. Pawns under a roof* or on a building's cell stay, and so does that
  ground. Big bodies are caught*.
- When a plate tears free: items and corpses go into the ball; small plants are removed (they are printed
  on the plate and the ball); a tree flies in as its picture and is gone (no wood); filth is gone.
- Held pawns keep ticking and cannot act or be targeted.
- At the burst: pawns fall within about 1 cell of the middle, items within about 2. A pawn takes 2 blunt per
  second held plus 8 for the fall, in hits of up to 8, stunned 2.5 s, back to its lord or a new assault
  lord. Items land unhurt, stacks whole. **This kills some raiders** (about 36 blunt): in two test runs one
  raider died each time. The user has not said whether that is wanted.
- When the ball forms: pulled soil more fertile than stony soil becomes Gravel ("stony soil", 0.7).
- At the burst: the biggest thrown rocks (pulled plates / 16, clamped 6 to 10) land as real chunks of
  `Find.World.NaturalRockTypesIn(map.Tile)`; the rest leave `Filth_RubbleRock`.
- Save mid-hold: taken things are saved; on load they are put down unhurt where the ball was (the picture is
  not saved). Stopping the preview does the same. **Neither has been run in game.**

## The proposed ability (from the sketch header; not formally agreed)

- Target a cell up to 25 cells away, line of sight. Warm-up 0.8 s: Pain raises his hand, a black core forms
  over the palm (Banshō's palm core), flies at 14 cells/s to the cell and climbs to 5 cells.
- The pull, ball, burst and ground changes as above.
- While the ball holds, Shinra Tensei and Banshō Ten'in are locked (the Deva Path is holding it); Black
  Receiver still works. If Pain is downed or killed the ball breaks at once.
- Pain is never pulled. A pawn pinned by 3 Black Receiver rods (`PainKit.Unmovable`) is never pulled.
- Cooldown 1 day. Echo charge 30 (Gravity Well's slot was 20). Heroes are never cast by the AI.
- Gravity Well's code stays: Gojo's Blue reuses it (`docs/hero-echo.md`, Gojo section).

## Work list for the real ability

1. Start a branch from main (the Pain kit and the preview are both there).
2. AbilityDef + XML fields for the balance numbers (radius, pull, hold, crush per second, fall damage, hit
   size, stun, range, cooldown, chunk rate). Balance numbers go in XML, shape and logic stay C# constants.
3. Replace `AG_GravityWell` with the new ability in `AG_Echo_Pain.xml` (cast cost 30 proposed); update
   validate.py / ApiChecks if they list Pain's abilities; update `docs/hero-echo.md` (Pain rows still say
   Gravity Well).
4. A `PainCast` subclass for Chibaku using Pain's cast job (warm-up, a cast called off before firing costs
   nothing), Pain's arm and palm core, the core's flight to the cell, then hand over to the ball.
5. Turn the preview driver into a cast-driven instance. Decide whether a map can have more than one ball at a
   time (the component holds one today).
6. Pain rules: exclude the caster; skip `PainKit.Unmovable`; lock Shinra/Banshō during the hold (the Deva gap
   helpers); break the ball when Pain is downed/killed/leaves (land everything, crush only for the time held).
7. Decide the open rules with the user: colonists caught, roofs protect, big bodies, whether the crush may
   kill, hold 12 s, fall damage scaling with the 5-cell height.
8. Port the 5 tests to cast the ability (`Ability.Activate`) and add Pain-specific ones (caster immune,
   pinned pawn stays, lock during hold, ball breaks when Pain goes down, save/load mid-hold).
9. Icon, sounds (none yet).

## Known faults and polish (after the ability works)

- **Polish, noted by the user:** the crater drawing (holes, ribs, drawn rocks) vanishes when the preview
  ends; it should fade over about a day, leaving the stony soil, chunks and rubble.
- The core glow reads as a small bright white dot (additive layers add up to near white).
- The ball looks slightly egg-shaped; this is the height rule's projection (1.17 times taller than wide).
- A tree tumbles as an upright sprite; may look odd in motion.
- For about 0.2 s while a plate heaves, an item on it is still drawn by the game at ground level.
- Burst rocks are the kit's generic rocks, not pieces of the captured ground.
- Only stills were looked at, never the motion in game.
- One test launch on the merged build crashed natively while the quicktest map was generated, before any
  test ran: a segfault in `PawnRenderer.ParallelGetPreRenderResults`, which 7 Harmony patches hook (RimArt's
  Mimic, Vergil pose, Minato look and Pain look among them). The rerun passed. If it happens again, suspect
  a race in one of those render patches; PainLook is the newest. macOS kept the report in
  `~/Library/Logs/DiagnosticReports/` (RimWorld by Ludeon Studios-2026-09-28-220438).
- Not tested: snow, mossy ground or ice sheet soils, forbidden items, stacks merging on landing, a circle
  partly off-screen, chunks landing on walls.

## Numbers measured in game

- Capture and cut: 11.6 ms once per cast (896 px for 14 cells, 153 plates at radius 6).
- One mid-pull frame's draw calls: 0.55 ms CPU (GPU not measured).
- Plates drawn at rest differ from the real ground by 6.4/255 mean, versus 6.6 frame noise outside the circle.

## How to build and test (Mac)

```bash
cd <worktree on your branch from main>
dotnet build Source/RimArt/RimArt.csproj -c Release
dotnet build Source/RimArt.MeleeAnimation/RimArt.MeleeAnimation.csproj -c Release
python3 validate.py
dotnet run --project Tests/OriginBlade/ApiChecks/ApiChecks.csproj
pgrep -f "Contents/MacOS/RimWorld by" || ./deploy.sh
rm -f ~/Library/Application\ Support/RimWorld/RimArtTests/results.txt
open -a ~/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app --args -quicktest -rimarttest=chibaku
```

- Results and screenshots: `~/Library/Application Support/RimWorld/RimArtTests/` (copy them out at once;
  other sessions share the folder). Last line `DONE PASS n/n`.
- The Melee Animation bridge DLL is rebuilt with no source change; restore it with
  `git checkout -- Patch_MeleeAnimation/1.6/Assemblies/RimArt.MeleeAnimation.dll` before committing.
- The debug log window can open by itself on other mods' startup warnings and cover screenshots; the Chibaku
  tests close `LudeonTK.EditWindow_Log` after setting up the arena.
- Lab: `.claude/launch.json` config `vfx-lab-pain-chibaku`, port 8797 (main checkout).
- Commit only when the user says so; one commit per decision; do not push unless asked.
