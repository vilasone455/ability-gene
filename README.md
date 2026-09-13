# RimArts: Combat Abilities

A RimWorld **1.6** combat ability mod. Each kit has one source: a gene, trait, implant,
piece of equipment, weapon trait, or earned origin.

| Source | Kit | How to obtain it |
|---|---|---|
| Gene | **Corrosive glands** | Acquire and implant the gene |
| Gene | **Hypermetabolic glands** | Acquire and implant the gene |
| Gene | **Anchor organ** | Acquire and implant the gene |
| Gene | **Fold organ** | Acquire and implant the archite gene |
| Gene | **Dispersal plexus** | Acquire and implant the gene |
| Trait | **Combat presence** | Appears naturally as a pawn trait |
| Trait | **Pain debt** | Appears naturally as a pawn trait |
| Trait | **Commanding voice** | Appears naturally as a pawn trait |
| Implant | **Neural accelerator** | Craft after Bionics research and install surgically |
| Implant | **Reflex booster** | Craft after Prosthetics research and install surgically |
| Implant | **Phase barrier** | Find through quests or deep-space trade and install surgically |
| Equipment | **Stasis belt** | Research Stasis Fields, craft it, and wear it |
| Equipment | **Retrieval hook belt** | Research Machining, craft it, and wear it |
| Weapon trait | **Resonant** | Find on a Unique Melee Weapon |
| Weapon trait | **Arcing** | Find on a Unique Melee Weapon; also requires Melee Animation |
| Earned origin | **Origin: Blade** | Reach Melee 14 and Crafting 12, study five blade types, then awaken |

## Dependencies

| Dependency | Required | Why |
|---|---|---|
| **Biotech DLC** | **Yes** | Required by the five gene kits; several ability icons also reuse Biotech art |
| **Harmony** (`brrainz.harmony`) | **Yes** | Patches `Thing.DoTick`, `Thing.TakeDamage`, `Projectile` flight and damage, and `Selector.SelectorOnGUI` |
| **Odyssey DLC** | No | Weapon trait abilities are `MayRequire`d against it |
| **Melee Animation** (`co.uk.epicguru.meleeanimation`) | No | Required for the Arcing weapon trait and its ability. Also supplies the grenade-throw animation: without it the frost bomb is thrown instantly and with no animation, which is how every thrown weapon in the base game works |
| **Combat Extended** (`ceteam.combatextended`) | No | Supported, not required. Its rounds are not `Verse.Projectile`, so the three kits that act on rounds in flight reach them through a reflection bridge — see *Rounds in flight, and Combat Extended*. The frost bomb also gets CE stats and a CE melee tool from `Patch_CombatExtended/`, and stays reusable rather than becoming one-use ammo — see *The frost bomb under Combat Extended* |
| **Unique Melee Weapons** (`shunter.uniquemeleeweapons`) | No | Melee weapon traits (resonance, arc) are `MayRequire`d against it |
| Royalty / Ideology / Anomaly | No | Not referenced |
| **Vanilla Expanded Framework** (`OskarPotocki.VanillaFactionsExpanded.Core`) | **Yes** | Shared framework available for future abilities; existing kits retain their current implementation |

## Abilities

### Repulsion eye → *Shinra Tensei*

Install an uncraftable repulsion eye from rare trade or standard quest rewards, then click
**Charge**, hold for up to three seconds of power, and **Release**. The hands freeze at the
chest until release. A four-cell wave pushes living pawns, including allies, damages them
only on obstacle collision, and briefly redirects qualifying incoming projectiles. Release
commits a 20-second cooldown. A charge meter, Cancel button and per-pawn Auto-release toggle
provide control; full charge can be held indefinitely.

The eye costs 3,200 silver, provides normal sight, and can be recovered surgically.
Installation requires Medicine 8, the device and two medicine. Multiple eyes share one
ability. Melee Animation is required; vanilla and Combat Extended projectiles are supported.
Development grants and independent VFX previews remain available. See
[controls, balance and verification notes](docs/shinra-tensei-vfx.md).

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

### Combat presence → *provoke* (trait)
Every hostile within 12.9 cells drops what it is doing and comes for the speaker, for
20 seconds. The speaker gets +0.20 sharp/blunt armor and ×0.85 incoming damage while it
holds — enough to make the decision survivable, not enough to make it safe.

RimWorld has no aggro system. This works by rewriting each hostile's `enemyTarget` and
interrupting their current job once so their AI re-acquires; `enemyTarget` is then
re-pointed every second to keep it sticky without thrashing their job queue. Ranged
pawns stay ranged rather than being forced into melee.

### Neural accelerator → *time alter* (implant)
Alters the flow of time **inside its user**. No field, no radius — nothing around the
user changes rate.

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

### Reflex booster → *reflex surge*, *vector manipulation*, *vector shove* (implant)
A spinal implant that reads momentum and spends it again.

| Ability | Effect | Cooldown |
|---|---|---|
| Reflex surge | 5s of game time at a **quarter** the world's real-time rate — about 20s of yours. Buys time and nothing else | 30s |
| Vector manipulation | Catches every round due to pass within 12 cells of the carrier in the next 21 ticks, pauses, and lets you turn and re-throw them in up to four groups | 5s |
| Vector shove | Throws one pawn ~6 cells away from the carrier, stunned, hurt by how far they went | 20s |

**Vector manipulation is an editor, not a cast.** Clicking the gizmo scans once, stops the
clock, and opens a panel on what it caught. Drag on the map to sort the rounds into as many
as four non-overlapping groups; give each group a rotation from −180° to +180° and a force
from ×0.25 to ×2; press **Apply and resume**. Everything commits in one go and the game runs
on at the speed it was going before. Cancel discards the draft and restores the pause state
you found. An empty scan costs nothing and does not even pause.

**Reflex surge is what makes that click possible, and it is a separate decision.** Measured
against vanilla projectiles, a rifle round crosses the 12-cell catch radius in about 10
ticks — a sixth of a second, less than a person's reaction time before they have moved the
mouse. No radius fixes that; even a map-wide catch would not, because the round's entire
flight from a shooter 25 cells away lasts roughly a third of a second. So the surge drops
the world to a quarter rate, which turns that sixth of a second into **1.4 seconds**. A half
rate would give 0.7s — still a coin flip, which is why there is one surge setting and not
two.

**The surge buys time and nothing else.** It changes no in-game relationship at all: the
bullet still crosses a cell in the same number of ticks, the carrier still walks the same
cells per tick, and every cooldown, decay and duration in this mod is counted in game ticks
and so is untouched. What changes is how much *real* time one game tick takes. The fiction
carries that honestly — the carrier's perception is what sped up, and they are slowed along
with everyone else — but the effect is on the person holding the mouse, not on the pawn.
It is priced accordingly: a small strain charge to open, a 30-second cooldown, and a gizmo
on the hediff to end it early, because 20 seconds of real time is a long while to sit
through once the shooting has stopped.

| Force | Speed and primary damage | New travel |
|---|---|---|
| ×0.25 | Quarter | 5 cells |
| ×0.5 | Half | 10 cells |
| ×1 | Baseline | 20 cells |
| ×2 | Double | 40 cells |

Range is **recalculated, not extended**. Every edited round starts again from where it was
caught with twenty cells times its force in front of it, clipped where that line leaves the
map — so a spent volley two cells from the dirt is a loaded one again, and that counts as an
edit even at no rotation and ×1. Force is absolute against the round's own def, so editing
the same round twice at ×2 leaves it at ×2 rather than ×4.

Rotation is stored as a delta and applied to each round's own captured heading, so a volley
arriving as a fan comes out of a ninety-degree turn still fanned. Nothing moves the rounds
themselves; only where they are pointing changes.

The cost is **brain strain**, and it is steeply superlinear in how many groups one
application changes: 8 for one group, 24 for two, 48 for three, 80 for four, out of 100.
Strain sheds at 100 a day and does not reset between fights, so it stacks across a
campaign. Fill the bar and the booster overloads: the top hediff stage takes 0.6 off
Consciousness, which is enough to put the carrier on the floor until enough strain has bled
off to drop out of the stage — about fifty seconds from full. The five-second cooldown is
there to stop double-clicks; strain is the real limit.

Edited rounds are credited to the manipulating pawn, keep vanilla collisions, cover,
shields and impact behaviour, and can hit allies. The preview line is where the round is
going, not a promise that it gets there.

### Phase barrier → *phase guard* (implant)

An archotech implant that divides approaching distance instead of blocking it.

| Path | What the barrier does | Half-life |
|---|---|---|
| Projectiles | Round is taken off the engine's clock; its reported position halves its remaining distance to the point it was caught heading for, forever | 6s |
| Melee | Attacker's chance to connect halves for every second spent inside the field | 1s |
| Verbless damage | Explosions, fire, collapsing roofs and point-blank rounds are scaled to 15% | — |

**Nothing is ever cancelled.** Every path is a continuous scaling toward zero that never
arrives, which is the whole point — the moment any of it becomes a hard "this does not
apply", the barrier stops being a receding distance and becomes an invulnerability
shield, which nothing in this mod does any more. A round held by the barrier is still in
flight, and when the barrier drops it resumes from exactly where it was drawn and
finishes the trip. Everything queued against the carrier's face lands in the same tick.

Capture tests the step a round took this tick, not the point it was standing on when the
scan looked at it. The field is under four cells across, and a round moving faster than that
is never sampled inside it — a vanilla rifle round is looked at six or seven times crossing
the field, a Combat Extended round two or three, and the fastest CE rounds cross it between
two ticks. The round enters at the point where its step crossed the boundary, which matters
as much as catching it at all: measured from a sample already past the carrier, the curve
would hold the round *behind* them and recede away.

A held round's anchor is fixed in world space at the moment of capture, not re-read from
the carrier. A round is a thing travelling its own straight line; re-anchoring it every
tick would drag held rounds along behind a walking carrier. Stepping out from behind your
own barrier therefore releases everything it was holding on that spot — the rounds were
never stopped, and the line they are on no longer has anyone standing on it.

The barrier cannot tell that air is also approaching. `Breathing` drops on a ramp from
the moment it opens: ~14s to the first stage, ~24s to oxygen-starved, ~32s to
asphyxiating, and at ~39s Breathing reaches zero and the carrier dies standing up.
There is no duration field anywhere — the barrier stays open until the player drops it
with the gizmo on the hediff, or until it closes them. That, not the one-hour cooldown,
is the limit.

Melee is the interesting case, because a swing in RimWorld has no position and no travel —
`Verb_MeleeAttack` resolves damage in the instant it is cast, so there is no object for the
curve to move. What there is instead is the roll the game already makes to decide whether
the blow connects, and a failed roll is something RimWorld already renders: a miss mote, a
miss sound, a combat log line. Scaling that roll gives the entire presentation for free.

### Anchor organ → *mark*, *clap*, *double clap*
Leaves marks on people and on ground, then exchanges two marked places. Met −2, Cpx 4.

| Ability | Effect | Range | Cooldown |
|---|---|---|---|
| Mark | Places or lifts a mark on a pawn or a standable cell. Three held at once, fading after one in-game day | 9.9 | 10s |
| Clap | Carrier and one mark change places. A pawn mark is a swap; a tile mark is a one-way move | map-wide, no LOS | 30s |
| Double clap | Any two marks change places with each other, both picked at cast. Carrier does not move | map-wide, no LOS | 30s |

**The clap takes both hands, so the carrier never holds a weapon.** That is the whole cost, and
it is a permanent decision about what that colonist is rather than a number on a stat. It is
enforced in `EquipmentUtility.CanEquip` rather than by dropping weapons afterwards, so the game
never offers the player a gun it is about to take away. `banWeapons` on the gene's mod extension
turns it off, which leaves only the check at cast time and makes this a support ability on an
armed pawn — a large balance swing, hence a field.

The other cost is that **marking needs range 9.9 and line of sight**. Distance is the thing this
gene does not care about; getting there in the first place is. A clap is a plan made earlier, not
an answer to what is happening now, and marking a raider means walking an unarmed pawn to within
ten cells of them.

Double clap is the one that will define it: pull a bleeding colonist out and leave whoever
dropped them standing in your firing line, without the carrier going anywhere. It spends the two
marks it was given, which is why the carrier holds three — one survives, so a double clap does
not leave them completely empty.

### Resonant → *resonance* (weapon trait)

A unique melee weapon that holds a note.

| | |
|---|---|
| Duration | 30s, self-cast |
| Note lifetime | ~5s per struck part |
| Cooldown | 1 in-game hour |

Every melee blow the carrier lands leaves the **exact part it hit** ringing for about five
seconds. A second blow on that same part while the note is live takes the part off the body.
Both blows land for exactly what they were worth — it is not a damage bonus, it is the timing.

**Which part rings is not up to anyone.** RimWorld picks a hit part by coverage weighting, so
this is pressure held on one body until the dice repeat, not a place the player can aim. That
is the whole balance of it: it is a probability engine, not an execute button.

**The note needs something that can ring.** Torso, neck, head — anything with a life inside it —
is too well damped to carry one, and blows there do nothing unusual. So this takes people
apart; it does not kill them. Losing both arms is what it does instead, which is a raider who
will never hold a weapon again.

The cost is that **the resonance does not know which side of the blow it is on**. For the whole
thirty seconds the carrier's own frame is ringing on identical terms, and anything that lands
twice on the same part of them takes that part off just as readily. Against one opponent it is
a duel the carrier is winning; walking it into a melee crowd is how a carrier comes home with
one arm.

### Pain debt → *wound debt* (trait)

The will to keep fighting before the body receives the news.

