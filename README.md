# Ability Genes

A RimWorld **1.6** mod that expands Biotech's very short list of genes granting an **active ability**.

Vanilla Biotech ships 9 ability genes. This adds **15 more**, all in the vanilla `Ability`
gene category so they slot straight into the gene assembler alongside fire spew and longjump legs.

## Dependencies

| Dependency | Required | Why |
|---|---|---|
| **Biotech DLC** | **Yes** (hard) | Genes do not exist without it |
| Royalty / Ideology / Anomaly | No | Deliberately not referenced |
| Harmony | No | No C# assembly, so nothing to patch |
| Any framework (VEF, EBSG, …) | No | Everything uses base-game ability comps |

Everything is XML. There is no compiled assembly, which means: no Harmony, no framework
mod, no version-specific DLL to rebuild, and nothing to break on a game patch that does not
change def schemas.

## Contents

### Self-buff
| Gene | Ability | Effect | Met / Cpx |
|---|---|---|---|
| adrenal reserve | adrenal surge | 60s: +move, +melee damage/hit/dodge, pain ×0.35 | -2 / 1 |
| reflex node | reflex overdrive | 45s: aim delay ×0.6, ranged cooldown ×0.7, +2 accuracy | -2 / 1 |

### Mobility
| Gene | Ability | Effect | Met / Cpx |
|---|---|---|---|
| spring tendons | kinetic leap | 13.9-tile jump, 20s cooldown, no hemogen cost | -2 / 0 |

### Offence
| Gene | Ability | Effect | Met / Cpx |
|---|---|---|---|
| barb sac | barb volley | 3 charges, 12 dmg stab projectile, 12.9 range | -2 / 0 |
| hemo lance | hemo lance | 26 dmg / 0.75 AP projectile, costs 0.25 hemogen (needs Hemogenic) | -1 / 1 |
| venom glands | paralytic spit | 30s near-total immobilisation, no damage — take prisoners alive | -2 / 1 |
| shriek sacs | terror shriek | forces PanicFlee on one target | -2 / 1 |

### Defence & utility
| Gene | Ability | Effect | Met / Cpx |
|---|---|---|---|
| smoke glands | smoke burst | 4.9-radius smoke, 2 charges | -1 / 0 |
| retardant bladders | firefoam burst | 4.9-radius firefoam on self | -1 / 0 |
| coolant bladder | coolant burst | extinguish a 2.9-radius area at 12.9 range, 2 charges | -2 / 1 |
| osseous bulwark | osseous bulwark | raise a 5-cell cross of wall as instant cover | -3 / 2 |
| calming pheromones | calming pheromones | end another pawn's mental break | -1 / 1 |
| rallying voice | rallying call | grant an inspiration, 5-day cooldown | -2 / 2 |

### Archite tier (each costs 1 archite capsule)
| Gene | Ability | Effect | Cpx / Arc |
|---|---|---|---|
| storm gland | storm call | call a flashstorm at 29.9 range, 10-day cooldown | 3 / 1 |
| fold organ | fold | teleport the caster and nearby pawns home, 15-day cooldown | 4 / 1 |

## Layout

```
About/About.xml              mod metadata + Biotech dependency
loadFolders.xml              1.6 only
1.6/Defs/AbilityDefs/        15 AbilityDefs + AG_Genetic category + self-cast abstract base
1.6/Defs/GeneDefs/           15 GeneDefs
1.6/Defs/HediffDefs/         3 temporary hediffs (surge, overdrive, paralysis)
1.6/Defs/ThingDefs_Misc/     2 projectiles
Textures/AbilityGenes/       empty — see "Art" below
```

Def prefix is `AG_` throughout.

## Art

The mod currently ships **no textures**. Every `iconPath` points at an existing Core or
Biotech texture, so icons render correctly with only Biotech installed. To replace one,
drop a 128×128 PNG at `Textures/AbilityGenes/<Name>.png` and change that def's `iconPath`
to `AbilityGenes/<Name>`.

## Testing

`./deploy.sh` copies the mod into the local RimWorld `Mods/` folder. Then enable
"Ability Genes" in the mod list, start a dev-mode game, and use the gene assembler
or the character editor to attach a gene and try the gizmo.

## Extending: what is available without C#

The base game assembly exposes 57 `CompProperties_Ability*` classes. This mod only uses
ones whose XML field names are confirmed by an existing vanilla def:

`AbilityGiveHediff` `AbilityGiveMentalState` `AbilityStopMentalState` `AbilityGiveInspiration`
`AbilityLaunchProjectile` `AbilitySmokepop` `AbilityFirefoampop` `AbilityWaterskip`
`AbilityWallraise` `AbilityFlashstorm` `AbilityFarskip` `AbilityHemogenCost`
`AbilityRequiresCapacity` `AbilityFleckOnTarget`

Also verified-and-unused, if you want more: `AbilityTeleport`, `AbilitySprayLiquid`,
`AbilitySpawn`, `AbilityChunkskip`, `AbilityFireSpew`, `AbilityFireBurst`, `AbilityCoagulate`,
`AbilitySocialInteraction`, `AbilityOffsetPrisonerResistance`, `AbilityEffecterOnTarget`.

Comps that exist in the assembly but that **no** vanilla or workshop def uses — so their XML
field names are unverified — include `AbilityExplosion`, `AbilityReleaseGas`,
`AbilityPutToSleep`, `AbilityFixWorstHealthCondition`, `AbilityAnimalRoar`,
`AbilityTrainRandomSkill`. Confirm field names by decompiling before relying on them.

Anything genuinely new (a resource gene that fuels abilities, a channelled ability, an
ability that scales with a skill) needs a C# assembly. That would add Harmony as a
dependency and require a .NET SDK targeting `net48` with a reference to
`RimWorldWin64_Data/Managed/Assembly-CSharp.dll`. See `Source/` — currently empty.
