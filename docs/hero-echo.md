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

### Echo-only genes (built 2026-09-27)

An EchoDef may list `awakenGenes`. Each is added as a xenogene on awakening (the record scan adds
it back if a xenogerm cleared it) and the Host keeps it for life; the awakening letter and the card
list it under the cost. Such a gene lists no abilities of its own: they go in the EchoDef's
`abilities`, so they come with the form and go on revert (validate.py's single-source rule holds).
Its passive behaviour asks `EchoUtility.GeneActive(pawn, gene)`, true only while the Host is
manifested. First use: Itachi's dispersal plexus (Scatter, its toggle and its charges; the charges
keep regrowing between manifests). Obito's fold organ, Nakime's castle gene and Shikamaru's shadow
plexus follow the same hook.

## Hero form

- Per-Echo `manifestHediff` (child of `AG_EchoManifestBase`): stat offsets in its stage.
- No colony work: `disabledWorkTags` on the base comp, copied into each stage at startup. The skill
  panel shows work skills as "-" while manifested.
- `abilities` granted on manifest, taken on revert; cooldowns kept between manifests.
- `hairColor` (alpha 0 = unchanged) and `bodyType` (Thin, from `AG_EchoBase`) on adult humans only;
  other races keep their body. Both restored on revert.
- Removing the hediff any other way reverts the Echo.
- `wealth` is added to the Host's market value.
- Costume (built for Vergil 2026-09-28): a `renderNodeProperties` entry of class
  `RimArt.PawnRenderNodeProperties_EchoCostume` on the manifest hediff, drawn by
  `RimArt.PawnRenderNodeWorker_EchoCostume` under the `ApparelBody` node, one texture per body type
  through `bodyTypeGraphicPaths`. It is a picture only: the Host keeps wearing its apparel and its
  armour and insulation still count. `hideBodyApparel` stops worn clothes and armour (OnSkin, Middle,
  Shell layers) from being drawn; belts, packs and other layers are still drawn. `hideHeadgear` stops
  headgear from being drawn (a Harmony postfix on `HeadgearVisible`), so a helmet no longer hides the
  hair. Vergil's coat sets both. Layer 29 (over belts and packs at 20 + one per piece, under the
  post-apparel wounds at 30); 89 facing north, over the head as vanilla shells are. Textures from
  `make_costume_textures.py`, fitted to the vanilla body outlines. Vergil: DMC3 blue coat, 256 px,
  Thin/Male/Female/Fat/Hulk x south/east/north.
- Shared costume (built 2026-09-28): the Akatsuki cloak lives on the abstract hediff
  `AG_EchoManifest_Akatsuki`; Pain's and Itachi's hero forms take it as their parent, and another
  member (Obito once his Echo exists) needs only `ParentName="AG_EchoManifest_Akatsuki"`. Two nodes:
  the cloak under `ApparelBody` (layer 29 in every facing, 15 textures), and the high collar under
  `Head` (layer 64: over the beard at 60 and the hair at 62, under the head wounds at 65; 3 textures,
  one set for every body type, fitted to the vanilla heads). On the head it covers the chin and
  follows the head when the pawn crawls or swims. The cloak sets `hideBodyApparel` and
  `hideHeadgear`. Black cloak, red clouds with a pale outline and pale curls (the cloud traced from
  the anime symbol: a pointed wisp to one side, round lobes, curls in four lobes), red lining along
  the collar rim and down the front.
- Head pieces are fitted to the average vanilla heads; `narrowHeadScale` (x facing south/north, y
  facing east/west) narrows them on narrow heads, which are about 0.84 as wide face-on and 0.7 as
  deep in profile. The collar and Obito's mask use (0.84, 0.7).
- Obito's spiral mask (built 2026-09-28): on `AG_EchoManifest_Obito` (parent
  `AG_EchoManifest_Akatsuki`, so he also wears the cloak), a head node at layer 63, over the hair and
  under the collar. Orange egg from the hairline to the chin, one black spiral groove from the eye
  hole on his right eye (vanilla eyes are at x 52-59, y 67-72 on every head) to the rim, a red
  Sharingan in the hole. The face-on drawing is projected onto the profiles; west is its own picture
  (no hole on his left side), stored facing right because the render tree mirrors every west
  picture; north is empty. `coversFace` hides Facial Animation's eyebrows, which it draws at layer
  100 (over hats and masks) by default; its other face parts are at 50-60, under the mask. No
  EchoDef names this hediff yet: it comes with his kit's port.
- Pain's piercings (built 2026-09-28): a head node on `AG_EchoManifest_Pain` (besides the parent's
  cloak and collar) at layer 61: over the beard and Facial Animation's face parts, under the hair,
  so hair over the ears hides the ear studs. Six dark studs down the nose bridge in two columns,
  two short studs under the lower lip (just above the collar), silver studs along each ear. Placed
  to suit both the vanilla heads and Facial Animation's narrower heads with ears. The same on both
  sides, so west is east mirrored.