| | |
|---|---|
| Window | 20s, self-cast |
| Cooldown | half an in-game day |
| Settle early | gizmo on the hediff |

For twenty seconds every wound the carrier takes is **written down instead of applied** — no
damage, no bleeding, no pain, no going down. They keep walking, and nothing that happens to
them slows them at all.

**Nothing is prevented and nothing is reduced.** When the window ends, every wound lands at
once, in the order it was received, in a single instant. What the ability sells is not less
damage, it is a different *arrival shape* — and the shape is worse. Sixty damage spread over
twenty seconds is something a person survives while a doctor runs to them; the same sixty in
one moment very often is not, because RimWorld's death and shock checks look at what arrives
together.

That is the whole cost, and it needed no invented penalty bolted on. Dying with a debt
outstanding cancels it, which is the one mercy in the design and costs nothing to give: there
is no longer a body to tell.

The gizmo settles early, and that is the decision the ability actually offers. The bill is
coming either way. The only thing anyone gets to choose is whether it arrives here, in cover,
beside a doctor — or wherever they happen to be standing when the time runs out.

### Origin: Blade → *rain*, *loose*, *grasp* (earned)

A permanent martial origin earned through blade study, Melee 14 and Crafting 12.

A colonist studies five distinct bladed melee weapon types for one in-game hour each. When
the skill and study requirements are met, the game asks whether to awaken the origin. Awakening
removes psycasts and psylinks and forbids ranged weapons permanently; declining costs nothing.

| Ability | Effect | Cooldown |
|---|---|---|
| Rain | 14 blades come down over a 6.9-cell blob, ~12 stab where they land, and stay standing | half an in-game day |
| Loose | Every planted blade within 12.9 of a point lifts, turns and flies at it **from its own cell** | 15s |
| Grasp | The nearest blade tears out of the ground and arrives in an empty hand as real steel | 10s |

**Rain is placement, not damage.** Half the blades land on nobody and the ones that connect are
worth about one good melee hit. What the ability actually produces is a dozen objects in a shape
the player chose, and both of the other abilities are readers of that shape. Rain across a
doorway and loose is a crossfire; rain on the wrong side of a wall and loose is a wall being
stabbed twelve times.

**A planted blade is not an item, and that is the load-bearing decision.** Two vanilla rules
apply to items and both would have quietly wrecked this: only one item stack may occupy a cell,
so rain could not put two blades near each other, and a weapon on the floor generates haul jobs,
so colonists would tidy the battlefield into a stockpile mid-fight. A thing of its own has
neither problem, draws itself at the lean it landed at, and leaves room for a blade that acts
later. The cost of that choice is that nobody can loot the field, which is why grasp exists.

**The cost is a readout, not an accrual.** `AG_PanoplyDebt` has no `severityPerDay` anywhere; its
severity is *assigned* from the live blade count at 0.02 a blade every time that count changes.
So a full rain puts the carrier at 0.28 and holds them there, and the moment a blade is loosed,
grasped or called back the number falls in the same tick. The debt cannot outlive the field
because it **is** the field. Manipulation and Moving carry the cost of maintaining that arsenal.

**Loose does not consume anything.** Each blade flies from where it was standing and plants
itself again wherever it stops, which is usually much closer to the people it was thrown at.
Nothing about the volley is computed here: cover, line of sight and the bodies in between are
resolved per blade by the same projectile code a rifle round goes through, so a blade behind a
raider genuinely ignores the sandbag in front of him.

**Grasp is the only place this kit puts real matter into the world.** What arrives is an
ordinary steel longsword and it stays one — ownable, tradeable, lootable off the corpse. It
needs an empty hand and will not make one; silently dropping a colonist's rifle to give them a
sword is help nobody asked for.

Full design notes in [docs/origin-blade.md](docs/origin-blade.md).

### Stasis belt → *stasis field* (equipment)
Collapses a 6.9-cell sphere of stopped time around the caster for 20 seconds. Inside it:
pawns, projectiles in flight, fire and gas all halt, and **nothing can be harmed**.

It does not spare your own colonists and it does not spare the caster, who is at the
centre and always inside. What you buy is twenty seconds for everyone standing outside
the bubble. The belt has a five-day cooldown.

The belt is spacer tech, behind the *stasis fields* research (one
step past vanilla shields), machining table, 2 spacer components / 60 plasteel / 20 gold.

The five-day charge lives on the belt rather than on the wearer, so taking it off and
putting it back on does not refresh it, and neither does passing it between two pawns —
the longest remaining charge is the one that carries. Granted through
`RimArt.CompProperties_ApparelAbility`, because no vanilla comp grants an `AbilityDef`
from apparel: `CompEquippableAbility` replaces `CompEquippable` and works only on a
weapon, and `CompApparelVerbOwner` grants a `Verb` rather than an ability.

### Retrieval hook belt → *retrieval hook* (equipment)
Fires a padded net on a tether and pulls a **downed colonist, colony slave or ally**, or up to
**20 kg of an item stack**, to the wearer. Range 15 tiles, line of sight required. Items:
weapons, apparel, medicine, food, resources and minified buildings; a heavier stack is split
and the rest stays. The wearer stands still until the target arrives; moving, another order,
being downed or removing the belt interrupts it.

After every shot, including misses and interrupted shots, the tether must be reeled in:
**Reel in tether** is 10 seconds of stationary work. Interrupted progress is kept on the belt
and resumes when ordered again. The loaded state is saved on the belt, so swapping wearers or
reloading a save does not refresh it. Usable drafted or undrafted, by pawns incapable of
violence, not by AI.

**Injury cost:** when a dragged pawn first moves, one external wound gets up to +1 severity and
loses its bandage. Tended wounds that can bleed are chosen first. The increase is reduced in
0.01 steps so it cannot kill the pawn or destroy the part; the wound can still bleed again.
No eligible wound, no penalty.

Industrial tech, *Machining* research, machining table, Crafting 5, 60 steel / 2 components /
20 cloth, 2 kg, belt layer, no armor. See [the retrieval hook belt guide](docs/retrieval-hook-belt.md)
for targets, the pull sequence, save behaviour and the in-game test list.

### Frost bomb (weapon)
A thrown grenade, equipped in the weapon slot and aimed like the game's own. Range 12.9
cells; it bursts into a freezing cloud that stuns everything caught in it for up to three
seconds and then thaws over about ten more, movement and manipulation climbing back as it
goes.

It does not choose sides. Your own pawns freeze in it exactly as well, frozen targets can
still be shot, and bullets pass through the cloud normally — this is not the stasis belt.
Effect falls off from the centre to 45% at the rim, large targets take proportionally less
(a body-size-4 thrumbo takes about a third of what a human does), and mechanoids take 60%
of that again — they seize rather than freeze.

The freeze reaches 3.4 cells, half a cell further than the 2.9-cell blast, which is why
`Verb_ThrowFrostBomb` overrides `HighlightFieldRadiusAroundTarget`: the ring drawn under the
cursor is the cold, not the bang, because the cold is the part that catches your own line.

The damage is almost nothing. `AG_Cryo` does 5 damage, is resisted by heat armour, and has
a *negative* explosion heat energy, so what the bomb leaves behind is a cold room rather
than a crater. The seconds are the weapon.

It is pinpoint: it lands on the cell you picked, at any range. That takes a
`forcedMissRadius` of 0.4 rather than 0, because two different parts of the game read the
field. `ThingDef.ConfigErrors` requires one on a verb that launches an explosive projectile
and refuses one on a verb that does not — it tests `forcedMissRadius > 0f != CausesExplosion`,
an equality, so zero is a load error. The code that actually displaces a shot tests
`ForcedMissRadius > 0.5f`. Anything between the two satisfies the check and never moves a
grenade.

`Verb_ThrowFrostBomb.ThrownAt` still implements the real scatter — the engine's own falloff,
nothing inside three cells, half inside five, four fifths inside seven — so raising that one
number turns the bomb back into a grenade that misses without touching any C#.

Reusable, like every grenade in the base game — the tubes hold charge and the compressor
builds the next bomb between throws. The balance lever is a six-second `RangedWeapon_Cooldown`
against 2.66 for a frag grenade, and it is long for two independent reasons that agree: an
infinitely reusable *stun* on a short timer is a chain-lock rather than a tool, and the throw
animation runs 72 ticks with the hand opening at tick 35, so anything under 1.2 s would let
the pawn walk out of the cooldown stance mid-swing. **1.2 s is a floor, not a tuning knob.**

Industrial tech, behind the *cryogenic munitions* research (one step past machining),
machining table, 20 steel / 2 industrial components / 40 chemfuel. It carries no AI weapon
tags, so no raider is ever generated holding one.

It was an ability granted by a worn cryo bandolier until it became a weapon; the bandolier
and `AG_FrostBomb`'s `AbilityDef` are both gone. `CompProperties_ApparelAbility` above stays,
because the stasis belt still uses it.

**Supply model, resolved differently.** `docs/tactical-ability-ideas.md` records a preference
for XCOM-style replenishment after a sustained *safe period*. That is moot now: a reusable
grenade has nothing to replenish. The doc's underlying objection — not wanting to buy or craft
bombs one at a time — is satisfied by the item never being consumed.

### Where the freeze lives
On the `DamageDef`, not on the projectile. `RimArt.DamageWorker_Cryo` overrides
`ExplosionStart` and calls `FrostBurst.At`, so *anything* that explodes as `AG_Cryo` freezes
what it touches — a thrown bomb, or a rack of them cooking off in a warehouse fire through
`CompProperties_Explosive`.

That started as a Combat Extended requirement and turned out to be the better design
regardless. CE's verb casts what it spawns straight to `ProjectileCE`:

```csharp
protected virtual ProjectileCE SpawnProjectile()
    => (ProjectileCE)ThingMaker.MakeThing(Projectile, null);
```

so a `Projectile_Explosive` subclass of ours can never be a projectile CE throws. Hanging the
freeze on the damage unties the effect from the delivery, and `ExplosionStart` fires once per
explosion — a per-cell hook would have frozen a pawn once for every cell it stands in.

### Mimic beacon (weapon)
A thrown projector, equipped in the weapon slot and aimed like the game's own grenades.
Range 12.9 cells; where it lands it stands a **copy of the person who threw it** — same body,
same face, same clothes, same gun — which cannot move, cannot act, and cannot fight.

What it can do is look like the better target. The projection is a real object with 120 hit
points, and most hostiles shoot it instead of your colonists for the twenty seconds it holds.
When it ends — the charge running out, or the enemy taking it apart — it leaves **nothing**.
No corpse, no blood, no dropped gun, no haul job. A flash, and the cell is empty.

It is a **strong preference, not a compulsion**, and that distinction is the entire mechanic.
A raider who has already been firing on one of your colonists keeps firing on them; a raider
picking a target picks the decoy. Melee raiders walk to it and swing at it, which is the half
of the item that stops a rush rather than a volley.

There is **no taunt radius**, deliberately. The bias lives inside the game's own target
scoring, so the decoy's reach is each enemy's own acquisition radius — the distance at which
they would have noticed a colonist standing there. See *How the mimic beacon works* below for
the arithmetic and where the 1.6 comes from.

**Throw it forward.** Distance is worth a point a cell in the game's own target scoring, so a
decoy standing further from the enemy than your people are is spending its whole advantage on
making up that gap. It has about twelve cells of slack covering somebody in a ten-cell
firefight and about six at twenty-five, and past roughly forty it has none at all — at that
range the decoy has to be nearer the shooter than the person it is covering. A beacon lobbed
sideways or backwards does very little.

The cost is position. Nothing is consumed, so there is no material price per throw; what
there is instead is the decision of where to put it, and a beacon thrown badly pulls a firing
line toward your own people. Standing one beside a colonist is how you call fire onto them.

Eight-second cooldown, against six for the frost bomb. That is a short number on purpose:
`RangedWeapon_Cooldown` is not a gate on the item, it is **how long the thrower cannot move or
shoot** — see *Why a thrower stands still* below. Uptime is limited where it belongs instead,
by one decoy per thrower.

Spacer tech, behind the *holographic projection* research (past machining and microelectronics),
machining table, 40 steel / 4 industrial components / 10 gold. Like the frost bomb it carries no
AI weapon tags, so no raider is ever generated holding one.

**One decoy per thrower.** Throwing a second pops the first. Two colonists with two beacons get
two decoys, which is a squad investment and deliberately allowed. This, rather than a long
cooldown, is what stops one pawn blanketing the map — and a projection under fire rarely lives
out its twenty seconds anyway.

**It is drawn as light, not as a person.** The copy is tinted blue and rendered through the
game's own cloaking shader, which is translucent and carries a distortion texture, and it casts
no shadow. `MimicDefaults.Shimmer` turns that off for a solid blue clone with a shadow instead.

**The projection ends when its source does.** If the thrower dies, is downed, is picked up or
leaves the map, the decoy goes with them — it is copying a live signature, and there is nothing
left to copy. That is a fiction covering a real constraint, and the constraint is in the next
section.

### Fold organ → *vent*, *fold*, *swallow*, *post*, *collapse* (archite)

One hole, normally on the carrier's body, leading to a volume that is not anywhere.
Met −4, Cpx 7, Arc 1.

| Ability | Effect | Cooldown |
|---|---|---|
| Vent | 30s. Anything that finds the part the hole is in passes through instead of landing | 1 in-game hour |
| Fold | Step through your own hole. It stays behind as a ground aperture; cast again to return | 1 in-game hour |
| Swallow | Any pawn, ally or enemy, goes into the volume. Range 9.9, 3s warmup, LOS | 1 in-game day |
| Post | Push one item through an aperture from outside. Range 15.9 | 20s |
| Collapse | Destroy the volume and everything in it. Outside only | 1 in-game day |

