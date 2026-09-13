# Fūma Shuriken runtime verification

Automated checks cover geometry, preview/sweep agreement, cooldown arithmetic, damage
falloff, installed API signatures, and animation/definition contracts. These do not run a
RimWorld combat simulation. The following runtime checks remain unperformed in this change.

1. Use **RimArts → Grant kit… → Fūma Shuriken** on an armed pawn. Verify direct equipping,
   preservation of the old weapon in inventory (or on the ground), and refusal when already
   holding one. Also craft at either smithy after Smithing with Crafting 6 and 80 steel. Confirm quality,
   2 kg mass, 200 base HP, and normal melee attacks; only the throw command throws it.
2. Throw through a row of unarmored pawns. Verify 30/24/19/15/12/10/8 base damage, one
   contact each, attribution in the combat log, and inclusion of allies and downed pawns.
   Move a pawn into and out of the path during flight; hits should follow its current cell.
3. Aim cardinally and diagonally. Confirm the yellow preview and selected landing cell,
   red friendly cells, a 12-tile limit, closed-door/wall obstruction, diagonal corner
   blocking, and passage over low cover and through open doors. Close a door during flight.
4. Test armor and personal shields; absorbed contact still consumes one falloff step.
   Test an area projectile shield; the weapon drops on the incoming side of the shield.
5. Throw an Excellent weapon with damaged HP and record its Thing ID. Recover using Equip,
   then using a retrieval-hook pull followed by Equip. Confirm the same ID, quality, HP,
   and components. No automatic recovery; no free cooldown reset when another pawn equips it.
6. Cancel or down the thrower before release: the weapon follows ordinary equipment rules,
   with no projectile or cooldown spent. Cancel, down, kill, or move the thrower after
   release: the weapon continues and lands exactly once.
7. Save/load during warmup, immediately after release, after one hit, and after landing.
   Confirm one weapon, no repeated contact, correct remaining warmup and cooldown, and no
   exceptions. Force a failed landing placement; clear space and verify the held item drops.
8. Redirect with Vector Edit or Shinra; capture/release with the halving membrane. Verify
   trajectory/speed changes, no damage while held, and no loss/duplication of the weapon.
9. Inspect folded carry, open melee/ground appearance, all four facings including diagonals,
   ring grip, unfolding, spin, and release at tick 48. Test animation speed settings above
   and below 1; the blades must disappear at actual release, without duplicate equipment art.
10. Repeat essential combat/recovery checks with Melee Animation absent, CE absent, and both
    present. Under CE, confirm melee tool stats, shields/armor, and no ammo consumption.

Build: `dotnet build Source/RimArt/RimArt.csproj` and
`dotnet build Source/RimArt.MeleeAnimation/RimArt.MeleeAnimation.csproj`.
Checks: `python3 validate.py`, `dotnet run --project Tests/Fuma/Fuma.csproj`,
`dotnet run --project Tests/RetrievalHook/RetrievalHook.csproj`, and
`dotnet run --project Tests/OriginBlade/ApiChecks/ApiChecks.csproj`.
