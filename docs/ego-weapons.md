# E.G.O. weapons and Corrosion

Design agreed 2026-09-30. The Corrosion core is built (2026-10-02, see Code); no weapon has rules or
a def yet. Post-v1 (the 2026-10-20 list is full). Source: Project Moon
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
- Every `corrodedInterval` the weapon's corrosion action fires at the nearest living pawn on the
  map, any faction. Nearest, not enemy: a raider, a friend, the dog.
- Any per-weapon counter (Magic Bullet's seven) keeps running while corroded.
- Ends on `corrodedDuration` or when the pawn is downed. Then `AG_EgoExhausted` for
  `exhaustionHours` (consciousness capped, moving 50 %).
- A corroded pawn who hits a colonist gets vanilla's "harmed me" opinion hit. That is the
  consequence; no extra thought.
- Look: the Abnormality bleeds through, drawn over the pawn as VFX quads, the Vergil pose trick.
  Each weapon supplies its own. No Melee Animation clips.

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
| `MapComponent_EgoCorrosion.cs` | calls each weapon's `DrawCorroded` for every corroded or overclocking pawn on the shown map |
| `Tests_EgoCorrosion.cs` | `-rimarttest=ego`: bands and requirement, a shot corrodes and the state fires at the nearest pawn then exhausts, going down ends it, Overclock aims only at hostiles in range, never rolls, and pays mood |
| `Ego<Weapon>Corrosion.cs` (per weapon, not written) | the action and the look |

Defs: `AG_EgoCorroded` (MentalStateDefs/AG_Ego_MentalStates.xml), `AG_EgoExhausted`, `AG_EgoOverclocked`,
`AG_EgoCorrodedHold`, `AG_EgoOverclock`, and the think node in `1.6/Patches/AG_Ego_ThinkTree.xml`.

Settled while building:

- The action is an abstract class, not an interface: `DrawCorroded` has an empty default.
- A pawn already in any mental state does not roll, so corrosion never replaces Berserk or False Face.
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
| Butterfly | A stacking hediff. Each stack lowers consciousness. At the cap the pawn goes down covered in butterflies. Stacks fade. | cap 10, -8 % consciousness per stack, -1 stack per 10 s | `butterflyCap`, `butterflyConsciousness`, `butterflyFade` |
| Ammo | A pool, reload on empty. | 20 shots, reload 3 s | `ammo`, `reloadSeconds` |

No dual-wield mod dependency. The alternating fire is the weapon's own rule. If a dual-wield mod
is loaded the item can be flagged one-handed so it pairs; that is a compat line, not the design.

Corrosion: bands 25 / 75 / 100, requirement Shooting 4, duration 15 s, interval 1 s, exhaustion
2 h. Action: the coffin. A butterfly cloud on the corroded pawn; every pawn within radius 3 takes
1 stack per second. No target: it is an area, so the nearest rule does not apply. Overclock: the
coffin for 5 s at hostiles only (allies inside the radius are skipped), mood -15 for 1 day.
Look: a monochrome butterfly over the head, the Abnormality's face.

Overlap: none. Nothing in the mod has a stacking non-lethal ranged debuff.

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
Action: a swing at the nearest adjacent pawn, a 2-cell lunge if nobody is adjacent. Each hit
dealt grows the arm a stage; each hit the wielder takes removes one, so the colony can wear it
down. Overclock: 5 swings at hostiles only, the same arm, mood -20 for 1 day. Look: the arm as
the Limbus Inquisitors have it, then flesh over the face with one eye and bared teeth.

Changed 2026-10-01: corroded hits used to grow the blade with no reset. Samehada already
lengthens its blade per hit and heals its holder, so the growth moved to the arm, and hits taken
take it back.

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
so the ring cannot pay for itself. Overclock: 3 rings in 6 s, hostiles only, mood -20 for 1 day.

| Corrosion field | Placeholder | XML field |
|---|---|---|
| Ring radius | 6 cells | `ringRadius` |
| Ring damage | 12 | `ringDamage` |

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
  the red ring spreading to `ringRadius`. Soft additive layers, the light rule.
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

## Order

1. Magic Bullet. Proves the shared system with the simplest action.
2. Solemn Lament. The newest Limbus bait; needs the Butterfly hediff and the two-gun draw.
3. Mimicry. Needs the mesh work and a balance pass against Samehada.
4. Paradise Lost. Needs the room query and the ring; the corroded look (wings, thorn star) is the
   most drawing of the four.

Roland (the Black Silence) as an Echo hero kit is the other Project Moon candidate, separate from
this page.

## Open

- All numbers, after a balance pass against Bank Shot, Coil and Samehada in game.
- Whether `useMood` is needed (see Left out on purpose).
- Whether a corroded pawn should be attackable by colonists without a hostility prompt, as Berserk
  pawns are.
- Mimicry: whether the corroded swing skips downed pawns (the sketch skips them, so it does not
  finish them; "nearest living pawn" above does not say).
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