**The hole is permanent and the connection is not.** Which body part it is in is rolled once
when the gene lands, never player-chosen, and it costs a `partEfficiencyOffset` on that part
forever. Closed, it is a hole that goes nowhere and simply does not work as a hand. That split
is deliberate: a permanent pass-through would be the only always-on effect in this mod, and
generating the volume at all would mean a pocket map ticking forever for a carrier who never
used it.

The part is the entire balance. RimWorld picks hit parts by coverage weight, so the share of
incoming fire the hole eats *is* that part's coverage — a hole in the torso is near-immunity
and a hole in a finger is nothing. The roll is banded to hand-sized, 2–10%, and the candidate
set is `ResonanceUtility.CanRing`: the parts that can hold a note are the parts that can hold
a hole, and both questions are answered off the game's own body data rather than a list.

**Nothing is cancelled and nothing is reduced.** A round that finds the hole is not stopped —
it is put back in flight inside the volume, entering from a random edge cell and crossing the
room. That distinction is the whole mechanic: setting it down on a random cell would make this
cosmetic, because a pawn occupies one cell in a thousand and a full burst would touch them
about three times in a hundred. A round that *travels* can hit whatever is standing in the way
of it.

It comes in from a random direction because the volume is not anywhere and has no orientation
relative to whoever fired. There is no honest vector to preserve, and neither side gets to aim.

**The aperture cannot be brought down.** It has no hit points, and `PreApplyDamage` reports
everything aimed at it as absorbed after handing it to the far side — the same rule the hole
follows on a body, in the other place it can be. So shooting it is not wasted ammunition; it is
a way of shooting into the room the carrier is hiding in. It gives no cover and blocks no
movement, because an indestructible thing that did either would be a permanent invulnerable
wall segment.

**Two rules need no code at all.** Despawning takes a pawn's `carryTracker` with them, so a
carrier brings in exactly one item or one downed person, each way — that is the whole capacity
rule. And RimWorld only lets you carry a *downed* pawn, so an ally swallowed while bleeding can
be carried back out and a raider swallowed on his feet cannot be picked up at all. The volume
releases what cannot walk and keeps what can, which is the entire difference between a rescue
and a prison, and neither sentence of it is enforced by this mod.

Time inside runs at normal rate, deliberately. Two other kits already own time and both pay for it;
a third doing it for free would blur their roles. Leaving it alone also
means the two compose — a carrier with a neural accelerator can fold out and Square Accel through
their own healing, in a room with no doctor, at four times the hunger.

Swallow then collapse is an unconditional kill with no corpse. It costs the carrier the volume,
everything stored in it, and every round the hole has ever taken.

### Commanding voice → *stop*, *drop*, *kneel*, *come*, *run* (trait)

A force of personality that makes one-word commands difficult to disobey. Using it still
strains an ordinary throat.

| Word | Effect | Cooldown |
|---|---|---|
| Stop | Stands still, 3s | 10s |
| Drop | Stops, half a second, lets go of their weapon where they stand | 20s |
| Kneel | Lies down, 3s | 30s |
| Come | Walks to the speaker, once | 30s |
| Run | Runs ~24 cells away | 30s |

Range 16.9, no line of sight — a wall stops a lance and does not stop a voice.

**Nothing here is mind control and nothing is a mental state.** Vanilla has both, and both take
the person away. A target keeps their faction, their hostility, their memory and their think
tree, and does one thing they did not choose on the way through — then picks up exactly where
they were, still armed, still coming, and perfectly clear on who made them do it. What the trait
sells is not control. It is seconds, and where people are standing when those seconds end.

**The cost is the speaker's own throat, and it is priced by the target.** Every word accrues
`AG_LarynxWear` on the neck, which sheds at 0.12/day — slower than the raid it came from, so it
follows the carrier into the next week. What a word costs depends on how hard that particular
head was going to be to talk to: telling someone to do what they were already doing is nearly
free, a hostile costs double, and anyone in a mental state costs two and a half times on top of
that, because nobody in a tantrum is listening. Shouting *run* at an ordinary raider is about
0.18, so roughly five words across a fight. Shouting it at a berserk one is 0.45, and two of
those take a colonist's voice for four days.

**The penalty is Talking, so this mod does not have to describe it.** RimWorld already runs
recruiting, warden work and trading off that capacity. A hoarse carrier is quietly a worse
negotiator and nothing announces it; at the top stage they cannot speak at all and the ability is
simply gone until it heals. Your best talker being your best shouter is the tension the trait
actually creates, and it costs no code to create it.

**Kneel is the one that will define this kit, and vanilla designed it.** A prone pawn is hit at
×0.5 from 4.5 cells or more and at ×7.5 from 3.9 or less. So the same word is an execution setup
at melee range and an actively harmful mistake at rifle range — shouted across a killbox it
protects the raider from your own firing line. It is one number that changes sign at about four
cells, the player can read it in the shot tooltip as a *Target prone* line, and it punishes
reflexive use. Nothing in this mod computes any of it.

**Deafness is the counter and it needed no code.** A word is a sound, so anything below 0.15
Hearing does not hear it and nothing happens — no effect, no cooldown spent, a message saying
so. Mechs are excluded at the targeting params. No vanilla mechanic attacks or defends Hearing,
which makes this a real answer a player can find rather than an immunity flag this mod invented.

### Arcing → *arc* (weapon trait; needs Melee Animation)

One cast, up to three people, and none of the damage is this mod's.

| Ability | Effect | Cooldown |
|---|---|---|
| Arc | The carrier crosses to one enemy and strikes, then to the nearest enemy still standing to *that* one, up to three in a cast | half an in-game day |

The Arcing trait can appear on compatible weapons from Unique Melee Weapons. The ability and
its effect are absent when Melee Animation is not loaded, because each hop uses that mod's
execution animation and combat resolution.

**The damage is theirs, deliberately.** The strike is resolved by `OutcomeUtility` against the
carrier's own weapon, melee skill and Lethality stat, exactly as an execution the player started
by hand would be. It can kill, and often does. A number invented here would only have disagreed
with every other execution in the game. An arc also puts the carrier's ordinary execute button on
its own cooldown, because it just spent three of them.

**The chain is decided in the tick the button is pressed**, and each link is the nearest enemy to
the *previous target* rather than to the carrier — which is what makes the shape of the chain the
shape of the crowd. A raider standing on their own is one hop; five in a doorway is three. Every
link needs line of sight from the last one, for the same reason the first target does: an arc is a
step, not a teleport past masonry. The arc does not re-pick as it goes — anyone who has gone down,
died or been caught in someone else's animation by the time it arrives is skipped, and the arc
spends what is left.

**The cost is charged per person hit, not per cast.** `AG_ArcRecoil` takes 0.33 a hop and clears
itself in twenty seconds, so a full three-target chain lands on the top stage and leaves the
carrier standing over three bodies barely able to hold anything — and an arc that found one target
and ran out costs a third of that. The player is never punished for the arc running out of people.

## Implants

Three kits come from surgical implants. Each is a real item that has to be built or found
before a surgeon can fit it; the installation recipes consume the device plus two medicine.

| Implant | Grants | Tier | How you get it |
|---|---|---|---|
| **Reflex booster** | *reflex surge*, *vector manipulation*, *vector shove* | Industrial | Machining table, behind **Prosthetics**. 35 steel, 6 industrial components |
| **Neural accelerator** | *time alter* ×3 | Spacer | Fabrication bench, behind **Bionics**. 20 plasteel, 4 spacer components |
| **Phase barrier** | *phase guard* | Archotech | **Not craftable.** Quest rewards and deep-space trade only |

The phase barrier stays uncraftable on purpose: no archotech part in the base game has a
recipe, and it is the strongest thing on this list.

Each device ThingDef shares its `defName` with the HediffDef it installs, which is the
vanilla convention — `Joywire` is both an item and a hediff, and the two live in separate
databases. No new art: the Core health-item sprite is tinted per tier by the parent def,
exactly as every vanilla prosthetic, bionic and archotech part is drawn.

## How vector manipulation works

Four mechanisms: catching the rounds, owning the input, rewriting a flight, and charging for it.

**The catch is measured in ticks, not in cells.** A round is caught when its next
`LeadTicks` (21) of flight bring it within `ScanRadiusCells` (12) of the carrier — so the
window the player gets is the same 21 ticks whatever fired it, and a round moving four times as
fast is simply taken hold of four times as far out. The two constants do different jobs and it
is worth keeping them apart: twelve cells is the *threat* test, a round on a line that never
brings it that close is somebody else's problem; twenty-one ticks is the *reaction* test, and
it is the only number that decides whether the ability can be pressed in time.

Twenty-one is not a new figure. It is how long a vanilla rifle round already spent crossing the
old twelve-cell circle, which is the window the kit was tuned against and the one that plays.

| Round | cells/tick | 12 cells was | at quarter rate |
|---|---|---|---|
| Vanilla rifle (70) | 1.17 | 10.3 ticks | 0.69s |
| CE median (123) | 2.05 | 5.9 ticks | 0.39s |
| CE fast (208) | 3.47 | 3.5 ticks | 0.23s |
| CE fastest (1000) | 16.7 | 0.7 ticks | 0.05s |

Stating the window directly is what makes every round behave like the one the kit was tuned on.
A round already inside the reach passes at once, so one that has just gone by can still be taken
hold of and thrown back.

**Catching is a single frozen scan.** `VectorEditSession.Begin` walks
`ThingRequestGroup.Projectile` once, keeps the rounds coming into reach that the carrier has
line of sight to and that are not fogged, and freezes that list for the life of the session.
The sight test is against the cell the round is standing in *now*, which is what stops a round
the carrier could not possibly have seen being caught at the far end of its lead.
For the engine's own rounds the filter is `Bullet`, which is the right filter rather than a
coincidence: arrows are Bullets, and so are this mod's loosed blades, for the same reason — it
is the class that makes vanilla resolve what they hit. Combat Extended has no class that means
the same thing, so its rounds are narrowed on the def instead; the two tests agree on every
case that matters. Mortar shells and rockets are refused (`flyOverhead`, `explosionRadius`), and so is
anything already held by a phase barrier or standing still inside a stasis field, because those
rounds belong to those effects. Freezing the set is what stops the list changing under a drag
box.

**Slowing the world is one postfix, because there is nowhere else to put it.** `TimeSpeed`
bottoms out at `Paused` and jumps straight to `Normal`, so there is no sub-normal speed to
select. What there is instead is `TickManager.TickRateMultiplier`, the figure every other
part of the tick loop is derived from: a postfix returning 0.25 makes `CurTimePerTick` a
fifteenth of a second, the loop affords one tick every four frames at 60fps, and
`PawnTweener` — which scales its interpolation by the same figure — glides pawns at quarter
speed instead of stuttering them through it. Quarter-rate movement therefore looks right
rather than looking like a frame drop.

It is **forced, not scaled**. Multiplying the existing result would leave superfast at six
times a quarter, which is faster than normal — the ability would be bypassable by pressing a
speed button. A result of zero is the one case left alone: that means the player paused, or
the editor did, and neither should be restarted by a surge.

The radius is the other loud knob. It stopped being a reaction-time number once the surge
took that job — what it decides instead is how much of a volley is in the air when you
click. At six cells the answer was usually one round, which makes a four-group panel a panel
with nothing to put in it; twelve is the first radius at which rounds from separate shooters
overlap reliably.

**Owning the input is one prefix, in the right place.** `Selector.SelectorOnGUI` is the last
stop in the frame for anything the mouse does over the map, and it is where a drag becomes a
selection box and a right-click becomes an order — which are exactly the gestures the editor
needs. While a session is open the whole method is replaced by the session's own handling, so
no pawn is selected and no order is issued by drawing a group over a field of bullets. The
panel has already had its turn by then: windows are processed earlier in
`UIRoot_Play.UIRootOnGUI`, so nothing here can steal a click that belonged to a slider. The
panel itself does not absorb input around it, because the map underneath has to stay clickable.

The map half is drawn from two places for two reasons. Lines and the reach ring are world-space
and go down in `MapComponentUpdate`, so they lie on the ground and scale with the camera.
Markers, group numbers and the drag box are screen-space and go down in `MapComponentOnGUI`,
which runs *before* the window stack — so the panel draws on top of them rather than under.

**Rewriting a flight is done by hand, and has to be.** A projectile's position is a lerp along
`origin → destination` driven by `ticksToImpact` against `StartingTicksToImpact`, which is
computed from the def's speed with no setter — the same wall the phase barrier hits. `Launch`
is not the answer either: it scatters the destination by up to a third of a cell and re-reads
the origin from the projectile's current cell, and the whole promise of the editor is that the
line drawn on the paused map is the line the round takes. So `CapturedProjectile.Commit` writes
`origin`, `destination`, `ticksToImpact`, `lifetime`, `launcher` and both targets directly, and
a postfix on `StartingTicksToImpact` divides the total by the round's force. Setting
`ticksToImpact` to that same figure at commit keeps both halves of the lerp in step: the round
covers its recalculated range at its chosen speed, and interception, cover, shields and impact
all run unchanged. The instance is preserved, so a blade still plants itself where it stops.

A second postfix on `Projectile.DamageAmount` scales the primary damage by the same force.
Only the primary figure — armour penetration, extra damage rolls and everything an impact does
afterwards are deliberately left alone, because force is a decision about momentum rather than
a general damage multiplier that quietly rewrites how a round meets armour.