### Manifest weapon (decided and built 2026-09-27)

Some Echoes force what the Host holds while manifested. EchoDef fields (XML): `manifestWeapon` (a
ThingDef), `emptyHands` (bool), `weaponReturnTicks` (default 600 = 10 s, placeholder). Code:
`Source/RimArt/Echo/EchoWeapon.cs`.

| Echo | Forced | In XML |
|---|---|---|
| Vergil | Yamato | not yet: no Yamato def (comes with the Vergil port) |
| Nakime | biwa (weak blunt or none) | not yet: no Nakime Echo and no biwa def |
| Goku | empty hands | `emptyHands` set |
| Todo | empty hands | not yet: no Todo Echo |
| everyone else, Sasuke included | nothing | |

Rules:
- Manifest: the held weapon goes to the inventory (its mass already counts toward what the Host
  carries); only a pawn without an inventory drops it at its feet. The hero weapon appears in hand.
- While manifested the Host cannot equip another weapon (`EquipmentUtility.CanEquip` refuses, so the
  right-click Equip option is greyed out with the reason). The gear tab's drop button and the
  right-click Drop option refuse the hero weapon.
- A weapon put in the hand by other code is moved to the inventory at the next pool interval (1 s).
- Revert: the hero weapon is destroyed and the stored weapon is equipped again, if it is still in the
  inventory and the Host is not downed.
- Not loot: made with default stuff and normal quality (no roll); the def must set `MarketValue` 0
  in `statBases` (a config error otherwise).
- If it leaves the hand it is destroyed instead of dropping and returns after `weaponReturnTicks`
  while still manifested and not downed. Every vanilla drop goes through the
  `Pawn_EquipmentTracker.TryDropEquipment` patch: downing, death, Disarm, Chain Sickle Stake,
  Inumaki's "drop". Vacuum Suck removes weapons its own way and has its own check. Any other path is
  caught by the pool interval check.
- Downing drops the whole inventory (vanilla), so the stored weapon lands on the ground, forbidden,
  and revert does not pick it up.
