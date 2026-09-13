# Retrieval hook belt

A craftable belt that fires a padded net on a tether and pulls a **downed friendly person or an
item** to the wearer. Pulling an injured person worsens one wound and removes its bandage.

| Setting | Value |
|---|---|
| Range | 15 tiles, line of sight required |
| Item capacity | 20 kg per shot; heavier stacks are split |
| Reload | 600 ticks (10 seconds) of standing still |
| Ammunition | None |
| Research | Machining |
| Crafting | Machining table, Crafting 5, 12,000 work |
| Materials | 60 steel, 2 components, 20 cloth |
| Equipment | Belt layer, 2 kg, no armor, 2-second equip delay |
| Cooldown | None; the tether is the gate |

No new VEF integration and no Pirates dependency. Development mode: **RimArts → Grant kit... →
Retrieval hook belt**, plus **Retrieval hook: reload belt** and **Retrieval hook: unload belt**.

## Controls

- Wearing the belt grants **Retrieval hook**. Taking it off removes the ability.
- Aim at a target. The cursor shows what will be pulled (`Pull 40 of 75 (20 kg of 20 kg)`), the
  injury warning for people, or the reason a target is refused.
- Firing takes a 0.5-second warmup. The net flies at 0.6 cells per tick (25 ticks at 15 tiles).
- After any shot, the ability is disabled and the belt shows **Reel in tether (N%)**. Reeling is
  a stationary job. If it is interrupted, progress stays on the belt and the order has to be
  given again. It never resumes by itself.
- Drafted and undrafted player-controlled pawns can use it, including pawns incapable of
  violence. Manipulation is required. AI pawns never use it, and no generated pawn wears one.
- A new belt starts loaded. Loaded state and reel-in progress are saved on the belt, so moving
  it to another pawn, dropping it, or loading a save does not reload it.

## Targets

**People:** alive, downed, humanlike, spawned, and a colonist, colony slave, or member of an
allied faction. Refused: prisoners (any faction), neutral or hostile pawns, animals, mechanoids,
the wearer, carried or contained pawns, and pawns someone else has reserved (for example a
doctor already tending them). The mass limit does not apply.

**Items:** spawned, haulable weapons, apparel, medicine, food, resources (raw and manufactured)
and minified buildings. Refused: chunks, corpses, installed buildings, plants, contained items,
and items reserved by someone else. A stack is split only when the net connects: the moved part
is the most whole units that fit in 20 kg (`floor(20 / unit mass)`); the rest stays.

## The pull

1. **Launch.** The wearer is held by a pull job. Nothing is split, reserved or moved.
2. **Connection.** The target is checked again: eligibility, reservation, range and line of
   sight. A failed check is reported as a miss. A pawn target is reserved by the wearer's pull
   job and its current job is stopped; it cannot take new jobs while dragged. An item stack is
   split and the moved part is held by the pull.
3. **Drag.** The target moves one cell every 8 ticks along a straight line toward the wearer and
   stops in the cell beside them. A pawn stays spawned, so its health keeps updating; its drawing
   is interpolated between cells. The target is re-checked every tick. A cell that has become a
   wall, a closed door, or a diagonal squeeze between walls stops the drag at the last valid cell.
4. **Rest.** A pawn is released where it is. An item is placed at its cell, merging with a
   matching stack nearby. If placement fails, the item stays held and placement is retried every
   tick. The item is never deleted or duplicated.

Moving the wearer, giving them another order, the wearer being downed, stunned, killed or
having a mental break, or removing the belt interrupts the pull. Every launched shot needs the
reel-in, including misses and interrupted shots.

## Injury penalty

Applied **once, when a dragged pawn first moves a cell**, even if the pull is interrupted after
that. Not applied if the pawn never moves (blocked at once, or already beside the wearer).

1. Eligible wounds: non-permanent injuries on a present, external, organic body part (not on an
   artificial part or its children). Tended wounds that can bleed are chosen first, at random
   among them. If there are none, any eligible wound is chosen at random. No eligible wound
   means no penalty.
2. Severity increases by up to **1**. The increase is reduced in 0.01 steps until it would not
   make `ShouldBeDead()` true and would not bring the part's health to 0. Zero is allowed.
3. The same wound's tend timer is set to -1 and its tend quality to 0, and the health tracker is
   notified. The wound shows as untended and doctors re-tend it normally.

Wound age, infections and other injuries are unchanged. A wound that had stopped bleeding with
age keeps its age, but a higher severity can make it bleed again, and later blood loss can be
fatal. The affected wound is reported in the message when the pull ends.

## Save and load

Pulls are saved in a map component. A save during launch resumes the flight. A save during a
drag keeps the path, cell, step progress and whether the penalty was applied, so it cannot apply
twice. A held item is saved inside the pull. Reservations and the wearer's job are saved by the
game. Reel-in progress is saved on the belt. Existing saves need no migration.

## Automated checks

```bash
dotnet run --project Tests/RetrievalHook/RetrievalHook.csproj
dotnet run --project Tests/OriginBlade/ApiChecks/ApiChecks.csproj
```

`Tests/RetrievalHook` runs the game-free rules in `RetrievalRules.cs`: target eligibility for
every listed inclusion and exclusion, the 20 kg boundary (20.00 kg allowed, 20.01 kg refused,
0.5 kg × 40 and 0.1 kg × 200 not rounded down), stack conservation for every split, wound
selection order, the severity cap and its 0.01 steps, blocked paths, and the once-only penalty
including a resume from saved fields. `ApiChecks` checks the Harmony targets, the fields and
health checks the penalty uses (`Hediff.severityInt`, `HediffComp_TendDuration.tendTicksLeft`
and `tendQuality`, `ShouldBeDead`, `Notify_HediffChanged`, `GetPartHealth`), and the belt and
ability def numbers.

## In-game test list

Not covered by the automated checks; run with the belt from the development grant:

- Craft the belt at a machining table after Machining; confirm cost, work, Crafting 5.
- Equip and remove: the ability appears and disappears; other belt abilities (stasis belt,
  drone control rig) still work.
- Targets: downed colonist, slave and ally are pulled; prisoner, neutral, hostile, animal,
  mechanoid, corpse, chunk, plant and a carried pawn show a reason.
- Range: a target at exactly 15 tiles is accepted, beyond is refused.
- Blocked path: build a wall across the line during the drag; the target stops at the last cell.
- Interruption: move the wearer, draft/undraft, remove the belt, down the wearer mid-drag.
- Partial stack: 75 steel (0.5 kg) pulls 40 and leaves 35; totals match after it lands.
- Field re-tending: pull a tended colonist; the wound is untended and a doctor tends it again.
- Critical injuries: a pawn one hit from death or part loss gets a smaller or zero increase.
- Injury-free downed pawn (for example downed by a drug): no penalty, no wound message.
- Save and load during launch, during a drag, and during a partial reel-in; check for no
  duplicated items, no second penalty, no stuck jobs on either pawn, and no free reload.