Nothing on any def is touched, so a rifle round edited to half speed does not make every other
round of its kind slow.

### The edited-round table

`VectorEditRegistry` is a `Dictionary<Thing, float>` behind an `EditedCount` check, for
the same reason `RecursionRegistry` is: both hot paths are extremely hot.
`StartingTicksToImpact` is read from the position lerp several times per projectile per tick
and `DamageAmount` at every impact, so both must cost one static integer read when nothing has
been edited, which is almost always.

A round at ×1 is **not** an entry. Force is absolute rather than cumulative, so the baseline is
exactly "no entry", and editing a round back to ×1 removes it from the table instead of storing
a redundant multiplier.

The table is committed state and is scribed by reference with the save, so a round mid-flight
when the game is saved resumes at the same speed and damage rather than snapping back to its
def. The editor's draft groups are not saved and cannot be: a session does not survive the
frame the game was saved in. Nothing tells a registry when a projectile lands, so
`MapComponent_VectorEdit` prunes the table every 600 ticks rather than putting a scan in front
of every impact.

### What replaced reflection

Reflection returned every damage instance to its instigator and refused every reservation aimed
at the carrier. Both are gone: the `Thing.TakeDamage` and `ReservationManager.CanReserve`
prefixes are deleted, so the booster no longer sits in the ordering that Wound Debt, Phase
Guard and the stasis field have to agree about.

The `AG_VectorReflection` **defName is kept** for both the ability and the old hediff. It is the
key a saved reflex booster's ability list is written under, so renaming it would silently take
the ability off every carrier in every existing save. The hediff def survives with no comps and
no stat factors purely so that a save made while reflection was up still resolves the
reference; `GameComponent_VectorEdit.FinalizeInit` takes it off every pawn at load, and clamps
any inherited cooldown to the new five-second maximum so a carrier who cast the old ability
shortly before saving is not locked out for most of a day.

### Shoving

`VectorPush` walks the target outward a cell at a time and stops at the first cell it cannot
stand in or cannot see from where it started, so a shove into a wall moves them up to the
wall rather than through it. Being stopped early is what makes it hurt: distance travelled
pays `damagePerCell`, and a blocked throw adds `slamDamage` on top. Pushes are divided by
body size, so a thrumbo barely moves.

## Rounds in flight, and Combat Extended

Three kits take hold of rounds that are already in the air: vector manipulation redirects them,
the phase barrier parks them on a halving curve, and the involute looses one inside its own
volume. All three were written against `Verse.Projectile`.

**Combat Extended's rounds are not `Verse.Projectile`.** `CombatExtended.ProjectileCE` derives
straight from `ThingWithComps`, so every `as Projectile` in this mod was null for anything a CE
weapon fired, and every Harmony patch typed to `Projectile` never ran. The kits did not
misbehave under CE so much as silently stop seeing the fight — and the phase barrier's failure
was the worse of the two, because it did not go inert. Its `Thing.TakeDamage` fallback still
fired, and a CE bullet carries no `Tool`, so every gunshot fell into the verbless branch and was
scaled to 15%. The barrier quietly became a flat 85% damage shield, which is the exact failure
mode the rest of its design argues against.

### One round, either engine

`Rounds` is the dispatch and `RoundBackend` is what a kit is allowed to ask of a round: where it
is, where it was last tick, its heading, its speed, who fired it, and four writes — redirect it,
place it while held, resume it, maintain its sound. `VanillaRounds` answers for the engine's own
rounds through the same Harmony field refs this mod always used. `CombatExtendedRounds` answers
for CE's, by reflection, and is the only file in the mod that knows CE exists — same rule and
same reason as `MeleeAnimation` for the Arcing weapon: the dll has to load and run with CE
absent, and a compile-time reference is resolved the moment any method touching one of their
types is jitted. `CombatExtendedRounds.Install` asks the question once at startup and leaves the
answer in `Rounds.Foreign`, which is null in a game without CE.

The speed a force multiplies is read from the **def** in both engines, never from the instance.
That is the whole reason force can be absolute rather than cumulative: a round already at ×2
still reports its def's figure, so editing it again to ×2 is ×2 and not ×4.

### A redirected CE round is made to fly like a vanilla one

An edited CE round is forced onto CE's own `LerpedTrajectoryWorker` with its gravity zeroed —
which is to say it is made to fly the way a vanilla round flies: a straight line from where it
was caught to where the player pointed it, arriving in a known number of ticks.

CE's ballistic model cannot express that. Under ballistics a flat shot travels until it falls to
the ground, so its range is decided by height and gravity rather than by the editor, and the
editor's one promise is that the line drawn on the paused map is the line the round takes.

It also settles damage. CE scales damage by remaining kinetic energy — `shotSpeed²/initialSpeed²`
— so under ballistics a round thrown twice as hard would arrive **four** times as hard. The
lerped worker reports full energy, which leaves force scaling damage linearly and puts CE on
exactly the vanilla rule. Force is applied by clearing CE's cached damage figure and reading it
back, so a second trip through the editor is absolute rather than cumulative there too.

Two smaller things fall out of the same place. The redirect does not go through CE's `Launch`,
because `Launch` clamps speed up to the def's figure — which would make the ×0.25 setting
impossible — and derives its destination from the ballistics rather than taking the one it was
given. And a redirected round is floored at chest height, because CE impacts a lerped round when
its height reaches zero: a round caught in the last moment before it hit the dirt would
otherwise land on the first tick of its new flight.

### Holding a CE round

There is no CE counterpart to the postfix on `Projectile.ExactPosition`, and there should not be.
CE reads its rounds out of a field that its drawing, collision and impact checks all share rather
than through one getter, and the two ends of that read — `LastPos` and `ExactPosition` — are the
segment its collision runs along. A held round whose reported position moved but whose `LastPos`
did not would trail a segment across the map and collide with whatever it crossed.

So a held CE round is **moved** rather than described: `ProjectileCE.Tick` is prefixed away by
hand (there is no type to name in an attribute unless CE is loaded), and the curve writes both
ends each tick. With the tick skipped nothing else would move it, and with both ends equal the
collision segment has zero length and finds nothing.

### Why the catch had to stop being a distance

Making the kits see CE rounds was not enough to make vector manipulation *usable* under CE, and
the reason is worth stating because it is not a CE bug. Twelve cells is ten ticks of a vanilla
rifle round and three and a half of a fast CE one, so the same catch radius was handing the
player a third of the time to react. No tick rate fixes that: the window scales with the rate, so
CE's speed advantage survives any slowdown — a quarter rate leaves 0.23 seconds against vanilla's
0.69, and buying that back would need a rate of about 0.04, which is under three game ticks a
second.

So the catch is now measured in ticks of the round's own flight rather than in cells, and it is
the same twenty-one ticks for every round in the game. See *The catch is measured in ticks, not
in cells* above; the change is not CE-specific and applies in a vanilla game too, where it also
means a round is caught while it is still coming rather than once it is already close.

### The frost bomb under Combat Extended

The frost bomb is a weapon rather than a round in flight, so it does not use the reflection
bridge above. Its CE support is a patch folder, and the interesting part is what the folder
does *not* do.

CE turns every hand grenade into ammo: `Class="CombatExtended.AmmoDef"`, `thingClass`
`AmmoThing`, `stackLimit` 75, and `Verb_ShootCEOneUse`, whose `SelfConsume()` destroys the
grenade you just threw and equips the next one out of the pack. The frost bomb stays reusable
instead — one item, one set of numbers, whatever the modlist says. It is a charge compressor
in a throwable tube, and the six-second cooldown is the compressor working.

`Patch_CombatExtended/1.6/Patches/AG_Frost_CE.xml` therefore adds CE's `Bulk` and
`SightsEfficiency` stats, a `CombatExtended.ToolCE` so bashing somebody with it goes down a
path that finds the armour-penetration fields it expects, and `CE_OneHandedWeapon` so it can
be carried with a shield. It deliberately leaves two things alone:

- **The verb.** `RimArt.Verb_ThrowFrostBomb` hands the launch to `MapComponent_Throws` so the
  grenade leaves the hand on the tick the animation opens it. CE's verb spawns its projectile
  inside `TryCastShot` and casts what it spawns to `ProjectileCE`, so taking their verb costs
  both the animation and the projectile class. What is given up by keeping ours is CE's
  ballistic flight model for this one lobbed grenade — no height, no cover interception on the
  way in. On a cell-targeted 3.4-cell stun that is a far smaller loss than it would be on a
  bullet.
- **The projectile**, which names `Projectile_Explosive` itself precisely so CE's rewrite of
  `BaseGrenadeProjectile`'s `thingClass` does not reach it.

Nothing in that file is conditional on anything but the folder: `loadFolders.xml` reads the
directory only when CE is active, so `ToolCE` is guaranteed to resolve. `ApiChecks` still
asserts it exists, because a `Class` attribute that does not resolve is a red error at load for
the player who has both mods and silence for everybody else.

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

`PawnRenderer` exposes no alpha, so these are **solid** copies of the pawn drawn at recorded
positions, not fading ghosts. Ghost count tracks the tier, so you can read how hard someone is
pushing without opening the gizmo bar. If it looks wrong in play, set `drawAfterimages` false on
the hediff comp rather than rebuilding.

This shipped drawing nothing at all. A lone `RenderPawnAt` at a position of your own is a no-op in
1.6 — see *How the Arcing weapon works* for why, and for what replaced it.

## How the anchor organ works

**Marks live on the gene, not on a hediff or a map component.** They belong to a person: they
save and load with the pawn, they travel between maps with them, and they are gone the moment the
gene is. `Gene_Anchors` overrides `ExposeData`, which makes persistence free the same way driving
time alter off a hediff did.

**Nothing prunes on the draw path.** A mark stops holding when it fades, or when the pawn wearing
it dies, despawns or changes map, and every reader has to cope with that. The trap is that both
the overlay and the ability gizmos are evaluated every frame, so pruning inside them would drop a
mark long before anything could tell the player it had gone. `Prune` is therefore called only
from `MapComponent_Anchors`, once a second; every other reader filters with `StillHolds` and
mutates nothing.

**Double clap picks both ends.** It is a two-target ability of exactly the shape the Skip psycast
uses — `CompAbilityEffect_WithDest` with `destination` set to `Selected`, so the targeter asks
twice and the second pick belongs to the player rather than being derived from the first. The
second pick has no `throwMessages` parameter to explain a refusal, so an invalid pair is simply
not clickable; the two rules that make a pair invalid (both ends must be marked, at least one must
be something that can move) are stated in the ability description instead.

**The destination highlight had to be taken over.** `CompAbilityEffect_WithDest.DrawHighlight`
always draws a radius ring from its own `range`, and `GenDraw.DrawRadiusRing` works off a
precalculated cell list that runs out far below a map-wide number — it logs
`not enough squares in the precalculated list` every frame the targeter is open. That method is
not virtual, so the only way past it is a Harmony prefix. What replaces it outlines every mark
still being held while the targeter is open, which is also the answer to the second pick having
no way to explain a refusal.

**`CanApplyOn` is called twice and means different things each time.** The first call is the
targeter validating the *first* click, and it passes `LocalTargetInfo.Invalid` as the destination
because the player has not been offered a second pick yet. A comp that insists on a valid far end
at that point refuses the first click, so the destination step never opens and the ability
silently does nothing at all — no error, no message, no cast. The pair check has to be skipped
whenever `dest` is invalid.

**The second pick is a cell, not a thing.** `CompAbilityEffect_WithDest` supplies its own
targeting parameters for the destination step and they are `canTargetLocations` only, so the far
end of a double clap comes back as the clicked cell with no `Thing` attached. A mark therefore has
to answer to its current position as well as to its identity — matching on the pawn alone means a
marked pawn silently cannot be chosen as the far end, and the click does nothing at all.

**The swap is two position writes.** `Pawn.Position` on a spawned pawn re-registers them with the
map grids and regions, and `Notify_Teleported` drops the job and resets the pather so nobody keeps
walking a route that started on the other side of the map. Both ends are read before either is
written. A tile mark is a move rather than a swap, because a marked cell has nothing standing on
it to send back — and a double clap between two tile marks is refused for the same reason.

**Only the player's own colonists have their marks drawn.** A hostile carrier's marks are
deliberately invisible: the mark is the thing the whole ability is planned around, and reading an
enemy's plan off the map would give the gene away before it did anything.

The overlay is the Core `Things/Mote/PsycastSkipFlash` texture under `MoteGlow`, tinted the same
off-white-blue as the stasis dome, plus a `GenDraw.DrawFieldEdges` outline so the exact cell is
readable — a tile mark on open ground is otherwise a glow with no edge. The material is resolved
lazily in `AnchorGraphics` for the same reason `TimeBubbleGraphics` does it.

The teleport plays Core's `Skip_EntryNoDelay` and `Skip_ExitNoDelay` effecters, looked up by name
and cached rather than through a `DefOf`, so a missing def leaves the clap silent instead of
throwing at startup.

## How the resonance works

**The part is read out of the damage, not out of the swing.** A melee swing carries no hit part
into `Thing.TakeDamage` — it comes back out of it, because the damage worker is what decides
where the blow landed. So this is a *postfix* on `TakeDamage` reading `DamageWorker.DamageResult`,
where every other patch in this mod is a prefix. Sitting at the damage layer rather than on the
melee verb is the same choice the vector reflex made, and buys the same thing: one choke point
covers every source of a melee hit, including tools this mod has never heard of.

