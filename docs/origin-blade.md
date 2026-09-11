# Origin: Blade

A permanent earned pawn trait granting the existing Rain, Loose, and Grasp kit.
The trait does not roll on generated pawns and cannot be suppressed by another trait.

## Awakening

- Melee level **14** or higher.
- Crafting level **12** or higher.
- Study **five different bladed melee weapon types**.

Select an undrafted colonist and right-click a weapon on the ground to **Study blade**.
The pawn reserves the weapon, walks to it, and studies it for **2,500 ticks** (one game
hour). The weapon is left intact. Interrupted jobs do not award credit; completed studies
are permanent and saved per pawn. The Origin: Blade command shows progress and the warning.

A type is a weapon's ThingDef, so material and quality variants count once. Melee weapons
with Cut or Stab tools qualify, including compatible modded weapons. Guns with bayonets
do not qualify. Core provides five examples: knife, ikwa, spear, gladius, and longsword.
Pawns can study before reaching the skill requirements. When all requirements are met,
awakening occurs automatically (checked every 250 ticks and immediately on study completion).

## Permanent tradeoffs

- Removes existing vanilla psycasts and all psylink hediffs.
- Blocks learning vanilla psycasts and acquiring/increasing psylinks.
- Prevents equipping ranged weapons; an equipped one is dropped intact, or moved into
  inventory when it cannot be dropped. Also guards against firing a ranged equipment verb.
- Psychic sensitivity and unrelated abilities or health conditions are unchanged.
- Skill loss does not remove the trait or its abilities.

The study dialog warns before starting that awakening removes psycasting and ranged
weapon use. It also explains that awakening can occur later when the skill requirements
are met. Psytrainers and psylink neuroformers reject use before consuming the item.

## Existing kit behavior

This adds a pawn-trait source alongside the existing Panoply gene. It reuses the existing
trait ability extension and does not add a weapon trait or require Unique Melee Weapons.
Deployed blades still belong to the pawn, incur bodily debt, and can become permanent
steel longswords through Grasp. Those balance choices have not been redesigned.

Royalty is optional; the restriction hooks reference base-game types without adding
Royalty-only XML definitions. Vanilla Psycasts Expanded and other replacement psycast
frameworks use separate ability storage and require additional compatibility work.

See [verification instructions](../Tests/OriginBlade/README.md) for automated and in-game checks.
