# Shinra Tensei: animation kit

The pawn draws both hands back, thrusts them outward together, holds briefly, and returns to
idle. A transparent pressure shell expands at the thrust, with silver highlights, curved
surface sweeps and a ground dust ring. There are no rocks, damage, knockback, projectile
interception, terrain changes, resource costs, or combat AI.

## Try the kit

Build with `dotnet build Source/RimArt/RimArt.csproj -c Release`, deploy, and restart RimWorld.
Enable development mode, open debug actions, choose **RimArts → Grant kit...**, click a
humanlike pawn, and choose **Shinra Tensei (animation only)**. Select that pawn and press its
**Shinra Tensei** ability button. It is available drafted or undrafted, and casts in place
using the pawn's current facing. Another cast becomes available when its VFX finish.

The kit currently uses a developer-granted hediff as its single ability source; there is no
recipe, research or permanent acquisition decision yet. Removing that hediff removes the
ability. The grant survives saving. The transient wave itself is not saved.

**Melee Animation is required for the pawn cast.** The button explains this when unavailable.
The existing optional integration supplies the pawn body, both hands, skin/glove rendering,
and its temporary animation job. North and south have separate clips; east mirrors for west.
No weapon or grenade is drawn. The animation contains no gameplay events or end-cell move.

## Timing

The hand thrust is at 0.38 seconds, the same `ChargeEnd` used by the shell. The gesture
recovers by 1.35 seconds. During the gesture, VFX read Melee Animation's actual `CurrentTime`,
so pausing or changing its animation speed does not separate the hands from the wave.
After recovery, the remaining VFX use game ticks: the shell disappears at effect time 2.05
seconds, and dust finishes at 3.4 seconds. A downed/dead/despawned caster or an interrupted
gesture cancels its VFX. Different pawns have independent casts.

## Separate VFX inspection tools

Search **Shinra Tensei** in the debug actions menu, then click visible ground:

- **VFX preview** plays the wave without a pawn gesture.
- **VFX slow motion** plays the wave at quarter speed.
- **VFX frozen peak** holds the expanded shell at 1.2 seconds.
- **clear VFX** removes that debug preview.

These inspection tools intentionally run while the game is paused. A new debug preview
replaces the previous one on that map. They do not start, replace or clear pawn casts.

## Rendering and authoring

The hemisphere is an illustrated projection: the ground footprint has depth `0.68 * radius`,
and height shifts artwork north by `0.75 * height`. The visual width radius is eight cells.
These are art dimensions, not a gameplay area. A later circular hit radius needs its own
truthful ground indicator if relevant. Fixed render altitudes avoid clipping a tall sphere
into RimWorld's close camera. There is no physical volume, refraction or custom shader.

The shell has a nearly clear interior, a soft bright outline, two curved highlights and a
faint base arc. Four moving ribbon meshes trace projected latitudes. Seventy-six dust sprites
are layered behind and in front of the shell, with a private deterministic RNG. Materials
and one 192-triangle sweep mesh are cached; frames reuse their property block.

`python3 make_shinra_textures.py` generates the five committed PNGs using Pillow. Changing
projection constants requires updating that script and `ShinraVfxTiming` together.
`python3 make_shinra_anim.py` generates the three hand-animation JSON files; it reads the
release time from `ShinraVfxTiming` and shares the grenade generator's JSON/curve helpers.

## Verification

Run `dotnet run --project Tests/ShinraVfx/ShinraVfx.csproj` for timing, preview lifecycle,
and pawn-cast synchronization/cancellation checks. These use production lifecycle code with
map, draw and animation API stubs. Run `Tests/OriginBlade/ApiChecks/ApiChecks.csproj` to check
the installed Melee Animation clock API and exported clip schema, and `python3 validate.py`
for XML, classes, textures and single-source ability grants.

In-game checks still needed: grant/remove kit, all four facings, both hands visible with
weapons/gloves, pause and animation-speed changes, normal/max zoom, repeated casts, two
casters, cancellation, save/load, and overlaps with pawns, trees and walls. The actual
renderer is required to judge hand layering, brightness and resemblance to the concept.