`dinfo.Tool` is the melee test — the same one the phase barrier uses to tell a swing from a
blast. Bullets, explosions and fire carry no tool, so none of them ring. Neither does the
shatter itself, which is what keeps the postfix from recursing into its own damage.

**Which parts can ring is derived, never listed.** `ResonanceUtility.CanRing` asks four
questions that are the same question four ways: is the part on the surface (anything deeper is
never struck directly, only through what covers it), does it have a parent (the core part is
what everything else rings *against*), is it actually flesh, and does its subtree contain a
part tagged `vital`.

The vital test has to walk the whole subtree rather than the part itself. A neck carries no
vital tag — but a head hangs off it and a brain hangs off that, so removing one kills. **The tag
that matters is never on the part you would name.**

The flesh test is `BodyPartDef.conceptual`, and the waist is the case that demands it. Its label
is *utility slot*: it is not a body part at all, it is where a belt hangs. But it sits Outside,
under the torso, with no vital descendant, so all three of the other tests wave it through — and
breaking it would take the pelvis and spine with it on the way to destroying an apparel slot.
Waist is the only conceptual part in Core, which is exactly why it wants a rule rather than an
exception.

Reading all of this off the game's own body data instead of a list of `BodyPartDef`s is what
makes it hold for animals, mechs and modded races with no patch. It resolves to the right set on
each:

| Body | Rings | Damped |
|---|---|---|
| **Human** | 36 — shoulders, arms, hands, fingers, legs, feet, toes, eyes, ears, nose, jaw | 28 — torso, waist, neck, head, skull, brain, heart, lungs, liver, kidneys, stomach, every internal bone |
| **Quadruped** | 14 — legs, hooves, eyes, ears, nose, jaw | 13 — body, neck, head, brain, and the organs |
| **Centipede** | 7 — sensors and the rear body rings | 9 — head, artificial brain, reactor, reprocessor, forward rings |

**The break goes through the ordinary damage path.** `ResonanceUtility.Shatter` applies one
damage instance sized to the part's remaining health rather than adding a missing-part hediff
directly, so everything downstream of losing a limb happens by itself — the bleeding, the pain,
the drop, the combat log line, the notification, and the death check for a body that could not
afford it after all. Propagation is off because the note is in one part and nowhere else, and
the armour penetration is absurd on purpose: armour has already had its say on both blows that
caused this, and this is what those two blows did, not a third one.

**The outermost part that can ring wins, not the last one hit.** A cut to a leg that carries on
into the femur reports both parts, and the femur is the deeper entry — taking the tail of
`DamageResult.parts` would mean a blow that broke a bone inside a limb silently failed to ring
the limb it went through.

**Live notes are deliberately not saved.** They last five seconds, and a `BodyPartRecord` is
resolved by walking a body rather than by an id, so persisting them would cost a resolver and a
class of load-order bugs to buy back a window shorter than the pause between hitting save and
the game coming back.

### The knob worth knowing about

`selfResonance` on the hediff comp turns off the carrier's own ringing. It is the entire cost of
the weapon trait, so it belongs in XML the way `banWeapons` does on the anchor organ rather than being
hardcoded — but turning it off does not make this a slightly easier ability, it makes it a
different and much stronger one.

## How arrears works

*Player-facing this ability is **wound debt**; `AG_Arrears` and `HediffComp_Arrears`
keep the old name in code, so the implementation notes below use it.*

**One prefix, and the ordering is the whole design.** `Thing.TakeDamage` carries several
prefixes from this mod, and arrears sits at `Priority.Low`, deliberately last:

| Patch | Priority | What it does first |
|---|---|---|
| Stasis field | `First` | Frozen pawns are immune and run up no debt at all |
| Phase barrier | default | *Scales* verbless damage rather than cancelling it |
| **Arrears** | **`Low`** | Records whatever is left |

The reflex booster is no longer in this list. Vector manipulation acts on rounds in flight
rather than on damage instances, so it never reaches `TakeDamage` at all.

The phase-barrier row is the one that had to be right. It scales rather than cancels, so if arrears
ran first a carrier holding both would owe the full blast they never actually took.

**Armour is applied at settlement and only at settlement**, which looks wrong and is correct.
Armour reduction happens inside the damage worker, downstream of the prefix that takes the wound
onto the books — so nothing in the ledger has met armour yet, and re-applying the raw figure at
settlement runs it through exactly once, with the original instance's penetration value.

**The ledger is saved, and that is not optional.** A debt that evaporated on load would make
reloading a way of not paying. Each entry keeps the damage def, amount, penetration, angle and
hit part through a small `IExposable`; the instigator and weapon are deliberately dropped, since
they are `Thing` references that may be dead or on another map by the time the bill lands and
none of them change what the body is about to find out. The cost is that kills at settlement are
not credited to whoever fired the shot.

**Settling removes the hediff before it applies anything.** Otherwise every wound paid would
walk straight back into the prefix and be recorded again. The pawn may die partway down the
list, which needs no special case — the loop stops the moment there is nobody left to tell.

Removal by any other route — a dev tool, another mod, anything that clears hediffs — settles
rather than forgives, so there is no back door. `Settle` is re-entrancy guarded, so the removal
it performs itself lands there harmlessly.

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

## How the fold organ works

**The hole is read one layer deeper than the resonance.** `Patch_Thing_TakeDamage_Resonance` can
be a postfix because it only wants to know which part was struck. This has to stop the strike
landing, and by the time the damage worker returns the injury is already on the body — so it
prefixes `DamageWorker_AddInjury.ApplyDamageToPart` instead.

The trap there is that the part has not been chosen yet, and choosing it is a weighted random
roll. Rolling in the prefix and then letting vanilla roll again inside
`GetExactPartFromDamageInfo` would give two different answers: the hole would eat hits that
never landed on it and miss ones that did. The roll is therefore made exactly once and written
back with `dinfo.SetHitPart`, which sends vanilla down its already-decided branch.

**The aperture uses no Harmony patch.** `Thing.PreApplyDamage` is virtual and runs before the
damage worker is even chosen, so a round, a swing, a blast and a fire all arrive at one override
and all leave the same way.

**The origin map has to be held open.** A carrier standing in their own volume is not standing
on the map they left, and a map with nobody on it is one the game removes — taking the way back
with it. `Patch_MapParent_CheckRemoveMapNow_Involute` blocks removal outright for any map
holding an aperture, rather than pretending the aperture is a colonist.

**State lives on the gene**, the same choice the anchor organ's marks made and for the same
reason: the hole belongs to a person. `Gene_Involute` scribes the part, the volume and the
aperture, so save/load is free. The aperture stores its owner as a `Pawn` reference rather than
a `Gene`, because the gene is one lookup off the pawn and the save system will hand back the
pawn.

### The volume

`GenStep_InvoluteVolume` is written rather than borrowed. Alpha Genes lays its pocket plane out
with `KCSG.StructureLayoutDef` from Vanilla Expanded Framework; adding a framework dependency
for set dressing is not a trade worth making, and scattering procedurally gives a different room
every time instead of four fixed prefabs.

Every def it names is Core except `AncientExostriderRemains`, which is Biotech. The floor is
`Gravel` with `SoftSand` blotches and `WaterOceanDeep` pools; the light is `Agarilux`,
`Bryolux` and `Glowstool`, which glow on their own with no power and no wiring; the scale comes
from warwalker wreckage and ship chunks.

Every cell is given thick roof. That is not decoration — a roofed cell takes no sky glow, so the
light level is entirely what the GenStep scattered, which is what lets the weather's sky colours
be a tint rather than a clock. `AG_InvoluteVoid` sets all four sky slots identically for the same
reason, so the volume looks the same at every hour. `saturation` above 1 with a low `sky` is what
keeps it from being grey sludge: dark **and** vividly coloured, not dimmed.

### It ships no art

The aperture is Core's `Things/Mote/SkipInnerDimension` — the swirl vanilla draws inside a skip,
which is already a picture of a hole with somewhere else behind it. `InvoluteGraphics` resolves
it lazily from the draw path, the third use of the pattern `TimeBubbleGraphics` and
`AnchorGraphics` set.

The hole on a body is drawn beside the pawn rather than on the part it is in. `PawnRenderNode`
does not hand out a screen position for a left hand, and chasing one across every body type,
rotation and posture is a great deal of work for a dot under half a cell across. The health tab
names the part; the mote only has to say the hole is open.

Violet, and deliberately not the skip-blue of the anchor marks and the stasis dome. Those move
things through normal space. This opens somewhere else, and the player should be able to tell
which family of effect they are looking at without reading a word.

A `PsycastSkipFlashEntry` fires every time anything passes through, on both sides. Without it
the mechanic is invisible — the player watches a raider shoot, sees no damage, and learns
nothing about the gene they are carrying.

### Tuning it

`InvoluteGeneExtension` on the GeneDef holds the levers: `volumeSizeX`/`volumeSizeZ`,
`minHoleCoverage`/`maxHoleCoverage` — which is the balance dial, see above — and
`holeEfficiencyOffset`. `durationTicks` on `HediffCompProperties_Vent` is the window.

### Known gaps

Hostiles are not made to attack a ground aperture. Insects and mechs go for structures and will
find it; a manhunter pack will not, which makes the escape free against wildlife. That is left
alone on purpose — forcing hostiles onto a target is Provoke's job, and reaching for
it here is the drift the rest of this file keeps refusing. A gene that is strong against gunfire
and cheap against animals is a matchup, not a bug.

None of this has been run in-game yet.

## How Commanding Voice works

**One job, inserted, then handed back — and no Harmony patch anywhere.**
`Pawn_JobTracker.StartJob` already takes `resumeCurJobAfterwards`, so the interruption and the
return are the game's own rather than an imitation of them. That is the whole mechanism. It also
means this trait adds no contact surface with the other kits: nothing here prefixes
`Thing.TakeDamage`, so it sits outside the ordering that Wound Debt, Phase Guard and the
stasis field all have to agree about.

Provoke reaches into the same AI one layer shallower — it rewrites `enemyTarget` and
interrupt once, leaving the target to decide what to do about it. This decides for them, once,
and then stops deciding.

**The cost model reads five things, and the honest limit is that it wanted to read a sixth.**
What `WearFor` would like to know is how far the command sits from what the think tree actually
wanted, and that number does not survive: a think tree does not keep the scores it rejected. So
it reads what is still there — whether the target is already doing the thing, whether they are
in a mental state, whether they are hostile, how conscious they are, and how big they are. Every
term is a number the game holds for its own reasons, which is the point, but it is inference
from observable state and not the ranking itself. `LarynxDefaults.BaseWearFor` is the only place
with invented numbers in it and the only place worth tuning.

The bill is worked out *before* the order lands. Obeying changes `CurJobDef`, so measuring
afterwards would make every word look like one the target was already following and price the
whole kit at 15%.

### The two capacities

`Talking` carries the cost and `Hearing` is the counter, and neither needed a custom stat. The
wear lands on the neck because there is no `Throat` body part — `Neck` is as deep as vanilla
goes — which puts it on the one part Resonance calls too damped to hold a note. The
top hediff stage sets Talking to zero rather than offsetting it, so `CanSpeak` fails and every
gizmo greys out. Making that permanent is a fifth stage with `severityPerDay` 0; it is left
recoverable on purpose, because an ability that can permanently delete itself in one bad fight is a
trap rather than a cost.

### Two traps this design walks into

**`JobDefOf.LayDown` is the rest job.** It has `CanSleep => true`, so a raider told to kneel
would have gone to sleep on the doorstep. `LayDownAwake` subclasses the same driver and
overrides `CanSleep` and `CanRest` to false. Posture is what the shot factors actually read, and
posture comes from the job driver: `PawnUtility.GetPosture` returns `p.jobs.posture` for anyone
not downed, which is why a job can produce a prone pawn without touching their health at all. A
commanded pawn is at full health the entire time.

**Drop looked like the one word that was not a job.** It is. `JobDefOf.DropEquipment` stops the
pather dead, waits 30 ticks and drops at the pawn's own position — the word exactly, and for
free. Writing it by hand would have grown a second copy of what the corrosive glands already do.

### Why drop does not scatter and does not forbid

The corrosive glands throw a weapon clear and forbid where it lands. This makes someone let go,
and it lands at their feet, and they will pick it back up. That difference is deliberate: acid
puts the weapon somewhere, a shout only ends someone's grip on it. Keeping the scatter and the
forbid exclusive to the older gene is what stops a Met −2 gene quietly obsoleting a Met −1 one
that exists to do this properly.

### Known gaps

All five gizmos share `UI/Abilities/AnimalWarcall` — the only voice-themed icon in Core or
Biotech, with `Gene_VoiceRoar` already used by Provoke. Five identical buttons in
a row is the one part of this kit that wants art.

The tooltip quotes base cost only. The decision the trait offers is whether *this* target is
worth the voice, so the real multiplied figure should be visible at targeting time —
`ExtraTooltipPart()` has no target, so doing it properly means taking over the targeter draw the
way the anchor organ took over `DrawHighlight`.

`JobDriver_LayDownAwake` has `LookForOtherJobs => true`, so a hostile is permitted to abandon
kneel early. The job is marked `playerForced`, which usually holds, but three seconds may turn
out to be one.

Confirmed in-game: all five words cast and resolve. The tuning has not been
played against a real raid — the five-words-per-fight figure for *run* is an estimate, not a
measurement.

## How Origin: Blade works

**The stagger lives on the blade, not on the ability.** Loose finds the blades near the point,
hands each one the target and a number of ticks to wait, and stops existing. Each blade counts
its own delay down, lifts, turns toward what it was pointed at and launches itself. That is one
integer per blade and it buys the whole read — a volley that leaves in sequence rather than a
dozen projectiles appearing in the same instant — and it means a blade destroyed or expiring
mid-windup simply never fires, with nothing to clean up.

