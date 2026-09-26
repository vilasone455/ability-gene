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
| Inumaki | Social 10, not Psychopath | Kind | the five imperatives | 8 | 5 each |
| Vergil | Melee 16, 20 longsword kills, not Wimp | Bloodlust | none until his kit is ported | 10 | - |
| Shirou | has Origin: Blade | none (Origin: Blade's awakening already cost psycasts and ranged weapons) | Unlimited Blade Works | 12 | 40 |

Accelerator, Pain and Inumaki reuse abilities that still come from their pre-hero item (reflex
booster implant, repulsion eye, Commanding Voice trait). validate.py allows that second source only
for those abilities (`SHARED_WITH_ECHO`) until it is decided whether the old items stay.

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