- Tests: `-rimarttest=echo`, "weapon 1" to "weapon 4" (the forced-weapon tests lend Vergil a
  vanilla knife for the run).
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
| Pain (`AG_Echo_Pain.xml`) | Intellectual 10, Kills 25 | Iron-willed | Shinra Tensei, Banshō Ten'in, Black Receiver, Chibaku Tensei (replaced Gravity Well 2026-09-28) | 15 | Shinra 5 charged / 3 quick (tap/hold button, 2026-09-29), Banshō 3, Black Receiver 0 (its three charges), Chibaku Tensei 30 |
| Inumaki | Social 10, not Psychopath | Kind | stop, drop, crush, come, run, explode | 8 | 0 each (the throat is his cost) |
| Vergil | Melee 16, 20 longsword kills, not Wimp | Bloodlust | Judgement Cut, Yamato Dash, Summoned Swords, Judgement Cut End (the def grants none until the kit is ported) | 10 | Judgement Cut 3, Yamato Dash 2, Summoned Swords 10, Judgement Cut End 0 (Style is its limit) |
| Shirou | has Origin: Blade | none (Origin: Blade's awakening already cost psycasts and ranged weapons) | Unlimited Blade Works | 12 | 40 |
| Itachi ("Crow of the Crimson Eye") | Melee 12, Intellectual 12, Kills 30 | Sickly (Immunity -1) + the dispersal plexus gene (awakenGenes) | crow dispersal (Murder + automatic Scatter), carrion, false face, susanoo | 10 | crow abilities 0 (the plexus's 3 charges), false face 3, susanoo 20 |
| Shikamaru | Intellectual 14, 5 people captured | Lazy | shadow imitation, shadow seam, shadow grasp, shadow double, shadow neck bind | 10 | imitation 3, seam 3, grasp 1, double 5, neck bind 2 |
| Sasuke ("Avenger of the Crimson Eye") | Melee 14, Intellectual 10, 15 kills with the Fūma Shuriken | Pessimist | Amenoyodomi, Amenotejikara, Raikō Kusari, Amaterasu | 12 | Amenoyodomi 0, Amenotejikara 2, Raikō Kusari 8, Amaterasu 5 (+ Bleeding eye); +0.4 move speed, black hair, wealth 6000, no forced weapon, Fourth War outfit |
| Goku | Melee 15, downed and recovered 3 times (`Trial_DownedRecovered`, counted from the moment the mod is loaded) | Gourmand | Solar Flare, Instant Transmission, Kamehameha, Spirit Bomb; Warp Kamehameha is a button during the Kamehameha hold, not a def | 12 | Solar Flare 3, Instant Transmission 2, Kamehameha 15, Spirit Bomb 30; Warp pays Instant Transmission's 2 and cooldown on top |
| Todo (no subtitle) | Melee 14, 20 humanlikes downed (`PawnsDownedHumanlikes`) | Brawler + the anchor organ gene (awakenGenes) | stone, clap, double clap, Black Flash, provoke | 8 | stone 0, clap 0, double clap 0 (the organ's three claps are their limit), Black Flash 1, provoke 5; +0.5 move speed, black hair, wealth 6000, forced empty hands |
| Minato ("Hero of the Yellow Flash") | Melee 12, Intellectual 10, 30 kills with a thrown kunai (`Trial_KillsWith` on AG_Kunai; `Projectile_Kunai` names the kunai as its weapon) | Kind | flying thunder god, flying thunder god: chain, guiding thunder, rasengan; sealing touch passive (AG_MinatoSeal); throw kunai stays on the belt | 10 | throw kunai 0 (belt charges), flying thunder god 2, chain 10, guiding thunder 5, rasengan 3; blond hair, the Hokage haori and forehead protector, no forced weapon, no kunai regeneration |

Accelerator reuses abilities that still come from his pre-hero item (the reflex booster implant).
validate.py allows that second source only for those abilities (`SHARED_WITH_ECHO`) until the
implant is made Echo-only. Pain's four come only from his Echo since his port (2026-09-28; Chibaku
Tensei took Gravity Well's place the same day): the
repulsion and attraction eyes grant nothing and are no longer quest rewards; their defs stay so saves
that hold one still load. Inumaki's words have one source, his Echo:
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
| Obito ("Watcher Behind the Spiral Mask"; built 2026-09-28, see "Obito" below) | Melee 12, Intellectual 10, has a missing or artificial body part (`Trial_ArtificialPart`: any missing part or added part) | Depressive (Natural mood -2) | Kamui: Phase, Kamui: Warp, Kamui: Store, Wood Release | 12 | Phase 0 (its own 30 s pool), Warp 2, Store 1, Wood Release 2 | fold organ gene Echo-only: added on awakening (the dimension is generated then), abilities only while manifested |
| Gojo ("Bearer of the Six Eyes") | Intellectual 14, 1 day spent downed in total (vanilla `TimeDowned` record; the Trial label shows hours), colony wealth 200,000 | The Strongest (new custom trait) | Infinity, Blue, Red, Unlimited Void (+ Hollow Purple combo) | 15 | Infinity 0 (its breath is the price), Blue 3, Red 3, Hollow Purple 20 (+ its own 1-day cooldown), Unlimited Void 30 | white hair; phase barrier implant Echo-only; no forced weapon |
| Nakime ("Player of the Endless Halls") | Artistic 12, Construction 10, 15 humanlike kills (vanilla `KillsHumanlikes`) | Night Owl | Infinity Castle (commands: shift, drop, seal/open, crush, summon, release; passives castle sight, void rule) | 12 | Infinity Castle 30, commands 0 (the 1.5 s strum rhythm and crush's own 10 s cooldown limit them) | black hair; forced biwa; castle gene Echo-only, added on awakening; sunlight burns her always (the gene's rule, 4 per second outdoors by day), not only while manifested |
| Satō | Shooting 12, 30 kills, 500 damage taken | Ajin (new custom trait carrying the Reset passive) + Psychopath | Reset (passive), Black Ghost, The Game, Sever, Headshot Reset, Grenade Reset | 10 | Sever 0, Headshot Reset 0, Grenade Reset 5, Black Ghost 10, The Game 5 (a kill on the marked enemy refunds 15); every Reset while manifested costs by the piece he rises from (body or leg 20, arm 25, hand 40, finger or ear 60) | half pain while manifested; on revert the Ghost dissolves and the mark ends |

Goku (built 2026-09-27, `Source/RimArt/Goku/Kit`): Kamehameha and Spirit Bomb are channels that hold the
caster in `AG_GokuChannel`; Cancel, a move order or a revert gives the charge and cooldown back, a
stun, a downing or death spends them. Spirit Bomb's Lend energy is a job (`AG_GokuLend`) on every
other colonist on the map; a lender can stop. Empty hands in hero form (`emptyHands`, built
2026-09-27). No Melee Animation clips and no drawn arms: the caster stands.

Sasuke (built 2026-09-28, `Source/RimArt/Rinnegan/Kit`, EchoDef in `AG_Echo_Sasuke.xml`; full rules in
the four `rinnegan-*.js` sketch headers; numbers are placeholders in `AG_Sasuke_Abilities.xml`):
- Amenoyodomi: a toggle (off / hang / drift), not a cast, so it never stops him. While on, throw kunai
  can target a cell; the kunai (or a thrown Fūma, at the end of its line) is held there, at most 5,
  creeping on at 1 % (drift 10 %) of its flight speed. Let go: each flies on up to its range from where
  it hung; a kunai flies at the first standing pawn on its line (allies too, never Sasuke). Held
  weapons drop after 60 s, at a wall or the map edge, when it is turned off, or when Sasuke is downed,
  asleep, dead, off the map or reverts. Kunai supply: while manifested his worn belt regains 1 conjured
  kunai every 5 s; conjured kunai are thrown first and never become items (they vanish where they land,
  when pulled out, and leave the belt on revert). Only the conjured kunai itself vanishes: real kunai
  stuck in a pawn it kills still drop (the projectile passes `Conjured`; there is no global flag).
- Amenotejikara: two picks (the game's destination step), both within 12 cells in sight; ends are
  Sasuke, a pawn up to body size 2, an item, or a held weapon; one end must be Sasuke, an item or a held
  weapon. The picture includes the full-screen negative flash (0.12 s + 0.1 s back).
- Raikō Kusari: needs 2+ held weapons within 12; links in throw order up to 6 cells, a ring with 3+;
  8 s; pawns on a line's cells are stunned until it ends and burn 3 per second; mechs stay stunned 3 s
  after; shield belts break; Let go ends it and the weapons fly on charged (2 s stun on what they hit).
- Amaterasu: a pawn (4 burn per second for 20 s, jumps to adjacent pawns at 10 % per second carrying
  the time left, never to Sasuke) or every held weapon (lit weapons light what they hit; a lit kunai
  that misses burns on its cell 20 s; a lit Fūma lands burning and cannot be picked up). Release puts
  out all his black flames. Price: Bleeding eye 60 s (sight -50 %); casting again while it bleeds
  blinds him 10 s.

Minato (built 2026-09-28, `Source/RimArt/ThunderGod/Kit`; rules in docs/flying-thunder-god-kit.md): a
mark is one of his sealed kunai in a pawn or on the ground, or a pawn with his seal. The jump and the
chain land in the free cell most behind the pawn, seen from where he stood, and cut with his own melee
attack at x1.5 / x1.0, a sure hit that also seals; an ally is landed behind, not cut. Guiding Thunder
holds him in the cast job (`AG_CastMinato`) and takes vanilla projectiles in a `Projectile.TickInterval`
prefix: a shot whose roll hit him, or an explosive coming down inside the 1.3-cell ring. The
Rasengan's verb range is the marked range; an unmarked pawn is walked up to first. The four pictures
take live positions; Minato narrows into a sliver and back through the render tree's root matrix
(`MinatoLook`, Vergil's hooks). No clips (the caster stands) and no sounds.

Sasuke's Melee Trial follows the throw: kunai accuracy uses Melee (`KunaiAccuracy.cs`). The Fūma
kills count because the Fūma throw passes the weapon to the kill counter; kunai kills were not
checked. A "lost family" Trial was considered and dropped (2026-09-27).

Itachi (agreed 2026-09-27; numbers are placeholders; the mechanics and the Susanoo picture were
built the same day, see "Echoes defined"; the picture is `Source/RimArt/Itachi/Susanoo*.cs`, ported
from `itachi-susanoo.js` and matched against it in the lab; previews "Susanoo: raise / block / seal
/ hit / end"; the stab lands 0.45 s after the click, when the blade reaches the target):
- Crow Dispersal: the built Murder and automatic Scatter, counted as one ability.
- Carrion: built, unchanged.
- False Face (genjutsu): one button, no target. Every enemy within 15 cells whose current target is
  Itachi (`enemyTarget`, job target or aiming stance) attacks its nearest ally for up to 10 s,
  believing it is Itachi. Each victim breaks free when it takes damage, when its false Itachi goes
  down, or when Itachi goes down. Mechanoids are immune. Cooldown 45 s, 3 charge. Built as a short
  mental state (as vanilla Berserk is), because the AI drops targets that are not hostile to it.
  Built: `MentalState_FalseFace` (category Aggro, 600 ticks, its own break rules) with a
  `JobGiver_FalseFace` node patched into MentalStateCritical; the state's `ForceHostileTo` makes the
  false Itachi hostile both ways, so the ally fights back on its own (checked: `GenHostility`
  asks both pawns' states). Only humanlike enemies are caught (a manhunter pack would be a free
  win); the small effect is a red glint on Itachi, a red flash and crow feathers on each victim,
  and a red mark over each while it lasts.
- Susanoo: self, warm-up 1 s, lasts 12 s, Itachi walks at half speed. Yata Mirror blocks every hit
  from outside. Totsuka Blade (changed 2026-09-27, the one-stab seal of anything was too strong):
  while Susanoo is up, Itachi can stab a target within 4 cells every 3 s. The stab seals the target
  only if it is downed or at 30 % summary health or less (removed from the map, counts as a kill,
  drops its gear, no corpse); on any other target it is a hit of 30 stab damage. At most one seal
  per Susanoo; after it, stabs are hits only. Mechanoids cannot be sealed (they take the hit).
  Built: the Totsuka Blade is a targeted command on the Susanoo hediff (chosen 2026-09-27 over an
  automatic stab, so the one seal goes where the player wants); Crow Dispersal and Scatter are
  disabled while it stands; the seal is Strip, Kill with Itachi as instigator, corpse vanished.
  Cooldown 1 day, 20 charge. After it ends: -30 %
  consciousness and 10 % blood loss for 6 h (his illness). Look: his complete armoured form (never
  Perfect), code meshes plus a swirl texture, always facing the camera, growing ribs -> skeleton ->
  armour and face during the warm-up (growth is picture only, my recommendation). About 2 days.

Obito (agreed 2026-09-27; numbers are placeholders; replaces the built fold organ kit; built 2026-09-28,
see "Obito as built" after this list):
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

Obito as built (2026-09-28; EchoDef in `AG_Echo_Obito.xml`, abilities in `AG_Obito_Abilities.xml`, code in
`Source/RimArt/Obito/` and `Source/RimArt/Involute/`):
- The fold organ gene (`AG_InvoluteOrgan`, now labelled Kamui) is Echo-only: no abilities, not in genepacks.
  It holds the dimension (built on awakening), the Phase pool, the Warp state and the stored list. The
  old Vent, Fold, Swallow, Post and Collapse code and defs, the hole hediffs and the aperture are deleted.
- Readings that were open and are now code (each an XML field or easy to change): Wood Release hits
  allies (`hitAllies`) and stops at the first wall (`stopAtWalls`); the counter's tell is a small swirl
  over the enemy for the 5 s window; release costs no charge; a revert while he is inside the dimension
  puts him back where he went in; the Phase button is an instant toggle (no job, nothing he does stops);
  while phased his weapon and belts are not drawn (the sketch draws his body alone).
- Death and loss of the gene: everything in the dimension comes out where he is (or lies), then the
  dimension closes. Off every map (a caravan) or dead inside it, it comes out where he went in, else at
  the middle of a home map; with no map at all nothing is destroyed and the dimension stays. The close
  after a death runs in `GameComponent_ObitoReset`'s tick, after the maps tick.
- Pictures: his real body is drawn bent (the game's pawn-cache camera renders him into a texture each
  frame, drawn on a 27 × 27 grid moved by the sketch's twist and vortex): see-through while phased, a
  twist from the right eye as Phase turns on and off, a twist and a swirl at every hit that goes through
  him, the vortex into and out of the eye for Warp. Store streams the target's picture into his eye and
  unwinds a released thing out of a swirl; Wood Release is the sketch's strands, twigs, spikes and
  crumble, with splinters lying 8 s.

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

Pain, built 2026-09-28 (the port; code in `Source/RimArt/Pain/Kit`, abilities in
`AG_Pain_Abilities.xml`, tests `-rimarttest=pain`). Choices made in the port, beyond the sketch
headers:
- Shinra Tensei and Gravity Well check that Pain has the ability (his Echo grants it while
  manifested), not the eye. Shinra's button never calls Ability.Activate, so it pays its 5 when the
  wave is released; a cancelled charge costs nothing, and a release the pool cannot pay cancels the
  charge. Since 2026-09-29 the button is one tap/hold button (`Command_TapHold`): a tap is the
  quick version (2.5 cells, 3 charge, paid when it starts, 8 s cooldown); a hold let go before 1 s
  is the quick version too; 1 s gives 3 cells (16 s), 2 s gives 4 cells (20 s), both paying 5.
  Numbers in `ShinraTuning` on AG_ShinraTensei.
- The Shinra / Banshō gap (5 s, XML `devaGapSeconds` on Banshō) starts at Shinra's burst and at
  Banshō's grip. Banshō is also disabled while Shinra charges, and Shinra while a Banshō or Black
  Receiver cast holds Pain.
- A Banshō or Black Receiver cast called off before it fires costs nothing and starts no cooldown.
- Banshō: the target's cell follows it as it crosses cells; its draw point follows the pull. A wall
  or closed door that got into the line stops it like a pawn (the target takes the block damage and
  stun). If Pain goes down mid-pull the target stays where it has got to, with no damage. A dragged
  body is stopped by a standing pawn too. Face-down (drawn) while the slam's stun lasts.
- Black Receiver: the rod turns each tick toward where the target is now, so a walking target is
  still hit; the first standing pawn it crosses takes it, whoever that is. It breaks on a wall and
  falls past range + 2 cells. Only hostile pawns can be targeted (no pinning allies on purpose), and
  a big body takes the same three rods. The pin is a stun renewed every tick (never StopStun, which
  would end other stuns); the pinned pawn is drawn lying on its back 0.35 cells further from Pain
  and gets up in 0.3 s. Rods block every Ability, psycasts included. Every rod breaks when the Pain
  who threw it is downed, dies or no longer has the ability (revert).
- "Cannot be moved": a pinned pawn and a pawn in a Banshō pull are skipped by Shinra Tensei's push,
  refused by Banshō, and skipped by Gravity Well.

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

Gravity Well, built 2026-09-28 with Pain's port (numbers are XML fields on
`CompProperties_AbilityGravityWell`; `GravityRules.cs` keeps only shape and logic; tests
`-rimarttest="Gravity Well"`). Choices made in the build:
- Bullets bend inside the current pull radius, at most 5 cells (`maxBulletRadius`), so never outside
  the drawn ring. Explosive and arcing rounds are still not bent, but one whose path this tick crosses
  a core is eaten (a mortar shell flying over it too).
- A pawn is counted once by its id; its corpse later reaching the core is destroyed but not counted
  again. Items are destroyed in the core only if they can be destroyed.
- The drift picks the heaviest thing the well owns (pawns except Pain, corpses, items in the pull
  radius with a clear line; ties: nearer the centre, then the lower id), again every 15 ticks. The
  centre does not step into a cell it cannot cross into or that Pain has no clear line to.
- The Echo's 20 is taken when the well opens. Cancelled during its 0.5 s opening, it is refunded and
  no cooldown starts; once open there is no refund.
- The timer is a thin ring that shrinks from the pull edge to the core, a faint ring on the pull edge,
  and "N.N s" under the well; the button shows eaten, radius, implosion and seconds left.
- The core pulse and the implosion used armour penetration -1, which the armour roll reads as +1
  armour on every pawn; fixed to 0.
- Frame time (same load, 64 stacks and 20 pawns, 8-cell well): a whole game tick 7.3-8.3 ms before,
  1.8-1.9 ms after. Owner and clear lines cached per tick, items commit their cell every 15 ticks,
  dragged pawns re-path only when off their path (at most every 20 ticks), no per-tick allocations,
  and the patches return at once on maps with no well.

Pain, Chibaku Tensei replaces Gravity Well as his ultimate (2026-09-28; the user found Gravity Well
"not look like pain kit and not epic enough to be ultimate"). Gravity Well's def is retired (no source;
validate.py `RETIRED`) and its code stays for Gojo's Blue. Sketch `pain-chibaku-tensei.js`; the ball
preview is `Source/RimArt/Chibaku`, the cast `Source/RimArt/Pain/Kit/ChibakuCast.cs`; numbers are XML
fields on `CompProperties_ChibakuTensei`; tests `-rimarttest=chibaku`. Rules:
- Target a cell up to 25 cells away in sight. Warm-up 0.8 s (hand up, core over the palm), then the core
  flies at 14 cells/s to 5 cells over the cell. Pain stands with his hand up until the ball has formed
  (about 4-6 s from the click), then is free.
- Pull 3 s, radius 6: every pawn on the torn ground is taken (any faction, colonists and big animals
  too), with items and corpses; plants and filth are destroyed, trees torn out. The ground under a roof,
  a building or a built floor stays, with what is on it. Pain and pawns pinned by Black Receiver are
  never taken, and the plates they stood on stay.
- The ball holds 12 s: 2 blunt per second, then the burst: 8 blunt for the fall, in hits of up to 8,
  from Pain (kills count for him), stunned 2.5 s. It may kill (agreed 2026-09-28).
- Pulled soil becomes stony soil; 6-10 real rock chunks land (one per 16 plates pulled).
- While the ball holds, Shinra Tensei and Banshō Ten'in are locked; Black Receiver is not. If Pain is
  downed, killed, leaves the map or hero form, the ball bursts at once (after it has formed if it is
  still forming). Lost before the core arrives, the core fades and nothing happens.
- One ball per map. Cooldown 1 day, Echo charge 30.
- The command works for any ability with this comp, so Gojo's Blue can reuse it with its own XML
  (min radius = max radius for a fixed well, no growth, no drift).

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

Shikamaru port (built 2026-09-27 on `feature/shadow-plexus-port`; `-rimarttest=shadow` 11/11, Echo
10/10; not played by hand). The kit's design is in the sketch headers
(`Tools/VfxLab/web/sketches/shadow-plexus-*.js`); the port is `Source/RimArt/ShadowPlexus/Kit/` on top
of the drawing already in `Source/RimArt/ShadowPlexus/`.
- No gene (decided 2026-09-27): the Echo `AG_Echo_Shikamaru` is the only source of the five abilities,
  like Inumaki's words. "Shadow plexus gene Echo-only" in older notes means this. Subtitle "Strategist
  of the Binding Shadow", black hair, no forced weapon, +0.2 move speed in hero form.
- Numbers in XML: the shared rule (sky light x0.5, dark under 30 %, smoke density 15 %, a pawn of body
  size 0.9 or more cuts a line) is the `ShadowPlexusExtension` on the EchoDef; each ability's own
  numbers are its comp (`AG_ShadowPlexus_Abilities.xml`): imitation reach 19.9 / hold 15 s / cd 20 s,
  seam reach 15.9 / 20 s / 4 cells apart / cd 30 s, grasp reach 24.9 / 12 cells per s item, 6 body /
  cd 15 s, double 24.9 (not scaled by light) / 20 s / cd 60 s, neck bind climb 1.5 s / +12.5 % per s /
  8 s of choke / cd 45 s; the choke hediff `AG_ShadowChoked` holds the target out for 120 s at 100 %
  and drains 5 % per s otherwise.
- Light on a real map: the game's glow grid gives lamp and fire light as 0.5 at most unless the cell is
  saturated (1), so daylight is half reach and a cell next to a lamp or a campfire is full reach, which
  is the rule as agreed. Reach is measured from the cell the ability is cast from (the standing
  double, else the caster) and the button is greyed out when that cell is dark; the ring on hover shows
  the reach now. A shadow line also cannot cross a wall or a closed door.
- Choices made in the port: Seam and Grasp are two picks (the Skip psycast's shape); the second pick
  cannot show a refusal, so the mouse text says why. Grasp slides loose items, weapons on the ground,
  corpses and downed pawns; a thrown grenade is a projectile in the air, not an item, so it cannot be
  picked. A slid thing stops in the cell of the first pawn on the path and before a wall or another
  item. Imitation holds by stun (as Chain Sickle pins), one hold per caster; the drag moves the target
  one cell per caster step and a wall or a pawn stops that step. The Seam pulls the one that did not
  move one cell at a time after the mover, the smaller body when both moved (an item counts as 0), and
  undoes the mover's step when the pull is blocked. The Double copies steps into open cells only; when
  it ends mid-hold the held line snaps back to the caster and is judged again next tick. Neck bind's 8 s
  is the choke after the 2.1 s the hands take to arrive (8 x 12.5 % = 100 %); when the target passes
  out the Imitation hold lets go with the hands, a Seam stays. A move order, the caster going down or
  the line breaking drops the hands and the meter drains; a pawn choked out wakes after the hold and
  drains from there. Mechanoids can be held and sewn but not choked.
- Not done: the caster's own drop shadow is not hidden while the double is out; the held pawn is not
  darkened; the double is the sketch's two-disc silhouette, not the Host's body; nothing mid-cast is
  saved (a game loaded mid-hold sees the stun run out on its own). Icons are the placeholder
  `UI/Abilities/AnimalWarcall`.
- Polish phase (agreed 2026-09-27, not built): **one cast clip** for all five abilities, the Nara hand
  sign held at the chest in four facings, started by `JobDriver_CastShadowPlexus` over the warm-up.
  It must hold the pose through Neck bind's channel (up to about 10 s), like the Gravity Well channel
  clip, not play once. Melee Animation is optional, so without it he just stands. **Sound effects**,
  none yet: the line running out and snapping, the hold landing, the seam's stitch and taut twang,
  grasp's slide, the double rising and bursting, the choke. Played by `MapComponent_ShadowPlexus` at
  the picture's times (the Power Pole pattern), in an `AG_ShadowPlexus_Sounds.xml`, with the SoundDefs
  named in XML; Core sounds as placeholders until real ones are chosen.
- Debug window, kit "Shadow Plexus": the fourteen picture previews, plus make Shikamaru (Host +
  manifest), light level at a cell, release every hold, a full-light override and its clear.

Todo (agreed 2026-09-27, built 2026-09-28 on `feature/todo-port`): stone, clap and double clap are
the anchor organ's rework (PRs #44, #56) and count as two abilities; Black Flash (from
`feature/todo-black-flash`, 0cb9387) is the third; provoke is the existing `AG_Provoke` with its
cooldown changed from 12 h to 90 s plus 5 charge (20 s, 12.9 cells, +20 % sharp/blunt armour, x0.85
damage taken, unchanged). The Combat presence trait no longer grants provoke: commonality 0, kept
loadable for saves. The old epithet "Conductor of the Marked Stage" is dropped. The anchor organ
gene is Echo-only: `AG_Echo_Todo` gives it on awakening (`awakenGenes`) and it stays for life,
because the stones and the three claps live on it; the gene lists no abilities, the Echo grants
them in hero form, stones stay on the map and claps grow back between manifests. Hero form's
empty hands replaces the gene's old weapon ban. The pictures are the three Todo sketches ported to
C# (`Source/RimArt/Todo/`): Boogie Woogie for the claps, the stone throw for the stone, Black Flash
for the punch. The magician's cards stay in the code for a later hero. Sounds are the old clap and
puff and the vanilla punch.

## Debug and tests

- Debug window, kit "Echo": spawn device, fill charge, charge to 1, finish device research, make
  Host (no trials), tune to pawn, meet candidate's trials.
- God mode gizmos: "DEV: Meet trials" on a candidate, "DEV: Fill charge" on a Host.
- `-rimarttest=echo`: 10 scenarios (pool refill/drain, pool empty, manifest/revert, hediff removed,
  cast cost, awaken, cap, longsword kills, dev command, UI shots), plus the weapon tests and
  "costume 1" (Vergil's coat in the render tree, all 15 textures load; shirt, marine armour and
  helmet not drawn in hero form, smokepop pack still drawn, all drawn again after revert; 8
  screenshots) and "costume 2" (Pain and Itachi both get the cloak and collar from the shared
  parent, 18 textures load, collar on the head over the hair, shirt and cowboy hat not drawn, pack
  drawn, all gone after revert; 4 screenshots) and "costume 3" (Obito's hero form hediff added
  directly on an average and a narrow head: cloak, collar and mask in the tree, hair < mask <
  collar, mask width x0.84 / x0.7 on the narrow head and x1 on the average one, hat hidden, Facial
  Animation's eyebrows not drawn when that mod is loaded, all back when the hediff is removed; 4
  screenshots) and "costume 4" (Pain's piercings on an average and a narrow head: head node, beard
  < piercings < hair, x0.84 / x0.7 on the narrow head, face not covered, gone on revert; 3
  close-up screenshots). `-rimarttest="Echo: costume"` runs all four.
- Debug window, kit "Itachi": make Host + manifest, false face (no cost, no cooldown), susanoo 12 s,
  weaken to 30 % health, totsuka stab the pawn under the mouse. Dispersal's "refill the plexus"
  and "shoot the carrier" still apply.
- `-rimarttest=itachi`: 14 scenarios (gene on awakening and only in hero form, Scatter only
  manifested, charges persist, stale abilities; False Face ranged victim, ally fights back, the
  three breaks and the 10 s end, immunities; Susanoo cost/speed/Mirror, crows stand down, Totsuka
  cut/seal/mech, drained and revert; defs and think tree; UI shot).
- `-rimarttest=shadow`: 11 scenarios (the line check; imitation hold, drag and release; line cut by a pawn and by a
  dark cell; the dark; the seam's leash; grasp's slide, block and body drag; the double as origin and
  its ends; neck bind's knockout and wake; its cut, refusals and move order; the cast cost).
- `-rimarttest=todo`: 9 scenarios (stone place and take back, clap on the last charge and a direct
  swap, Black Flash plain and after a clap, the Echo (organ on awakening, kit only in hero form,
  stones and claps kept over a revert; cast costs, provoke 90 s, Combat presence grants nothing), and
  the three pictures in game with 10 screenshots). Debug window kit "Todo": make Host, open the
  Black Flash window, refill claps, and the picture previews.
- `-rimarttest=Sasuke`: 12 scenarios (the Echo; hold, drift, drops at a wall, 60 s, toggle off, downed and
  revert, the 6th throw; conjured kunai refill, never items, gone on revert; a conjured kill leaves the real
  kunai stuck in the body; the held Fūma; Amenotejikara
  ends and refusals; Raikō Kusari catch, shield, burn, mech EMP, let go; Amaterasu burn, spread, never
  Sasuke, bleeding eye then blind, Release; lit held kunai and Fūma; all four through the real cast jobs
  and the buttons), with screenshots of every picture. Debug window kit "Sasuke": make Host (manifested,
  belt, Fūma), refill kunai belt, hold 3 kunai round here, and the picture previews ("amenotejikara: ...",
  "raiko kusari: ...", "amaterasu: ...").
- `-rimarttest=goku`: 8 scenarios (flare, transmission alone / hostile passenger / downed ally,
  kamehameha lane / wall / blast, cancel refund and stun spent, warp, spirit bomb with a lender, the
  downed-and-recovered Trial). Debug window kit "Goku": make Host, cancel casts, all lend, recovery +1.
- `-rimarttest=minato`: 9 scenarios (the Echo, sealed throws and the kunai kill Trial; jump to a ground
  kunai and into a marked pawn, an ally not cut; sealing touch and its limit of 3; the chain refused with
  one mark and run on three; Guiding Thunder with a bullet into the marked pawn and a grenade out at a
  ground kunai; the Rasengan walked up to, into a wall, and from range; the walk called off by a move
  order; 15 screenshots). Debug window kit
  "Minato": make Host with a kunai belt, stick his kunai in a pawn, seal a pawn, plant his kunai.

## Not built

- Meteor incident that brings the device (the device is researched and built for now).
- Costumes for the other Echoes (Vergil's coat, the Akatsuki cloak for Pain, Itachi and Obito,
  Obito's mask and Pain's piercings are built), the other head pieces, eye overlays, a transform effect per Echo, a marker
  for manifested Hosts.
- Pocket spaces closing on `PoolEmptied`, except Unlimited Blade Works: its world closes when the
  caster loses the ability, which an empty pool causes by reverting every Host.
- Echoes for the other heroes; their kits have no mechanics yet.
- Death setting to reopen a closed Echo after a season; per-Echo settings multipliers.
- Save/load has not been tested by a game test.