**It is a `Bullet`, not a bare `Projectile`.** `Bullet.Impact` is where vanilla resolves damage
against armour, body parts and the hit roll, so subclassing it means the numbers come off the
def and none of the maths is this mod's. The subclass only decides what the thing looks like on
the way and that it plants itself again where it stops.

**This is the one kit that had to be drawn.** The other kits use Core textures that already mean
the right thing, and this one spent two attempts proving it
cannot. A planted blade has to read as standing *in* the ground, and every weapon texture in
RimWorld is a sword photographed from directly above while lying flat — there is no rotation of
a picture like that which produces a side view. The first attempt also had the sprite's own
heading wrong: Core's longsword points at 155°, not the 45° that was assumed, which put every
blade 110° out and produced a field of swords lying at diagonals. Deriving the angle from
`equippedAngleOffset` fixed the maths and changed nothing about the read, because the problem
was never the angle.

So `make_textures.py` draws two sprites: a sword point-up for every moment a blade is in the
air, and a side view with the point already buried and earth thrown up around the hole for a
blade that has arrived. The overlap of that near lip over the steel is the entire illusion — a
blade that stops at the surface reads as one lying on it. Both point north, so a compass heading
is the rotation with no texture correction in the way, and the planted sprite states where its
ground line falls (`PlantedGroundFraction`) so a leaning blade pivots about the hole it is
standing in rather than sliding out of it.

**The fall is a `Skyfaller`, and the first version's was not.** A blade coming down is the same
event as a drop pod coming down, and RimWorld has a whole class for that: an accelerating
approach along one fixed angle, a drop-spot shadow growing on the landing cell, a roof check,
an impact sound and dust thrown up on arrival — every one of them a field in the def rather
than a line of code. The hand-rolled version that shipped first reimplemented all of it out of
its own position maths and looked precisely as wrong as that suggests. What is left for
`FallingBlade` is the two things vanilla cannot know: that the blade hurts what it lands on,
and that it stays standing afterwards.

The stagger is added to each blade's own `ticksToImpact` rather than spawning blades later,
which is what makes a rain read as one: a skyfaller's height is derived from how long it still
has to fall, so a dozen blades are in the air at once at a dozen different heights and arrive
in sequence, with nothing holding a timer for the group. `hitRoof` is off on purpose — these
are blades, not meteors, and a rain called down indoors should not take the roof off.

**No Harmony patch anywhere.** Like Commanding Voice, this origin adds no contact surface with
the other kits — nothing here prefixes `Thing.TakeDamage`, so it sits outside the ordering that
Wound Debt, Phase Guard and the stasis field all have to agree about.

### The registry, and why it is not a lister query

`PanoplyRegistry` is self-healing in the same way `TimeAlterRegistry` is: blades add themselves
on spawn, remove themselves on despawn, and anything lost in between — a map discarded, a save
reloaded, a blade destroyed by a path that never ran `DeSpawn` — is pruned on the next read. That
survives save/load without a removal callback that has to fire, and it means the debt can be
recomputed without walking a map.

### Known gaps

**Nothing here has been in front of the game yet.** It compiles and validates. The parts most
likely to bite: whether a non-edifice building with `Standable` passability really does let two
blades share a cell in every spawn path, whether a blade planted by a loosed round inside a wall
cell looks like anything sensible, whether fourteen `RealtimeOnly` things each doing their own
`Graphics.DrawMesh` costs anything visible at low zoom, and whether the debt curve is a weight or
a punishment.

The blade lifetime (5000 ticks, about two in-game hours) is a guess. It wants to be long enough
that rain is a plan made early and short enough that a field cannot be left seeded across a whole
day of work, and only play decides which of those it currently is.

Rain has no `aiCanUse`, like every ability in this mod. A hostile pawn with this origin does nothing
with it.

## How the Arcing weapon works

**One reflection bridge, three calls, no assembly reference.** `MeleeAnimation` is the only class
in this mod that knows the other mod exists, and it reaches it through `AccessTools` rather than a
compile-time reference. The reason is that this dll has to load and run with Melee Animation
absent: a real reference is resolved the moment any method touching one of their types is jitted,
and the method jitted first is never the one you expected. Reflection turns that into a runtime
question with an answer the class can hold. What it needs from them is small and has been stable
across 1.4–1.6:

| Their member | What it answers |
|---|---|
| `AnimDef.GetExecutionAnimationsForPawnAndWeapon` | which executions this carrier's weapon can throw |
| `OutcomeUtility.GenerateRandomOutcome` | what the blow does to the person it lands on |
| `AnimationStartParameters.TryTrigger` | play it, and hand back how many ticks it lasts |

Anything thrown across that bridge switches it off for the session rather than repeating once a
tick. A silent ability is a bug report; a log full of the same exception is a bug report nobody can
read.

**The run is two phases and both are one line of state.** Dash puts the carrier beside somebody
and remembers who; strike, ten ticks later, throws the blow and waits out however long the
animation says it takes. Everything else — the beat between people, the afterimages, the recoil —
hangs off those two moments. It lives in a `MapComponent` rather than on a hediff because a run is
not a state the carrier is in; it is a three-second sequence being played at them that has to
survive them being knocked about mid-way.

**The twelve ticks between arriving and striking exist entirely for the trail.** Melee Animation
takes over drawing a pawn the moment its animation starts, so anything drawn in the same tick as
the strike is drawn for nobody. A fifth of a second of the carrier plainly standing somewhere they
were not is what makes the blink read as a blink.

**Drawing a pawn where it is not takes all three phases, and that is not obvious.** As of 1.6 the
draw phase uses results the render tree computed earlier in the frame for wherever the pawn
actually is, so `RenderPawnAt` called on its own with a position of your own draws *nothing* — no
error, no ghost, no clue. Both Arc and the neural accelerator initially did exactly that and drew
no afterimages at all in game. What works is `DynamicDrawPhaseAt` for `EnsureInitialized`,
`ParallelPreDraw` and `Draw` in sequence at the ghost position, which is the same thing Melee
Animation's own render patch does to put a pawn somewhere the game did not expect, and then a
fourth call pushing the results back to the real position so nothing else inherits the last
ghost's. The ghosts are still solid — `PawnRenderer` exposes no alpha — so the trail thins by
losing ghosts rather than by fading them, and their lifetimes are staggered by place in the line
so it retracts toward the carrier instead of blinking out evenly. A trail that vanishes all at once
reads as five people; one that retracts reads as one person moving.

**The streak is a plain quad, not one of Core's line motes.** Every line-shaped mote in the game is
drawn by a system that decides its own heading, and borrowing one means inheriting a texture whose
"up" has to be guessed at — which is the mistake Origin: Blade made twice with the longsword
sprite. A solid additive quad scaled along the path has no heading of its own to be wrong about:
the rotation is the compass angle of the dash and nothing else. Two layers, because one bright
rectangle reads as a bar rather than as speed — a wide dim glow for edges that fall off, a narrow
bright core inside it for the line. The fade is baked into the material at eight quantised steps
rather than pushed through a property block, so it cannot depend on whether a given shader honours
one.

**Their promotion roll is asked for, and that is most of what the ability looks like.** Melee Animation
does not simply play the execution it picked: on a killing outcome it rolls again to promote that
animation into a better one — the beheadings, the head removals, the lift on a spear. Skipping that
roll left the best half of their animation set on the shelf and made an arc look like three melee
hits in a row. Asking for it means handing over a `PromotionInput` built the way their float menu
builds one, including a real occupied mask off the carrier's landing cell, so a promoted animation
cannot finish with somebody standing in a wall. The pick before it is weighted by their
`Probability` too, which is a def value multiplied by the player's own settings — somebody who
turned an animation off in Melee Animation has said they do not want to see it, and an arc is not
the place to argue.

None of that is required for the ability to work. Every one of those members is null-checked
separately from the four the bridge cannot do without, so a version of Melee Animation that moved
or renamed them costs the arc its flourishes rather than its function.

**The carrier is held still between arriving and striking.** `Notify_Teleported` drops whatever job
they had, which left them free to start walking somewhere in the pause and get yanked back mid-step.
A short wait job holds them, expires on its own if the arc is cut short, and is replaced outright by
the animation when the strike begins. They also arrive already facing the person they came for.

**The arc does not borrow the anchor organ's skip flashes,** and that is a deliberate refusal. A
clap is a psychic exchange of two places and is dressed as one; an arc is a person crossing nine
cells faster than the eye follows, so what it leaves is disturbed ground, sparks off the stop and a
line of light. Two kits that both move somebody instantly should not look the same, or watching
either one teaches the player nothing.

**The landing cell is not a choice, it is their layout.** An execution is laid out with the
attacker at (0,0) and the victim at (1,0), so the only two cells that can carry one are directly
west of the victim, or directly east with the animation mirrored — which is what `flipX` is for.
The root transform is read off `Pawn.Position`, so the teleport can happen in the same tick as the
trigger. If neither cell is free the arc still happens: the carrier takes any adjacent cell and
throws an ordinary melee swing instead. A corridor fight is exactly where somebody presses this,
and refusing it there because the geometry is tight would read as broken rather than constrained.

**Downed targets are excluded, and that is their rule rather than a choice made here** — their
renderer refuses a downed pawn outright. It happens to be the right rule anyway: an arc is three
people taken out of a fight, not a tour of the wounded.

**No Harmony patch anywhere**, like Commanding Voice and Origin: Blade. Nothing here prefixes
`Thing.TakeDamage`, so it sits outside the ordering that Wound Debt, Phase Guard and the
stasis field all have to agree about.

### Known gaps

**Nothing here has been in front of the game yet.** It compiles, validates, and the API it reaches
for was read out of Melee Animation's own 1.6 source. The parts most likely to bite: whether
`Pawn.Position` written directly is enough for their root transform in every case or whether a
carrier who was mid-path needs a tick to settle, whether three executions back to back read as one
movement or as three separate animations with pauses in them, whether the promotion roll ever picks
an animation whose end cells do not suit a chain that is about to move again, and whether the recoil
curve is a weight or a punishment.

The carrier is held still only for the pause before each strike, not for the twenty ticks after one
finishes. If they visibly start walking somewhere in that gap, the hold wants extending to cover it
as well.

The cooldown — half an in-game day — is a guess made against what three executions are worth, not a
measured figure. So is the 9.9 chain radius, which is the number that decides whether this is a
crowd ability or a duel ability.

Arc has no `aiCanUse`, like every ability in this mod. A hostile pawn wielding an Arcing weapon does nothing
with it.

## How the mimic beacon works

Three decisions carry this feature, and all three were made by reading the game's code rather
than by guessing at it.

### The decoy is a Thing, and it still draws fire

The obvious build is a humanlike `Pawn` of the player's faction. It is the wrong one.
`Pawn.IsColonist` tests nothing more than "humanlike, and in the player's faction", so such a
pawn *is* a colonist: colonist bar, needs, mood, a health tab, a social log, recruitment, a
caravan slot, a "colonist died" letter, and a body on the floor afterwards. Every one of those
is something this item is specified not to do, and each would need its own patch to suppress.

The toy car's file records the cost of the other route as "enemy AI ignores the car, so it
draws no fire and is not a decoy". That is true of a plain `Thing` and **not** true of one that
implements `Verse.AI.IAttackTarget`, which is four members:

```csharp
public interface IAttackTarget : ILoadReferenceable
{
    Thing Thing { get; }
    LocalTargetInfo TargetCurrentlyAimingAt { get; }
    float TargetPriorityFactor { get; }
    bool ThreatDisabled(IAttackTargetSearcher disabledFor);
}
```

`ILoadReferenceable` comes free with `Thing`. Registration is automatic:
`AttackTargetsCache.Notify_ThingSpawned` files anything spawned that implements the interface.
`RimWorld.Hive` is the precedent — a building, not a turret, that raiders shoot and swing at.
Raiders reach it through `JobGiver_AIFightEnemy.FindAttackTarget`, whose scan flags are
`NeedLOSToPawns | NeedReachableIfCantHitFromMyPos | NeedThreat | NeedAutoTargetable`; the two
that could exclude it both pass, because `ThreatDisabled` is ours to write and
`AttackTargetFinder.IsAutoTargetable` only rejects dormant or uninitiated things. Melee finds it
by a different road — `ThingRequestGroup.AttackTarget` membership is literally
`typeof(IAttackTarget).IsAssignableFrom(def.thingClass)` — and arrives at the same place.

So "it leaves nothing" is true by construction rather than by cleanup. There is no corpse
because there is no pawn.

**The faction must be set before the spawn, not after.** `AttackTargetsCache.RegisterTarget`
files a new target under every faction it is hostile to *at that moment*, once, and nothing
re-files it afterwards except an actual change in faction relations. A decoy spawned
factionless is hostile to nobody, filed under nobody, and returned to nobody — it would stand
there as scenery, with no error in any log. This is the single easiest way to break the
feature and it is one line.

### The taunt is one number, and the number is derived

`AttackTargetFinder.GetShootingTargetScore` is the whole of enemy target selection:

```csharp
float num = 60f;
num -= Mathf.Min(distance, 40f);                                   // closer is better
if (target.TargetCurrentlyAimingAt == searcher.Thing) num += 10f;  // it is aiming back
if (searcher.LastAttackedTarget == target.Thing
    && TicksGame - searcher.LastAttackTargetTick <= 300) num += 40f;   // engaged, last 5s
num -= blockChance * 10f;                                          // cover
// ... terms that apply only to Pawns ...
return num * target.TargetPriorityFactor;
```

