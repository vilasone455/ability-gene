# Echoes, Hosts and the shared charge

Hero layer, decided 2026-09-26. Replaces the Sponsor draft (docs/hero-sponsor.md, never committed).
Numbers are XML placeholders. Code: `Source/RimArt/Echo/`. Defs: `1.6/Defs/EchoDefs/`,
`HediffDefs/AG_Echo_Hediffs.xml`, `ThingDefs/AG_Echo_Things.xml`, `ResearchProjectDefs/AG_Echo_Research.xml`.

## Terms

| Term | Meaning |
|---|---|
| Echo | a hero (`EchoDef`); label = the hero's real name, `subtitle` = optional epithet |
| Host | the colonist who awakened an Echo |
| Manifest | switch into hero form and back |
| Charge | the colony's one shared pool |
| Trials | conditions a candidate must meet |
| Resonance device | the building (`AG_EchoDevice`) that tunes, tracks and refills |

## Rules

1. One shared pool for the whole colony.
2. Each Echo drains at its own rate.
3. The device refills the pool; research upgrades improve it.
4. No ranks. Echoes differ by drain rate.

## Flow

1. Research `AG_EchoResonance`, build the device (2x2, 300 W).
2. Device tab: tune to one Echo and pick one colonist. Tuning another Echo stops the first.
3. Trials are tracked for that colonist only. A message for each Trial cleared.
4. All met: letter Awaken / Not yet. Not yet keeps an Awaken button on the candidate.
5. Awaken: the cost is applied, the colonist becomes the Host. Refused while Hosts = cap.
6. The Host toggles Manifest whenever charge > 0 and it is not downed or collapsed.
7. Host dies: the Echo is Closed for the playthrough.

## Pool

- Refill: passive, `refillPerHour` while any device on a home map is powered and not broken.
- Upkeep: `upkeepPerHour` per manifested Host, in in-game hours (2500 ticks). Checked every 60 ticks.
- Cast cost: `castCosts` per ability, paid when the ability fires. The gizmo is disabled while the
  pool cannot pay.
- Usable anywhere (caravans, pocket maps); refills only through a working device.
- At zero: every manifested Host reverts and gets `AG_EchoCollapse` (consciousness max 10 %, 1 h).
  `EchoUtility.PoolEmptied` fires; pocket-space kits are to close their space on it.

Device tiers (`CompProperties_EchoDevice.tiers`):

| Research | Max charge | Refill / h | Hosts |
|---|---|---|---|
| resonance device | 100 | 5 | 2 |
| resonance tuning II | 150 | 8 | 4 |
| resonance tuning III | 200 | 12 | 6 |

