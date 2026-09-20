# Power Pole: the weapon and its three abilities

Agreed 2026-09-21. Nothing here is in the game yet. Every number is a placeholder until the
abilities are built and played, and every number is an XML field, not a C# constant. The pictures
are the sketches under **Power Pole** in the VFX lab (`Tools/VfxLab/web/sketches/power-pole-*.js`);
each sketch header carries the same numbers as this page.

The source is Goku's staff from Dragon Ball. It gets an ordinary name; "Origin:" is the blade's
name only.

## Source and roster row

One source, as `REVAMP.md` requires: the kit comes from equipping the weapon and from nothing else.

| Source | Kit | Abilities | Acquisition |
|---|---|---|---|
| Equipment (weapon) | **Power Pole** | Extend Thrust, Sweep, Vault Strike | Quest reward or exotic goods trader; cannot be crafted |

Equipping the weapon grants the three abilities. Unequipping removes them. Proposed, not yet
agreed: cooldowns are kept on the pawn, so swapping the weapon off and on does not reset them.

## The weapon: `AG_PowerPole`

| Stat | Value | Note |
|---|---|---|
| Label | Power Pole | |
| Melee tool "shaft" | 14 blunt, 2.0 s cooldown | 7.0 damage per second. Vanilla mace head: 20 blunt, 2.6 s. |
| Melee tool "tip" | 11 blunt, 1.6 s cooldown | |
| Armor penetration | default for blunt | No override. |
| Melee reach | 1 tile | The long reach exists only inside the abilities. |
| Mass | 1.5 kg | |
| Recipe | none | No `recipeMaker`, `costList` or `WorkToMake`. Not smeltable. |
| Quality | none | One stat line. |
| Hit points | 300, no wear from use | |
| Market value | 3 500 silver, set by hand | Vanilla eltex staff and ultratech melee 2 000, persona weapons 3 000, this mod's one-ability eyes 3 200. |
| Sources | `thingSetMakerTags: RewardStandardCore`, `tradeTags: ExoticMisc` | Same tags as the Repulsion Eye and the Frost and Mimic items. |
| Raider spawning | none | No weapon tags. |
| Worn position | on the back, diagonal | |

Description:

> A red staff a little over one tile long. On command it extends to many times its length in a fraction of a second and retracts just as fast. It does not bend, does not wear, and nobody knows how to make another.
>
> In melee it is an ordinary blunt weapon with a reach of 1 tile. While it is equipped, the wielder can use Extend Thrust, Sweep and Vault Strike. The long reach exists only in those three abilities. All three hit allies as well as enemies.
>
> It cannot be crafted. It turns up as a quest reward or with exotic goods traders.

## Extend Thrust: `AG_PowerPole_ExtendThrust`

Role: one target far away, pushed back. Sketch: `power-pole-extend-thrust.js`.

| Field | Value |
|---|---|
| Target | A tile within 12 tiles, line of sight required |
| Warm-up | 0.3 s |
| Cooldown | 8 s |
| Damage | 22 blunt, 30% armor penetration, to the first pawn on the line, ally or enemy |
| Push | 3 tiles along the line |
| Wall bonus | +10 blunt if a wall or other solid object stops the push early |
| After | Staggered |

> Extend the pole in a straight line toward a tile up to 12 tiles away. It stops at the first pawn on the line, ally or enemy: 22 blunt damage, 30% armor penetration. The pole keeps extending and pushes that pawn 3 tiles further, then retracts. The pawn is staggered.
>
> If a wall or other solid object stops the push early, the pawn takes 10 more blunt damage. Needs line of sight. Does nothing to buildings.

## Sweep: `AG_PowerPole_Sweep`

Role: many targets close, no stun and no push. Sketch: `power-pole-sweep.js`.

| Field | Value |
|---|---|
| Target | A direction |
| Area | Front half-circle (180 degrees), radius 4 tiles, swung from the wielder's left to its right |
| Warm-up | 0.3 s, then a 0.4 s swing |
| Cooldown | 20 s |
| Damage | 12 blunt, 18% armor penetration, to every pawn in the area, ally or enemy |
| After | Staggered |
| Walls | The pole's length at each angle is the distance to the first wall on that line, capped at 4. A pawn behind a wall is not hit. One table per cast. |

