# Ability Genes

A RimWorld **1.6** mod adding five genes, each granting active abilities that do
something no vanilla gene — and as far as I can tell, no popular gene mod — does.

## Dependencies

| Dependency | Required | Why |
|---|---|---|
| **Biotech DLC** | **Yes** | Genes do not exist without it |
| **Harmony** (`brrainz.harmony`) | **Yes** | The stasis field patches `Thing.DoTick` and `Thing.TakeDamage` |
| Royalty / Ideology / Anomaly | No | Deliberately not referenced — no def or texture in this mod resolves to a DLC other than Core or Biotech |
| Any framework (VEF, EBSG, …) | No | — |

## The genes

### Corrosive glands → *disarm spit*
Spit contact acid at a target's weapon; they drop it and it lands a few cells away,
forbidden. **Deals no damage at all.** Two charges, 8 in-game hours each.
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

### Innate time lattice → *time alter* (three abilities)
Alters the flow of time **inside one body**. No field, no radius — nothing around the
carrier changes rate. Met −2, Cpx 4.

| Ability | Rate | Duration | Cooldown | Strain |
|---|---|---|---|---|
| Double accel | ×2 | 20s | 1 in-game hour | +0.020/s → 0.40 |
| Square accel | ×4 | 6s | 1 in-game day | +0.150/s → 0.90 |
| Stagnate | ×1/3 | 60s | 6 in-game hours | none |

`AG_TemporalStrain` accrues while accelerating and sheds only **0.15/day**, so it outlives
the fight — a full Square Accel takes ~6 days to clear, and repeated use stacks. At
severity 0.85 it starts applying **permanent** scars to internal parts; without that a
competent doctor would make the whole cost temporary.

**Stagnate** is the inverse and the part nothing else in RimWorld does: at 1/3 rate the
pawn is nearly useless, but bleeding, infection and toxic buildup all slow by the same
factor. It's what you press when someone is bleeding out and no doctor is free. Costs no
strain.

Because acceleration is implemented as extra ticks, hunger, rest, bleeding and infection
*also* run at the multiplier. That emergent penalty may be a better balance lever than the
strain numbers, and wants watching before either is tuned.

### Stasis organ → *stasis field* (archite)
Collapses a 6.9-cell sphere of stopped time around the caster for 20 seconds. Inside it:
pawns, projectiles in flight, fire and gas all halt, and **nothing can be harmed**.

It does not spare your own colonists and it does not spare the caster, who is at the
centre and always inside. What you buy is twenty seconds for everyone standing outside
the bubble. Cpx 4, Arc 1, five-day cooldown.

## How time alter works

Two directions, two mechanisms, and the harder-sounding one is the easier.

**Acceleration needs no Harmony patch.** `MapComponent_TimeAlter.MapComponentTick` runs once
per game tick and calls `Pawn.DoTick()` the extra times the multiplier owes. Driving it off a
hediff means save/load is free and the ability is a vanilla `GiveHediff`.

**Stagnation reuses the stasis field's `Thing.DoTick` prefix**, which now answers a rate
rather than a yes/no: a stagnating pawn ticks when
`(TicksGame + thingIDNumber) % 3 == 0`. The `thingIDNumber` offset matters — without it every
stagnating thing would tick on the same game tick, which reads as synchronised stuttering
rather than slow motion.

Precedence is correct by construction: a pawn in a stasis field has `DoTick` prefixed to
false, and the extra acceleration calls go through the same prefix, so stopped beats
accelerated with no special case.

### Two traps this design walks into

**`HediffCompProperties_Disappears` cannot own the duration.** An accelerated pawn ticks its
own hediffs N times per game tick, so a Disappears comp expires N times too early — Square
Accel would last 1.5 seconds instead of 6. `HediffCompProperties_TimeAlter.durationTicks`
counts *game* ticks instead.

**Anything rate-sensitive needs a game-tick guard.** Strain accrual, strain decay and the
permanent-injury roll all run inside comps that tick N times per game tick, which would
silently scale them by the multiplier. Each guards on `Find.TickManager.TicksGame`.

### Afterimages

