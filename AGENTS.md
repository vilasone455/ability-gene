# RimArts code standard

Rules for everyone writing code here, people and agents (Claude Code reads this through CLAUDE.md, Codex
reads it directly). Agreed 2026-09-30. New code follows it; old code is changed only where a rule says so.

## Layout

- One folder per kit: `Source/RimArt/<Kit>/`.
  - `<Kit>/` holds the picture: `*Timing.cs` (times and sizes, no drawing), `*Graphics.cs` (the drawing),
    the preview and its `DebugActions_*.cs`.
  - `<Kit>/Kit/` holds the rules: abilities, comps, jobs, hediffs, components, patches, `*DefOf.cs`, tests.
  - Old kits without `Kit/` are not moved.
- `Shared/` holds code used by two or more kits. `Testing/` holds the test runner and shared test helpers.
- Everything is in `namespace RimArt`, so class names must be unique: start them with the kit name
  (`GojoRedGraphics`, `MinatoCast`). Def names start with `AG_`.
- The file is named after its main type.

## Reuse before writing

- Before writing any of these, check `Shared/` and the list below: a cast that runs over several ticks,
  moving pawns to or from a pocket map, throwing a pawn into a wall, test setup (host, target, wall,
  waiting).
- **Extract on the second use.** When a kit needs code another kit already has, move it to `Shared/` (or
  `Testing/` for tests) in the same PR and make both kits call it. Do not copy it and do not write "the
  pattern of X" again. If the shared version would need many flags to fit both, say why in the PR and
  keep one copy.
- **Fix every copy.** A fix to code that exists in more than one place goes into every place in the same
  PR. Search for it (`grep`) before calling the fix done.
- Add each new shared helper to the list below in the same PR.

### Shared helpers