> Extend the pole to 4 tiles and swing it through the half-circle in front of the wielder, from left to right. Every pawn in that area, ally or enemy, takes 12 blunt damage with 18% armor penetration and is staggered.
>
> The pole shortens to pass a wall and lengthens again after it. A pawn standing behind a wall is not hit.

## Vault Strike: `AG_PowerPole_VaultStrike`

Role: the engage. Over a wall or the front line, onto one pawn, stun it. Sketch:
`power-pole-vault-strike.js`. It replaced the plain Pole Vault (`power-pole-vault.js`, kept for
comparison), which looked like a jump pack: up, down, nothing at the end.

| Field | Value |
|---|---|
| Target | A pawn or a standable tile within 10 tiles. No line of sight needed. Roofs do not matter. |
| Warm-up | 0.3 s, then 0.9 s in the air (a `PawnFlyer`) |
| Cooldown | 18 s |
| Damage | 20 blunt, 30% armor penetration, to a pawn on the target tile, ally or enemy |
| Stun | 1.5 s on that pawn |
| Splash | Every other pawn within 1.5 tiles of the target tile is staggered. No damage. |
| Landing | The tile next to the target on the side the wielder came from. It must be standable and unoccupied. |
| In the air | Cannot be attacked in melee |
| Empty tile | The strike only staggers |

> Plant the pole and let it push the wielder into the air, over walls and over other pawns, toward a pawn or tile up to 10 tiles away. No line of sight is needed. Just before landing, the wielder swings the extended pole down onto the target tile.
>
> A pawn on that tile takes 20 blunt damage with 30% armor penetration and is stunned for 1.5 seconds. Every other pawn within 1.5 tiles is staggered. The wielder lands next to the target. If the tile is empty, the strike only staggers.
>
> The wielder cannot be attacked in melee while in the air. The landing tile must be standable and unoccupied.

## Rules this kit follows

| Rule | How it applies here |
|---|---|
| One source per kit (`REVAMP.md`) | The weapon only. Add the roster row above when the kit is built. |
| Ability before VFX | Each sketch was built after its mechanic was stated; the headers hold the mechanic. |
| Balance in XML | Damage, armor penetration, range, radius, push, stun, cooldown and warm-up are fields on each ability's `CompProperties_AbilityEffect` subclass. C# constants are only for shape: pole width, ferrule length, timings of the drawing. The drawn reach and radius read the XML values. |
| The order gives the result | The player orders, the game resolves. Nothing depends on timed input or on the enemy reacting to a telegraph. |
| Match the source look | Red pole, no glow. It extends and retracts fast, it is planted to lift the wielder, and it comes down in an overhead strike. |
| One animation per action | Three new clips: thrust, sweep, plant-vault-strike, each with North and South variants. None reuses another item's clip. |
| Ground, not sky | Nothing drops from the sky. The strike comes from the wielder, who rose from the ground on the pole. |
| Per-facing drawing | Thrust and Sweep are flat at hand height and need one routine. Vault Strike has height: north and south aims shift the drawing east by `0.45 x height x abs(sin(aim))`. |
| Realistic gear placement | Worn on the back. |
| Debug previews | `[RimArtDebug("Power Pole", ...)]`, never `[DebugAction]`. |
| Verification | Build, validate, ApiChecks, deploy. Only a clean game log proves it works. |

## Open for the port

- Combat Extended: CE measures blunt penetration in its own units, so the three armor penetration
  values need a CE patch entry under `Patch_CombatExtended`. Not designed yet.
- Melee Animation: duel animations assume the usual weapon length. The held staff stays 1.2 tiles,
  so nothing should change, but it has not been checked in game.
- Textures: the sketches borrow the Six Paths `SoftDisc` and `Puff`. The kit needs its own PNGs
  and a `make_power_pole_textures.py`.
- The cracks Vault Strike leaves need a fade or a filth-style mark in game.
- True long-reach auto-attacks (custom verb and job) are deferred until the three abilities work.

## Port order

1. Extend Thrust: the pole draw routine (stretched shaft, fixed ferrules), the
   `DrawEquipmentAiming` prefix that hides the held staff while an ability draws it long (the
   same hook `Source/RimArt/Fuma/Patches_Fuma.cs` patches), the line walk and the push.
2. Sweep: reuses the pole routine; adds the per-angle wall table.
3. Vault Strike: reuses the pole routine; adds the `PawnFlyer`, the whip and the strike.
