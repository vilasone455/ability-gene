# Shinra Tensei: charged repulsion

Install a **repulsion eye** (3,200 silver) to gain Shinra Tensei. It uses the vanilla archotech
implant trade pool and standard quest rewards, has no crafting recipe, and replaces one eye
at normal sight efficiency. Installation requires Medicine 8, the device, and two medicine.
Vanilla remove-body-part surgery recovers the device for transfer. Additional eyes share one
set of controls, charge, toggle and cooldown. Legacy developer grants migrate on the first
game tick to one installed eye; existing ability and legacy hediff identifiers remain valid.

**Melee Animation is required.** Development mode still provides **RimArts → Grant kit... →
Shinra Tensei (repulsion eye)** and the independent VFX previews below.

## Controls and timing

Click **Charge Shinra Tensei**, then **Release**. The charge meter reaches full power after
180 game ticks (3 seconds), including the opening motion. The preview radius is always four
cells. **Cancel** discards charge without cooldown. **Auto-release** is saved per pawn and
starts off. Full charge holds indefinitely until released or cancelled.

The single existing clip draws the hands toward the chest from 0 to **0.27 seconds**, holds
exactly there while charging, then resumes on release. Crossing **0.38 seconds** commits the
push, projectile defense, dome and release sound once. The pawn recovers at **1.35 seconds**.
Early release freezes power and continues through the hold marker without stopping.
Requesting release commits **1,200 ticks (20 seconds) of cooldown**, even if interrupted
before the burst. Stun, downing, death, leaving the map, a movement order or loss of the last
eye cancels. Attacks and other orders are blocked while casting; ordinary damage does not
cancel or receive any reduction from Shinra.

A game component advances gameplay and calls the renderer's `Seek` explicitly with
`TimeScale = 0`. The Melee Animation speed setting changes clip progression, never charge
accumulation. Drawing cannot advance gameplay, including on another selected map. Pausing
stops both clocks. Held casts reload at their saved pose; loading interrupts releases without
refunding cooldown or clearing an already committed defense window. VFX tails finish even
when the pawn's recovery or a post-burst interruption has ended the animation.

## Combat defaults

These are tuning defaults, not measured balance results. Charge continuously interpolates
human-sized push from 3 to 7 cells, collision damage from 8 to 20 blunt, and projectile power
limit from 12 to 60 direct damage before armor. At half charge these are 5, 14 and 36.
Defense lasts 45 ticks (0.75 seconds) at every power. All living pawns, including allies,
animals, mechs and downed pawns, can be pushed if the caster has an unobstructed path.
Travel divides by `max(1, BodySize)` and ends on a map cell. Every pushed pawn staggers for
30 ticks. Only solid-obstacle collision deals damage; map edges and pawns do not. Buildings
and terrain are never damaged.

Incoming direct projectiles are redirected outward; outgoing shots pass. Direct-fire
explosives also require full charge. Overpowered rounds, overhead shells and arcing grenades
pass unchanged. Travel segments are checked before impact, including fast shots crossing the
whole field. Each burst remembers its redirected rounds. Overlapping fields choose the first
entry on the current segment. Speed, direct damage, remaining range and explosive behavior
are preserved, with hits attributed to the caster and friendly fire enabled. CE additionally
saves the forced straight trajectory and preserved damage, and clips its last step to the
remaining endpoint.

Auto-release requires full charge and a reflectable shot on a path to the caster, regardless
of allegiance. Its horizon is remaining clip time to burst divided by animation speed, plus
0.15 seconds. From the held pose at normal animation speed this is 0.11 + 0.15 seconds.
Close-range shots can hit before the burst begins protection. Enemy autonomous charging is
outside this version.

## Artwork timing

The dome appears at 88% of its four-cell size at 0.38 seconds, punches to 106% by 0.48 and
settles by 0.70. The existing textures, south-facing gesture, hand span, flash, impact pulses,
ring and dust remain. Pulses start at 0.38, 0.58 and 0.78 seconds. After hand recovery, the VFX
tail advances in game time: the shell ends at 2.05 seconds and dust at 3.4 seconds.

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
and height shifts artwork north by `0.75 * height`. The dome settles at a four-cell width
radius after its brief punch; the ground ring and dust reach five cells. The wave is centred
on the caster's cell. Its illustrated footprint differs from the circular gameplay area,
which has a separate four-cell ground indicator. Fixed render altitudes avoid clipping a tall sphere
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
ships, so there are no new audio files. `AG_ShinraCharge` plays when the charge controller
starts the gesture; `AG_ShinraRelease` layers a deep mortar body at pitch 0.38-0.45
with the same psychic texture an octave up, and is played from code when the effect clock
crosses `ChargeEnd`. That is what keeps the boom on the frame the dome appears: a paused or
slowed gesture carries the sound with it, exactly as it carries the VFX. It fires once per
cast, never for a gesture cut short before the thrust, and never for the frozen-peak preview,
which opens after the release.

Volumes are this mod's own. Core's `Explosion_Thump` carries the right body at volume 80,
which is a mortar landing next to the player every time the ability is pressed; the release
sits at 38 and 26 across its two layers. `ApiChecks` pins both defNames against the DefOf that
names them and both clip folders against Core.

## Visual acceptance

Live rendering is still needed to judge hand layering with weapons/gloves, attachment to the
body at peak span, sound levels, the south-facing gesture, normal/max zoom, overlapping VFX,
and the ground ring over floors and filth. Confirm the warp distorts rather than drawing a
dark square, and judge its intensity of 0.12 at normal zoom.

## Charged-combat verification

Automated checks:

```sh
dotnet build Source/RimArt/RimArt.csproj -c Release
dotnet run --project Tests/ShinraVfx
dotnet run --project Tests/ShinraCombat
dotnet run --project Tests/VectorEdit
dotnet run --project Tests/OriginBlade/ApiChecks
python3 validate.py
```

The lifecycle tests run the production game controller with stubbed animation, drawing and
combat boundaries. Combat tests run the production push/interception logic and both projectile
adapters against controlled game boundaries. API checks inspect the installed RimWorld,
Melee Animation and CE assemblies. These checks do not constitute live combat testing.

In-game acceptance checks still require RimWorld, once with vanilla projectiles and once
with Combat Extended:

- Install, remove and transfer an eye; install two eyes; reload an old developer grant save.
- Hold for a minute, pause, change animation speed and switch maps. Verify the hands stay at
  the chest with no dome or release sound, while the meter stops at full.
- Release early and at full charge, cancel, issue a movement order, attempt an attack, stun,
  down or kill the caster. Save/load during opening, hold, before burst and during defense.
- Push allies, animals, mechs, heavy bodies and downed pawns into open ground, walls and map
  edges. Confirm no damage to terrain/buildings and no collision damage from other pawns.
- Fire rounds at each threshold and just above it; test rockets, shells, grenades, friendly
  fire, edited projectiles, close shots, fast shots and overlapping bursts. Verify one burst
  per cast and one redirection per projectile per burst. Save/load redirected CE rounds.
- With auto-release off, verify incoming shots never release the charge. With it on, verify
  only full charge responds, misses pass without triggering, and close shots can hit first.
