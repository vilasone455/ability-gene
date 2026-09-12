# Shinra Tensei: visual prototype

This preview recreates the concept's transparent raised shell, silver highlights, curved
surface sweeps, ground dust, and tumbling stones with detached shadows. All stones are
decorative sprites. It applies no damage, knockback, projectile interception, terrain
changes, pawn animation overrides, or ability costs.

## Preview in RimWorld

Build with `dotnet build Source/RimArt/RimArt.csproj -c Release`, deploy the mod, and restart
RimWorld if it was already running. Enable development mode and open the debug actions menu.
Search for **Shinra Tensei** under **RimArts**, choose an action, then click visible ground
under a pawn or in an open area:

- **VFX preview** plays the 3.4-second sequence.
- **VFX slow motion** plays at quarter speed (13.6 seconds).
- **VFX frozen peak** holds the expanded shell for inspection.
- **clear VFX** removes the active preview, including a frozen one.

The clock runs while the game is paused, independently of the game's speed. A new preview
replaces the previous one on that map. Switching maps suspends it. Nothing is saved, and
loading a save removes the preview. There is no equipment or psycast to acquire yet.

## Animation and rendering

The first 0.38 seconds compress a small flash and ring at the caster's position. Expansion
decelerates into an eight-cell visual width radius at 1.28 seconds; the shell fades out by
2.05 seconds, with dust dissipating by 3.4 seconds. The frozen view samples 1.2 seconds.

The hemisphere is an illustrated projection: its ground footprint has depth `0.68 * radius`,
and height shifts artwork north by `0.75 * height`. These are art dimensions, not a gameplay
area. A later circular hit radius must get its own truthful ground indicator if needed.
Using fixed render altitudes avoids bringing a large sphere through RimWorld's overhead
camera or relying on pawn sprites to supply real 3D occlusion. This is not physical
volumetric rendering, screen refraction, or a custom shader.

The shell has a nearly clear interior, a soft bright outline, two curved highlights and a
faint base arc. Four independently moving ribbon meshes trace projected latitudes. Dust
is layered behind and in front of the shell; rear rocks are under its translucent surface,
and front rocks are over it. Each rock follows an outward path and vertical arc, spins,
and has a ground shadow. The particle layout is deterministic and uses a private RNG.

`make_shinra_textures.py` generates the eight committed PNGs using Pillow. Changing the
projection constants requires updating both that script and `ShinraVfxTiming`. Runtime
materials and one 192-triangle sweep mesh are cached; frames reuse their property block.
There are 76 dust sprites and 30 rocks, with at most one preview per map.

## Verification

Run `dotnet run --project Tests/ShinraVfx/ShinraVfx.csproj` for animation envelope and
preview lifecycle checks, and `python3 validate.py` for the repository's static checks.

In-game visual acceptance still requires checking normal and maximum zoom, a paused map,
slow motion, repeat clicks, frozen peak/clear, map switches, and save/load. Check overlap
with a pawn, trees and walls, and verify dust/rocks do not draw in fog or outside the map.
Compare the raised silhouette, readability of the pawn, and ground/shadow separation with
the concept. Transparency order and brightness need to be judged in the actual renderer.
