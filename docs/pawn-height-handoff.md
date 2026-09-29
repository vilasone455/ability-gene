# Pawn height: handoff for lowering on-pawn pictures in game

Written 2026-09-29 after the Satō port. The next conversation fixes the ported pictures that sit too high on
real pawns, and decides what to do with the lab's pawn stand-in. The in-game fix is done: see "Result" at
the end. The lab stand-in is not done yet.

## The problem

The VFX lab cannot load the game's pawn art, so every sketch draws a fake pawn (a "stand-in": two ellipses,
body and head) and effects are placed on it. A C# port copies the sketch's positions exactly, measured from
the pawn's cell centre / DrawPos. The stand-in most kits use is smaller than a real pawn and stands higher,
so in game anything placed on a body part lands too high on the real pawn: a chest hit at the neck, a feet
ring at the waist, a head flash on top of the head. Things sized against the stand-in (a coil round the
body, a silhouette) also come out too small next to a real pawn.

Ground effects (domes, lines, explosions, scorch) are not affected. Pictures that use the real pawn in game
(Vergil's poses and afterimages, pawns drawn by the real renderer) are not affected.

## Measured numbers

Measured in game on 2026-09-29: five naked, bald colonists facing south, one per body type, screenshot at
`ShotAs(name, cell, 3.5f)` = **111 px per cell**, silhouettes found by diffing against an empty shot.
Heights are cells from the pawn's cell centre (= DrawPos), + is screen-up.

| | head top | head centre | neck | chest | waist | feet | width |
|---|---|---|---|---|---|---|---|
| Real, thin | +0.63 | about +0.41 | +0.21 | | | -0.52 | 0.47 |
| Real, male | +0.62 | +0.41 | +0.20 | about +0.05 | about -0.20 | -0.53 | 0.60 |
| Real, female | +0.63 | | | | | about -0.58 | 0.56 |
| Real, fat | +0.64 | | | | | -0.58 | 0.94 |
| Real, hulk | +0.63 | | | | | about -0.6 | 0.89 |
| Lab stand-in, most kits | +0.75 | +0.58 | | body centre +0.18 | | -0.14 | 0.44 body |
| Corrected stand-in (lib/ajin.js, lib/itachi.js) | +0.645 | +0.42 | | body centre -0.10 | | -0.51 | 0.57 body |

- Lab stand-in, most kits: body ellipse radii 0.22 x 0.32 at +0.18, head radii 0.16 x 0.17 at +0.58. It is
  0.89 tall against a real 1.1-1.2, and its feet are 0.4 above the real feet. Its head centre sits at the
  real head top, its body centre at the real neck, its feet at the real waist.
- The corrected stand-in is the same shape x1.3 and 0.33 lower. It matches thin, male and female pawns in
  height and position; fat and hulk bodies are about 0.9 wide against its 0.57.
- Many sketches give heights in "lab height" h, drawn at h x 0.6 on screen (Lift); the numbers in this doc
  are already on-screen offsets.

The temporary measuring test (not committed) was, in `Source/RimArt/Testing/Tests_PawnSize.cs`:

```csharp
[RimArtTest("Lab", "pawn sizes (screenshots)")]
private static IEnumerable<int> Sizes(RimArtTestContext t)
{
    t.Clear();
    yield return 5;
    yield return t.ShotAs("pawnsize empty", t.center, 3.5f);
    var types = new[] { BodyTypeDefOf.Thin, BodyTypeDefOf.Male, BodyTypeDefOf.Female, BodyTypeDefOf.Fat, BodyTypeDefOf.Hulk };
    var pawns = new List<Pawn>();
    for (int i = 0; i < types.Length; i++)
    {
        Pawn p = t.Colonist(t.center + new IntVec3((i - 2) * 2, 0, 0));
        p.story.bodyType = types[i];
        p.story.hairDef = HairDefOf.Bald;
        if (p.style != null) p.style.beardDef = BeardDefOf.NoBeard;
        p.Drawer.renderer.SetAllGraphicsDirty();
        pawns.Add(p);
    }
    // then turn each pawn with a Wait_MaintainPosture job and shoot again (south, east)
}
```

## How the kits that are already right do it

Two patterns, both in C# only (the sketches keep the old stand-in):

- **Shift** (Vergil, Minato's kunai kit, Sasuke's Rinnegan, Obito, Pain): the picture's ground point is
  DrawPos minus 0.3. `VergilKit.Ground` in `Source/RimArt/Vergil/Kit/VergilDefOf.cs:39-44`:
  `public const float FeetBelowDrawPos = 0.3f; public static Vector2 Ground(Vector3 drawPos) => new Vector2(drawPos.x, drawPos.z - FeetBelowDrawPos);`
  Also `MinatoKit.Ground`, `RaikoKusariTiming.Feet`, `ObitoGraphics.Ground`, `PainPictures` (Ground and
  LyingGround). Shadow Plexus uses 0.35 (`MapComponent_ShadowPlexus.StandingFeet`) and a measured
  `NeckAboveFeet` 0.55.
  Leftover error with the shift: stand-in chest +0.18 lands at -0.12 (fine), head centre +0.58 lands at
  +0.28 (0.13 below the real head centre), feet -0.14 land at -0.44 (0.09 above the real feet).
- **Fit** (Nezuko's box, Itachi's Susanoo, Satō, Samehada's shark form): heights mapped y -> 1.3 y - 0.33
  (`SharkFit` 1.3 / -0.33 in Samehada). Head centre +0.58 -> +0.42, feet -0.14 -> -0.51, body centre
  +0.18 -> -0.10. Matches a real pawn within about 0.05. Body-shaped things (coils, shells, silhouettes)
  are scaled x1.3 in size as well.

**Recommendation:** use the fit for effects placed on anatomy, and scale body-shaped things x1.3; hit
flashes and sparks keep their size. The shift alone is what Vergil does and is good enough for the chest,
but leaves heads 0.13 low. Both are C#-only, so sketches and recordings are unchanged. Best put in the one
place each kit turns a pawn into a ground point.

## Work list: kits where the error shows in game

From a read-only survey of all 77 ported sketches on 2026-09-29 (five agents, one per group of kits). Paths
are under `Source/RimArt/`. "High" = how far above where the sketch meant it, in cells.

| # | Kit (test filter) | What is off in game | How far | Where it is placed (C#) | Notes |
|---|---|---|---|---|---|
| 1 | Bubble Pipe (`-rimarttest="Bubble Pipe"`) | the jar at the hip is drawn at the neck/shoulder on every standing holder, all the time; Eye Pop's tiny bubbles start a little high | ~0.4 (jar), ~0.1 (bubbles) | `BubblePipe/BubblePipeGraphics.cs` (`MouthH` 0.62, `JarBase` 0.30), `BubblePipe/Kit/MapComponent_BubblePipe.cs` (`Ground(Thing)` line 168, `DrawJarOn` at DrawPos) | the most visible one: it is on screen all the time. The pipe mouth (+0.37) is about right for a real mouth; Eye Pop's soap film already uses the game's head offset |
| 2 | Paper Bomb (`-rimarttest="Paper Bomb"`) | Shroud's 6 stuck tags: the "legs" tag on the belly, the "chest" tag on the face, the "head" tag on the head top; the seal glint (Shroud and Tag Line) on the caster's face; Tag Line's strip starts at the neck; a carried tag (Tag Throw) sits at the neck | ~0.3 (tags), ~0.45 (glint), ~0.25 (strip, carried tag) | `PaperBomb/PaperBombShroudGraphics.cs` + `PaperBombShroudTiming.Slots`, `PaperBomb/PaperBombTagLineGraphics.cs` (SealFlash +0.50), `PaperBomb/PaperBombTagThrowGraphics.cs` (`Up(anchor, 0.5)`); feet = DrawPos in `PaperBomb/Kit/MapComponent_PaperBomb.cs` (`Ground`, line 206-208) and `PaperBombAbilities.Feet` | |
| 3 | Power Pole (`-rimarttest="Power Pole"`) | pole, both hands and hit flashes at the neck/chin in Extend Thrust, Sweep, and the carry/plant of Vault Strike; the vault in the air is fine | ~0.25 | `PowerPole/PowerPoleThrustGraphics.cs`, `PowerPoleSweepGraphics.cs`, `PowerPoleStrikeGraphics.cs` + `PowerPoleStrike.cs` (`Raised(HandHeight 0.5)` = +0.30); feet = caster DrawPos in `PowerPole/Kit/MapComponent_PowerPoleCasts.Add`; `Kit/CompAbilityEffect_PowerPoleSweep.cs` (HitPlace) | **the Melee Animation clips also put the real hands at +0.30** (`make_power_pole_anim.py`: HAND_HEIGHT x LIFT), so the clips must be regenerated with the same correction or the hands and the pole will part |
| 4 | Water Gun (`-rimarttest="Water Gun"`) | the jet and splash hit the head instead of the chest (Stream and Hydro Pump); the raised gun at the chin; the backpack at the neck/head | ~0.3 | `WaterGun/WaterGunStreamGraphics.cs:62` (`contactH = 0.38f/Lift` at target DrawPos), `WaterGun/WaterGunPumpGraphics.cs:67` (`ShotImpact` +0.38), `WaterGun/WaterGunGraphics.cs` (`HandH` 0.5, `BagBase` 0.28) at caster DrawPos | the game's own gun is hidden during the cast |
| 5 | Vacuum (`-rimarttest=Vacuum`) | the wand at the chin; the hip coil at the chest; a disarmed pawn's rifle flies off from face height; daze stars 0.24 above the head top (0.11 in the sketch) | ~0.25-0.3 | `Vacuum/VacuumGraphics.cs` (`HandH` 0.5, lines 257-258), `Vacuum/VacuumSpitGraphics.cs` (flash `o.y + 0.3f*big`, daze +0.86), `Vacuum/VacuumDigest.cs`; anchored at DrawPos in `Vacuum/Kit/MapComponent_Vacuum.cs` (`Ground(Thing)` line 102) | Core's wand is hidden during the cast (`Patches_Vacuum.cs`) |
| 6 | Chain Sickle (`-rimarttest="Chain Sickle"`) | the coil on a snagged pawn wraps chest-to-neck and stays there while it is snagged; the weight "at the hip" at the neck; the hit flash at the neck; the chain hand high | ~0.2 (coil), ~0.45 (weight), ~0.22 (flash), ~0.3 (hand) | `ChainSickle/ChainSickleGraphics.cs` (hand 0.5, chest 0.45, coil radius 0.26), `ChainSickle/ChainSickleSnag*.cs`, `ChainSickleStake*.cs`; `ChainSickle/Kit/MapComponent_ChainSickle` at DrawPos; `ChainSickleLink` / `Snagged` keep drawing the coil | the coil is body-shaped: scale it too |
| 7 | Flame Gauntlet (`-rimarttest="Flame Gauntlet"`) | gauntlet and palm at the neck, jumping up from the ground-level resting spot when a cast starts; flame tongues on a burning pawn from the neck up (0.2 s); the arm burn mark on the neck; Release's fist and jet start above the hand | ~0.3 (gauntlet, jet), ~0.35 (tongues), ~0.4 (burn mark) | `FlameGauntlet/FlameGauntletGraphics.cs` (hand 0.5, flame spots), `FlameGauntletDevour*.cs`, `FlameGauntletRelease*.cs`; `FlameGauntlet/Kit/MapComponent_FlameGauntlet` from DrawPos; `DrawHeld` between casts at ground level with no lift | the resting heat (`DrawHeld`) and the cast hand should end up at the same height after the fix |
| 8 | Goku (`-rimarttest=Goku`) | Kamehameha: beam and hit bursts at the neck, the ki ball "at the rear hip" 0.2-0.7 high, the aura leaves the lower legs out. Instant Transmission: the vanish slices miss the legs and rise 0.17 above the head; brow glint and closing rings at the head top; stun stars high. Spirit Bomb: the lenders' ribbons start 0.3 above the head (no hand under them), the shell rings about 0.3 high. Solar Flare: the gathering light at the head top | 0.2-0.3 (up to 0.7 for the ki ball) | `Goku/Kit/GokuPictures.cs` (`Vec()`, no feet shift), `Goku/GokuGraphics.cs` (`Sliced` keeps the stand-in outline), `GokuKamehamehaGraphics.cs` (Chest 0.3), `GokuInstantTransmissionGraphics.cs`, `GokuSpiritBombGraphics.cs`, `GokuSolarFlareGraphics.cs`; `Kit/KamehamehaCast.cs`, `Kit/SpiritBombCast.cs` | the slices and shells are body-shaped: scale them too. The lend job has no hand-up animation |
| 9 | Samehada (`-rimarttest=Samehada`) | Feed and Shark Skin: bite, scratches and drain at the neck/chin; the Drained haze "round the feet" on the chest; rings from chest height; the heal glow at the head; the tally overlaps the feet. Fusion: the merge/revert flash and motes at the head instead of the grip | ~0.22 (bite), ~0.4 (Drained), ~0.3 (heal), ~0.55 (fusion flash) | `Samehada/Kit/MapComponent_Samehada.cs:93` (`Feet(pawn)` = DrawPos, no shift), `Samehada/SamehadaGraphics.cs` (`HandH` / `ChestH`, `Drained`, `Healing`, `Bite`, `Fit`), `Samehada/SamehadaFusionGraphics.cs` (`Flashes` uses raw +0.35 / +0.30) | the blade (Core's weapon points) and the shark form / regen ring (`SharkFit` 1.3 / -0.33) are already right: do not shift them twice |
| 10 | Todo (`-rimarttest=Todo`) | Black Flash: the hit point (bolts, core, ring, shards) on the face instead of the chest; the fist crackle from a shoulder at the head; stun stars 0.13 high; zone sparks neck to head top. Boogie Woogie: the ink figure spans -0.13..+0.8 against a real -0.53..+0.62, arrival crescents and flecks centred on the head (0.13 s) | ~0.33 (Black Flash), ~0.3 (Boogie) | `Todo/BlackFlash.cs` (`Chest` 0.38, shoulder +0.4), `Todo/BlackFlashGraphics.cs`, `Anchor/MapComponent_BlackFlashes` (DrawPos + fixed offsets); `Todo/BoogieWoogie.cs`, `Todo/BoogieWoogieGraphics.cs` (the white cover was already enlarged to 1.6 x 2.3 at +0.2 to hide a real pawn) | the palm burst (read off the clip) and the stone throw are fine |
| 11 | Bank Shot (`-rimarttest="Bank Shot"`) | the wound flash and blood at the neck; the charge glow, muzzle flash and tracer start 0.3 above where the game draws the aimed pistol | ~0.22 (wound), ~0.3 (muzzle) | `BankShot/BankShot.cs` (hand height 0.5, chest height 0.45), `BankShot/BankShotGraphics.cs`, `BankShot/Kit/MapComponent_BankShot` (from the caster's cell centre, wound at the hit cell centre +0.27) | never run in game; the pistol position in game was recalled, not checked: take it from `PawnRenderUtility.DrawEquipmentAiming` |
| 12 | Shadow Plexus, Shadow double only (`-rimarttest="Shadow Plexus"`) | the double is drawn as the old stand-in from the cell centre: feet 0.39 above a real pawn's, head top 0.13 above, 0.89 tall instead of 1.15, 0.44 wide instead of 0.6 | 0.39 | `ShadowPlexus/ShadowDoubleGraphics.cs` (`Silhouette`, same disc numbers), drawn at `CellGround` (`ShadowPlexus/Kit/MapComponent_ShadowPlexus.cs` lines 56-85) | the code comment says it stands for the carrier's own silhouette: the fit (size x1.3, 0.33 lower) makes it a real pawn's size. The rest of Shadow Plexus is already right |

**Small, fix if touched:**
- Pain (`-rimarttest=Pain`, uses the shift): the Rinnegan eye glint about 0.1 below the head centre; the
  drawn arm starts about 0.15 below the shoulder; Banshō's landed target uses `Ground`, not `LyingGround`,
  so its stun stars sit 0.22 low; the blocker's stars +0.54, about 0.17 low. Shinra Tensei's pushed pawns
  (`Pain/Kit/ShinraFlight.cs` -> `ShinraDomeGraphics.Pushed`) use cell centres: landing dust (`Kick`) about
  0.5 above the real feet, the wall-hit flash about 0.25 high. Files: `Pain/Kit/PainPictures.cs`,
  `Pain/BanshoGraphics.cs`, `Pain/BlackReceiverGraphics.cs`, `ShinraDomeGraphics.cs`.
- Rinnegan (already shifted): Amaterasu's head blotches 0.1-0.15 low, its dark cover misses the top 0.17 of
  the head, smoke starts at the head centre; Raikō Kusari's crackle misses the lowest 0.25 of the legs.
  These are what the shift leaves; the fit would fix them.

**Preview only now, would be wrong once tied to pawns** (fix when they are wired, same way):
- Gojo, Unlimited Void opening (`Gojo/UnlimitedVoidOpenGraphics.cs`, `GojoGraphics.RimLight`): the violet head
  disc sits 0.17 high as a cap above the head, the legs get no rim, the hand-sign light at the head top.
- Six Paths (`DebugActions_SixPaths.cs`): Serpent's coil would wrap the torso, not the legs (~0.4,
  `SixPathsSerpent.cs` Rise 0.55); Repulse's rope contact and flash at the head (~0.3,
  `SixPathsRepulse.cs` `ContactHeight` 0.65); Umbrella's hand ~0.3 high (`SixPathsUmbrella.cs` `Rel()`);
  Twin Maw's bite flash ~0.2 (`tile + 0.42f`); Bloom's carried orb ring ~0.2.
- Anchor's card-flick pictures (`Anchor/ClapTeleportGraphics.cs`, `MarkFlick.cs`) are only in their debug
  previews; the real Clap and Mark use Todo's pictures.

**Already right, no work:** Vergil, Minato's kunai kit, Sasuke's Rinnegan (small leftovers above), Itachi,
Obito, Nezuko's box, Satō, Shadow Plexus except the double, Pain except the small list. Not on the pawn:
Infinity Castle, Unlimited Void inside, UBW, Todo's stone throw.

## The lab stand-in

Separate from the in-game fix. The two-disc stand-in is copied into about 50 sketch files: the kit libraries
`lib/bank-shot.js`, `chain-sickle.js`, `flying-thunder-god.js`, `gojo.js`, `goku.js`, `infinity-castle.js`,
`kamui.js`, `nezuko-box.js`, `obito.js`, `pain.js`, `shadow-plexus.js`, `six-paths-impact.js`,
`six-paths-sage.js`, `ubw-crest.js`, `ubw-reveal.js`, `unlimited-void.js`, `vacuum.js`, `vergil.js`,
`water-gun.js`, plus sketches that draw their own (Power Pole's `figure()`, Six Paths Serpent's mannequin,
and others). `lib/ajin.js` and `lib/itachi.js` have the corrected one.

Options (not decided; the user wanted this list first):
1. One shared real-size stand-in (a new `lib/pawn.js`, with a body option: average, fat, hulk), and every
   kit's copy switched to it. All sketches then show real-size pawns; effects placed on the old stand-in
   show where they will really land and each sketch's effect heights would need updating to match its
   in-game fix.
2. The same shared stand-in behind a lab setting (old / real), old by default, so approved sketches do not
   change until switched.
3. The shared real-size stand-in only for new sketches.

Interaction with the in-game fix: if the fix is done in C# (recommended), the sketches still show the old
stand-in and the old heights, and the lab and the game disagree by the fit. Option 1 would bring them back
in line, but then every fixed sketch's heights have to move to the real body too. Decide this before
starting option 1.

## How to check each fix

- Game test runner: `[RimArtTest]` scenarios, run with
  `open -a ".../RimWorldMac.app" --args -quicktest -rimarttest=<filter>`; results in
  `~/Library/Application Support/RimWorld/RimArtTests/results.txt` and PNGs. Most kits already have a
  screenshot test ("pictures" or similar); add one where there is none. Look at the shots: crop round the
  pawn at 111 px per cell (size 3.5) and check the effect against the real head, chest and feet.
- Rules: never launch the game 8:30-17:30 Monday to Friday; `pgrep -f "Contents/MacOS/RimWorld by"` before
  every `./deploy.sh` (other sessions share the game folder); run only the kit's own filter, never `all`;
  delete results.txt before a launch (in zsh a failed glob in the same `rm` line stops the whole line).
- The C# picture classes are shared with the VFX recorder (`Tools/VfxLab/Recorder`). Putting the fix in the
  game-side ground function (the `Kit/` code) leaves the recordings and the lab comparison unchanged.
- Build loop as usual: `dotnet build Source/RimArt/RimArt.csproj -c Release`, the Melee Animation bridge,
  `python3 validate.py`, ApiChecks.

## Open decisions for the user

1. Fit (recommended) or shift for the in-game fix.
2. All 12 kits in one PR, or kit by kit.
3. Power Pole: regenerate the Melee Animation clips so the real hands follow the lowered pole.
4. What to do with the lab stand-in (options 1-3 above), and whether to do it before or after the in-game
   fix.

## Result (2026-09-29, branch fix/pawn-height)

Decisions: the fit; one PR with a commit per kit; Power Pole's clips regenerated; lab stand-in option 3
(a shared real-size stand-in for new sketches only), after this, in its own PR.

- `Shared/PawnFit.cs` is the switch. A kit's game code draws its pictures between `PawnFit.Begin()` and
  `PawnFit.End()`; the picture code puts heights on a pawn through `PawnFit.Y` (on-screen) or `PawnFit.H`
  (lab heights), body sizes through `PawnFit.Body`, and body points through `PawnFit.At`. Outside
  Begin/End they return the sketch's numbers, so the recorder and the in-game previews are unchanged. Floor
  things at a pawn's cell (rings, cones, lanes, outlines, dust) are not fitted.
- Worn gear and side-held hands: the north-south part of an offset on the body is depth, not height, on
  a real pawn (`OnBody` in the Bubble Pipe, Water Gun and Vacuum frames, `ChainSickleFrame.Hand`). A jar at
  the hip of a pawn facing east stays at the hip instead of going to the knees.
- Exceptions, where a real pawn's own point was already right: Bubble Pipe's raised pipe at the mouth and
  Eye Pop's face target; Flame Gauntlet's resting heat
  between casts (it sits on the game's own gauntlet texture); the vault in the air (Power Pole); Samehada's
  blade and shark form (already right).
- Each kit has a "height" game test with close shots (`-rimarttest="<kit>: height"`, several filters can be
  given with commas). Before/after crops were compared for all 12.

- Paper Bomb's clips were not fitted like Power Pole's: most of their hands already sit on a real body
  (facing south: rest at the chest, cocked overhead), and the fit sent them to the feet. Only the east clip
  (mirrored for west) was wrong: the rig showed the off side's offset as height, so the braced off hand
  and the roll sat at the neck. `make_paper_bomb_anim.py` now drops the off side's offset there
  (`sideways()`); in game the pin, the strip's hand end and each fan tag leave from the clip's hand
  (`PaperBombGraphics.ClipHand` and the `Clip*` numbers in the three timing classes; change them with the
  clips). `-rimarttest="Paper Bomb: height 2"` shoots the clips' hands facing east and south.

Left open: Bank Shot's muzzle is 0.35 short of the real pistol's muzzle along the aim. A pawn whose warmup
is shortened (Trigger-happy) releases Paper Bomb's tag before the fixed-length clip does. The small list
(Pain, Rinnegan) and the preview-only list were not touched.

Pain, afterwards (branch fix/pain-height): Shinra Tensei's pushed pawns draw their drag mark, landing dust
and wall flash from the pawn's ground point (`PainKit.Ground`, the 0.3 shift) in `ShinraFlight.Draw`, 0.3
lower than from the cell centres; the dust still starts about 0.2 above the feet, the shift's usual
leftover. Banshō's landed target uses `LyingGround` while it lies face-down, so its stars sit over the
lying head (0.22 higher). The blocker's stars go 0.22 higher (`PainPictures.StarsUp`, passed as
`BanshoView.blockStarsUp`, 0 in the lab), from +0.54 inside the head to +0.76. Close shots:
`-rimarttest="Pain: shinra 3,Pain: bansho 1,Pain: bansho 2"`, files named "pain height ...". Still left:
the Rinnegan eye glint (0.1 low) and the drawn arm (0.15 low). Also seen: when Banshō is blocked, the
target is not drawn face-down in game (`LiesFaceDown` excludes blocked), but its stars still use the
lying-head spot, so they sit at chest height between the two pawns.