`PawnRenderer.RenderPawnAt` exposes no alpha, so these are **solid** copies of the pawn drawn
at recorded positions, not fading ghosts. Ghost count tracks the tier, so you can read how
hard someone is pushing without opening the gizmo bar. If it looks wrong in play, set
`drawAfterimages` false on the hediff comp rather than rebuilding.

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

The same dome backs the Royalty **Bullet Shield** psycast, which spawns
`BulletShieldPsychic` carrying that same comp — so if you have Royalty, the two are the
same texture and need to be told apart by colour. Every vanilla force field is
desaturated and idles faint:

| Dome | Colour | Idle alpha |
|---|---|---|
| Bullet shield (psycast) | `(0.4, 0.4, 0.4)` | 0.2 |
| Mech shield / Legionary / Centurion | `(0.4, 0.4, 0.4)` | 0.2-0.5 |
| Broadshield projector | `(0.6, 0.6, 0.8)` | 0.05 |
| Mortar shield generator | `(0.6, 0.6, 0.6)` | - |

Two differences from those:

- The tint is off-white with a blue bias, `(0.75, 0.90, 1.0, 0.40)`, so it reads as ice
  rather than as another grey bullet shield, and it does not pulse. It sits only slightly
  more solid than vanilla idle because the player has to see exactly who got caught.
  Set `domeColor` on the ability comp to change it.
- A faint ground outline is drawn underneath it via `GenDraw.DrawFieldEdges`. The dome
  alone looks better, but this field freezes *your own pawns*, so the exact cell boundary
  needs to be readable rather than merely suggested.

Both fade in over 15 ticks and out over the last 45 so the field does not pop.

Colours are `DomeColor` and `EdgeColor` at the top of `MapComponent_TimeBubbles`.

### A trap worth knowing about

`[StaticConstructorOnStartup]` guarantees RimWorld *will* run a class's static constructor
on the main thread at startup. It does **not** prevent anything else triggering that
constructor earlier.

Def parsing runs on a background thread and builds CompProperties via
`Activator.CreateInstance`, which runs their field initializers on that thread. So a
CompProperties field written as:

```csharp
public Color domeColor = TimeBubbleGraphics.DefaultDomeColor;   // don't
```

fires `TimeBubbleGraphics`'s constructor during XML parsing. If that constructor loads a
texture you get `Tried to get a resource from a different thread`, and the material stays
broken for the whole session.

Hence the split: constants referenced by CompProperties live in `TimeBubbleDefaults`,
which loads nothing. `TimeBubbleGraphics` holds the Material and is touched only from the
draw path, and resolves it lazily as a second line of defence.

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

## Validating

```bash
python3 validate.py
```

Checks XML well-formedness, that every def and texture reference resolves in Core or
Biotech only, that custom `Class=` values exist in the built assembly, and that every
`Translate` key used in C# is defined.

It also checks for **duplicate `Name=` declarations**, which is worth calling out because
it is not obvious: RimWorld's `Name` attribute is a single namespace shared by *every def
type in a mod*. An abstract `HediffDef` and an abstract `AbilityDef` cannot share a name.
The second one is dropped with one error, and its children then silently inherit the wrong
base — which surfaces as a cascade of confusing "doesn't correspond to any field in type
HediffDef" errors against your AbilityDefs.

## Testing

`./deploy.sh` copies the mod into the local RimWorld `Mods/` folder.

Verified statically: the C# compiles against the real 1.6 assembly, every def/texture
reference is checked to resolve in Core or Biotech, and every custom `Class=` in XML is
checked against the built DLL.

Confirmed in-game (1.6.4871, alongside ~40 other mods including Vanilla Psycasts Expanded):

- the four genes appear under *special abilities*, archite gene behind Ignore restrictions
- the stasis field renders, and projectiles visibly halt in mid-air inside it
- no noticeable frame cost with a field up, despite the prefix sitting on `Thing.DoTick`

Still unverified:

- **saving and reloading with a field still up** - bubbles persist through
  `MapComponent_TimeBubbles.ExposeData`, and that path has never run
- provoke against pawns already fleeing or in a mental state
- disarm spit against mechs
- whether the metabolic overdrive exchange rate feels right; a full stomach buys about
  20 seconds, which is the number most likely to need tuning
