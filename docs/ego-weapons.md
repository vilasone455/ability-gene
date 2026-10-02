# E.G.O. weapons and Corrosion

Design agreed 2026-09-30. The Corrosion core is built (2026-10-02, see Code); Magic Bullet, Solemn
Lament and Paradise Lost have their rules and defs (2026-10-02, see each weapon's Built); Mimicry has
pictures only. The corrosion of Solemn Lament, Mimicry and Paradise Lost changed 2026-10-02 (see the
balance test under Rule 2). Post-v1 (the 2026-10-20 list is full). Source: Project Moon
(Lobotomy Corporation, Library of Ruina, Limbus Company). Every number below is a placeholder and,
when built, an XML field on the weapon's `CompProperties_EgoWeapon`, never a C# constant.

The purpose of the first piece is bait: a Project Moon weapon that a fan recognises from the
Workshop thumbnail. Magic Bullet is that piece. Corrosion is designed once, shared, because four
E.G.O. weapons are planned. All four live in `Source/RimArt/Ego/`, so the shared rules sit in
`Ego/Kit/`, not `Shared/` (which is for code used by two or more kits).

## Terms

| Term | Meaning |
|---|---|
| E.G.O. | a weapon that carries a piece of an Abnormality; its rules come from that Abnormality |
| Corrosion | the Abnormality takes the wielder: a mental state in which the weapon fires itself at the nearest pawn |
| Overclock | the wielder uses the corroded attack on purpose, against hostiles only, and pays in mood |
| Tier | the Abnormality's risk level in Lobotomy (HE, WAW, ALEPH); shows only in the corrosion numbers |

## Corrosion, the shared system

What it is in the source: an E.G.O. is a piece of an Abnormality. A wielder whose mind is too weak
for it is taken over. Limbus rolls it on Sanity bands (no chance above -22 SP, then 25 %, 75 %,
certain at -45), a corroded Sinner ignores orders and uses only the corrosion skill on
indiscriminate targets, and Overclock is the safe paid version. Lobotomy has no corrosion in
play (gear requirements are a hard gate); Ruina has none. So the system below is Limbus's rule,
applied to every E.G.O. weapon here. That is a design choice, not a port.

What it does in RimWorld: one thing. The weapon takes the pawn. For a while the pawn is not the
player's, and the weapon's own attack fires at whatever is nearest. Everything else is a parameter.

### Rule 1, the roll

On every use of the weapon (a shot, a swing), roll against the shooter's mood. The bands are the
pawn's own mental-break thresholds, so traits that move those (Neurotic, Sanguine, Depressive)
change the risk for free, and the player can read the risk off the mood bar before ordering the
shot.

| Mood at the moment of use | Chance the use corrodes | XML field |
|---|---|---|
| above the minor break threshold | 0 %, no roll | |
| minor to major | 25 % | `corrosionMinor` |
| major to extreme | 75 % | `corrosionMajor` |
| below extreme | 100 % | `corrosionExtreme` |
| requirement not met (skill under level) | one band worse | `requirementSkill`, `requirementLevel` |

The requirement row is the Lobotomy gear rule (Justice 3, Fortitude 5) mapped to a RimWorld skill.

### Rule 2, the corroded state

A `MentalStateDef` (`AG_EgoCorroded`), the same system as Berserk, so the game handles "ignores
orders" and shows it on the pawn.

- Control: ignores orders, drafting, jobs. Faction, stats, gear, relations, skills are unchanged.
- Movement: the pawn holds its cell, unless the weapon's action says it walks. A walking pawn goes
  to the nearest living pawn of any faction and stays next to it (Solemn Lament, Mimicry). Overclock
  never walks: the player starts it where the hostiles are.
- Every `corrodedInterval` the weapon's corrosion action fires at the nearest living pawn on the
  map, any faction. Nearest, not enemy: a raider, a friend, the dog.
- Any per-weapon counter (Magic Bullet's seven) keeps running while corroded.
- Ends on `corrodedDuration` or when the pawn is downed. Then `AG_EgoExhausted` for
  `exhaustionHours` (consciousness capped, moving 50 %).
- A corroded pawn who hits a colonist gets vanilla's "harmed me" opinion hit. That is the
  consequence; no extra thought.
- Look: the Abnormality bleeds through, drawn over the pawn as VFX quads, the Vergil pose trick.
  Each weapon supplies its own. No Melee Animation clips.

Balance test (2026-10-02): moving everyone out of reach must not be a free answer to a corrosion.
Each one has to cost the colony something when it gets away from it.

| Weapon | Why stepping away does not end it | What getting away costs |
|---|---|---|
| Magic Bullet | the line reaches 40 cells through walls | nothing gets away; down the wielder or take the hits |
| Solemn Lament | the cloud walks with the wielder | outrunning it, carrying the downed out through it, or downing the wielder |
| Mimicry | the wielder hunts the nearest pawn | shooting your own colonist (each hit shrinks the arm) or downing them |
| Paradise Lost | the ring grows 3 cells per firing | clearing most of a base, carrying out everyone who cannot walk, or downing your best shooter |

Before the change, the other three held still with a reach of 3 to 6 cells and were answered by
drafting everyone a few cells away; Solemn Lament's could not kill and Mimicry's mostly hit the
enemy it was already fighting.

### Rule 3, Overclock

A button on the weapon (`Command_EgoOverclock`), disabled while corroded or while the pool of
hostiles in range is empty.

- Runs the corrosion action `overclockCount` times at `overclockInterval`, aimed only at hostiles.
- Then a mood thought `AG_EgoOverclocked`, `overclockMood` for `overclockMoodDays`.
- The loop: a happy colony sits above the minor line most of the time, so corrosion would rarely
  fire on its own. Overclock is the paid strong mode, and each use pushes the pawn toward the bands.

### What each weapon supplies

- The corrosion action: what one firing of the corroded weapon is (`IEgoCorrosionAction.Fire(pawn,
  target)`), and whether it takes a target at all (Solemn Lament's is an area on the pawn).
- Whether the corroded pawn holds or walks to the nearest pawn.
- The look: what draws over the pawn while corroded.
- Its own numbers for the three rules. A WAW gun and an ALEPH sword do not corrode the same.

### Left out on purpose

- Full transformation into the Abnormality (the Lobotomy lore version). A per-weapon boss form and
  a lot of work. The state gets a hook (`onFullCorrosion`, off by default) so a later weapon can
  add it without touching the others. Paradise Lost is the one that would (see Weapon 4).
- Per-use mood drain on the weapon. One more field (`useMood`) if playtests show corrosion never
  fires in a happy colony.

### Code

Built 2026-10-02 in `Source/RimArt/Ego/Kit/`:

| File | What |
|---|---|
| `CompEgoWeapon.cs` | `CompProperties_EgoWeapon` (every number above, plus `actionClass` and `overclockRange`) and `CompEgoWeapon`, the weapon's CompEquippable: rolls in `Notify_UsedWeapon` (once per shot or swing that went off) and gives the Overclock button |
| `EgoCorrosion.cs` | the band, the requirement shift, the roll, `Corrode`, the nearest pawn / nearest hostile, `Fire`, `Exhaust`, `PayOverclock` |
| `EgoCorrosionAction.cs` | what a weapon supplies: `Fire(wielder, weapon, target, hostilesOnly)`, `TakesTarget` (false for an area), `DrawCorroded` (the look) |
| `MentalState_EgoCorroded.cs` | the state, its think-tree job giver and the hold job (not vanilla Wait, whose auto-attack would punch adjacent hostiles) |
| `JobDriver_EgoOverclock.cs` | the Overclock job and `Command_EgoOverclock` |
| `MapComponent_EgoCorrosion.cs` | calls each weapon's `DrawCorroded` for every corroded or overclocking pawn on the shown map; its tick corrodes the pawns whose roll passed once their burst is over |
| `Tests_EgoCorrosion.cs` | `-rimarttest=ego`: bands and requirement, a burst corrodes (after its last shot) and the state fires at the nearest pawn then exhausts, going down ends it, the weapon leaving the hands ends it and Corrode never replaces a state, Overclock aims only at hostiles in range, never rolls (even when the action rolls itself), pays mood, and at three memories renews the oldest |
| `Ego<Weapon>Corrosion.cs` (per weapon; Magic Bullet's, Solemn Lament's and Paradise Lost's written) | the action and the look |

Defs: `AG_EgoCorroded` (MentalStateDefs/AG_Ego_MentalStates.xml), `AG_EgoExhausted`, `AG_EgoOverclocked`,
`AG_EgoCorrodedHold`, `AG_EgoOverclock`, and the think node in `1.6/Patches/AG_Ego_ThinkTree.xml`.

Settled while building:

- The action is an abstract class, not an interface: `DrawCorroded` has an empty default.
- A pawn already in any mental state does not roll, and `Corrode` refuses one, so corrosion never
  replaces Berserk or False Face whoever calls it.
- A roll that passes corrodes the pawn once its verb has finished the burst (the same tick for a
  single shot), not inside the shot: the roll runs inside `Verb.TryCastNextBurstShot`, and starting
  the state there would stop the attack job while the burst goes on at the old target.
- Uses of the verb made by the weapon's own action never roll (a flag while the action fires), so
  Overclock and the corroded firing cannot corrode even when the action shoots through the verb.
- At three `AG_EgoOverclocked` memories the fourth payment renews the oldest with the paying weapon's
  mood and days; the game alone would renew it and keep the old numbers.
- The first corroded firing comes one `corrodedInterval` after the start; Overclock's first fires at
  once and the job ends one `overclockInterval` after the last.
- The state also ends when the weapon leaves the pawn's hands, and on sleep (like downed).
- Exhaustion caps consciousness at 80 % (placeholder in the hediff def). A second corrosion keeps the
  longer of the two exhaustion times.
- The Overclock thought stacks up to 3 at 0.75 each after the first; its mood and days are the
  weapon's (`Thought_Memory.moodOffset`, `durationTicksOverride`), not the def's.
- Overclock aims at hostiles that are not downed; Corrosion aims at the nearest pawn including downed
  ones.
- A weapon whose attack does not go through a vanilla verb calls `EgoCorrosion.Roll` itself.

The walk, built with Solemn Lament (2026-10-02): `EgoCorrosionAction.WalksToNearest` (default false,
hold). When true, `JobGiver_EgoCorroded` gives `AG_EgoCorrodedWalk` (`JobDriver_EgoCorrodedWalk`, to
touching the target, asked again every second) toward the nearest living pawn of any faction, downed
ones included, and the hold once next to it. The firing stays in the state. Magic Bullet and Paradise
Lost keep the hold. The action also gets `Begin` and `End`, called when a corrosion or an Overclock
starts and ends, for a picture that outlives single firings (Solemn Lament's coffin).
`EgoRound.Hit` (a hit with a never-spawned round's damage, logged as a bullet's) is shared by Magic
Bullet's line and Solemn Lament's black shot.

Class names start with `Ego` (`EgoMimicry`, not `Mimicry`): `AG_Mimic` is already Vergil's decoy.

## Weapon 1: Magic Bullet

Der Freischütz, HE Abnormality, WAW gear. Weber's opera: seven bullets from the devil, six go
where the shooter aims, the seventh where the devil aims, at the marksman's bride.

Source, checked 2026-09-30:

- Lobotomy: Request work fires a bullet across the whole floor in a straight line, 80 Black
  damage to everything in its path. Every seventh Request lowers the Qliphoth counter and the
  guide says the seventh bullet "will not fire where you desire". Story text: "the last bullet
  would puncture the head of his beloved." The weapon: 20 to 22 Black, range 50, the shot
  penetrates every entity in the room, enemies or allies. The weapon itself has no seventh-shot
  rule.
- Ruina: last abnormality on the Floor of Technological Sciences. Page rule: every seventh melee
  or ranged page targets a random character, allies included.
- Limbus: an Identity (Lobotomy E.G.O::Magic Bullet Outis), not an E.G.O. item. Count 0 to 7,
  reset at 7 with a damage bonus; the ally hit is a 50 % indiscriminate roll at 10 SP or less, not
  tied to the seventh. No corrosion form exists, so the hunter look below is ours.

| Rule | Mechanic | Placeholder | XML field |
|---|---|---|---|
| Piercing line | Every shot flies a straight line through walls and hits every pawn on it, allies included. Never misses, ignores cover. | range 40, 18 dmg | `range`, `damage` |
| The seventh | The count sits on the gun and survives reloads. The seventh shot cannot be aimed. It goes to the pawn on the map the shooter has the highest opinion of. It still pierces everyone between. Count resets after. | 30 dmg, 20 s before shot one | `seventhDamage`, `seventhCooldown` |

The seventh's target, in order:

1. Highest `OpinionOf` from the shooter's side among living pawns on the map. In practice spouse or
   lover, then a close friend, then family.
2. Tie or nobody above zero: a bonded animal.
3. Nobody at all: the shooter.

Lobotomy says the beloved, Ruina and Limbus say random. The beloved is chosen because in RimWorld
a random ally has no sting and the player knows exactly who the colonist loves.

Gizmo: a counter like the Flame Gauntlet's Heat meter, showing the next shot number. On six it
names the seventh's target so the player can holster. Hiding it would be a kill without a choice.

Corrosion: bands 25 / 75 / 100, requirement Shooting 6, duration 30 s, interval 3 s, exhaustion
2 h. Action: one piercing line at the nearest pawn. Overclock: 6 lines in 6 s, mood -15 for 1 day.
Look: the hunter's coat and wide hat over the pawn, the rifle drawn longer, black smoke off the
muzzle.

Picture: a black streak through walls, the seventh red. No clips.

Overlap: none. Bank Shot is ricochet, Coil and Frost are damage types. Nothing here has a shot
that ignores line of sight or a weapon that turns on its owner.

### Built

Rules and def 2026-10-02 (`AG_EgoMagicBullet` in `1.6/Defs/ThingDefs/AG_Ego_Things.xml`, code in
`Ego/Kit/`). Not played; game tests `-rimarttest=ego: magic bullet` written, not run yet.

| File | What |
|---|---|
| `CompEgoMagicBullet.cs` | `CompProperties_EgoMagicBullet` (`seventhDamage`, `seventhCooldown`, `actionAim`), the count on the gun (saved with it), and the counter in the command bar |
| `EgoMagicBullet.cs` | `Beloved` (the seventh's target), `Crosses` / `Crossed` (who the line hits), `Walls` (holes for the picture), `Hit` |
| `Verb_EgoMagicBullet.cs` | Core's shooting verb with the projectile replaced by the line; unavailable while the gun rests |
| `EgoMagicBulletCast.cs`, `GameComponent_EgoMagicBullet.cs` | one shot from its aim to the end of its picture; the tick and the draw |
| `EgoMagicBulletCorrosion.cs` | the corroded action and the held look between shots |
| `Patches_EgoMagicBullet.cs` | `HeldWeaponHide` while the picture draws the rifle |
| `Tests_EgoMagicBullet.cs` | the line through a wall, a shot at a building, the seventh to the lover, its fallbacks, the count on the gun, corroded and Overclock |

The texture (`Textures/RimArt/Ego/MagicBullet.png`, from `make_ego_textures.py`) is the picture's
rifle part for part, at the size the picture draws it, with a 1 px outline.

Settled while building:

- The line's range is the verb's `range` (40) and a shot's damage is the round's
  (`AG_EgoMagicBullet_Round`, 18, armour penetration 0.3), so the info card shows them. The round is
  never spawned. Warmup 1.5 s, cooldown 2 s.
- The line runs from the shooter's cell centre through the target's centre. It hits every pawn whose
  cell it passes through (a corner graze does not count), downed pawns included, nearest first. All
  damage lands on the shot's tick; the picture's bullet takes 0.33 s to cross 40 cells.
- A shot aimed at something that is not a pawn (a turret, a mortar, a building, a wall) hits it too,
  for the same damage. Other buildings on the line are not hit; walls in its way get the picture's
  holes only. Without this a drafted colonist firing at will at an enemy turret did nothing and still
  counted toward the seventh.
- The seventh's line ends at the beloved: pawns behind them are not hit. With nobody (no other
  person above 0, no bonded animal) it hits the shooter. A tie at the top goes to a bonded animal if
  there is one, else to the nearest of the tied.
- The seventh can be fired by a corroded gun: corroded firings count. Overclock does not count and
  never fires the seventh, so its "allies are never hit" holds; its lines pass through non-hostiles.
- The 20 s rest after the seventh stops everything: shots, corroded firings and Overclock.
- On six the counter names the seventh's target, and a selected wielder shows a red line to them.
  While aiming, the verb draws the line the shot will take.
- A corroded or Overclock firing aims 0.45 s (`actionAim`) before it goes off, so the picture's aim
  plays. Shots in flight are not saved: a save in the 3 s of a picture loses the picture.

Not built: the hunter's coat and hat, the longer rifle and the smoke (the picture's corroded look
is the contract circle, veins and eyes). How a colony gets the gun is open (see Open).

## Weapon 2: Solemn Lament

Funeral of the Dead Butterflies, WAW. The white pair of handguns. Current Limbus bait (Yi Sang's
Identity, Season 7).

Source, checked 2026-09-30:

- Lobotomy: dual handguns, range 10, a shot about every 0.25 s, alternating White and Black
  damage: the White gun fires at the listed speed and the Black gun in between. Requires Justice 3.
  The Abnormality's breach: butterfly projectiles (10 to 15 White) and a coffin that releases
  butterflies for about 15 s, 3 to 4 White per second to everyone near. Killed employees are
  covered in butterflies before they fall.
- Limbus: Yi Sang inflicts Butterfly, a Sinking effect; ammo pool The Living and The Departed,
  capped at 20, Reload on skill end.

| Rule | Mechanic | Placeholder | XML field |
|---|---|---|---|
| The pair | One weapon item that draws two guns and alternates its own shots. White shot: no body damage, Butterfly stacks. Black shot: damage plus one stack. | range 10, black 9 dmg, white 2 stacks, black 1 stack, 0.25 s between shots | `range`, `blackDamage`, `whiteStacks`, `blackStacks`, `shotInterval` |
| Butterfly | A stacking hediff. Each stack lowers consciousness. At the cap the pawn goes down covered in butterflies. Stacks fade. The guns never push past the cap, so they never kill; only the corroded coffin does (see Corrosion). | cap 10, -7.5 % consciousness per stack, -1 stack per 10 s | `butterflyCap`, `butterflyConsciousness`, `butterflyFade` |

The game downs a pawn under 30 % consciousness (`PawnCapacitiesHandler.CanBeAwake`). At -7.5 % an
unhurt pawn goes down at 10 stacks (25 %) and stands at 9 (32.5 %), so the cap is the down point.
At the first placeholder, -8 %, it went down at 9 and the cap never mattered. A pawn already hurt
goes down sooner.
| Ammo | A pool, reload on empty. | 20 shots, reload 3 s | `ammo`, `reloadSeconds` |

No dual-wield mod dependency. The alternating fire is the weapon's own rule. If a dual-wield mod
is loaded the item can be flagged one-handed so it pairs; that is a compat line, not the design.

Corrosion: bands 25 / 75 / 100, requirement Shooting 4, duration 30 s, interval 1 s, exhaustion
2 h. Action: the coffin. A butterfly cloud on the corroded pawn; every other pawn within radius 3
takes 1 stack per second. The wielder takes none, or they would go down at 10 s and end the state.
The action takes no target, but the pawn walks: it goes to the nearest pawn of any faction and the
cloud goes with it.

The funeral: a pawn already down inside the cloud keeps taking stacks past the cap, and at 20
stacks it dies covered in butterflies (Lobotomy: killed employees are covered in butterflies before
they fall). A pawn caught from the start goes down at about 10 s and dies at about 20 s. Carrying
it out takes a rescuer into the cloud, where the rescuer takes stacks too. Stacks past the cap fade
like the others once the pawn is out.

| Corrosion field | Placeholder | XML field |
|---|---|---|
| Coffin radius | 3 cells | `coffinRadius` |
| Death | 20 stacks | `funeralStacks` |

Overclock: the coffin for 5 s at hostiles only (allies inside the radius are skipped), mood -15 for
1 day. It holds still and stays under the cap: at most 5 stacks, so it never downs or kills by
itself. Look: a monochrome butterfly over the head, the Abnormality's face.

Changed 2026-10-02: the coffin used to stay where the wielder stood, last 15 s and never kill. The
colony drafted everyone 3 cells away, and the pawns it downed were up again 10 to 20 s after it
ended. The cloud now walks, lasts 30 s, and kills a downed pawn left in it.

Overlap: none. Nothing in the mod has a stacking non-lethal ranged debuff.

### Built

Rules and def 2026-10-02 (`AG_EgoSolemnLament` in `1.6/Defs/ThingDefs/AG_Ego_Things.xml`, the
`AG_EgoButterfly` hediff in `AG_Ego_Hediffs.xml`, code in `Ego/Kit/`). Not played; game tests
`-rimarttest=ego: solemn lament` written, not run yet.

| File | What |
|---|---|
| `CompEgoSolemnLament.cs` | `CompProperties_EgoSolemnLament` (`whiteStacks`, `blackStacks`, `ammo`, `reloadSeconds`, `coffinRadius`, `cloudStacks`, `funeralStacks`), which gun is next, the pool and its meter |
| `EgoButterfly.cs` | `Hediff_EgoButterfly` (a stage per stack count, the fade), `EgoButterflyExtension` (`cap`, `consciousnessPerStack`, `fadeSeconds`), `EgoButterfly.Add` (the funeral's kill) |
| `Verb_EgoSolemnLament.cs` | Core's shooting verb with the projectile replaced by a hit on the target |
| `EgoSolemnLamentCorrosion.cs` | the coffin: walks, the cloud's stacks, the funeral; Overclock |
| `EgoSolemnLamentBurstCast.cs`, `EgoSolemnLamentCoffinCast.cs`, `EgoSolemnLamentMarked.cs`, `GameComponent_EgoSolemnLament.cs` | the pictures fed from the rules as they happen: bursts, coffins, the butterflies on each pawn with stacks |
| `Patches_EgoSolemnLament.cs` | `HeldWeaponHide` while a burst or a coffin draws the pair |
| `Tests_EgoSolemnLament.cs` | the pair and the cap, the pool and the fade, the funeral, the walk turning to a nearer pawn, a wielder killed outright, Overclock |

The texture (`Textures/RimArt/Ego/SolemnLament.png`, `make_ego_textures.py`) is the picture's two
pistols, white above and black below, at the size the picture draws them, with a 1 px outline.

Settled while building:

- A burst is 4 shots 0.25 s apart, white first; warmup 0.5 s, cooldown 1 s. The guns keep taking
  turns across bursts: a burst cut short after an odd number of shots (its target died) starts the
  next one black.
- Every shot hits its target (accuracy 1); cover and range bands do not apply inside the 10 cells.
  A target that is not a pawn takes only the black damage.
- Stacks go on when the shot fires; the picture's butterflies land about 0.1 s later.
- The cap downs through consciousness (the game's 30 % rule), not a separate rule. Stacks past the
  cap take no more consciousness, or consciousness 0 would kill the pawn at 14 before the funeral.
- A downed target at the cap still takes the black shot's damage; Butterfly from the guns never kills.
- One stack fades every 10 s whatever the count; a new stack does not reset the timer.
- The corroded walk goes to the nearest pawn, downed ones included (decided 2026-10-02): the cloud
  stays on the first pawn it downs and the funeral follows unless someone carries it out.
- The walk asks again every second and turns to whoever is nearest then. `JobDriver_EgoCorrodedWalk`
  overrides `IsContinuation`: without it the game keeps a walk to the first pawn it picked, because
  the new job has the same def and comes from the same job giver.
- The coffin also ends when its wielder dies, leaves the map, or is neither corroded nor
  overclocking. `Pawn.Kill` does not end a mental state, so a wielder killed without going down first
  never reaches the state's end. Once the coffin has ended, its picture draws no guns and no face,
  and Core draws the weapon on the pawn again while the cloud flies home and the coffin sinks.
- Overclock: 5 firings 1 s apart, holding still, `overclockRange` 3 (the radius), standing hostiles
  only, never past the cap.
- The pictures are not saved: a save in a burst or a corrosion loses its picture, not its stacks.

## Weapon 3: Mimicry

Nothing There, ALEPH. The Red Mist's sword (Kali, in Lobotomy's story; Gebura in Ruina).

Source, checked 2026-09-30 (Lobotomy) and 2026-10-01 (Ruina, Limbus):

- Lobotomy: 10 to 14 Red, fast (1.17), range 4, requires Fortitude 5 and level 5. Heals the
  wielder 25 % of damage dealt. 10 % chance per swing of a special attack: the blade enlarges and
  swings down for 40 to 90 Red. The Abnormality: a five-limbed thing that breaches as a fast
  beast, then an egg of muscle, then a red bipedal true form.
- Ruina: Mimicry is an E.G.O page of the Floor of Language: cost 5, melee, one 19 to 35 Slash die,
  heals HP equal to the damage dealt, 10 Bleed next Scene.
- Limbus: no Mimicry E.G.O for a Sinner, but the corrosion itself is in the game. The Corroded
  Inquisitors O-06-20-TE (Proceeding Inquisitor, Everything There of an Inquisitor) are
  Mittelhammers taken by Mimicry: the right arm is a muscle bundle bigger than the body with an
  eye, a toothed mouth and two curved bone blades, and the flesh runs over the face. Their
  Instincts go up each turn they are not hit and down by 1 each time they are hit; Attack Power Up
  and Protection scale with the count. The arm-stage rule below is taken from this.

| Rule | Mechanic | Placeholder | XML field |
|---|---|---|---|
| The grown swing | Each swing has a chance to swell the blade for one heavy downswing. The blade shrinks back afterwards: a length that stays is Samehada's picture. | 10 % per swing, 3.5x damage, 2x blade size, or every 8th hit if the roll feels bad | `growChance`, `growEveryHits`, `growDamageFactor` |
| The heal | Lifesteal on hit. Small, because Samehada already owns the sword that feeds its holder. | 10 % of damage dealt | `healFraction` |
| The arm | While corroded or overclocked, the flesh takes the sword arm in stages: hand, forearm, arm and shoulder, face. Starts at 1; +1 per hit dealt, -1 per hit taken; all gone when the state ends. Each stage adds melee damage. | 4 stages, +15 % damage per stage | `armStages`, `stageDamage` |

Corrosion: bands 50 / 100 / 100, requirement Melee 8, duration 40 s, interval 1.5 s, exhaustion
3 h. Mimicry corrodes hardest because Nothing There is ALEPH; tier shows only here and in
Paradise Lost, the other ALEPH.
Action: the wielder hunts. It walks to the nearest pawn of any faction, like a Berserk pawn, and
every 1.5 s swings at it when adjacent, or lunges 2 cells at it when it is within 2 cells. Each hit
dealt grows the arm a stage; each hit the wielder takes removes one, so the colony can wear it
down by shooting its own colonist, and downing them ends the state. Nothing There's breach form is
a fast beast that hunts, so the walk is the source's. Overclock: 5 swings at hostiles only, the
same arm, mood -20 for 1 day; it holds still like every Overclock, so the player starts it next to
the hostiles. Look: the arm as the Limbus Inquisitors have it, then flesh over the face with one
eye and bared teeth.

Changed 2026-10-01: corroded hits used to grow the blade with no reset. Samehada already
lengthens its blade per hit and heals its holder, so the growth moved to the arm, and hits taken
take it back.

Changed 2026-10-02: the corroded wielder used to hold its cell and reach about 3 cells (a swing
plus the lunge). It usually corrodes mid-melee, so it kept hitting the enemy next to it with a
stronger arm, and drafting colonists 3 cells away answered it. The arm's "hit taken" rule never
came into play, because nobody had to fight a pawn that stood still. Now it hunts.

Overlap: Samehada on the heal, Vergil on the sword. The arm that takes the wielder and the one
swollen downswing are what separate it, and both are the mod's identity (code-drawn meshes that
change shape).

Sketch: `Tools/VfxLab/web/sketches/ego-mimicry.js` (swings, grown swing, corroded, overclock).

## Weapon 4: Paradise Lost

WhiteNight, ALEPH Abnormality, ALEPH gear. Added 2026-10-01. The staff of the Abnormality that
turns twelve employees into Apostles. The fourth and last weapon of the set: the crowd weapon
next to a line gun, a debuff pair and a melee sword.

Source, checked 2026-10-01:

- Lobotomy: 22 to 28 Pale, fast (1.83), range 80, requires all four stats at level 5. A normal
  attack hits every enemy in the room (and neutral units); damage per target falls with the
  count: 22 to 28 on one target, 19 to 23 each on 2 to 5, 16 to 20 each on 6 or more. Each hit
  restores 2 to 4 HP and SP to the wielder and slows the target 60 % for 0.5 s. The wielder gets
  no HP or SP from department regeneration. Special attack, only while WhiteNight is in the
  facility, 20 s cooldown: the staff vibrates and swings down, leaving a large black trail, 50 to
  60 Pale, and a gold shield absorbs 100 damage for 10 s.
- Lobotomy breach: WhiteNight turns twelve employees into Apostles. Every 60 s a red ring deals
  40 Pale to every non-Apostle in the facility and revives downed Apostles.
- Ruina: an E.G.O page of the Floor of Religion, cost 6, Synchronize. The user becomes WhiteNight
  for 3 Scenes and every other ally becomes an Apostle (cannot be controlled or defeated; at 1 HP
  it stops acting until the Synchronization ends). WhiteNight's pages support, they do not attack:
  restore 1 Light, 1 Strength to the other allies, all allies recover 6 HP, recover 8 HP per enemy
  present. The Apostles (Spear, Scythe, Staff, Guardian) do the fighting.
- Limbus: no Paradise Lost E.G.O for a Sinner. Limbus has no ALEPH E.G.O for Sinners at all.

| Rule | Mechanic | Placeholder | XML field |
|---|---|---|---|
| The room hit | Aim at one hostile in range; that pawn needs line of sight like any shot. Every hostile in that pawn's room takes the hit, seen or not. Outdoors, a radius around the aimed pawn instead. The damage ignores armor, which is how Pale is read here. Damage per pawn falls with the number hit. | range 30; 16 for 1 pawn, 12 each for 2 to 5, 9 each for 6 or more; outdoors radius 6; one shot every 2 s | `range`, `damageSingle`, `damageFew`, `damageMany`, `fewMax`, `outdoorRadius`, `shotInterval` |
| The slow | Every pawn hit is slowed. | -60 % moving for 1 s | `slowFactor`, `slowSeconds` |
| Sanity | Each hostile hit gives the wielder one stack of a mood thought. This is the SP half of Lobotomy's heal; the HP half is dropped, because Samehada and Mimicry already heal on hit. A crowd keeps the wielder above the corrosion bands; one target or an empty room does not. | +1 mood per hostile hit, up to +10, lasts 2 h | `sanityPerHit`, `sanityCap`, `sanityHours` |

Corrosion: bands 50 / 100 / 100, requirement Shooting 10, duration 30 s, interval 6 s, exhaustion
3 h. Action: WhiteNight's ring. No target: every pawn of any faction within the radius of the
wielder takes armor-ignoring damage, colonists and animals included. Ring hits never give Sanity,
so the ring cannot pay for itself. The wielder holds its cell (WhiteNight hovers, it does not
chase), and each ring is 3 cells wider than the one before: 6, 9, 12, 15 cells at 6, 12, 18,
24 s, and 18 if a fifth fires before the state ends at 30 s. Lobotomy's ring covers the whole
facility; the growing ring ends up covering most of a base. Overclock: 3 rings in 6 s, hostiles
only, mood -20 for 1 day. Overclock rings stay at `ringRadius` and do not grow: the growth is the
cost of losing control.

| Corrosion field | Placeholder | XML field |
|---|---|---|
| Ring radius (first ring) | 6 cells | `ringRadius` |
| Ring growth per ring | 3 cells | `ringGrowth` |
| Ring damage | 12 | `ringDamage` |

Changed 2026-10-02: every ring used to be 6 cells. The first came 6 s after the state started, and
6 cells is about 1.5 s of walking, so a paused player moved everyone out before it fired. Only pawns
that cannot walk (downed, asleep, in bed, prisoners, penned animals) were at risk.

Left out: Lobotomy's special (black trail and gold shield; it needs WhiteNight present, and
Overclock is this set's paid strong mode) and the no-regeneration cost (it balances the HP heal,
which is dropped). Full corrosion is not in the first build, but if `onFullCorrosion` is ever
used, this is the weapon: Ruina already has the form for a person, the wielder as WhiteNight and
the colonists inside the ring as Apostles the player cannot control.

### Looks

Lobotomy for the staff and the normal hit, Ruina for the corroded look and the ring. The rule is
the one Mimicry followed: the item from Lobotomy, the corroded look from the game that shows a
person taken by it. Red thorns appear in all of them (Lobotomy's hit, the breach ring, Ruina's
star), so the hit and the corroded look read as one weapon.

| Scene | What | Link |
|---|---|---|
| The staff (Lobotomy) | Thin white shaft, a snake coiled round it, a red apple at the head, a small gold halo of thorns, a grey feathered wing at the base, a gold tip. At RimWorld zoom the apple and the halo carry it. | https://lobotomycorporation.wiki.gg/images/EGOWeaponParadiseLost.png |
| Normal hit (Lobotomy) | Red, angular, branching thorns burst round the pawn hit, about 1.5x its height, about 0.6 s, then fade; a Pale damage number. The wielder is off-screen (range 80). | https://www.youtube.com/watch?v=3Upidd7ZLBE&t=762s |
| Special, probably (Lobotomy) | A row of huge black curved shapes, like claws or feathers, sweeps across the foreground for about 0.2 s as the bigger hit lands. Not used. | https://www.youtube.com/watch?v=3Upidd7ZLBE&t=758s |
| Breach (Lobotomy) | WhiteNight: a white sphere with one red eye inside layered grey-white wings, a gold halo, a glowing red ring of thorns behind. | https://lobotomycorporation.wiki.gg/wiki/WhiteNight |
| Synchronized (Ruina) | White misty stage, blood streaks on the floor. WhiteNight above inside a red thorn star (a red circle with 8 long red spikes); the user inside a smaller star; the Apostles with white wings, red and white robes, spears, scythes and staffs. | https://www.youtube.com/watch?v=qVxyNQHWeuk&t=477s |
| The big move (Ruina) | About 1.3 s: a white cross of light at WhiteNight, red curved slashes, a burst of thin red spikes outward, an expanding red ring, a full-screen pink-white flash. | https://www.youtube.com/watch?v=qVxyNQHWeuk&t=499s |
| Realization sprite (Ruina) | A figure with six white wings streaked with blood, the gold thorn halo, a red heart held in both hands. | https://libraryofruina.wiki.gg/images/Paradise_Lost_Realization_Sprite.png |

The picture, top-down:

- Normal hit: red thorns rise out of the floor under every hostile hit, on the same tick, stand
  about 0.6 s, sink back.
- Corroded and overclocked: the red thorn star flat on the floor round the wielder, white wings
  open behind the pawn, the gold thorn halo over the head.
- Each ring: a short white cross flash on the wielder, red spikes shooting out along the floor,
  the red ring spreading to that ring's radius (`ringRadius` plus `ringGrowth` per earlier ring
  while corroded, `ringRadius` in Overclock). Soft additive layers, the light rule. The C# picture
  already takes a radius per ring (`EgoParadiseLostRingShot.Radius`); the preview passes the fixed
  6 cells.
- No clips, like the other three.

Sound, matched by clip name only, not listened to: Core `psychicpulse` and `psychic_shock_lance`
for the hit and the ring; Royalty `psycast_psychic_pulse` with `MayRequire` and the Core clip as
the fallback. The game has no choir or bell, so a holy layer is a synth chord (like the Spirit
Bomb chord) or a CC0 file.

Overlap: Inumaki's Cursed Speech reaches everyone who hears it and spreads through doorways, so it
often catches the same pawns as a room; the per-count split, the armor-ignoring damage and Sanity
separate it. Things rising from the floor at a pawn: Frost Gun's Flash Freeze (an ice block with
spikes round one pawn) and Obito's Wood Release (two spikes through each pawn on a line). The red
thorns differ in colour and shape and land on every hostile in a room at once; compare them side
by side in the lab. No wings like these exist in the mod (only crows and butterflies).

Sketch: `Tools/VfxLab/web/sketches/ego-paradise-lost.js` (room hit, room hit outdoors, corroded,
overclock).

### Built

Rules and def 2026-10-02 (`AG_EgoParadiseLost` and its round in `1.6/Defs/ThingDefs/AG_Ego_Things.xml`,
`AG_EgoPale` in `DamageDefs/AG_Ego_Damages.xml`, the `AG_EgoParadiseLostSlow` hediff, the
`AG_EgoParadiseLostSanity` thought, code in `Ego/Kit/`). Not played; game tests
`-rimarttest=ego: paradise lost` written, not run yet.

| File | What |
|---|---|
| `CompEgoParadiseLost.cs` | `CompProperties_EgoParadiseLost` (`damageFew`, `damageMany`, `fewMax`, `outdoorRadius`, `slowSeconds`, `sanityPerHit`, `sanityCap`, `sanityHours`, `ringRadius`, `ringGrowth`, `ringDamage`, `ringSound`), the corrosion's ring count, the Sanity meter |
| `EgoParadiseLost.cs` | who a room hit strikes, the damage by count, the room hit, the ring, the slow, Sanity |
| `Verb_EgoParadiseLost.cs` | Core's shooting verb with the projectile replaced by the room hit |
| `EgoParadiseLostCorrosion.cs` | WhiteNight's ring: grows while corroded, fixed and hostiles-only in Overclock |
| `GameComponent_EgoParadiseLost.cs` | the pictures fed from the rules as they happen: thorns round each thing struck, the rings, each wielder's corroded look, the staff in every holder's hand |
| `Patches_EgoParadiseLost.cs` | `HeldWeaponHide`: Core never draws the held staff |
| `Tests_EgoParadiseLost.cs` | the room hit (split, slow, Sanity and its cap), outdoors, the corroded ring growing through walls, Overclock, the def's corrosion numbers, Pale against power armour, the holder set |

The texture (`Textures/RimArt/Ego/ParadiseLost.png`, `make_ego_textures.py`) is the picture's staff
lying diagonally, apple to the upper right, at the size the picture draws it, with a 1 px outline.

Settled while building:

- A shot every 2 s is the verb's warmup 1 s plus the weapon's `RangedWeapon_Cooldown` 1 s. The
  range (30) is the verb's. The damage to a single target (16) is the round's
  (`AG_EgoParadiseLost_Round`, never spawned), so the info card shows it; `damageFew` and
  `damageMany` are on the comp. All three are times the weapon's ranged damage multiplier.
- Pale is `AG_EgoPale`: no armour category, so `ArmorUtility.GetPostArmorDamage` returns the full
  amount, and not ranged, so a shield belt does not stop it. Its injury is a stab.
- Outdoors means the aimed thing's room touches the map edge. Outdoors the hit takes hostiles within
  `outdoorRadius` of the aimed thing that are in the same outdoor room, so a wall still keeps a pawn
  inside a building out.
- The room hit skips downed hostiles (other than the aimed one), so it does not finish raiders the
  player may want as prisoners. The aimed thing is always struck, whatever it is; a thing with no
  room (a wall) is struck alone.
- The aimed thing needs line of sight when the shot goes off, not only at the order.
- Damage lands on the firing tick. The ring's picture raises each pawn's thorns as its edge passes
  (up to 0.62 s later), so the health bar can move before the thorns rise.
- The slow is a `MoveSpeed` factor of 0.4 on the hediff, not a Moving capacity cut: a cut could
  push a limping pawn under the 15 % Moving that downs it. A pawn struck again keeps the longer time.
- Sanity is one memory, not stacked ones: each hostile struck raises its mood by `sanityPerHit` up
  to `sanityCap`, and each hit renews it for `sanityHours`. So all of it lasts 2 h after the last
  hit, and the mood tab shows one line. The ring never gives Sanity, Overclock's included.
- The corroded ring strikes every pawn within the radius, through walls, downed ones included; the
  count of rings is on the weapon and starts again at each corrosion. Overclock's rings strike only
  standing hostiles and do not count.
- The state's end is checked every 30 ticks (so it can come up to 29 ticks before 30 s) and before
  that tick's firing, so it always comes before a fifth ring at 30 s: a corrosion is 4 rings, 6, 9,
  12, 15 cells.
- Overclock: 3 rings 2 s apart, `overclockRange` 6 (the ring's radius).
- Core never draws the held staff. The picture draws it upright in the right hand wherever Core would
  show a held weapon (drafted, aiming), and while corroded or overclocking. The corroded look outlives
  the state by 1 s so the wings fold, and ends early when the wielder dies, as Solemn Lament's coffin.
- Sounds, matched by name only, not listened to: Core `PsychicShockLanceCast` for the shot,
  `PsychicInsanityLanceCast` for the ring (`ringSound`).
- The pictures are not saved: a save in a picture's second loses the picture, not the damage.

## Order

1. Magic Bullet. Proves the shared system with the simplest action.
2. Solemn Lament. The newest Limbus bait; needs the Butterfly hediff and the two-gun draw.
3. Mimicry. Needs the mesh work and a balance pass against Samehada.
4. Paradise Lost. Needs the room query and the ring; the corroded look (wings, thorn star) is the
   most drawing of the four.

Roland (the Black Silence) as an Echo hero kit is the other Project Moon candidate, separate from
this page.

## Open

- How a colony gets an E.G.O. weapon. None can be made, bought or found yet; tests and the debug
  spawner are the only way.
- All numbers, after a balance pass against Bank Shot, Coil and Samehada in game.
- Whether `useMood` is needed (see Left out on purpose).
- Whether a corroded pawn should be attackable by colonists without a hostility prompt, as Berserk
  pawns are.
- Mimicry: whether the corroded swing skips downed pawns (the sketch skips them, so it does not
  finish them; "nearest living pawn" above does not say).
- Mimicry: whether the walk goes to downed pawns. Solemn Lament's does (decided 2026-10-02), and the
  shared walk counts them; the hunter would finish whoever it downs.
- Mimicry: the grown blade is about 2.4 cells long and lands across 3 cells, but the rule hits one
  pawn. Either keep one target or hit every pawn under the blade.
- Paradise Lost: RimWorld makes each door its own room, so the hit stops at doors and misses a
  pawn standing in a doorway. Whether a large hall needs a cap on pawns hit.
- Paradise Lost: the source requires all four stats; the requirement here is one skill
  (Shooting 10). Whether to require a second skill.

## Sources

- Cogitopedia, Der Freischütz (Lobotomy Corporation): https://projectmoon.miraheze.org/wiki/Der_Freisch%C3%BCtz_(Lobotomy_Corporation)
- Cogitopedia, Der Freischütz (Library of Ruina): https://projectmoon.miraheze.org/wiki/Der_Freisch%C3%BCtz_(Library_of_Ruina)
- Limbus Company Wiki, Magic Bullet Outis: https://limbuscompany.wiki.gg/wiki/Lobotomy_E.G.O::Magic_Bullet_Outis
- Limbus Company Wiki, E.G.O/Gameplay (corrosion bands, Overclock): https://limbuscompany.wiki.gg/wiki/E.G.O/Gameplay
- Cogitopedia, Funeral of the Dead Butterflies (Lobotomy Corporation): https://projectmoon.miraheze.org/wiki/The_Funeral_of_the_Dead_Butterflies/Lobotomy_Corporation
- Limbus Company Wiki, Solemn Lament Yi Sang: https://limbuscompany.wiki.gg/wiki/Lobotomy_E.G.O::Solemn_Lament_Yi_Sang
- Cogitopedia, Nothing There (Lobotomy Corporation): https://projectmoon.miraheze.org/wiki/Nothing_There/Lobotomy_Corporation
- Library of Ruina Wiki, Mimicry (E.G.O page): https://libraryofruina.wiki.gg/wiki/Mimicry
- Library of Ruina Wiki, Floor Realization (Language): https://libraryofruina.wiki.gg/wiki/Floor_Realization_(Language)
- Limbus Company Wiki, Corroded Inquisitors: https://limbuscompany.wiki.gg/wiki/Corroded_Inquisitors
- Limbus Company Wiki, Proceeding Inquisitor (Instincts, skills): https://limbuscompany.wiki.gg/wiki/Proceeding_Inquisitor
- Cogitopedia, WhiteNight (Lobotomy Corporation): https://projectmoon.miraheze.org/wiki/WhiteNight/Lobotomy_Corporation
- Lobotomy Corporation Wiki, WhiteNight (weapon stats, breach): https://lobotomycorporation.wiki.gg/wiki/WhiteNight
- Library of Ruina Wiki, E.G.O Pages (Paradise Lost and its Synchronize pages): https://libraryofruina.wiki.gg/wiki/E.G.O_Pages
- YouTube, Lobotomy Corporation: All Weapons Overview (Paradise Lost at 12:36 to 12:51): https://www.youtube.com/watch?v=3Upidd7ZLBE
- YouTube, Library of Ruina: All EGO Pages for Every Floor (Paradise Lost at 7:53 to 8:21): https://www.youtube.com/watch?v=qVxyNQHWeuk
