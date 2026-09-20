# Paper Bomb: the tag scroll and its three abilities

**Built 2026-09-20, not played.** Numbers are placeholders; the four structural decisions at the end are made. Nothing has been
run in RimWorld yet: it compiles, `validate.py` and ApiChecks pass, and the lab records all eight previews. What exists:

- Sketches under **Paper Bomb** in the VFX lab (`Tools/VfxLab/web/sketches/paper-bomb-*.js`, shared code in `lib/paper-bomb.js`).
- Textures (`make_paper_bomb_textures.py`) and four Melee Animation clips (`make_paper_bomb_anim.py`, `Patch_MeleeAnimation/1.6/Defs/AG_PaperBomb_Anims.xml`).
- Pictures in C#, ported from the sketches: `Source/RimArt/PaperBomb/` (`PaperBombGraphics`, and a timing and a graphics class each for Tag Throw, Tag Line and Paper Shroud), with previews in the mod's debug window under "Paper Bomb".
- The kit: `Source/RimArt/PaperBomb/Kit/` (`CompTagScroll`, `PaperBombAbilities.cs`, `MapComponent_PaperBomb`, `JobDriver_CastPaperBomb`) and the defs `AG_PaperBomb_Things.xml`, `AG_PaperBomb_Abilities.xml`, `AG_PaperBomb_Recipes.xml`, `AG_ExplosiveTags` in `AG_Research.xml`, `AG_CastPaperBomb` in `AG_CastAbility.xml`.

Not built yet: rain and fire acting on a laid line; interception of a thrown tag on its way; the hand-seal clip on Detonate (it uses Core's cast job); sounds of the kit's own (bursts use Core's explosion sound); Combat Extended patch.

Every number is a placeholder, and when built every number is an XML field, not a C# constant.
Each sketch header carries the same numbers as this page; change them together.

The source is Naruto: the explosive tag that sizzles before it goes off, the chained tags of the
Second Hokage, Konan's sheets that cover a target, and Tenten fighting with a scroll held open.
The kit is built on paper drawn as code meshes that change shape: a strip that runs out and lies
down, sheets that flip in the air and stick, tags that curl before they burst.

This page replaces the "Kunai Paper Bomb" section of `docs/tactical-ability-ideas.md`, which is
out of date.

## Source and roster row

| Source | Kit | Abilities | Acquisition |
|---|---|---|---|
| Equipment (weapon) | **Paper Bomb** | Tag Throw, Tag Line, Paper Shroud (+ Detonate while a line is laid) | Craftable behind one research project |

Equipping the scroll grants the abilities and unequipping removes them, as with the Power Pole.
The scroll is a held weapon, so it does not use the Waist/Belt slot that the kunai belt and the
makibishi pouch share, and a pawn can carry it with either. The cost of the kit is the gun the
pawn is not holding.

## The weapon: `AG_TagScroll`

| Stat | Value | Note |
|---|---|---|
| Label | tag scroll | |
| Melee tool "rod" | 7 blunt, 2.0 s cooldown | A wooden rod. Weak on purpose. |
| Mass | 0.8 kg | |
| Tags held | 20 | Reloaded from `AG_PaperTag` items. |
| Texture | `RimArt/PaperBomb/TagScroll` | |

## Tag Throw: `AG_PaperBomb_TagThrow`

Role: the plain ranged attack and the breaching tool. Sketch: `paper-bomb-tag-throw.js`. Clip: `RimArt_TagThrow`.

| Field | Value |
|---|---|
| Target | A tile, pawn or wall within 12.9 tiles, line of sight required |
| Warm-up | 0.3 s (the clip's release) |
| Cooldown | 4 s |
| Tags | 1 |
| Hit | Hit chance as the kunai throw (Melee skill). A miss sticks in the ground or wall where it lands. |
| Fuse | 2 s from sticking, the ability's own timer, not a `CompExplosive` wick (pawns run from wicks, and the carried case needs them not to) |
| Burst | 35 Bomb damage, radius 1.5, allies included |
| Stuck in a pawn | Travels with the pawn; the burst is centred where the pawn is at 2 s |
| Stuck in a wall | That building takes 4x damage (140) |

## Tag Line: `AG_PaperBomb_TagLine`

Role: a line charge laid along a corridor before a raid. Sketch: `paper-bomb-tag-line.js`. Clips: `RimArt_TagFlick`, `RimArt_TagSeal`.

| Field | Value |
|---|---|
| Target | A tile within 10.9 tiles, line of sight required |
| Cast time | 2.5 s, standing still |
| Cooldown | 20 s |
| Layout | 2.5 tiles of plain strip from the pawn, then one tag per tile up to the target, at most 8 |
| Tags | 1 per tag laid |
| Lifetime | 1 day. Fire sets the tags off early; rain makes the strip fizzle without bursting. |
| Set off | Manual (Detonate) or tripwire, by a toggle; see decision 2. The fuse reaches a new tag every 0.1 s. |
| Burst | 30 Bomb damage, radius 1.1, per tag, allies included |

The plain strip exists because the first tag's radius (1.1) would otherwise reach the caster.

## Paper Shroud: `AG_PaperBomb_Shroud`

Role: single-target burst that also pins. Sketch: `paper-bomb-shroud.js`. Clips: `RimArt_TagFan`, `RimArt_TagSeal`.

| Field | Value |
|---|---|
| Target | One pawn within 6.9 tiles, line of sight required |
| Warm-up | 0.35 s |
| Cooldown | 45 s |
| Tags | 6 |
| Held | From the first tag landing (0.9 s after the cast) the target cannot move or act for 2 s |
| Burst | Automatic at the end of the hold: 60 Bomb damage to the target, 20 to everything else within 0.9 tiles, allies included |

## Boundaries with other items

| Item | What it is | Why this does not repeat it |
|---|---|---|
| Frost Bomb | A thrown circle that slows | Tag Throw damages, sticks to pawns and breaches |
| Makibishi | A 3x3 patch that slows | Tag Line is a line, damages, and is set off by the player |
| Remote Toy Car | One mobile charge that goes where there is no line of sight | Everything here needs line of sight |
| Kunai | Single target, bleed | Area damage |

## Decisions, made 2026-09-20

1. **Tags are ammunition.** The scroll holds 20 tags and each ability spends them: Tag Throw 1,
   Tag Line 1 per tag laid, Paper Shroud 6. Tags (`AG_PaperTag`) are a stackable item and the
   pawn reloads the scroll as it reloads the kunai belt.
2. **Tag Line is set off either way, by a toggle.** A gizmo on the pawn switches a laid line
   between manual and tripwire. Manual: a Detonate ability (the hand seal, 0.35 s warm-up, no line
   of sight, any distance on the map) that appears while the pawn has a line laid; the fuse starts
   at the pawn's end. Tripwire: the first hostile to step on a tag starts the fuse at that tag,
   and it runs both ways along the strip from there.
3. **Craftable.** Scroll: wood 20, cloth 30. Tags: 5 per batch from cloth 5, chemfuel 10. Both
   behind one new research project.
4. **Tag Throw rolls to hit** with the kunai's Melee-skill roll. A miss sticks in the ground or a
   wall near the target and still bursts there.