| File | What it does |
|---|---|
| `Shared/BoundWeapon.cs` | Weapons that must never land (Echo hero weapons, Trace copies): one `TryDropEquipment` patch, `Register`, `Leave` |
| `Shared/CastClips.cs` | Plays a kit's Melee Animation clips on one pawn; works without Melee Animation loaded |
| `Shared/CastJobFail.cs` | `FailBeforeFired`: the fail condition for a cast job that holds the caster after it fires |
| `Shared/Command_TapHold.cs` | One button: tap for the quick version, hold for the charged one |
| `Shared/CrossMapMove.cs` | `Move`, `FreeCellNear`, `ClampInside`, `Assault`: pawns between a home map and a pocket map |
| `Shared/PocketGuest.cs` | One pawn taken to a pocket map: its home cell and lord, `TakeTo`; kits extend it with their own state |
| `Shared/PocketReturn.cs` | Everyone home from a pocket map: `Bring`, `Rejoin` (old lord if it takes the pawn), `NoLord`, `Items`, `Place`, `Finish` |
| `Shared/Gizmo_Meter.cs` | A weapon's meter in the command bar: label, value, bar, optional line and whole-unit ticks (Flame Gauntlet Heat, Last Prism sunlight) |
| `Shared/HeldWeaponHolders.cs` | Who holds one kit weapon on every map for a GameComponent that draws it: `Add`/`Remove` from the comp's equip calls, `Rescan` once a second and after a load, `Copy` for a draw loop (Last Prism, Paradise Lost, Mimicry) |
| `Shared/HeldWeaponHide.cs` | One `DrawEquipmentAiming` prefix: `Register(def, whileHeldBy)` keeps Core from drawing a kit weapon its picture draws (Vacuum, Power Pole, Water Gun, Fuma, Last Prism, Yamato, Paradise Lost, Mimicry) |
| `Shared/InjuryHeal.cs` | `Heal(pawn, amount)`: hit points off the injuries that are not scars, in the health tab's order, as Core's regeneration spreads them (Samehada's Feed and Fusion, Mimicry's lifesteal) |
| `Shared/FollowView.cs` | Camera follows pawns moved between maps and keeps them selected, if the player was watching |
| `Shared/ItemAbilityGrant.cs` | Gives a held item's abilities to its holder, takes them back, keeps cooldowns |
| `Shared/PawnDash.cs` | A short dash: the pawn drawn along a straight line while its cell changes once, on arrival. `Keep` every tick of it (runs are not saved), `Arrive` (optionally refusing a cell someone stands on), `Stop`; one `DrawPos` postfix (Yamato Dash, Mimicry's lunge) |
| `Shared/PawnBody.cs` | Head top, head, neck, chest, feet and ground contact of a real pawn above its DrawPos: the numbers of the lab's real-size stand-in (`lib/pawn.js`), used as they are. `Over`/`Under`: draw heights over and behind a real pawn, added to its own DrawPos.y (Core gives each pawn a random height of up to +/-0.0366), never to `AltitudeLayer.Pawn` |
| `Shared/PawnFit.cs` | Fits a picture drawn on the lab's older 0.89-tall stand-in pawn to a real pawn's height |
| `Shared/SoundLayers.cs` | `Play`: a one-shot SoundDef whose layers start at different times, each on its game tick (`SoundLayerDelays` on the def; RimWorld takes `startDelayRange` only on sustainers). The sound lab writes picked delays this way. `Play(sound, map, cell)` is a kit's sound from a cell, noted in `Heard` for the game tests (Vergil, Pain, Nakime, Gojo). `Sustain(sound, map, cell)` starts a kit's sustainer the same way; `MoveTo` moves one that travels (Gravity Well and Blue's pull, Hollow Purple) |
| `Shared/PictureClock.cs` | `Since(tick)`: game seconds since a tick for drawing, smoothed between ticks at the current speed, held still while paused; rules count whole ticks instead |
| `Shared/VfxDraw.cs` | Materials, meshes, sprites, strips, streaks and the lab's `Tube` (a ribbon with a width per point) every ported picture uses; `BeginBake`/`DrawBaked` keep a picture that only turns, moves or changes height instead of rebuilding its strips every frame |
| `Shared/VfxMath.cs` | The lab's `Smooth`, `Hash`, `Rand`, so a port matches its sketch |
| `Shared/WeaponStow.cs` | `Stow`: empties a hand; a bound weapon goes to its owner (a copy breaks), any other to the inventory, else to the ground |
| `Echo/EchoUtility.cs` | Hero layer: `ForceHost`, `Awaken`, `Manifest`, `Revert`, `ManifestedWith` |
| `Testing/RimArtTestContext.cs` | `Clear`, `Colonist`, `Enemy`, `Mech`, `Hold`, `Equip`, `Describe`, `Check`, `Log`, `ShotAs`; kit tests: `WaitFor`, `ClearEchoes`, `Host`, `EndHost`, `Target`, `Strike`, `NoWimp`, `Note`/`Hurt`/`Untouched` (health or a new wound since the note), `Down`/`Struck` (a downed pawn hit again), `Shield`/`Unshield` (hits deal nothing), `Stunned`, `Free`, `Wall`, `Room` (optional door), `Face`; sounds: `Listen`, `HeardAt`, `HeardOnly`, `HeardNear` (within a tick), `HeardSince`, `LayersAt`, `LayersOnTime` |

### Known copies, not shared yet

- Each kit keeps its own list of running casts: 27 Map/GameComponents in 21 kits, 21 `JobDriver_Cast*`.
  One shared base is planned after v1 (2026-10-20). Until then a new kit copies the Vergil shape
  (`VergilCast`, `GameComponent_Vergil`, `JobDriver_CastVergil`) instead of making a new one.
- Throwing a pawn into a wall is written separately in Gojo Red, Accelerator's shove, Shinra and Banshō. The next
  kit that needs it extracts it.
- Older MapComponents keep their own list of who holds the kit's weapon (Vacuum, Bubble Pipe, Flame Gauntlet, Frost Gun,
  Nezuko Box). `Shared/HeldWeaponHolders.cs` is the shared version; move one to it when its kit is next changed.

## Numbers

- Every number a rebalance could change (damage, radius, range, cooldown, stun, charges, costs, duration)
  is a public field with a default on the ability's `CompProperties_*` or the gene's `DefModExtension`,
  and is set in the XML def.
- C# `const` is only for shape, drawing, timing of the picture, and logic.
- A picture that must match a gameplay radius reads the XML value, not a second constant.

## Size

- Aim for files under 500 lines. Split by job: rules, timing, drawing, patches.
- One main type per file. Small types used only by it (a struct, an enum, a saved record) may sit beside it.

## Comments

- `///` on public types, and on members whose name does not say what they do.
- Write the reason, the numbers, and game-engine gotchas (for example: a lord whose toil refuses new
  pawns logs an error, so check `CanAddPawn` first).
- No summary that only repeats the member's name.
- Plain, factual sentences. The same goes for def descriptions and docs: state the mechanic and the numbers.
- Old files are not trimmed in bulk.

## Tests and debug entries

- Every new rule gets a `[RimArtTest]` scenario next to the kit (`Tests_<Kit>.cs`). Log the pawns' state
  over time; do not rely on PASS/FAIL alone.
- Test setup goes through `Testing/` helpers, not private copies. A kit keeps a wrapper (`Host`, `Target`, `Setup`)
  only for its own steps on top of them; `using static RimArt.RimArtTestContext;` brings the static ones in.
- Rule code with no game calls (timing, geometry, layout) can also get a console test in `Tests/<Kit>/`,
  which compiles the source files it needs and runs without RimWorld.
- Run only the filter for what changed (`-rimarttest=<kit>`), never `all`.
- Debug and preview entries use `[RimArtDebug("<Kit>", "<label>")]`, never `[DebugAction]`. Each entry is a
  one-line call into a preview.

## Harmony

- Patch classes use `[HarmonyPatch]` and are named `Patch_<Target>_<Purpose>`.
- A kit's patches go in `Patches_<Kit>.cs`.

## Pull requests

- One kit or one system per PR. The pictures may go in a PR of their own before the rules (like #124, then
  #127).
- List generated files (clip JSON, the DLL, textures) and their line counts apart from the written code in
  the PR body. `.gitattributes` marks `Animations/*.json` as generated, so GitHub collapses them.

## Verify

1. `dotnet build Source/RimArt/RimArt.csproj -c Release`
2. `dotnet build Source/RimArt.MeleeAnimation/RimArt.MeleeAnimation.csproj -c Release`
3. `python3 validate.py`
4. `./deploy.sh`, then the kit's game tests (`-quicktest -rimarttest=<kit>`).

A clean game log after a restart is the only proof that the mod loads. A rebuilt `1.6/Assemblies/RimArt.dll`
differs from the committed one by its version string after every commit; restore it with
`git checkout -- 1.6/Assemblies/RimArt.dll` instead of committing it again.
