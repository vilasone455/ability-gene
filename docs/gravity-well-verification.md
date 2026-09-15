# Gravity Well runtime verification

Automated checks cover the cast clock, interruption, mass accounting, dragging arithmetic,
save/load of a committed implosion, collision routing and both projectile adapters
(`Tests/Gravity`), plus the eye's defs, the tuning constants, the quoted damage range, the
animation clip and the members both engines expose (`Tests/OriginBlade/ApiChecks`). None of that
runs a RimWorld combat simulation. The following runtime checks remain unperformed in this change.

1. Use **RimArts → Grant kit… → Gravity Well (attraction eye)** on a colonist, and install a
   traded eye by surgery with Medicine 8, the device and two medicine. Confirm normal sight, a
   3,200-silver market value, surgical removal returning the item, and that a second eye adds no
   second button and no second cooldown. Confirm attraction and repulsion cool down separately on
   a pawn carrying both.
2. Cast on a cell at 20 tiles and just past it; on a fogged cell, a wall cell and a cell behind a
   closed door. Confirm the 0.5-second opening, the 6-second hold, the mass/damage panel counting
   down, **Implode** ending it at once, **Cancel** ending it with no damage, and the automatic
   implosion at 6 s. Confirm 40 s of cooldown after each of those three endings and no cooldown
   when cancelling during the opening.
3. Interrupt an open well each way: move order, draft/undraft, stun, downing, killing the caster,
   removing the eye, a mental break, and blocking line of sight with a door. Each must end the
   well with no implosion, and each must still commit the cooldown. Confirm the caster cannot be
   ordered elsewhere, cannot attack, and that a move order cancels rather than being refused.
4. Stand a colonist, a raider, an animal, a thrumbo and a downed pawn at 7, 4 and 2 cells.
   Confirm everything but the caster is drawn in, that a healthy human near the edge can walk out,
   that the thrumbo resists deeper in, and that victims keep shooting, reloading and taking orders
   while sliding. Confirm allies are pulled and damaged the same as enemies.
5. Put a wall, a closed door and a diagonal corner between the well and a victim. Confirm no
   displacement through any of them, no collision damage, and no damage to the shielded pawn on
   implosion. Open the door mid-channel and confirm the pull begins.
6. Pre-position chunk piles, a corpse, dropped weapons and a minified building inside the core.
   Confirm whole stacks move without splitting or duplicating, that item identity, quality and HP
   survive, that the panel's mass matches 15/30/45 damage at 0/100/200 kg, and that pawn
   inventories and equipped weapons are neither moved nor counted. Pick an item up mid-drag, kill
   a victim mid-drag, and confirm the count follows.
7. Overlap two wells. Confirm each thing is moved by one well only, that its mass is counted once,
   and that a thing already held by a retrieval hook stays with the hook.
8. Shoot through the field from both sides with a bolt-action, a minigun and a sniper rifle, in
   vanilla and under Combat Extended. Confirm the curve, that damage, remaining range and the
   combat-log shooter survive it, that a bent shot can hit either side, that cover on the way in
   still stops it, that a shot reaching the core vanishes without adding power, and that rockets,
   grenades, mortar shells, fire and gas are unaffected.
9. Save and load during the opening, mid-channel, on the tick of the implosion and during the
   recovery. Confirm the well resumes, the implosion never fires twice, the cooldown survives, and
   nothing is lost or duplicated. Repeat with the game paused, with the map switched away and
   back, and with the map abandoned while a well is open.
10. Watch the clip at animation speeds above and below 1: the hand must open, hold for the whole
    channel and close on the implosion, with no leftover `AM_InAnimation` job afterwards. Confirm
    the core, bands, debris, warp, brightness and hum all respond to gathered mass, and that the
    mod still loads with Melee Animation absent, where the button is present but disabled, and
    with the distortion shader unavailable, where the warp is skipped and the rest still draws.

Build: `dotnet build Source/RimArt/RimArt.csproj -c Release` and
`dotnet build Source/RimArt.MeleeAnimation/RimArt.MeleeAnimation.csproj -c Release`.
Checks: `python3 validate.py`, `dotnet run --project Tests/Gravity/Gravity.csproj`, and
`dotnet run --project Tests/OriginBlade/ApiChecks/ApiChecks.csproj`.
