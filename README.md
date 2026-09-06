# Ability Genes

A RimWorld **1.6** mod adding four genes, each granting an active ability that does
something no vanilla gene — and as far as I can tell, no popular gene mod — does.

## Dependencies

| Dependency | Required | Why |
|---|---|---|
| **Biotech DLC** | **Yes** | Genes do not exist without it |
| **Harmony** (`brrainz.harmony`) | **Yes** | The stasis field patches `Thing.DoTick` and `Thing.TakeDamage` |
| Royalty / Ideology / Anomaly | No | Deliberately not referenced — no def or texture in this mod resolves to a DLC other than Core or Biotech |
| Any framework (VEF, EBSG, …) | No | — |

## The four genes

### Corrosive glands → *disarm spit*
Spit contact acid at a target's weapon; they drop it and it lands a few cells away,
forbidden. **Deals no damage at all.** Two charges, ~5.5 in-game hours each.
Works on mechs. Met −2, Cpx 1.

The point is removing a threat without removing the body — a raider you would rather
recruit, or a doomsday launcher you would rather not eat.

### Hypermetabolic glands → *metabolic overdrive*
Burns nutrition directly into wound repair, ~1.5 injury severity per second for
0.05 nutrition. Ends itself the moment the pawn runs out of injuries *or* runs out of
food. Bleeding wounds are prioritised. Met −3, Cpx 2.

The cost is real: healing a badly mangled colonist can take them from fed to starving.

### Alarm pheromones → *provoke*
Every hostile within 12.9 cells drops what it is doing and comes for the carrier, for
20 seconds. The carrier gets +0.20 sharp/blunt armor and ×0.85 incoming damage while it
holds — enough to make the decision survivable, not enough to make it safe. Met −2, Cpx 1.

RimWorld has no aggro system. This works by rewriting each hostile's `enemyTarget` and
interrupting their current job once so their AI re-acquires; `enemyTarget` is then
re-pointed every second to keep it sticky without thrashing their job queue. Ranged
pawns stay ranged rather than being forced into melee.

### Stasis organ → *stasis field* (archite)
Collapses a 6.9-cell sphere of stopped time around the caster for 20 seconds. Inside it:
pawns, projectiles in flight, fire and gas all halt, and **nothing can be harmed**.

It does not spare your own colonists and it does not spare the caster, who is at the
centre and always inside. What you buy is twenty seconds for everyone standing outside
the bubble. Cpx 4, Arc 1, five-day cooldown.

## How the stasis field works

RimWorld 1.6 reworked ticking. `TickList.Tick()` now calls `Thing.DoTick()`, which is
public, non-virtual, and fans out to `Tick` / `TickInterval` / `TickRare` / `TickLong`
plus held-contents ticking. That makes it the single place to intercept: a Harmony prefix
returning false stops a thing completely. Because `tickDelta` is only incremented inside
`DoTick`, there is no catch-up burst when the field lifts.

`Thing.Suspended` looks like the natural hook but is not usable — it returns false for
anything spawned on a map.

Lifetime is owned by `MapComponent_TimeBubbles`, not by a Thing. A Thing at the centre
would freeze along with everything else and could never expire itself.

`TimeBubbleRegistry` exists purely so the `DoTick` prefix stays cheap: `DoTick` runs for
every ticking thing every tick, so the no-bubble case costs one static field read.

### What it looks like

The dome is the same visual vanilla uses for projectile-interceptor shields — mech
cluster shields and firefoam poppers: the `Other/ForceField` texture on a `plane10` quad
under the `MoteGlow` shader, drawn at `AltitudeLayer.MoteOverhead` and scaled to
`radius * 2 * 1.1601562`. That last constant is vanilla's `TextureActualRingSizeFactor`;
the visible ring occupies less than the full quad, so the quad has to be scaled past the
true radius for the ring to land on the right cells.

That texture lives in the base game's `resources.assets`, not in a DLC asset bundle, so
it resolves with only Biotech installed.

Two differences from the vanilla shield:

- The tint is a cold near-white blue rather than the shield's saturated colour, and it
  does not pulse. The field is meant to read as an absence, not an energy weapon.
- A faint ground outline is drawn underneath it via `GenDraw.DrawFieldEdges`. The dome
  alone looks better, but this field freezes *your own pawns*, so the exact cell boundary
  needs to be readable rather than merely suggested.

Both fade in over 15 ticks and out over the last 45 so the field does not pop.

Colours are `DomeColor` and `EdgeColor` at the top of `MapComponent_TimeBubbles`.

### Tuning it

`frozenAreInvulnerable` on the ability comp (`1.6/Defs/AbilityDefs/AG_Abilities.xml`)
controls whether frozen things can be damaged. It ships **true**, which makes the field a
stall. Setting it **false** turns it into a free-hit window — freeze a raid, then shoot it
apart while it cannot respond. That is a very large balance swing, which is why it is a
field you can flip rather than a decision baked into the code.

`radius` and `durationTicks` sit beside it. 60 ticks = 1 second.

## Layout

```
About/About.xml                  metadata, Biotech + Harmony dependencies
loadFolders.xml                  1.6 only
1.6/Defs/AbilityDefs/            4 AbilityDefs + AG_Genetic category
1.6/Defs/GeneDefs/               4 GeneDefs
1.6/Defs/HediffDefs/             2 hediffs (overdrive, provoking)
1.6/Assemblies/AbilityGenes.dll  built output, committed
Languages/English/Keyed/         message strings
Source/AbilityGenes/             C# source
```

Def prefix is `AG_`. All icons point at existing Core/Biotech textures, so the mod ships
no art and still renders correctly; see `iconPath` on each def to swap in your own.

## Building

Needs a .NET SDK; the game assemblies are referenced straight out of the install.

```bash
dotnet build Source/AbilityGenes/AbilityGenes.csproj
```

Output goes directly to `1.6/Assemblies/`. Override the game path if yours differs:

```bash
dotnet build Source/AbilityGenes/AbilityGenes.csproj \
  -p:RimWorldManaged="/path/to/RimWorld/RimWorldWin64_Data/Managed"
```

Harmony is referenced with `ExcludeAssets="runtime"` so `0Harmony.dll` is never copied
into `Assemblies/` — shipping a second copy alongside the Harmony mod causes load errors.

## Testing

`./deploy.sh` copies the mod into the local RimWorld `Mods/` folder.

**Not yet tested in-game.** Everything here is verified statically: the C# compiles
against the real 1.6 assembly, every def/texture reference is checked to resolve in Core
or Biotech, and every custom `Class=` in XML is checked against the built DLL. None of
that is a substitute for loading a save.

Worth watching for on the first run:
- frame time while a stasis field is up (the `DoTick` prefix is on the hottest loop in the game)
- a stasis field saved and reloaded mid-duration
- provoke against pawns that are already fleeing or in a mental state