The design wants a heavy bias that a hostile **already engaged** is allowed to resist. That
exemption is already in the engine and is worth exactly +40, for five seconds after the last
shot. So the whole of the taunt reduces to one inequality — at a shared distance `d`, the decoy
must beat a target nobody is shooting at and lose to one somebody is:

```
(60 - d) * F  >  (60 - d) + 10          and          (60 - d) * F  <  (60 - d) + 40
```

Each side is written for the case that is *worst for the decoy* rather than the average one,
and that turns out to matter. The left-hand bound assumes the unengaged colonist is shooting
back, so it holds the +10; it is hardest at the 40-cell clamp and demands `F > 1.5`. The
right-hand bound assumes the engaged colonist is **not** shooting back, so it holds only the
+40; it is hardest at point-blank and demands `F < 1.667`.

That upper bound is the easy one to get wrong. A factor of 1.75 satisfies the same inequality
at ten cells and beyond and quietly breaks it inside seven — which is precisely where "a raider
who has already reached somebody does not turn around" is the promise being made. The window is
therefore **(1.5, 1.667)** and the constant is **1.6**, near the middle of it, beating an idle
target by three fifths.

`Tests/Mimic` holds a transcription of the formula and asserts both bounds at *every* distance
from zero to the clamp, along with the window itself and the forward-throw slack, so changing
the constant fails a test rather than quietly changing how raids behave.

There is no `enemyTarget` rewrite here, and that is the difference from *provoke*, which
does exactly that and is a hard taunt (see *Combat presence*). Doing it natively means the
decoy competes inside the same function as everything else: cover, distance, friendly-fire
avoidance and the engaged bonus all keep working, and no enemy's job queue is thrashed.

### The picture is the thrower's own renderer, run twice

The decoy has no appearance of its own. It is drawn by running the source pawn's
`PawnRenderer` a second time at the decoy's position, which is the same trick the arc and the
time lattice already use for afterimages, and which gets body, head, hair, colours, xenotype,
every worn garment and the weapon in the hand exactly right — with no `PawnGenerator` call, no
apparel to clone, and nothing to destroy on the way out.

The three phases are not optional. As of 1.6 a pawn is drawn from results the render tree
computed earlier in the frame for wherever that pawn actually is, so asking for a draw at a
position of your own draws nothing at all; `EnsureInitialized` and `ParallelPreDraw` at the
decoy's position re-point those results first, and they are then pushed back to the source's
real position so nothing else drawing that pawn this frame inherits the decoy's.

Two things are pinned that the afterimages do not pin. The rotation is overridden with a facing
captured at throw time, so the decoy does not turn as its source turns, and `neverAimWeapon`
keeps it from raising a gun it cannot fire.

**What is not pinned is posture, and that is the known limit.** A decoy whose source is running
bobs; a decoy whose source is lying down lies down. Rather than paper over it, the decoy simply
**ends when its source stops standing** — dead, downed, carried, or off the map. That disposes
of every posture case at once, and it is why "one decoy per thrower" is a rendering rule as much
as a design one: two decoys sharing a source would run that renderer twice in a frame and push
the results back once, drawing the second from the first one's position.

### It is coloured by the game's own tint field, not by new materials

The copy is drawn blue, translucent, faintly rippling, and without a shadow. None of that is
painted; all four fall out of two fields the render tree already carries.

`PawnDrawParms.tint` is multiplied into every material the pawn draws with —
`PawnRenderNodeWorker.PreDraw` does `parms.tint * mat.color` into a `MaterialPropertyBlock` —
so a postfix on `PawnRenderer.GetDrawParms` that multiplies in a colour tints the whole figure,
clothes and weapon included, with nothing to enumerate. Multiplied rather than assigned, so the
damage flash still reads through: a decoy being shot flickers the way anything else does.

`PawnRenderFlags.Invisible` is the second field, and it does two jobs. `PawnRenderNodeWorker`
swaps every material for one on the `Misc/Invisible` shader, which is translucent and carries a
distortion texture — light with a ripple in it. And `RenderPawnAt` skips the shadow for anything
carrying that flag, which the game does for its own cloaked pawns and which matters here more
than it does for them: a shadow under a hologram is the most obvious tell there is.

Both patches are inert unless `MimicRender.DrawingAsDecoy` names the pawn being rendered, which
is true for exactly one call a frame per live decoy. Plain static state is enough — the game
finishes every parallel pre-draw job inside `DrawDynamicThings`, and `MapComponentUpdate`, where
the decoy draw happens, runs after that on the main thread.

**There is a third patch and it is the one that is easy to miss.** Zoomed out past a threshold
the game stops rendering humanlike pawns through the render tree and blits a cached frame from a
texture atlas, taking its material from the atlas and passing `PawnRenderFlags.None`. That path
honours neither the tint nor the shader, so the decoy would look right while the camera was
close and turn back into an ordinary-looking colonist the moment the player zoomed out.
`ParallelGetPreRenderResults` already takes a `disableCache` parameter and simply never gets it
passed from there, so a prefix sets it.

`MimicDefaults.Shimmer` turns the cloaking flag off and leaves the tint, which gives a solid
blue clone with a shadow instead. That switch exists because it is the one decision in this
feature that cannot be made by reading code.

### What the Combat Extended patch does and does not do

The same shape as the frost bomb's, for the same reasons — CE's stats and a `ToolCE`, and
neither CE's verb nor CE's projectile, because their verb casts what it spawns straight to
`ProjectileCE` and taking it would cost the throw animation. One difference is worth stating:
the frost bomb gives up CE ballistics for a cell-targeted stun, which is a small loss, while the
beacon gives up nothing at all. It is a device that arrives at a cell and switches on; there is
no damage to model and therefore no ballistics to miss.

What CE does change, correctly, is how long a decoy lasts. CE rounds hit harder, so 120 hit
points come apart faster — and the beacon is a way to spend somebody else's ammunition in a mod
about ammunition being finite.

## How the grenade throw is animated

Melee Animation draws the arm. This mod supplies the clip, decides when the hand opens, and
launches the grenade at that moment — and works without any of it.

### Why a thrower stands still

A pawn that has just thrown something cannot walk, and cannot fire again, until the weapon's
cooldown is up. This is not something either thrown item does — it is how every ranged weapon in
RimWorld works, and it is worth writing down because it is the single biggest constraint on what
`RangedWeapon_Cooldown` may be set to.

Three separate things hold the thrower still, in order:

| Phase | Length | What holds them |
|---|---|---|
| Warmup | `warmupTime`, 0.8s here | `Stance_Warmup`. A move order cancels it and the throw never happens |
| The clip | 1.2s, hand opens at 0.58s | Melee Animation pins the pawn for the length of the animation. Without that mod this phase does not exist and the object launches instantly |
| Cooldown | `RangedWeapon_Cooldown` | `Stance_Cooldown` |

The third is the one that matters. `Stance_Cooldown` extends `Stance_Busy`, whose `StanceBusy`
is `true`, and `Pawn_PathFollower.PatherTick` returns immediately while `stances.FullBodyBusy`:

```csharp
else
{
    if (this.pawn.stances.FullBodyBusy)
    {
        return;
    }
    ...
```

So for the whole cooldown the pawn does not move and cannot start another shot. **A new order
clears it** — `Pawn_JobTracker.StartJob` takes `cancelBusyStances = true` by default and calls
`CancelBusyStanceHard`, so clicking somewhere frees them on the spot. What is stuck is a pawn
with nothing else to do: a drafted colonist holding position stands through the entire cooldown
with the little countdown circle under them.

That is why the frost bomb's six seconds is described as a long cooldown rather than a normal
one, and why the mimic beacon is eight rather than the thirty its first draft had. Thirty
seconds is a fine gate on an item and an unusable one on a person — it would have taken the
thrower out of the fight the beacon exists to save. Anything that needs to be gated for longer
than a few seconds has to be gated on the item instead, the way the stasis belt holds its charge
on the belt rather than on the wearer.

### It is a sidearm throw, because the camera looks straight down

There is no vertical axis on screen: x is east, z is north, y is nothing but draw order. So a
grenade cannot be raised overhead — moving it "up" sends it north, and the throw comes out as
a dogleg. And a true overarm throw seen from directly above is foreshortened onto a line, since
up is the one direction the camera cannot see.

So the hand orbits the body in the horizontal plane: hip, back around the pawn's right, whip
through the facing axis, release at full reach pointing at the target. The path is authored in
polar — an angle and a reach — and sampled into position curves, because interpolating the
cartesian points directly pulls the hand inward on fast segments (a chord between two points on
a circle passes inside it) and the arm goes limp exactly where the throw is fastest.

Their own clips read the same way once you stop seeing height in them: `Execution_Behead` swings
z from −0.58 to +0.85 around a victim standing due east, which is a sword sweeping horizontally,
not a sword being raised. They never animate scale either, so they have no way to show height
and do not try.

### The clip is generated, not exported

Melee Animation's own guide animates in Unity and exports a json. The json is the real
interface: `AnimDataSourceManager.ScanForDataFiles()` walks **every active mod** for an
`Animations/` folder and indexes the `.json` files it finds by filename, so a mod ships a
clip simply by having one. And the format is a flat bag of keyframe curves, which means it
can be written directly.

`make_throw_anim.py` does that. No Unity install, the keyframe table diffs as a keyframe
table rather than as 40KB of regenerated json, and the release moment is a number in the
script rather than a marker somebody has to go and look at.

What is given up is the visual preview. Melee Animation ships one anyway: dev mode →
*Melee Animation* → *Open Debugger* → *Animation Starter* plays any loaded clip on any pawn.

### The rig

Six parts: `BodyA` and its `HeadA`, an invisible `PawnAHolding` container carrying `HandA`,
`HandB`, and the grenade. Two of those names are load-bearing and one is load-bearing by
its **absence**:

- `HandA` / `HandB` are looked up by name in `ConfigureHandsForPawn`, which supplies the
  hand sprite, the pawn's own skin colour, and a glove if they are wearing one. Both must
  exist even though only one throws: their off-hand lookup is guarded by the *main* hand's
  null check, so a rig with `HandA` and no `HandB` throws inside their code. `HandB` hangs off
  `BodyA` rather than off the throwing cluster, so it stays braced at the body instead of
  orbiting on the end of the arm — parentage is free, since the lookup is by name only.
- There is deliberately **no `ItemA`**. Their `AddPawn` claims that name and overwrites its
  texture with whatever melee weapon the pawn is carrying — which on a grenade throw would
  put a sword in the hand. The grenade part is called `Grenade`, so it is left alone.

Hands also have to be asked for explicitly. Their visibility resolves as
`animation ?? weapon ?? false`, and a thrower is holding no melee weapon, so the weapon term
is null and the whole thing falls through to invisible. `<handsVisibility>` in the AnimDef
is the only way a weaponless clip ever draws hands.

### Four facings, three clips

A pawn body is not drawn at a free angle. `DrawPawns` hands the pawn renderer a `Rot4` taken
from the clip's own `PawnBody.Direction` curve, so the body snaps to one of RimWorld's four
facing sprites. The arm and the grenade are ordinary sprites drawn at a real angle and can
sweep anywhere — the body cannot. So the facing is **baked into the clip**, and one clip
cannot serve every direction.

East also covers west through horizontal mirroring. North and south have dedicated clips,
with the body step and supporting hand rotated into the facing direction. The throwing hand
passes behind the torso on the far side and in front on the near side:

| Throw | Clip | Flags | Facing |
|---|---|---|---|
| East | `RimArt_ThrowGrenade` | — | Rot4.East |
| West | `RimArt_ThrowGrenade` | `FlipX` | Rot4.West |
| North | `RimArt_ThrowGrenadeNorth` | — | Rot4.North |
| South | `RimArt_ThrowGrenadeSouth` | — | Rot4.South |

`ThrowAnimation.TryThrow` selects the clip by dominant axis, with ties going to east/west.
All three clips use the overhand poses in `make_throw_anim.py`. The throwing hand rises
beside the head, sweeps over the crown, releases while raised, and follows through downward.
Forward reach and shoulder placement follow the facing; screen-space lift stays upward in
all three clips. The body leans back while loading and shifts forward through release.
Release remains at tick 35 of 72; projectile flight is unchanged.

Adding the south definition and runtime selection requires a full mod deployment and game
restart. Subsequent curve-only edits can use `./deploy.sh anims` and the animation reload action.

### The grenade leaves on a tick, not on an event

Melee Animation has an event system, and this does not use it. The clip's length in ticks
times `ThrowAnimation.ReleaseFraction` is the tick the hand opens; `MapComponent_Throws`
parks the launch until then and `PendingThrow` fires it. A thrower who died, was downed or
left the map during the wind-up drops the throw rather than launching from an empty cell.

That fraction is single-sourced: `make_throw_anim.py` prints it every time it writes the
clip, and it is the same number as the constant. Change the timing in the script and the
script tells you what to change the constant to.

Without Melee Animation, `TryThrow` returns false, the delay is zero, and the grenade is
launched on the spot — which is what every thrown weapon in the base game does.

### Two separate bridges to one mod

`Source/RimArt/Arc/MeleeAnimation.cs` and `Source/RimArt/Throw/ThrowAnimation.cs` both talk
to Melee Animation by reflection and neither uses the other. That is deliberate. The Arc
bridge wants an execution — two pawns, a weapon filter, an outcome roll, a promotion pass —
and switches itself off wholesale unless every one of those resolves. A throw needs none of
it, and hanging it off the execution bridge's stricter gate would mean a change to their
execution API silently costing this mod its grenade animation too.

