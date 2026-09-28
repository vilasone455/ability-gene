# Flying Thunder God: how the kit is earned

Abilities agreed 2026-09-27 (Minato's hero kit). The earning sections (unlock order, awakening,
practice) predate the Echo framework and are replaced by his Echo entry in docs/hero-echo.md
(agreed 2026-09-27: Melee 12, Intellectual 10, 30 thrown-kunai kills, Kind, upkeep 10). All five
abilities were built on 2026-09-28 and are granted by the Echo while manifested (see "Built" at the
end for the rules the port had to decide). Every number is a placeholder, in XML, until they are
played. The pictures are the four sketches under **Kunai belt** in the VFX lab
(`Tools/VfxLab/web/sketches/kunai-*.js`); each sketch header carries the same numbers as this page.

The kit is earned, like Origin: Blade, and sits on top of the kunai belt. The belt stays what it
is now: Smithing research, craftable, *throw kunai* only. A pawn who uses the belt enough is
offered the kit once. "Origin:" is the blade's name only, so this kit is called Flying Thunder God.

## Unlock order

| # | Ability | Unlock condition |
|---|---|---|
| 0 | throw kunai | Wear a kunai belt (in the game now) |
| 1 | flying thunder god | Melee 12, 100 kunai hits on hostile pawns, then accept the awakening letter |
| 2 | sealing touch (passive) | 25 jumps to a hostile pawn |
| 3 | flying thunder god: chain | 25 jumps to a hostile pawn (same moment as sealing touch) |
| 4 | guiding thunder | 60 jumps to a hostile pawn |
| 5 | rasengan | Melee 16 and 20 one-hour practice sessions. Does not need the awakening |

Rules for the counters:

- A kunai hit counts when a thrown kunai damages a hostile pawn that is standing. Hits on animals
  being hunted, downed pawns, colonists, prisoners and buildings do not count.
- A jump counts when its target is a kunai stuck in a hostile pawn. Jumps to a kunai on the ground
  do not count, so the count cannot be raised inside the base.
- Counts are per pawn, saved on the pawn, and shown on the belt's gizmo tooltip
  ("Kunai hits 63 / 100", later "Jumps 12 / 25").

## The awakening

When a pawn reaches Melee 12 and 100 counted hits, a letter asks whether to awaken. Declining
costs nothing and the letter can be reopened from the pawn's belt gizmo.

Accepting is permanent:

- Psycasts and psylinks are removed, and the pawn can never equip a ranged weapon. This is the
  same cost as Origin: Blade.
- A pawn holds one earned kit. A pawn with Origin: Blade is not offered this one, and the other
  way round.

Abilities 1 to 4 need a kunai belt worn. Without the belt they are hidden, not lost. Rasengan
needs no belt for its touch-range form.

Proposed letter text:

> **{PAWN} has understood the formula.** After a hundred thrown kunai, {PAWN} can write the
> Flying Thunder God seal on their own blades and jump to it. Taking this path is permanent:
> {PAWN} loses any psylink and psycasts and will never use a ranged weapon again.
> *Awaken* / *Not now*

## Abilities

Labels and descriptions are written as they would appear in game.

### throw kunai (in the game now)

Range 14.9, line of sight, 0.3 s warmup, 1.5 s cooldown, 12 stab, 18% armor penetration. A hit
can leave the kunai stuck in the target. Every ability below reads those stuck kunai, and kunai
lying on the ground.

No kunai regeneration for Minato (decided 2026-09-27). His supply is the belt's 6, spare kunai
carried for the reload job, the jump to a ground kunai (which returns it to the belt) and sealing
touch. Ground kunai stay as jump targets, so how many he has decides how many anchors he can place.
Sasuke's belt regenerates conjured kunai instead; see the Amenoyodomi sketch header.

### flying thunder god

> Jump to one of your own kunai, up to 29.9 tiles away, with no line of sight needed. If the
> kunai is stuck in someone, you appear behind them and attack at once for 150% melee damage;
> the kunai stays in. If it lies on the ground, you appear on it and it returns to your belt.

Target: own kunai, in a pawn or on the ground. Range 29.9. No warmup. Cooldown 20 s.
Sketch: *Flying Thunder God (sketch)*.

### sealing touch (passive)

> Your melee hits leave the seal on whoever they strike. A sealed pawn counts as holding one of
> your kunai for every Flying Thunder God ability. You can keep 3 seals at a time; each fades
> after one in-game day.

No gizmo. A 4th seal replaces the oldest. Seals are lost if the pawn leaves the map. No new
picture: the seal glow the sketches already put on a stuck kunai is drawn on the pawn's body.

### flying thunder god: chain

> Jump to every marked enemy within 29.9 tiles, nearest first, up to 5, one jump every 0.24
> seconds. At each you appear behind them and attack for 100% melee damage. Needs at least 2
> marked enemies. You stay at the last one.

No target to pick. No warmup. Cooldown 60 s. The caster stays at the last target (decided
2026-09-27); the sketch's "jumps back to the start" option is not built.
Sketch: *Flying Thunder God: Chain (sketch)*.

### guiding thunder

> Choose one of your own kunai, up to 29.9 tiles away. For 6 seconds, every projectile that would
> hit you comes out at that kunai instead: it hits the pawn the kunai is stuck in, or lands on
> the kunai's tile. Explosives go off there. You cannot move or attack while it holds. Ends early
> after 12 projectiles. Does not stop melee.

Target: own kunai. Duration 6 s. Cooldown 45 s. Barrier radius 1.3 tiles.
Sketch: *Guiding Thunder (sketch)*.

