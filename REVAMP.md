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
- [x] ~~Make Biotech optional~~ Cut. Biotech stays a declared dependency. Dropping it means redrawing 23 gene-art icons, and those icons want drawing on their own merits rather than as the price of shedding a DLC that players of a gene-sourced mod overwhelmingly own. The def-level gating stays in place and costs nothing -- genes are `MayRequire`d, shared abilities use the mod's own `AG_CastAbilityOnThingWithoutWeapon` -- so if this is ever revisited it is deleting the Biotech entry from `modDependencies`, not redoing the work
- [x] Update README.md header, dependencies, layout, and build sections

---

## Phase 3: Code Architecture — Multi-Source Abilities

Currently all abilities are granted via `GeneDef.<abilities>`. Need to support:

### Source Types

| Source | How RimWorld grants abilities | Needs custom C#? |
|---|---|---|
| **Gene** | `GeneDef.<abilities>` | No (existing) |
| **Trait** | `TraitDef` + custom C# to grant/revoke abilities | Yes |
| **Equipment/Apparel** | `RimArt.CompProperties_ApparelAbility` on apparel; `CompEquippableAbility` on weapons | Yes, for apparel |
| **Weapon Trait (Odyssey)** | Odyssey weapon trait system | Yes (MayRequire) |
| **Special condition** | Any grant vanilla has no mechanism for — the mod checks a condition or a checklist itself | Yes |

### Special Condition — Definition

A pawn gains an ability in a way vanilla has no mechanism for, gated behind a condition or
a checklist the mod checks itself. Gene, equipment and weapon-trait sources are pure vanilla
XML. What this category adds is **the check** — the grant itself may still ride on a vanilla
mechanism once the check passes. Two sub-types, because they are different code:

| Sub-type | Granted | Storage | Example |
|---|---|---|---|
| **Permanent** | Once, when the checklist completes | `GameComponent` with saved per-pawn records | Origin: Blade — five blade types studied, Melee 14, Crafting 12 |
| **Transient** | While a state holds, revoked when it lifts | None — a hediff comes and goes | Adrenaline Surge, Last Stand |

Shipped so far: **Origin: Blade** (permanent). See `docs/origin-blade.md`.

### Architecture Tasks

- [x] Build trait-based ability granting system (`TraitAbilityExtension` + Harmony patches)
- [x] Equipment abilities — `RimArt.CompProperties_ApparelAbility` plus two `Pawn_ApparelTracker` patches. The plan said vanilla `CompProperties_AbilityItem` and no C#; that class does not exist. Vanilla's real options are `CompEquippableAbility`, which replaces `CompEquippable` and so only works on a weapon, and `CompApparelVerbOwner`, which grants a `Verb` rather than an `AbilityDef`. Neither fits a belt, so the comp is ours. It also holds the cooldown on the item, which is what stops a long charge being refreshed by unequipping. Shipped as the stasis belt
- [x] Special condition, transient — use vanilla `HediffDef.<abilities>` (XML only, no custom C#)
- [x] Special condition, permanent — `GameComponent_BladeStudy` + `OriginBladeUtility`, shipped as Origin: Blade
- [x] Odyssey weapon traits — use vanilla `WeaponTraitDef.abilityProps` (XML only, no custom C#, MayRequire on defs)
- [x] Unique Melee Weapons traits — same `abilityProps` mechanism, `MayRequire="shunter.uniquemeleeweapons"` (resonance, arc)
- [x] ~~Create `AbilitySourceDef`~~ Not needed — each source uses its own vanilla or light-custom mechanism

### Key Principle

The **AbilityDef** and **ability implementation** (CompAbilityEffect, HediffComp, etc.) stay the same regardless of source. Only the *granting mechanism* changes. A disarm is a disarm whether it comes from acid glands or an earned discipline.

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
| Stasis Organ (time bubble) | Archite organ | **Equipment (stasis belt) — shipped** | Time-freeze could be a deployable |
| Time Lattice (speed up/slow down) | Nerve lattice | Drug, implant | Go-juice style speed boost |

### New Abilities to Add (future)

| Ability | Source | Description |
|---|---|---|
| Martial Disarm | Special condition (permanent) | Trained technique to disarm (non-acid version) |
| Defensive Stance | Special condition (permanent) / Trait | Temporary defense boost, can't move |
| Shield Bash | Equipment | Knock back + stun from shield |
| Adrenaline Surge | Special condition (transient) | Speed/damage boost when ally downed nearby |
| Berserker Rage | Trait | Damage boost + can't stop attacking |
| Tactical Reposition | Special condition (permanent) | Quick dash to cover |
| Last Stand | Special condition (transient) | Massive buffs when health is critical |
| Counter Strike | Special condition (permanent) / Weapon Trait | Auto-retaliate after dodge |

---

## Phase 5: Writing Pass

Do this AFTER Phase 3 & 4, since ability descriptions change when they move to new sources.

### Rules

1. **Lead with the mechanic.** First sentence says what the ability does in gameplay terms.
2. **One line of flavor max.** After the mechanic, one sentence of flavor if there's room.
3. **No Zeno's paradox essays.** If the description is longer than 3 sentences, it's too long.
4. **Names should be self-explanatory.** A player hovering over the name should know roughly what it does.
5. **Cut the dramatic one-liners.** No more "Not all of it." and "It is not enough to make this safe."
6. **Different sources get different flavor.** Gene version: biological flavor. Equipment version: tech flavor. Earned version: martial flavor. Same mechanic, different text.

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

- [x] ~~Update Workshop thumbnail/preview images~~ Skipped -- not revamp work, and the word
      "update" was wrong anyway. No preview image has ever existed: `About/` holds only
      `About.xml`, and nothing named `Preview.png` or `ModIcon.png` appears anywhere in the
      repo's history. Creating one is store design, and it belongs to publishing rather than
      to this plan
- [x] ~~Update Steam Workshop description~~ Skipped for the same reason, and on the same wrong
      premise -- the mod has never been published. There is no `About/PublishedFileId.txt`,
      which is the file Steam writes on first upload; the only `steamWorkshopUrl` in
      `About.xml` is Harmony's, sitting in `modDependencies` to tell players where to get the
      dependency. Both of these become real work the day you decide to ship, not before
- [ ] Test all abilities with each source type
- [x] ~~Test save compatibility (AG_ prefix preserved)~~ Skipped -- nothing to be compatible
      with. The mod has never been published, so no save outside this machine has ever loaded
      it. The `AG_` prefix itself stays as it is: the reason for keeping it is gone, but
      renaming 86 defs now would be pure churn against working files, and it costs nothing
      where it sits
- [x] ~~Test without Biotech loaded (gene abilities hidden, others work)~~ Moot -- Biotech is required
- [ ] Test without Odyssey loaded (weapon trait abilities hidden via MayRequire)

---

## Order of Work

1. ~~Phase 1: Mod Identity~~ (done)
2. Phase 2: About.xml — quick win, do first
3. Phase 3: Code architecture — build the multi-source framework
4. Phase 4: Move abilities to new sources — ability by ability
5. Phase 5: Writing pass — rewrite all descriptions
6. Phase 6: Polish and test