Cost guideline for hero kits (agreed 2026-09-27; a default, not a hard rule: some heroes have their
own cost, some do not):
- Default: Echo charge per cast plus a cooldown.
- Cheap, frequent abilities: the cooldown limits them; 0-2 charge.
- Big abilities: 15-40 charge plus a long cooldown, so they cannot be chained.
- A hero may have its own cost or meter when the kit is built around it: Vergil's Style (fills from
  play, unlocks Judgement Cut End), Accelerator's brain strain (fills on use, downs him at 95 %),
  Amaterasu's Bleeding eye (a one-off hediff). An ability that pays its own cost costs little or no
  charge (Amaterasu 5; Accelerator's strain abilities 0).
- Sasuke's eye strain was dropped: it was a new system that only taxed his kit.

## Trials

`<li Class="RimArt.Trial_...">` in `EchoDef.trials`, read live from the pawn:

| Class | Fields | Counts |
|---|---|---|
| `Trial_Skill` | skill, level | skill level (0 if incapable) |
| `Trial_Record` | record, count | a vanilla record over the pawn's life (Kills, DamageTaken ...) |
| `Trial_Stat` | stat, min | a stat value |
| `Trial_ColonyWealth` | wealth | richest home map |
| `Trial_NotTrait` | trait, degree, matchDegree | exclusion: met while the pawn lacks the trait |
| `Trial_Trait` | trait, degree, matchDegree | met while the pawn has the trait (Shirou: Origin: Blade) |
| `Trial_KillsWith` | weapons, anyMelee, anyRanged, count, weaponLabel | kills by weapon, counted by this mod from load |

## Cost

`forcedTraits` (trait, degree), applied on awakening:
- conflicting traits are removed; traits from genes are suppressed instead;
- a trait the pawn already has at that degree costs nothing;
- a trait with levels is set to the Echo's degree.

The card and the letter show the cost as it applies to the candidate ("Gains Abrasive (replaces Kind)").

## Hero form

- Per-Echo `manifestHediff` (child of `AG_EchoManifestBase`): stat offsets in its stage.
- No colony work: `disabledWorkTags` on the base comp, copied into each stage at startup. The skill
  panel shows work skills as "-" while manifested.
- `abilities` granted on manifest, taken on revert; cooldowns kept between manifests.
- `hairColor` (alpha 0 = unchanged) and `bodyType` (Thin, from `AG_EchoBase`) on adult humans only;
  other races keep their body. Both restored on revert.
- Removing the hediff any other way reverts the Echo.
- `wealth` is added to the Host's market value.

### Manifest weapon (decided 2026-09-27, not built)

Some Echoes force what the Host holds while manifested. Proposed field `manifestWeapon` on the
EchoDef (XML), with an empty-hands option.

| Echo | Forced |
|---|---|
| Vergil | Yamato |
| Nakime | biwa (weak blunt or none) |
| Goku | empty hands |
| Todo | empty hands |
| everyone else, Sasuke included | nothing |

Proposed handling (not yet confirmed rule by rule):
- Manifest: the held weapon goes to inventory (dropped at the Host's feet if it cannot be carried);
  the hero weapon appears in hand.
- While manifested the Host cannot drop, swap or equip another weapon.
- Revert: the hero weapon vanishes and the stored weapon is equipped again.
- Not loot: fixed stats in XML, no quality or stuff roll, market value 0.
- If it leaves the hand (downed, Chain Sickle Stake, Vacuum Suck, Disarm, an imperative) it vanishes
  instead of dropping and returns to the hand after 10 s (placeholder) while still manifested.
- Minato's kunai stay on the kunai belt the player equips; no forced apparel (recommended, not
  answered).

## UI

- Device tab "Echoes": charge bar, refill, drain now, Hosts / cap, next upgrade; one card per Echo
  sorted manifested, awakened, tracking, untuned. Tracking cards show a bar per Trial.
- Host: charge gizmo (charge, net per hour) and the Manifest toggle.
- Candidate: "<Echo> trials n/m" gizmo; becomes "Awaken: <Echo>" when all are met.

## Echoes defined

| Echo | Trials | Cost | Abilities | Upkeep | Casts |
|---|---|---|---|---|---|
| Accelerator | Intellectual 12, Damage taken 300 | Abrasive | vector manipulation, reflex surge, vector shove | 12 | surge 10, shove 8 |
| Pain | Intellectual 10, Kills 25 | Iron-willed | Shinra Tensei | 15 | 20 |
| Inumaki | Social 10, not Psychopath | Kind | stop, drop, crush, come, run, explode | 8 | 0 each (the throat is his cost) |
| Vergil | Melee 16, 20 longsword kills, not Wimp | Bloodlust | Judgement Cut, Yamato Dash, Summoned Swords, Judgement Cut End (the def grants none until the kit is ported) | 10 | Judgement Cut 3, Yamato Dash 2, Summoned Swords 10, Judgement Cut End 0 (Style is its limit) |
| Shirou | has Origin: Blade | none (Origin: Blade's awakening already cost psycasts and ranged weapons) | Unlimited Blade Works | 12 | 40 |

Accelerator and Pain reuse abilities that still come from their pre-hero item (reflex booster
implant, repulsion eye). validate.py allows that second source only for those abilities
(`SHARED_WITH_ECHO`) until the items are made Echo-only. Inumaki's words have one source, his Echo:
the Commanding Voice trait was removed when his kit was ported.
Decided 2026-09-27: the reflex booster implant becomes Echo-only, like Todo's anchor organ; the
repulsion eye and Commanding Voice became Echo-only later the same day. Accelerator keeps brain strain as his cost; his
strain abilities (manipulation, surge, shove, Plasma) cost 0 charge, so `AG_Echo_Accelerator`'s
castCosts (surge 10, shove 8) are to be removed at the port. Upkeep 12/h stays (his choker battery).
Plasma is agreed; Uplift is dropped. Fifth ability, Vector Flick (agreed 2026-09-27, no sketch
yet): he kicks a pebble off the ground at bullet speed; target a pawn within 24.9 cells with line of
sight, warm-up 0.3 s, one projectile of 14 blunt at 30 % armour penetration, cooldown 2 s, no item
used, no strain, 0 charge. Accelerator's v1 kit: vector manipulation, reflex surge, vector shove
(the 2026-09-24 rework), Plasma, Vector Flick.

Vergil (agreed 2026-09-27; numbers are placeholders; full rules in the four `vergil-*.js` sketch
headers): forced Yamato on Manifest (its stats not set yet); +0.5 move speed.
- Judgement Cut: a cell within 18, line of sight, warm-up 0.6 s; a sphere of radius 1.9, every pawn
  inside (allies too) takes 5 x 7 Cut at 50 % armour penetration; cooldown 12 s.
- Yamato Dash: a straight dash up to 8 cells; hostiles in the 1-cell path are marked and take 24 Cut
  at 40 % when the blade clicks home 0.4 s after he stops; cooldown 8 s.
- Summoned Swords: self, 20 s, a ring of 8 blades, cooldown 45 s. Fire mode: one blade every 1.0 s
  at a hostile within 12 cells, 9 Stab at 30 %, -15 % move speed per stuck blade (up to -60 %).
  Spin mode (toggle): hostiles within 1.6 cells take 5 Cut every 0.9 s at 20 %.
- Judgement Cut End: warm-up 1 s, marks hostiles within 10 cells in sight, he is gone 1.5 s while
  they are stunned, then sheathes and the cuts land. Limited by Style, not a cooldown: a meter
  0-100 while manifested, ranks D 0 / C 15 / B 30 / A 45 / S 60 / SS 75 / SSS 90; +4 per pawn hit by
  Judgement Cut, +3 per Dash mark, +1 per Summoned Swords hit, +2 per Yamato melee hit; -20 when he
  takes damage; drains 5 per second after 10 s without a hit. Needs S and spends it all: Style / 5
  cuts (12-20) of 10 Cut, dealt nearest first round the marked pawns (one pawn takes up to 200);
  every marked pawn is stunned. During the vanish, projectiles in the radius are destroyed; at the
  click, hostile or unowned buildings in range take 60, outside the cut pool.

Agreed, not built (no EchoDef yet; the abilities do not exist):

| Echo | Trials | Cost | Abilities | Upkeep | Casts | Hero form |
|---|---|---|---|---|---|---|
| Sasuke ("Avenger of the Crimson Eye") | Melee 14, Intellectual 10, 15 kills with the Fūma Shuriken | Pessimist | Amenoyodomi, Amenotejikara, Raikō Kusari, Amaterasu | 12 | Amenoyodomi 0, Amenotejikara 2, Raikō Kusari 8, Amaterasu 5 | +0.4 move speed, black hair, wealth 6000, no forced weapon |
| Itachi ("Crow of the Crimson Eye") | Melee 12, Intellectual 12, 30 kills | Sickly (Immunity -1) | Crow Dispersal (Murder + automatic Scatter, one ability), Carrion, False Face, Susanoo | 10 (proposed) | crow abilities 0 (they spend the gene's 3 charges, +1 per hour), False Face 3, Susanoo 20 | dispersal plexus gene Echo-only: added on awakening, abilities only while manifested |
| Obito ("Watcher Behind the Spiral Mask") | Melee 12, Intellectual 10, has a missing or artificial body part (new Trial class; a prosthetic or bionic counts) | Depressive (Natural mood -2) | Kamui: Phase, Kamui: Warp, Kamui: Store, Wood Release | 12 | Phase 0 (its own 30 s pool), Warp 2, Store 1, Wood Release 2 | fold organ gene Echo-only: added on awakening (the dimension is generated then), abilities only while manifested |
| Gojo ("Bearer of the Six Eyes") | Intellectual 14, 1 day spent downed in total (vanilla `TimeDowned` record; the Trial label shows hours), colony wealth 200,000 | The Strongest (new custom trait) | Infinity, Blue, Red, Unlimited Void (+ Hollow Purple combo) | 15 | Infinity 0 (its breath is the price), Blue 3, Red 3, Hollow Purple 20 (+ its own 1-day cooldown), Unlimited Void 30 | white hair; phase barrier implant Echo-only; no forced weapon |
| Goku ("Heir of the Monkey King") | Melee 15, downed and recovered 3 times (new counter; vanilla only records total time downed) | Gourmand | Solar Flare, Instant Transmission, Kamehameha, Spirit Bomb (+ Warp Kamehameha) | 12 | Solar Flare 3, Instant Transmission 2, Kamehameha 15, Spirit Bomb 30; Warp Kamehameha pays Kamehameha + Instant Transmission | black hair; forced empty hands |
| Minato ("Hero of the Yellow Flash") | Melee 12, Intellectual 10, 30 kills with thrown kunai (needs `PendingThrow` to pass the kunai as the weapon so the kill counter sees it) | Kind | flying thunder god, sealing touch (passive), chain, guiding thunder, rasengan; throw kunai stays on the belt | 10 | throw kunai 0 (belt charges), flying thunder god 2, chain 10, guiding thunder 5, rasengan 3 | blond hair (haori v1.1); no forced weapon; no kunai regeneration; replaces the earning rules in docs/flying-thunder-god-kit.md |
| Shikamaru ("Strategist of the Binding Shadow") | Intellectual 14, 5 people captured (vanilla `PeopleCaptured`) | Lazy (Industriousness -1) | shadow imitation, shadow seam, shadow grasp, shadow double, shadow neck bind | 10 | imitation 3, seam 3, grasp 1, double 5, neck bind 2 | black hair; shadow plexus gene Echo-only; no forced weapon; Neck Bind's sketch rules agreed 2026-09-27 |
| Nakime ("Player of the Endless Halls") | Artistic 12, Construction 10, 15 humanlike kills (vanilla `KillsHumanlikes`) | Night Owl | Infinity Castle (commands: shift, drop, seal/open, crush, summon, release; passives castle sight, void rule) | 12 | Infinity Castle 30, commands 0 (the 1.5 s strum rhythm and crush's own 10 s cooldown limit them) | black hair; forced biwa; castle gene Echo-only, added on awakening; sunlight burns her always (the gene's rule, 4 per second outdoors by day), not only while manifested |
| Satō | Shooting 12, 30 kills, 500 damage taken | Ajin (new custom trait carrying the Reset passive) + Psychopath | Reset (passive), Black Ghost, The Game, Sever, Headshot Reset, Grenade Reset | 10 | Sever 0, Headshot Reset 0, Grenade Reset 5, Black Ghost 10, The Game 5 (a kill on the marked enemy refunds 15); every Reset while manifested costs by the piece he rises from (body or leg 20, arm 25, hand 40, finger or ear 60) | half pain while manifested; on revert the Ghost dissolves and the mark ends |
| Todo (no subtitle) | Melee 14, 20 humanlikes downed (`PawnsDownedHumanlikes`) | Brawler | stone, clap, double clap, Black Flash, provoke | 8 | stone 0, clap 0, double clap 0, Black Flash 1, provoke 5 | +0.5 move speed, black hair, wealth 6000 (placeholder), forced empty hands |

Sasuke's Melee Trial follows the throw: kunai accuracy uses Melee (`KunaiAccuracy.cs`). The Fūma
kills count because the Fūma throw passes the weapon to the kill counter; kunai kills were not
checked. A "lost family" Trial was considered and dropped (2026-09-27).

Itachi (agreed 2026-09-27; numbers are placeholders; Susanoo has a lab sketch, `itachi-susanoo.js`;
False Face has no sketch; neither has code):
- Crow Dispersal: the built Murder and automatic Scatter, counted as one ability.
- Carrion: built, unchanged.
- False Face (genjutsu): one button, no target. Every enemy within 15 cells whose current target is
  Itachi (`enemyTarget`, job target or aiming stance) attacks its nearest ally for up to 10 s,
  believing it is Itachi. Each victim breaks free when it takes damage, when its false Itachi goes
  down, or when Itachi goes down. Mechanoids are immune. Cooldown 45 s, 3 charge. Built as a short
  mental state (as vanilla Berserk is), because the AI drops targets that are not hostile to it.
  To check before building: whether the attacked ally fights back on its own.
- Susanoo: self, warm-up 1 s, lasts 12 s, Itachi walks at half speed. Yata Mirror blocks every hit
  from outside. Totsuka Blade (changed 2026-09-27, the one-stab seal of anything was too strong):
  while Susanoo is up, Itachi can stab a target within 4 cells every 3 s. The stab seals the target
  only if it is downed or at 30 % summary health or less (removed from the map, counts as a kill,
  drops its gear, no corpse); on any other target it is a hit of 30 stab damage. At most one seal
  per Susanoo; after it, stabs are hits only. Mechanoids cannot be sealed (they take the hit).
  Cooldown 1 day, 20 charge. After it ends: -30 %
  consciousness and 10 % blood loss for 6 h (his illness). Look: his complete armoured form (never
  Perfect), code meshes plus a swirl texture, always facing the camera, growing ribs -> skeleton ->
  armour and face during the warm-up (growth is picture only, my recommendation). About 2 days.

Obito (agreed 2026-09-27; numbers are placeholders; replaces the built fold organ kit; no code yet):
- Kamui: Phase. Toggle intangibility from his own 30 s pool, refilling 1 s per 4 s solid. Every hit
  on his whole body goes into the Kamui dimension (`InvoluteUtility.PassThrough` on the whole body
  instead of one rolled part): rounds are relaunched inside, other damage lands on a random cell
  there. While phased he cannot attack, carry or cast; turning solid takes 0.25 s.
- Kamui: Warp. 1 s warm-up to go in; he comes out at any revealed cell of the map he left, with a
  0.5 s warning mark; cooldown 15 s after the exit.
- Kamui: Store (absorb and release, one ability). Absorb: touch range, a pawn up to body size 1.2 or
  one item stack, warm-up 0.4 s, instant on an enemy whose attack passed through him in the last
  5 s, cooldown 15 s; held enemies stay stunned inside (no capture). Release: pick a stored thing,
  then a cell within 6 cells in sight; enemies come out stunned 2 s; cooldown 5 s.
- Wood Release: Cutting Technique. Branches shoot from his right arm along a straight line up to 10
  cells and skewer every pawn on it: 15 stab each, 30 % armour penetration. Warm-up 0.5 s, cooldown
  10 s, 2 charge. No pin (pinning is Pain's Black Receiver).
- Cut: Vent's rolled body part and hole, the aperture, Post, Collapse, Izanagi. On his death or loss
  of the kit, the contents come out at his cell. The Kamui dimension map (PR #41) stays.

Gojo (agreed 2026-09-27; numbers are placeholders):
- Infinity: the built Phase Guard (`Membrane/`): everything moving toward him halves its remaining
  distance and never arrives; it holds back air, so he stops breathing until he drops it (his own
  cost). The phase barrier implant becomes Echo-only.
- Blue: a lesser Gravity Well (Pain's). Range 15, pulls pawns and loose things within 4 cells for
  3 s, bends no bullets, implodes for 10-25 blunt by the mass in its core, allies affected.
  Cooldown 20 s, 3 charge. Reuses the Gravity code; its constants (`GravityRules.cs`) move to XML.
- Red: a push projectile to a cell up to 20 away at 20 cells/s. The first pawn or loose thing it hits
  is thrown 6 cells along Red's direction (1.5 blunt per cell, +10 into a wall); everything else
  within 1.5 cells of the burst is pushed 2 cells outward (8 blunt). Cooldown 10 s, 3 charge.
- Hollow Purple (a combo, not a counted ability): when Red passes within 1 cell of an active Blue's
  centre and Purple is ready, both are used up and Purple forms at Blue's centre, travelling in Red's
  direction: radius 1.5, 6 cells/s, 30 cells or the map edge. It erases what it touches: walls,
  buildings, plants and items are destroyed with no drops; pawns take 60 erasure damage that ignores
  armour; friend or foe. Its own cooldown 1 day plus 20 charge; if it is not ready or the pool cannot
  pay, Red passes through Blue and pushes as normal.
- Unlimited Void: the pocket map (pictures on main, PR #34). Everyone within 9 cells is taken in,
  keeping positions relative to Gojo; 10 s or Release, then back to the matching cells. Anyone not
  spared is overloaded (consciousness capped at 10 % for 60 s, then void-scarred 2 days). Cooldown
  2 days. Rules: allies are taken too; Gojo spares an ally by touching it during the domain, and one
  touch spares it for the whole domain; androids and mechanoids are immune but still taken in and can
  act inside; overloading neutrals costs goodwill with their faction.
- Forced trait, The Strongest (new custom trait): Gojo has +20 opinion of young colonists (children
  and anyone under 30 % of the race's life expectancy, 24 for a human) and they have +10 of him; he
  has -20 opinion of old colonists (past 70 %, 56 for a human) and they have -10 of him; -6 mood while
  no other Host is awakened in the colony, +4 once another is. No psychic sensitivity anywhere in his
  entry (the user's rule).

Pain's Echo (agreed 2026-09-27): it grants all four abilities, Shinra Tensei, Gravity Well, Banshō
Ten'in and Black Receiver (the last two once they are built; `AG_Echo_Pain` lists only Shinra today).
The repulsion and attraction eyes become Echo-only. Cast costs (agreed 2026-09-27): Shinra Tensei 5
(was 20, set before the cost guideline), Banshō Ten'in 3, Black Receiver 0 (its 3 rods are its own
cost), Gravity Well 20. Upkeep 15 unchanged.

Pain, Gravity Well rework "hungry well" (agreed 2026-09-27; replaces the fixed 8-cell, 6 s well in
`GravityRules.cs`; numbers are placeholders and move to XML):
- It opens small and grows with what it has eaten: pull radius 3 at the start, up to 10 (+25 % on the
  old 8) at 200 eaten.
- Eaten = everything that entered the 1.5-cell core, added up by weight, each thing once: bullet or
  arrow 2, rocket / grenade / mortar shell 10, dropped item or chunk its RimWorld mass, corpse
  60 x body size, living pawn 60 x body size (counted once, when it first reaches the core).
- Items and corpses in the core are destroyed (eaten); pawns are held and take core damage as now.
- Duration: 4 s at the start, +1 s per 10 eaten, at most 15 s. A ring that shrinks round the well and
  the seconds left show how long it has.
- Implosion: 15-45 blunt by total eaten; its radius grows from 2 to 3. Cooldown 40 s unchanged.
- Drift (agreed 2026-09-27): it creeps toward the heaviest mass inside its pull radius at 0.5 cells/s,
  never more than 5 cells from where Pain cast it; not random and not steerable. Allies' mass attracts
  it too. The known FPS drop (tick logic, see the deferred Gravity Well note) is to be fixed before or
  with the drift, since a moving centre makes pulled pawns re-path more often.
- Gojo's Blue stays a fixed 4-cell lesser well.

Satō (agreed 2026-09-27; numbers are placeholders; Reset, explosions, The Game, Black Ghost at
anchors and Tear were agreed 2026-09-26):
- Reset (passive, carried by the Ajin trait, so it works for life): he never truly dies. Manifested
  and able to pay: after 20 s he rises at his biggest piece (body, or a severed limb as an anchor),
  paying by that piece's size (body or leg 20, arm 25, hand 40, finger or ear 60). Not manifested, or the
  pool cannot pay: a slow reset where he fell, after 1 in-game day. Any damaging explosion resets him
  at once and counts as the body destroyed, friendly fire included.
- Actives, five separate: Sever (throw one of his own parts, a leg, arm, hand, finger or ear, up to
  6 cells as an anchor, at most 2, they rot in 3 days, cd 10 s; an ear is piece size 1 like a finger.
  With both ears severed he is deaf, so Inumaki's words don't reach him, but both anchor slots are
  used and he can't use the Explode reset; Reset regrows the ears; ears added 2026-09-27); Headshot Reset (kills himself for a clean Reset); Grenade Reset (his
  own explosion, radius 3, hurts everyone near, cd 60 s); Black Ghost (summoned at any anchor within
  30 cells, 45 s, takes 50 % damage, cd 120 s, lifetime by piece: leg 45 s, arm 35 s, hand 20 s,
  finger 10 s; Tear order once per summon; the Relay order is still undecided); The Game (mark one
  enemy 30 s, +30 % damage from his shots, cd 45 s).

Inumaki revisit (agreed 2026-09-27; numbers are placeholders; the built words are in
`Source/RimArt/Larynx`, `AG_Larynx_Abilities.xml`):
- Words: stop, drop, come and run stay as built. Kneel is replaced by crush (15 blunt + 1 s stun,
  throat 9 %). New word explode: a burst on the target, 35 damage in radius 1.5, throat 35 % (about
  two uses reach "torn"), cooldown 1 day. Warm-up 0.4 s as built; reach and who is hit come from
  the sound model below (the old single target at 16.9 cells goes).
- The throat meter ("strained larynx") stays his only cost: every word costs 0 Echo charge (was 5).
  Upkeep 8 unchanged.
- Backlash by target strength (canon): the throat cost is multiplied by the target's body size,
  at least 1 (a human x1, a thrumbo x4), on top of the existing factors (hostile x2, mental state
  x2.5, already obeying x0.15).
- Throat syrup: a short job drinking one herbal medicine removes 30 % throat wear (recovery is
  otherwise 12 % per day).
- Sound model (agreed 2026-09-27, for v1; replaces single targets): a word is a sound that spreads
  from Inumaki through open space and open doors; walls and closed doors stop it, and passing a
  doorway costs part of its reach. Everyone it reaches obeys: enemies, animals and colonists alike.
  Mechanoids and deaf pawns are immune; low Hearing obeys only close in. No ear plugs: colonists are
  protected by doors, distance and volume.
- Volume toggle: whisper 3 cells (throat x0.5 per listener), speak 8 (x1), shout 16 (x2).
- Throat cost = word base x volume x each listener's factors (hostile x2, mental state x2.5,
  already obeying x0.15, body size), summed over listeners.
- Preview while aiming: reached cells shaded, listeners marked (enemies red, allies blue), total
  throat cost shown. A word that would push the throat past 100 % cannot be said.
- Explode's burst also hurts pawns standing next to each listener, allies included, like a vanilla
  explosion (agreed 2026-09-27). It uses Bomb damage, which is on Satō's explosion list: a Satō who
  hears it resets at once and rises at his biggest anchor (the Explode + Reset combo). As an ally
  listener he costs x1 throat, not x2.
- Satō's Black Ghost is deaf: words never reach it (a summoned figure, like mechanoids).
- Deaf pawns include anyone missing both ears, so Satō can sever his ears to stand in a shout (see
  his Sever).
- The Commanding Voice trait becomes Echo-only (agreed 2026-09-27): only Inumaki's Echo gives the
  words.
- About 1.5-2 days of work on top of the built Larynx code.

Inumaki port (built 2026-09-27 on `feature/inumaki-port`; `-rimarttest=inumaki` 11/11, Echo 10/10;
not played by hand):
- All of the above is built. The numbers are in XML: `LarynxExtension` on `AG_LarynxWear` (reach and
  factor per volume, doorway cost 3 cells, listener factors, deaf at Hearing 15 % or less, max wear
  100 %, syrup) and each word's `CompProperties_AbilityImperative` (base throat cost, damage, stun,
  flee distance). Crush has 30 % armour penetration; explode uses Bomb's default.
- Choices made in the port: a word is cast on Inumaki with no targeting step, so the preview shows
  while its button is hovered (reached cells shaded, rings red hostile / blue colony / yellow other)
  and the listener count and throat cost are in the tooltip. A word no one would hear is greyed out.
  Explode does not hurt Inumaki. The throat check is on the button; people who walk into reach
  during the 0.4 s warm-up are still obeyed and billed. Consciousness still scales each listener's
  factor (not below 0.2), as built before. A listener the word can do nothing to (drop to someone
  unarmed, stop / drop / come / run to someone downed) is not a listener and costs nothing. The
  volume toggle is on the hero form hediff and starts at speak on each manifest. Throat syrup is a
  right-click on herbal medicine, 1.5 s.
- `AG_Imperative_Kneel` and the Commanding Voice trait are removed (an old save drops them with a
  load error line). Icons are the placeholder `UI/Abilities/AnimalWarcall`.
- Distances are any-angle (straight line from the speaker, a wall corner or the doorway the sound
  came through), so a volume's reach is a circle on open ground.
- VFX and sound (added the same day, straight in C#, no lab sketch): rings at his head, a ripple
  band with a magenta / cyan fringe moving out at 30 cells/s over the reached cells (stops at walls,
  half-ring out of a doorway, fades before the edge of reach), purple-white crackle on each listener
  when the band reaches them. Looks from the anime: ep 19 "Blast away" (ripple with colour fringe
  from the mouth), ep 17 (crackle on the target). Sound: Core `PsychicShockLanceCast`, volume and
  pitch by whisper / speak / shout, a placeholder.

Todo (agreed 2026-09-27): stone, clap and double clap are the anchor organ's rework on main (PRs
#44, #56) and count as two abilities; Black Flash is built on `feature/todo-black-flash` (0cb9387,
not pushed); provoke is the existing `AG_Provoke` with its cooldown changed from 12 h to 90 s plus 5
charge (20 s, 12.9 cells, +20 % sharp/blunt armour, x0.85 damage taken, unchanged). The Combat
presence trait is to be retired from the loaded defs so provoke has one source. The old epithet
"Conductor of the Marked Stage" is dropped. The anchor organ gene becomes Echo-only (decided
2026-09-27): it leaves genepacks and generic content, and only Todo's Echo gives it.

## Debug and tests

- Debug window, kit "Echo": spawn device, fill charge, charge to 1, finish device research, make
  Host (no trials), tune to pawn, meet candidate's trials.
- God mode gizmos: "DEV: Meet trials" on a candidate, "DEV: Fill charge" on a Host.
- `-rimarttest=echo`: 10 scenarios (pool refill/drain, pool empty, manifest/revert, hediff removed,
  cast cost, awaken, cap, longsword kills, dev command, UI shots).

## Not built

- Meteor incident that brings the device (the device is researched and built for now).
- Body and head costume pieces, eye overlays, a transform effect per Echo, a marker for manifested Hosts.
- Pocket spaces closing on `PoolEmptied`, except Unlimited Blade Works: its world closes when the
  caster loses the ability, which an empty pool causes by reverting every Host.
- Echoes for the other heroes; their kits have no mechanics yet.
- Death setting to reopen a closed Echo after a season; per-Echo settings multipliers.
- Save/load has not been tested by a game test.