### rasengan

> Form a ball of spinning chakra over 0.6 seconds, then drive it into an adjacent target: 30
> blunt damage, 40% armor penetration. The target is thrown 3 tiles and stunned for 2 seconds. If
> something solid stops it sooner it takes 10 more damage. If the target holds one of your kunai
> and you know Flying Thunder God, you can cast this from up to 29.9 tiles: you form the ball
> where you stand, then jump to them and strike.

Target: a pawn. Range 1.9, or 29.9 to a marked pawn after the awakening. Warmup 0.6 s.
Cooldown 30 s. After a jump the caster lands behind the target, so the target is thrown back
toward where the caster came from (decided 2026-09-27).
Sketch: *Rasengan (sketch)*.

**Practice.** A pawn with Melee 16 gets a *Practise rasengan* order on itself while undrafted.
One session is one in-game hour standing in place, gives Melee XP at the training rate, and
counts once. After 20 sessions the ability is learned. Sessions need no item and no bench. The
belt is not needed and the awakening is not needed, so a melee pawn who never throws a kunai can
still learn the touch-range form.

## What this needs in code

- A per-pawn counter component (hits, jumps, practice sessions), patterned on
  `GameComponent_BladeStudy`.
- An awakening letter patterned on `ChoiceLetter_OriginBladeAwakening`, and a shared "one earned
  kit per pawn" check used by both.
- A hediff or pawn comp that holds the learned abilities and hides 1 to 4 when no belt is worn.
- A practice job patterned on `JobDriver_StudyBlade`.
- The seal from *sealing touch* as a hediff that the ability target checks accept next to
  `AG_EmbeddedKunai`.

## Open decisions

1. ~~Chain ending~~: decided 2026-09-27, stay at the last target.
2. ~~Rasengan after a jump~~: decided 2026-09-27, land behind the target.
3. Whether the awakening cost should differ from Origin: Blade's. It is copied here so that the
   two earned kits follow one rule.
4. The three thresholds (100 hits, 25 jumps, 60 jumps) and 20 practice sessions are guesses. With
   a 6-kunai belt that has to be refilled, about 10 counted hits per raid seems likely, which
   makes 100 hits about 10 raids. That rate is an estimate, not measured.

## Built (2026-09-28)

Code in `Source/RimArt/ThunderGod/Kit`, defs in `AG_Minato_Abilities.xml`, `AG_Minato_Hediffs.xml`,
`AG_Minato_Jobs.xml` and `AG_Echo_Minato`. Tests: `-rimarttest=minato`. Rules the doc left open, as built:

- **Marks.** A mark is one of his sealed kunai stuck in a pawn, a pawn with his seal, or a sealed kunai
  on the ground (flat or planted). Clicking the pawn, the kunai item or the cell all work. His kunai
  are sealed only while he is manifested; any Minato Host can use any sealed kunai.
- **Landing.** In a pawn: the free cell next to it that is most behind it, seen from where he stood
  (straight behind first, then the diagonals, then the sides; front cells last). Decided again on
  arrival, so a pawn that moved is still landed behind. No free cell: the jump is refused.
  On the ground: the kunai's cell, or its nearest free neighbour if someone stands on it.
- **The cut.** His own melee attack (weapon or fists), applied without the hit roll: it cannot miss.
  x1.5 for the jump, x1.0 per chain link; armour penetration as the attack makes it. It is a landed
  melee hit, so it also seals. A non-hostile pawn is landed behind and not cut.
- **Ground kunai picked up.** Into the belt as far as it has room (the seal goes, as when loading);
  the rest of the stack into his inventory, still sealed. No belt needed for any Minato ability.
- **Sealing touch.** Every landed melee hit in hero form. A hit on a sealed pawn renews its day. The
  limit of 3 counts every seal on every map; the 4th replaces the oldest. Drawn as a small turning
  ring of script round the feet and a gold glow at the chest.
- **Chain.** Marked hostiles that are standing (not downed), within 29.9 cells. "Nearest first" is
  greedy: nearest to him, then nearest to that one. A pawn that died, went down or has no free cell
  when its turn comes is skipped; the next jump keeps its time.
- **Guiding Thunder.** Active from the click (no gap while the ring is written). A projectile is taken
  when it reaches the ring if its hit roll hit him, or if it is an explosive or a mortar shell coming
  down inside the ring; a shot that missed him flies on. It comes out at the mark already hitting:
  the pawn (whoever fired it), or the kunai's cell and whoever stands there. If the marked pawn dies
  the shots land where it fell; if the ground kunai is picked up, the barrier ends. The cast job holds
  him (no move orders) for the 6 s. Combat Extended projectiles are not taken.
- **Rasengan.** One ability, verb range 29.9. A marked pawn not next to him: the ball forms where he
  stands, then he jumps behind it. Any other pawn: he walks up to it first (the player can call the walk
  off with any order: no cooldown, no charge; from the warmup on the job holds); if it has left touch range
  (1.9) when the ball is formed, the ball is lost (cooldown and charge spent). The damage lands on the
  release (0.3 s after the ball reaches the body); the grind stuns it from the first contact. The
  throw goes one cell at a time away from him and stops before a cell it cannot stand in or see
  (walls, buildings, the map edge; pawns do not stop it); stopped short adds the 10. Its cell changes
  once, on landing. A pawn killed by the hit is not thrown. Body size does not change the distance.
- **Not built.** Melee Animation clips (RimArt_RasenganForm / Thrust exist but are not played: the
  caster stands and the ball is at chest height), sounds, AI use. Not played by hand; save/load in the
  middle of a cast is untested.