### Where it fails loudly instead of quietly

Three things depend on their API and every one of them fails silently: the bridge resolves
members by name, the AnimDef sets XML fields that DirectXml drops with a warning if renamed,
and the generated json is deserialised straight onto their model classes — where Newtonsoft
ignores a key it does not recognise, so a renamed curve loads happily and plays with the arm
missing.

So `Tests/OriginBlade/ApiChecks` states the whole contract independently, the same way the
Combat Extended bridge contract is stated: the reflection targets, the AnimDef fields the XML
sets, every key and curve name in the generated json, and the `BodyA`/`HandA`/`HandB`/
`Grenade`-present, `ItemA`-absent rule. It is skipped, not failed, when their mod is not
installed.

This is not theoretical. The check caught, on its first run, that `AnimPartData` and
`AnimPartOverrideData` carry **no namespace** — their `AnimData.cs` declares none, unlike
every file around it — so the bridge's texture override had been binding `AM.Data.AnimPartData`
and silently skipping itself.

## Layout

```
About/About.xml                  metadata, Biotech + Harmony + VEF dependencies
loadFolders.xml                  1.6 only
1.6/Defs/AbilityDefs/            AbilityDefs + AG_Genetic category
1.6/Defs/GeneDefs/               GeneDefs, some MayRequire'd
1.6/Defs/HediffDefs/             hediffs + abstracts
1.6/Defs/ThingDefs/              the involute aperture and blade states
1.6/Assemblies/RimArt.dll        built output, committed
Textures/RimArt/Panoply/         blade sprites
make_textures.py                 draws them; run it after editing, commit the PNGs
Animations/                      Melee Animation clips, as json (one per facing pair)
make_throw_anim.py               writes both throw clips; run it after editing, commit the json
Patch_MeleeAnimation/1.6/Defs/   defs that name their types; loaded only when their mod is
Patch_CombatExtended/1.6/        CE stats and tools for the frost bomb and mimic beacon
Languages/English/Keyed/         message strings
Source/RimArt/                   C# source
Source/RimArt/Rounds/            one round in flight, either engine's; the CE bridge
Source/RimArt/Throw/             the throw animation bridge and the launch it delays
Source/RimArt/Frost/             the frost bomb: its verb, its burst, its damage worker
Source/RimArt/Mimic/             the mimic beacon: the decoy, its targeting, its renderer copy
Source/RimArt/ToyCar/            the remote vehicle, its link, its operator lock
Source/RimArt/RetrievalHook/     the retrieval hook belt: tether state, pulls, wound penalty
```

Def prefix is `AG_`. Custom blade, crow and stasis art lives under `Textures/RimArt/`;
the remaining icons reuse Core or Biotech textures. Run `make_textures.py` after editing
the generated art and commit the resulting PNGs.

## Building

Needs a .NET SDK and Vanilla Expanded Framework for RimWorld 1.6; assemblies are referenced from the local game and Workshop installs.

```bash
dotnet build Source/RimArt/RimArt.csproj
```

Output goes directly to `1.6/Assemblies/`. Override the game and VEF paths if yours differ:

```bash
dotnet build Source/RimArt/RimArt.csproj \
  -p:RimWorldManaged="/path/to/RimWorld/RimWorldWin64_Data/Managed" \
  -p:VEFAssemblies="/path/to/VanillaExpandedFramework/1.6/Assemblies"
```

VEF is required and must load before RimArts. Its assembly uses `Private=false`, so the build
does not bundle `VEF.dll`; it is supplied by the installed framework mod.

Harmony is referenced with `ExcludeAssets="runtime"` so `0Harmony.dll` is never copied
into `Assemblies/` — shipping a second copy alongside the Harmony mod causes load errors.

The generated art and the generated animation are both committed, so a plain `dotnet build`
is enough. Re-run their scripts only after editing them:

```bash
python3 make_textures.py     # -> Textures/RimArt/**.png
python3 make_throw_anim.py   # -> Animations/RimArt_ThrowGrenade{,North}.json
```

## Validating

```bash
python3 validate.py
```

Checks XML well-formedness, that every def and texture reference resolves in Core or
Biotech only, that custom `Class=` values exist in the built assembly, that no def ends up
carrying the same comp class twice, and that every `Translate` key used in C# is defined.

The duplicate-comp check exists because a child's `<comps>` list is **merged** with its
parent's rather than replacing it. Declaring a comp on both an abstract base and its child
silently gives the hediff two copies, and RimWorld only says so at load time, as
`two comps with same compClass`. Note that the parent lookup is keyed on `Name=`, not
`defName` — an AbilityDef and a HediffDef may legitimately share a defName.

One tag is deliberately exempt: `<category>` inside a `ThingDef` is the `ThingCategory` enum,
not a def name, and checking it asks the game for a def that was never meant to exist. On an
`AbilityDef` the same tag *is* a reference, so the exemption is keyed on the parent def type
rather than on the value.

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

Three harnesses run production code against small game-boundary doubles, plus one that loads
the built mod against the installed game and resolves every Harmony target and injected
parameter. That last one also states the Combat Extended contract independently of the bridge
that uses it — twenty-nine members of `ProjectileCE` by name and type — so a CE update that
moves one of them fails a test rather than a single `Log.Warning` in a game nobody is watching
the log of. It is skipped, not failed, when CE is not installed. Each harness has its own README
with the in-game checks it cannot make:

```bash
dotnet run --project Tests/VectorEdit/VectorEdit.csproj          # vector manipulation arithmetic
dotnet run --project Tests/OriginBlade/OriginBlade.csproj        # Origin: Blade lifecycle
dotnet run --project Tests/Carrion/Carrion.csproj                # Carrion lifecycle
dotnet run --project Tests/RetrievalHook/RetrievalHook.csproj    # retrieval hook targets, mass, wounds, drag
dotnet run --project Tests/OriginBlade/ApiChecks/ApiChecks.csproj # Harmony targets, signatures, CE bridge contract
```

Confirmed in-game before the single-source roster change (1.6.4871, alongside ~40 other mods
including Vanilla Psycasts Expanded):

- the stasis field renders, and projectiles visibly halt in mid-air inside it
- no noticeable frame cost with a field up, despite the prefix sitting on `Thing.DoTick`
- Resonance: notes are set on the struck part, a second blow in phase takes the part
  off, and the damage-layer postfix reads hit parts correctly off `DamageResult`
- Wound Debt: wounds are held rather than applied, the carrier keeps moving while in
  debt, and settling delivers the whole bill at once - including the prefix ordering on
  `Thing.TakeDamage` holding up with the other damage effects present

### Testing the fold organ

The pass-through cannot be tested in a fight, and that is a property of the design rather than a
gap in it: it fires only on the one part the hole is in, which is a few percent of incoming hits,
and the carrier is under fire the whole time you wait for it. So the test loop takes the combat
out.

Dev mode → the debug actions menu → **RimArts**, all pawn-targeted:

| Action | What it is for |
|---|---|
| Name the hole part | Says which part, what share of hits it covers, whether the hole is connected, and whether the volume exists |
| Re-roll the hole part | Tries a different part without generating a new pawn. The gene rolls once in play, which is right, and useless for a test session |
| **Move the hole to…** | Picks the part off a list, filter and all. The only way to reach the torso — no amount of re-rolling gets there, see below |
| Connect hole (10 min) | Vent, long enough to run a whole test without re-casting |
| **Put a round through the hole** | Fires a rifle round with the part named rather than rolled. The pass-through is guaranteed to be the branch under test |
| Fire a round, part unrolled | The same round with the part left to the engine, which is what a real shot does |
| Go to the volume | Jumps the camera into the room, generating it if the hole has never been opened |

The order that answers the most in the fewest clicks: *name the hole part* → *connect hole* →
*put a round through the hole*. A pass looks like a violet flash on the carrier, **no injury on
their health tab**, and a rifle round crossing the volume when you switch to it.

**The torso is a rare roll, not an impossible one.** `allowAnyPart` on the gene drops the
core-part and vital-organ exclusions from `ResonanceUtility.CanRing` while keeping the rest, so
the roll can land on a torso or a head — about one candidate in fifteen — and still never on an
internal organ. It is a trade rather than a prize: a torso hole stops roughly 40% of incoming
fire and puts the efficiency penalty on the part everything else hangs off, so that carrier is
hard to kill and barely able to work.

To get one on demand rather than waiting for the roll, *move the hole to…* picks it off a list,
and `forceHolePart` does the same from the def side if it needs to survive a reload.

*Fire a round, part unrolled* is the one that checks the part of this most likely to be subtly
wrong. The prefix rolls the hit part itself and writes it back with `SetHitPart` so vanilla does
not roll a second, different answer - if that skewed the odds, this is where it shows. Run it
twenty times and roughly the part's coverage share should pass through and the rest should
wound normally.

For the abilities rather than the mechanic, the ordinary way round is: *connect hole*, fold, and
have something shoot the aperture - insects and mechs go for structures and will find it on
their own.

Still unverified:

- **everything under Combat Extended.** The bridge resolves against the installed CE build and
  every member it reaches is checked by `ApiChecks`, but none of it has been in front of the
  game. The parts most likely to bite: whether a redirected CE round forced onto the lerped
  trajectory worker reads as a bullet rather than as a glitch, whether chest height is the right
  floor for one caught near the ground, whether the phase barrier's segment capture now catches
  *too* much of a CE burst, and whether a held CE round drawn from a written position looks right
  at speed. The involute's CE relaunch is the least exercised path of the three

- **saving and reloading with a field still up** - bubbles persist through
  `MapComponent_TimeBubbles.ExposeData`, and that path has never run
- provoke against pawns already fleeing or in a mental state
- disarm spit against mechs
- whether the metabolic overdrive exchange rate feels right; a full stomach buys about
  20 seconds, which is the number most likely to need tuning
- **everything in the anchor organ.** It compiles and validates and has never been in front of
  the game. The parts most likely to bite: whether `CanEquip` refusal produces log spam from job
  givers that expect a weapon, whether a map-wide `range` of 9999 upsets the targeter, whether
  clapping a pawn out of a bed or out of a caravan-forming job leaves anything stuck, and whether
  two marks is too few to be interesting or exactly right
- **arrears across a save/load.** The ledger is scribed so that reloading cannot be used to
  duck a debt, and that path has never run. Cast arrears, take hits, save mid-window, reload -
  the debt should still be there and should still land
- **arrears tuning.** Confirmed working in-game; the numbers are still guesses. Twenty seconds
  and a half-day cooldown were picked to feel right rather than measured, and `severityPerDamage`
  at 120 only decides how fast the stage labels escalate
- **Resonance tuning.** Confirmed working in-game; what is still an open question is the
  numbers. `resonanceTicks` at 300 (5s) is a guess, not a measured value - too short and the
  second blow never lands, too long and every part on the field is live at once. `durationTicks`
  and the one-hour cooldown are untested against how often a player actually wants this
- **everything in the Reflex Booster.** The flight arithmetic has a test harness behind it
  (`Tests/VectorEdit`) and the Harmony targets resolve against the installed game, but none of
  it has been in front of the game yet. The parts most likely to bite: whether the editor's
  drag reads cleanly at the zoom levels a fight is actually watched at, whether six cells is
  enough reach to ever catch anything, and whether the strain table is a real limit or a
  formality. The numbers are initial defaults — the 20-cell range baseline, the 8/24/48/80
  strain costs, the consciousness caps on the strain stages, and the five-second cooldown —
  and all of them were picked to feel right rather than measured
- **Origin: Blade beyond its first look.** Rain has been cast in game and the blades land,
  plant and hold; what has not been tested is loose, grasp, the debt curve, or any of it in a
  fight. See its own *Known gaps* above
- **everything in the Arcing weapon.** It compiles and validates and has never been in front of the
  game, and unlike the rest of the mod it is talking to another mod's internals. The parts most
  likely to bite are in its own *Known gaps* above; the first thing to check is simply whether a
  three-target chain reads as one movement
- **everything in the fold organ.** It compiles and validates and has never been in front of
  the game. The parts most likely to bite: whether `Projectile.Launch` with a null launcher
  survives contact with real projectile code, whether the `MoteGlow` shader on the aperture's
  `SkipInnerDimension` texture reads as a hole or as a violet smear, whether a lone pawn on a
  pocket map will take jobs at all - tending, eating, sleeping on a posted bed, which is what
  makes the rescue play work - and whether hostiles ever choose to shoot a ground aperture

### Carrion: a feeding flock

Carrion spends one shared dispersal charge to send crows to a fresh flesh corpse within ten
cells. The carrier stays visible and does not travel with the flock. Birds fly out, land and
peck for two seconds, consume the body, and return to deliver recovery. A human-sized corpse
provides up to 20 points of healing to actively bleeding, non-permanent injuries, removes
0.20 blood-loss severity, and restores 0.20 Hemogen if the carrier has an active reserve.
Smaller bodies provide proportionally less; larger bodies are capped at the same recovery.
Scars, missing parts, diseases and nonbleeding injuries are not healed.

Each carrier can have one flock away, and each corpse can be claimed by one flock. Progress
and pending recovery are saved. A corpse removed before consumption, or a carrier who dies
or leaves the map, ends the flight without recovery or a charge refund. A concurrent Murder
pauses feeding until the carrier lands. The pawn never performs an ingestion job.

Lifecycle checks: `dotnet run --project Tests/Carrion/Carrion.csproj`. These use production
lifecycle code with game-boundary stubs; see `Tests/Carrion/README.md` for in-game checks.
