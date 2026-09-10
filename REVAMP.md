# RimArt Revamp Plan

## Goal

Transform "Ability Genes" (gene-only combat abilities) into **RimArt** — a combat ability mod where pawns gain abilities from many sources: genes, traits, equipment, training, weapon traits (Odyssey DLC), and specific conditions. Make combat feel more anime/MOBA. Exclude psycasts (VPE) and ROM class scope — those are too OP and already covered.

---

## Phase 1: Mod Identity (DONE)

- [x] Rename mod to RimArt
- [x] Update packageId to `vilasone455.rimart`
- [x] Rename C# namespace `AbilityGenes` → `RimArt`
- [x] Rename assembly `AbilityGenes.dll` → `RimArt.dll`
- [x] Rename source directory, texture directory
- [x] Update all XML class references and texture paths
- [x] Update Harmony ID
- [x] Keep `AG_` def prefix for save compatibility

---

## Phase 2: About.xml and Mod Description

- [x] Rewrite `About.xml` description for new scope
- [x] Add Odyssey and Melee Animation to `loadAfter`
- [ ] Make Biotech optional (after non-gene sources ship)
- [x] Update README.md header, dependencies, layout, and build sections

---

## Phase 3: Code Architecture — Multi-Source Abilities

Currently all abilities are granted via `GeneDef.<abilities>`. Need to support:

### Source Types

| Source | How RimWorld grants abilities | Needs custom C#? |
|---|---|---|
| **Gene** | `GeneDef.<abilities>` | No (existing) |
| **Trait** | `TraitDef` + custom C# to grant/revoke abilities | Yes |
| **Equipment/Apparel** | `CompProperties_AbilityItem` on apparel/equipment | No (vanilla comp) |
| **Weapon Trait (Odyssey)** | Odyssey weapon trait system | Yes (MayRequire) |
| **Training** | Custom system — pawn gains ability after meeting conditions | Yes |
| **Condition** | Hediff-granted abilities or situational triggers | Partial (hediffs can grant abilities via vanilla) |

### Architecture Tasks

- [ ] Create `AbilitySourceDef` or equivalent abstraction (optional — may not need one if each source uses vanilla mechanics)
- [ ] Test `CompProperties_AbilityItem` for equipment-granted abilities
- [ ] Build trait-based ability granting system
- [ ] Build training/skill-based ability system (pawn trains to unlock)
- [ ] Research Odyssey weapon trait API for `MayRequire` integration
- [ ] Build condition-based ability triggers (e.g. "adrenaline surge when ally downed nearby")

### Key Principle

The **AbilityDef** and **ability implementation** (CompAbilityEffect, HediffComp, etc.) stay the same regardless of source. Only the *granting mechanism* changes. A disarm is a disarm whether it comes from acid glands or martial training.

---

## Phase 4: Ability Roster — What Goes Where

### Stays Gene-Only (inherently biological)

| Ability | Gene | Reason |
|---|---|---|
| Corrosive Glands (disarm spit) | AG_CorrosiveGlands | Acid glands are organs |
| Hypermetabolic Glands (overdrive) | AG_HypermetabolicGlands | Metabolic function |
| Alarm Pheromones (provoke) | AG_AlarmPheromones | Pheromones are biological |
| Dispersal Plexus (scatter/murder/carrion) | AG_DispersalPlexus | Body comes apart into crows |
| Involute Organ (fold/swallow/collapse) | AG_InvoluteOrgan | Hole in the body, archite |

### Multiple Sources (gene + at least one other)

| Ability | Gene version | Other sources | Notes |
|---|---|---|---|
| Imperative Larynx (stop/drop/kneel/come/run) | Voice organ | Trait (natural authority), equipment (voice amplifier) | Voice commands fit multiple origins |
| Arc Tendon (dash-strike chain) | Tendon | Weapon trait (Odyssey), training | The strike is the point, not the organ |
| Resonant Marrow (double-hit destroys) | Skeleton | Weapon trait (Odyssey), training | Could be weapon resonance or technique |
| Panoply Organ (blade rain/loose/grasp) | Extrude blades | Weapon trait (Odyssey) | Weapon that scatters/recalls blades |
| Deferred Plexus (arrears) | Nerve plexus | Drug, implant, trait | Delay damage mechanic doesn't need biology |
| Vector Reflex (reflect/shove) | Reflex organ | Equipment (power armor), implant | Deflection tech or trained reflex |
| Halving Membrane (recursion) | Membrane | Psychic, archotech implant | Zeno barrier could be tech-based |
| Anchor Organ (mark/clap/swap) | Organ | Archotech equipment, psychic | Teleport swap doesn't need biology |
| Stasis Organ (time bubble) | Archite organ | Equipment (stasis device) | Time-freeze could be a deployable |
| Time Lattice (speed up/slow down) | Nerve lattice | Drug, implant | Go-juice style speed boost |

### New Abilities to Add (future)

| Ability | Source | Description |
|---|---|---|
| Martial Disarm | Training | Trained technique to disarm (non-acid version) |
| Defensive Stance | Training / Trait | Temporary defense boost, can't move |
| Shield Bash | Equipment | Knock back + stun from shield |
| Adrenaline Surge | Condition | Speed/damage boost when ally downed nearby |
| Berserker Rage | Trait | Damage boost + can't stop attacking |
| Tactical Reposition | Training | Quick dash to cover |
| Last Stand | Condition | Massive buffs when health is critical |
| Counter Strike | Training / Weapon Trait | Auto-retaliate after dodge |

---

## Phase 5: Writing Pass

Do this AFTER Phase 3 & 4, since ability descriptions change when they move to new sources.

### Rules

1. **Lead with the mechanic.** First sentence says what the ability does in gameplay terms.
2. **One line of flavor max.** After the mechanic, one sentence of flavor if there's room.
3. **No Zeno's paradox essays.** If the description is longer than 3 sentences, it's too long.
4. **Names should be self-explanatory.** A player hovering over the name should know roughly what it does.
5. **Cut the dramatic one-liners.** No more "Not all of it." and "It is not enough to make this safe."
6. **Different sources get different flavor.** Gene version: biological flavor. Equipment version: tech flavor. Training version: martial flavor. Same mechanic, different text.

### Name Changes to Consider

| Current | Problem | Possible rename |
|---|---|---|
| Halving membrane | Nobody knows what this means | Phase barrier, Zeno barrier |
| Deferred plexus | "Plexus" is jargon | Delayed nerves, Pain debt |
| Involute organ | "Involute" needs a dictionary | Pocket void, Fold organ |
| Recursion (ability name) | Sounds like programming | Phase guard, Zeno guard |
| Arrears (ability name) | Financial jargon | Debt settle, Pain release |

### Keyed Strings

- In-game messages (AbilityGenes.xml) are mostly fine — short and clear
- Review and simplify where needed
- Rename file from `AbilityGenes.xml` to `RimArt.xml`

---

## Phase 6: Polish

- [ ] Update Workshop thumbnail/preview images
- [ ] Update Steam Workshop description
- [ ] Test all abilities with each source type
- [ ] Test save compatibility (AG_ prefix preserved)
- [ ] Test without Biotech loaded (gene abilities hidden, others work)
- [ ] Test without Odyssey loaded (weapon trait abilities hidden via MayRequire)

---

## Order of Work

1. ~~Phase 1: Mod Identity~~ (done)
2. Phase 2: About.xml — quick win, do first
3. Phase 3: Code architecture — build the multi-source framework
4. Phase 4: Move abilities to new sources — ability by ability
5. Phase 5: Writing pass — rewrite all descriptions
6. Phase 6: Polish and test
