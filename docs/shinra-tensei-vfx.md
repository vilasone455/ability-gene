# Shinra Tensei: animation kit

The pawn draws both hands in to the chest, then throws both arms straight out to the sides
into a T-pose, holds, and returns to idle. A transparent pressure dome appears at the thrust
and holds one size, warping the view behind it, while impact waves cross its inside and a flat
ring travels outward across the ground. There are no rocks, damage, knockback, projectile
interception, terrain changes, resource costs, or combat AI.

## Try the kit

Build with `dotnet build Source/RimArt/RimArt.csproj -c Release`, deploy, and restart RimWorld.
Enable development mode, open debug actions, choose **RimArts → Grant kit...**, click a
humanlike pawn, and choose **Shinra Tensei (animation only)**. Select that pawn and press its
**Shinra Tensei** ability button. It is available drafted or undrafted and casts in place.
Another cast becomes available when its VFX finish.

The kit currently uses a developer-granted hediff as its single ability source; there is no
recipe, research or permanent acquisition decision yet. Removing that hediff removes the
ability. The grant survives saving. The transient wave itself is not saved.

**Melee Animation is required for the pawn cast.** The button explains this when unavailable.
The existing optional integration supplies the pawn body, both hands, skin/glove rendering,
and its temporary animation job. There is one clip and no mirroring: the wave is centred on
the caster and radial, so the gesture has no direction to carry and the bridge never reads the
caster's rotation. The clip draws the pawn facing south for its length, which is the one facing
that shows both arms at full extension instead of hiding one behind the torso. No weapon or
grenade is drawn. The animation contains no gameplay events or end-cell move.

## Timing

The hands snap out at 0.38 seconds, the same `ChargeEnd` used by the shell, reaching 0.76
cells across at 0.48. They hold their height through the extension; letting them descend as
they open reads as a strike toward the floor. The span is deliberately short: Melee Animation
draws a hand sprite and no arm, so a hand carried well away from the torso is a mitt floating
in open ground. The peak puts each hand just outside the body silhouette and no further, and
the gesture is carried by the hand splay and the held height rather than by distance. The
gesture recovers by 1.35 seconds.
During the gesture, VFX read Melee Animation's actual `CurrentTime`, so pausing or changing its
animation speed does not separate the hands from the wave.
The dome does not swell from nothing. It appears at 88% of its size at 0.38 seconds, punches
to 106% by 0.48, and settles to exactly its held size by 0.70. Three impact waves are born at
the core at 0.38, 0.58 and 0.78 seconds and die against the shell 0.44 seconds later each. A
bright flash at the caster covers 0.38 to 0.56 seconds; the ground ring and the dust travel
outward to 1.25 times the dome radius by 1.08 seconds, and the ring clears by 1.43. The screen
warp holds while the shell does and fades with it. After recovery, the remaining VFX use game
ticks: the shell disappears at effect time 2.05 seconds, and dust finishes at 3.4 seconds. A
downed/dead/despawned caster or an interrupted gesture cancels its VFX. Different pawns have
independent casts.

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
and height shifts artwork north by `0.75 * height`. The dome's width radius is 3.5 cells and
it never scales past that; the ground ring and dust reach 4.4 cells. The wave is centred on
the caster's cell and is the same in every direction.
These are art dimensions, not a gameplay area. A later circular hit radius needs its own
truthful ground indicator if relevant. Fixed render altitudes avoid clipping a tall sphere
into RimWorld's close camera. There is no physical volume and no custom shader of this mod's
own; the warp is the game's.

The shell has a nearly clear interior, a soft bright outline, two curved highlights and a
faint base arc. Four ribbon meshes trace projected latitudes and sweep up the shell once.
Each impact wave draws two rings, one on the dome's base and one half way up it, so it reads
as a sphere leaving the core rather than a ripple on the floor. Forty-one dust sprites are
layered behind and in front of the shell, with a private deterministic RNG. Materials and two
192-triangle meshes — the near-surface sweep and the
closed ring — are cached; frames reuse their property blocks.

The screen warp uses RimWorld's own `MoteLargeDistortionWave` shader with the core
`PsychicDistortionCurrents` and `PsycastNoise` maps, masked by this mod's `Distort` texture to
the dome silhouette. That shader type and both maps are declared by Core, not by a DLC, and
`ApiChecks` pins all three. The renderer resolves them once and draws everything else
unchanged if any of them is missing, so the warp is the only thing a failure costs.

Sprite sizes, drift distances and the dust count are fractions of `ShinraVfxTiming.Radius`
rather than absolute cells, so changing that one constant rescales the whole effect instead of
leaving full-size dust around a smaller shell. The ground ring draws at `AltitudeLayer.MoteLow`,
above filth and below pawns, and reuses the ribbon texture at a fixed column so its seam does
not show as a gap.

`python3 make_shinra_textures.py` generates the six committed PNGs using Pillow. Changing
projection constants requires updating that script and `ShinraVfxTiming` together.
`python3 make_shinra_anim.py` generates the single hand-animation JSON file; it reads the
release time from `ShinraVfxTiming` and shares the grenade generator's JSON/curve helpers.
`ApiChecks` fails if a second Shinra clip reappears.

## Sound

Two SoundDefs in `1.6/Defs/SoundDefs/AG_Shinra_Sounds.xml`, both pointed at audio Core already
ships, so there are no new audio files. `AG_ShinraCharge` is the verb's `soundCast` and plays
when the button is pressed; `AG_ShinraRelease` layers a deep mortar body at pitch 0.38-0.45
with the same psychic texture an octave up, and is played from code when the effect clock
crosses `ChargeEnd`. That is what keeps the boom on the frame the dome appears: a paused or
slowed gesture carries the sound with it, exactly as it carries the VFX. It fires once per
cast, never for a gesture cut short before the thrust, and never for the frozen-peak preview,
which opens after the release.

Volumes are this mod's own. Core's `Explosion_Thump` carries the right body at volume 80,
which is a mortar landing next to the player every time the ability is pressed; the release
sits at 38 and 26 across its two layers. `ApiChecks` pins both defNames against the DefOf that
names them and both clip folders against Core.

## Verification

Run `dotnet run --project Tests/ShinraVfx/ShinraVfx.csproj` for timing, preview lifecycle,
and pawn-cast synchronization/cancellation checks. These use production lifecycle code with
map, draw and animation API stubs. Run `Tests/OriginBlade/ApiChecks/ApiChecks.csproj` to check
the installed Melee Animation clock API and exported clip schema, and `python3 validate.py`
for XML, classes, textures and single-source ability grants.

In-game checks still needed: grant/remove kit, both hands visible with weapons/gloves and
staying visually attached to the body at peak span, sound level and whether the two release
layers muddy each other, the
pawn being turned to face south for the cast, pause and animation-speed changes, normal/max
zoom, repeated casts, two casters, cancellation, save/load, and overlaps with pawns, trees and
walls. The ground ring
needs checking over floors and filth and under pawns, and the reduced radius needs checking
for dust washing out the caster. The warp is the one part whose shader contract cannot be
checked without the renderer: confirm it distorts rather than drawing a dark square, and that
its intensity of 0.12 is not too strong at normal zoom. The actual
renderer is required to judge hand layering, brightness and resemblance to the concept.
