# RimArts: Combat Abilities — Single-Source Roster

## Goal

Each ability kit has one clear origin. A kit comes from one gene, trait, implant, piece of
equipment, weapon trait, or earned origin; no kit is duplicated across several sources.

The source supplies the kit's player-facing name. Biological names are reserved for actual
genes instead of being forced onto abilities that come from training, technology, or weapons.

## Roster

| Source | Kit | Abilities | Acquisition |
|---|---|---|---|
| Gene | **Corrosive glands** | Disarm Spit | Acquire and implant the gene |
| Gene | **Hypermetabolic glands** | Metabolic Overdrive | Acquire and implant the gene |
| Gene | **Anchor organ** | Mark, Clap, Double Clap | Acquire and implant the gene |
| Gene | **Fold organ** | Vent, Fold, Swallow, Post, Collapse | Acquire and implant the archite gene |
| Gene | **Dispersal plexus** | Scatter, Murder, Carrion | Acquire and implant the gene |
| Trait | **Combat presence** | Provoke | Appears naturally as a pawn trait |
| Trait | **Pain debt** | Wound Debt | Appears naturally as a pawn trait |
| Trait | **Commanding voice** | Stop, Drop, Kneel, Come, Run | Appears naturally as a pawn trait |
| Implant | **Neural accelerator** | Double Accel, Square Accel, Stagnate | Craft after Bionics research and install |
| Implant | **Reflex booster** | Reflection, Vector Shove | Craft after Prosthetics research and install |
| Implant | **Phase barrier** | Phase Guard | Find through quests or deep-space trade and install |
| Equipment | **Stasis belt** | Stasis Field | Research Stasis Fields, craft and wear |
| Weapon trait | **Resonant** | Resonance | Find on a Unique Melee Weapon |
| Weapon trait | **Arcing** | Arc | Find on a Unique Melee Weapon; requires Melee Animation |
| Earned origin | **Origin: Blade** | Rain, Loose, Grasp | Meet the skill and blade-study requirements, then awaken |

## Removed Duplicate Sources

- Alarm pheromones gene; Provoke belongs to Combat Presence.
- Innate time lattice gene; time alteration belongs to the Neural Accelerator.
- Vector reflex organ gene; Reflection and Vector Shove belong to the Reflex Booster.
- Halving membrane gene; Phase Guard belongs to the Phase Barrier.
- Resonant marrow gene; Resonance belongs to Resonant weapons.
- Deferred nerves gene and Pain Inhibitor implant; Wound Debt belongs to Pain Debt.
- Panoply organ gene; Rain, Loose and Grasp belong to Origin: Blade.
- Stasis organ gene; Stasis Field belongs to the Stasis Belt.
- Imperative larynx gene; the five commands belong to Commanding Voice.
- Arc tendon gene; Arc belongs to Arcing weapons.

The ability defNames and implementation class names remain unchanged where renaming would add
churn without changing what players see. The removed defs were never published, so no public
save compatibility migration is required.

## Origin: Blade

Origin: Blade never appears randomly. A pawn must reach Melee 14 and Crafting 12, study five
distinct bladed melee weapon types, and accept the awakening. Awakening permanently removes
psylinks and psycasts and forbids ranged weapons.

See [docs/origin-blade.md](docs/origin-blade.md).

## Remaining Verification

- Test all fifteen kits in game through their sole acquisition routes.
- Test with Odyssey absent.
- Test with Unique Melee Weapons absent.
- Test with Melee Animation absent.
