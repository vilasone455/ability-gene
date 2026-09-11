# RimArt

A RimWorld **1.6** combat ability mod. Pawns gain abilities from genes, traits, equipment,
training, weapon traits (Odyssey) and battlefield conditions — not just one source.

Currently shipping fourteen gene-based abilities. Multi-source support is in progress.

## Dependencies

| Dependency | Required | Why |
|---|---|---|
| **Biotech DLC** | No | Gene defs are `MayRequire`d against it. Without Biotech the genes vanish and every other source still works |
| **Harmony** (`brrainz.harmony`) | **Yes** | Patches `Thing.DoTick`, `Thing.TakeDamage` and `ReservationManager.CanReserve` |
| **Odyssey DLC** | No | Weapon trait abilities are `MayRequire`d against it |
| **Melee Animation** (`co.uk.epicguru.meleeanimation`) | No | The arc tendon is `MayRequire`d against it |
| **Unique Melee Weapons** (`shunter.uniquemeleeweapons`) | No | Melee weapon traits (resonance, arc) are `MayRequire`d against it |
| Royalty / Ideology / Anomaly | No | Not referenced |
| Any framework (VEF, EBSG, …) | No | — |

## Abilities

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

### Vector reflex organ → *reflection*, *vector shove*
A redundant nervous system that answers incoming momentum by reversing it. Met −3, Cpx 5.

| Ability | Effect | Cooldown |
|---|---|---|
| Reflection | 30s. Every damage instance aimed at the carrier is returned to whatever caused it | 1 in-game day |
| Vector shove | Throws one pawn ~6 cells away from the carrier, stunned, hurt by how far they went | 20s |

**Reflection is not a shield.** Bullets, blades, blasts and a fire burning on the carrier
are all cancelled and re-applied to their instigator — the fire case works because a
`Fire`'s damage carries the fire itself as instigator, so reflected flame destroys the
fire that was doing the burning. Damage with no live instigator is cancelled rather than
returned; there is nothing to hand it back to.

The cost is that the reflex cannot tell what it is reversing. For the whole 30 seconds the
carrier is **rooted** and **untouchable**: no tending, no feeding, no rescue, no arrest,
nobody hauling them to a bed. It is a decision to stop being a person and be a wall, and
the way to beat it is to stop shooting and wait.

### Halving membrane → *recursion*

Holds a boundary that divides distance instead of blocking it. Met −4, Cpx 6.

| Path | What the membrane does | Half-life |
|---|---|---|
| Projectiles | Round is taken off the engine's clock; its reported position halves its remaining distance to the point it was caught heading for, forever | 6s |
| Melee | Attacker's chance to connect halves for every second spent inside the field | 1s |
| Verbless damage | Explosions, fire, collapsing roofs and point-blank rounds are scaled to 15% | — |

**Nothing is ever cancelled.** Every path is a continuous scaling toward zero that never
arrives, which is the whole point — the moment any of it becomes a hard "this does not
apply", the membrane stops being a receding distance and becomes an invulnerability
shield, which is the vector reflex organ's job. A round held by the membrane is still in
flight, and when the membrane closes it resumes from exactly where it was drawn and
finishes the trip. Everything queued against the carrier's face lands in the same tick.

A held round's anchor is fixed in world space at the moment of capture, not re-read from
the carrier. A round is a thing travelling its own straight line; re-anchoring it every
tick would drag held rounds along behind a walking carrier. Stepping out from behind your
own membrane therefore releases everything it was holding on that spot — the rounds were
never stopped, and the line they are on no longer has anyone standing on it.

The membrane cannot tell that air is also approaching. `Breathing` drops on a ramp from
the moment it opens: ~14s to the first stage, ~24s to oxygen-starved, ~32s to
asphyxiating, and at ~39s Breathing reaches zero and the carrier dies standing up.
There is no duration field anywhere — the membrane stays open until the player closes it
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

### Resonant marrow → *resonance*

A skeleton that holds a note. Met −2, Cpx 3.

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

### Deferred plexus → *arrears*

A second nervous system that sits between the body and the news. Met −3, Cpx 4.

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

### Panoply organ → *rain*, *loose*, *grasp*

A frame that carries more of itself than it needs, and can throw the surplus. Met −4, Cpx 6.

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
because it **is** the field, and Manipulation and Moving carry it because what is missing is
structural — part of the carrier's own frame is standing in the ground twenty cells away.

**Loose does not consume anything.** Each blade flies from where it was standing and plants
itself again wherever it stops, which is usually much closer to the people it was thrown at.
Nothing about the volley is computed here: cover, line of sight and the bodies in between are
resolved per blade by the same projectile code a rifle round goes through, so a blade behind a
raider genuinely ignores the sandbag in front of him.

**Grasp is the only place this gene puts real matter into the world.** What arrives is an
ordinary steel longsword and it stays one — ownable, tradeable, lootable off the corpse. It
needs an empty hand and will not make one; silently dropping a colonist's rifle to give them a
sword is help nobody asked for.

### Stasis organ → *stasis field* (archite)
Collapses a 6.9-cell sphere of stopped time around the caster for 20 seconds. Inside it:
pawns, projectiles in flight, fire and gas all halt, and **nothing can be harmed**.

It does not spare your own colonists and it does not spare the caster, who is at the
centre and always inside. What you buy is twenty seconds for everyone standing outside
the bubble. Cpx 4, Arc 1, five-day cooldown.

### Involute organ → *vent*, *fold*, *swallow*, *post*, *collapse* (archite)

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

Time inside runs at normal rate, deliberately. Two genes already own time and both pay for it;
a third doing it for free would be the drift this mod keeps refusing. Leaving it alone also
means the two compose — a carrier with the time lattice can fold out and Square Accel through
their own healing, in a room with no doctor, at four times the hunger.

Swallow then collapse is an unconditional kill with no corpse. It costs the carrier the volume,
everything stored in it, and every round the hole has ever taken.

### Imperative larynx → *stop*, *drop*, *kneel*, *come*, *run*

A second set of folds above the first, wired to push a word out at a pressure no throat is
meant to hold. Met −2, Cpx 4.

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
they were, still armed, still coming, and perfectly clear on who made them do it. What the gene
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
negotiator and nothing announces it; at the top stage they cannot speak at all and the gene is
simply gone until it heals. Your best talker being your best shouter is the tension the gene
actually creates, and it costs no code to create it.

**Kneel is the one that will define this gene, and vanilla designed it.** A prone pawn is hit at
×0.5 from 4.5 cells or more and at ×7.5 from 3.9 or less. So the same word is an execution setup
at melee range and an actively harmful mistake at rifle range — shouted across a killbox it
protects the raider from your own firing line. It is one number that changes sign at about four
cells, the player can read it in the shot tooltip as a *Target prone* line, and it punishes
reflexive use. Nothing in this mod computes any of it.

**Deafness is the counter and it needed no code.** A word is a sound, so anything below 0.15
Hearing does not hear it and nothing happens — no effect, no cooldown spent, a message saying
so. Mechs are excluded at the targeting params. No vanilla mechanic attacks or defends Hearing,
which makes this a real answer a player can find rather than an immunity flag this mod invented.

### Arc tendon → *arc* (needs Melee Animation)

One cast, up to three people, and none of the damage is this mod's. Met −3, Cpx 5.

| Ability | Effect | Cooldown |
|---|---|---|
| Arc | The carrier crosses to one enemy and strikes, then to the nearest enemy still standing to *that* one, up to three in a cast | half an in-game day |

**This is the only gene here that does not exist on its own.** `MayRequire` sits on the GeneDef,
the AbilityDef and the HediffDef, so without Melee Animation loaded none of the three is built
and nothing in the game refers to them. That is the honest shape for it: the whole point of the
arc is the blow at the far end, the blow is one of their executions, and a version of this gene
that drew nothing would be a teleport with a damage number attached — which is not worth a gene.

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

## How reflection works

Three mechanisms, one for each thing the reflex has to do.

**Returning damage is a single choke point.** A prefix on `Thing.TakeDamage` cancels the
instance and re-applies it to `dinfo.Instigator`, with the reflecting pawn as the new
instigator so kills are credited correctly. Doing it at the damage layer rather than per
projectile means melee, explosions, EMP and fire are covered by the same six lines, with
no per-`Projectile`-subclass patching.

A static guard wraps the re-application. Without it two reflecting pawns hitting each other
would bounce one damage instance between them forever; with it, the second one takes the hit.

Precedence against the stasis field is free: the bubble's prefix sits at `Priority.First`
and returns false for anything frozen, and a prefix returning false skips the rest. A frozen
pawn is simply immune and never reflects.

**Being untouchable is a reservation problem.** Every friendly job that wants to reach a pawn
— tend, feed, rescue, arrest, haul to bed — goes through `ReservationManager.CanReserve`
first. Refusing there stops all of them without patching one WorkGiver per interaction.
Melee reserves nothing and still connects, which is exactly right: attacks arrive and are
returned, doctors never set out.

**Rooting is one stat.** A `MoveSpeed` factor of 0 clamps `Pawn.TicksPerMove` to its 450-tick
ceiling — 7.5 seconds a cell, about four cells over the whole reflection. No patch, no
capacity mod, and no risk of the pawn being counted as downed the way a zeroed `Moving`
capacity would.

### Bouncing the actual bullets is cosmetic

Damage return already sends a bullet's damage back to whoever fired it, so the round turning
around is a visual only. `HediffComp_Reflection` scans for hostile projectiles within
`catchRadius` (1.5 cells) each game tick and re-launches them at their launcher; anything
that steps over the check between ticks still lands and is returned as damage. Friendly
rounds are deliberately left alone — a colonist shooting past the reflector would otherwise
get their own bullet back.

`reflectProjectiles` on the hediff comp turns the visual off without touching the mechanic.

### Shoving

`VectorPush` walks the target outward a cell at a time and stops at the first cell it cannot
stand in or cannot see from where it started, so a shove into a wall moves them up to the
wall rather than through it. Being stopped early is what makes it hurt: distance travelled
pays `damagePerCell`, and a blocked throw adds `slamDamage` on top. Pushes are divided by
body size, so a thrumbo barely moves.

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
1.6 — see *How the arc tendon works* for why, and for what replaced it.

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

`dinfo.Tool` is the melee test — the same one the halving membrane uses to tell a swing from a
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
the gene, so it belongs in XML the way `banWeapons` does on the anchor organ rather than being
hardcoded — but turning it off does not make this a slightly easier ability, it makes it a
different and much stronger one.

## How arrears works

**One prefix, and the ordering is the whole design.** `Thing.TakeDamage` now carries four
prefixes from this mod, and arrears sits at `Priority.Low`, deliberately last:

| Patch | Priority | What it does first |
|---|---|---|
| Stasis field | `First` | Frozen pawns are immune and run up no debt at all |
| Vector reflex | default | A reflected round was never received, so nothing is owed for it |
| Halving membrane | default | *Scales* verbless damage rather than cancelling it |
| **Arrears** | **`Low`** | Records whatever is left |

The membrane row is the one that had to be right. It scales rather than cancels, so if arrears
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

## How the involute organ works

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
alone on purpose — forcing hostiles onto a target is the alarm pheromones' job, and reaching for
it here is the drift the rest of this file keeps refusing. A gene that is strong against gunfire
and cheap against animals is a matchup, not a bug.

None of this has been run in-game yet.

## How the imperative larynx works

**One job, inserted, then handed back — and no Harmony patch anywhere.**
`Pawn_JobTracker.StartJob` already takes `resumeCurJobAfterwards`, so the interruption and the
return are the game's own rather than an imitation of them. That is the whole mechanism. It also
means this gene adds no contact surface with the other eleven: nothing here prefixes
`Thing.TakeDamage`, so it sits outside the ordering that arrears, the membrane, the stasis field
and the vector reflex all have to agree about.

The alarm pheromones reach into the same AI one layer shallower — they rewrite `enemyTarget` and
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
whole gene at 15%.

### The two capacities

`Talking` carries the cost and `Hearing` is the counter, and neither needed a custom stat. The
wear lands on the neck because there is no `Throat` body part — `Neck` is as deep as vanilla
goes — which puts it on the one part the resonant marrow calls too damped to hold a note. The
top hediff stage sets Talking to zero rather than offsetting it, so `CanSpeak` fails and every
gizmo greys out. Making that permanent is a fifth stage with `severityPerDay` 0; it is left
recoverable on purpose, because a gene that can permanently delete itself in one bad fight is a
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
Biotech, with `Gene_VoiceRoar` already spent on the alarm pheromones. Five identical buttons in
a row is the one part of this gene that wants art.

The tooltip quotes base cost only. The decision the gene offers is whether *this* target is
worth the voice, so the real multiplied figure should be visible at targeting time —
`ExtraTooltipPart()` has no target, so doing it properly means taking over the targeter draw the
way the anchor organ took over `DrawHighlight`.

`JobDriver_LayDownAwake` has `LookForOtherJobs => true`, so a hostile is permitted to abandon
kneel early. The job is marked `playerForced`, which usually holds, but three seconds may turn
out to be one.

Confirmed in-game: the gene loads and all five words cast and resolve. The tuning has not been
played against a real raid — the five-words-per-fight figure for *run* is an estimate, not a
measurement.

## How the panoply organ works

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

**This is the one gene that had to be drawn.** Every other gene in the mod dresses itself in a
Core texture that already means the right thing, and this one spent two attempts proving it
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

**No Harmony patch anywhere.** Like the imperative larynx, this gene adds no contact surface with
the other twelve — nothing here prefixes `Thing.TakeDamage`, so it sits outside the ordering that
arrears, the membrane, the stasis field and the vector reflex all have to agree about.

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

Rain has no `aiCanUse`, like every ability in this mod. A raider carrying this gene does nothing
with it.

## How the arc tendon works

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
tick. A silent gene is a bug report; a log full of the same exception is a bug report nobody can
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
error, no ghost, no clue. Both this gene and the time lattice shipped doing exactly that and drew
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
"up" has to be guessed at — which is the mistake the panoply organ made twice with the longsword
sprite. A solid additive quad scaled along the path has no heading of its own to be wrong about:
the rotation is the compass angle of the dash and nothing else. Two layers, because one bright
rectangle reads as a bar rather than as speed — a wide dim glow for edges that fall off, a narrow
bright core inside it for the line. The fade is baked into the material at eight quantised steps
rather than pushed through a property block, so it cannot depend on whether a given shader honours
one.

**Their promotion roll is asked for, and that is most of what the gene looks like.** Melee Animation
does not simply play the execution it picked: on a killing outcome it rolls again to promote that
animation into a better one — the beheadings, the head removals, the lift on a spear. Skipping that
roll left the best half of their animation set on the shelf and made an arc look like three melee
hits in a row. Asking for it means handing over a `PromotionInput` built the way their float menu
builds one, including a real occupied mask off the carrier's landing cell, so a promoted animation
cannot finish with somebody standing in a wall. The pick before it is weighted by their
`Probability` too, which is a def value multiplied by the player's own settings — somebody who
turned an animation off in Melee Animation has said they do not want to see it, and an arc is not
the place to argue.

None of that is required for the gene to work. Every one of those members is null-checked
separately from the four the bridge cannot do without, so a version of Melee Animation that moved
or renamed them costs the arc its flourishes rather than its function.

**The carrier is held still between arriving and striking.** `Notify_Teleported` drops whatever job
they had, which left them free to start walking somewhere in the pause and get yanked back mid-step.
A short wait job holds them, expires on its own if the arc is cut short, and is replaced outright by
the animation when the strike begins. They also arrive already facing the person they came for.

**The arc does not borrow the anchor organ's skip flashes,** and that is a deliberate refusal. A
clap is a psychic exchange of two places and is dressed as one; an arc is a person crossing nine
cells faster than the eye follows, so what it leaves is disturbed ground, sparks off the stop and a
line of light. Two genes that both move somebody instantly should not look the same, or watching
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

**No Harmony patch anywhere**, like the larynx and the panoply organ. Nothing here prefixes
`Thing.TakeDamage`, so it sits outside the ordering that arrears, the membrane, the stasis field
and the vector reflex all have to agree about.

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

Arc has no `aiCanUse`, like every ability in this mod. A raider carrying this gene does nothing
with it.

## Layout

```
About/About.xml                  metadata, Harmony dependency
loadFolders.xml                  1.6 only
1.6/Defs/AbilityDefs/            AbilityDefs + AG_Genetic category
1.6/Defs/GeneDefs/               GeneDefs, some MayRequire'd
1.6/Defs/HediffDefs/             hediffs + abstracts
1.6/Defs/ThingDefs/              the involute aperture and blade states
1.6/Assemblies/RimArt.dll        built output, committed
Textures/RimArt/Panoply/         blade sprites
make_textures.py                 draws them; run it after editing, commit the PNGs
Languages/English/Keyed/         message strings
Source/RimArt/                   C# source
```

Def prefix is `AG_` (kept for save compatibility). Every icon but the panoply organ's
points at an existing Core/Biotech texture; see `iconPath` on each def to swap in your
own. The two exceptions are the blade sprites in `Textures/RimArt/Panoply/`, drawn by
`make_textures.py` — see *How the panoply organ works* for why that gene could not
borrow one.

## Building

Needs a .NET SDK; the game assemblies are referenced straight out of the install.

```bash
dotnet build Source/RimArt/RimArt.csproj
```

Output goes directly to `1.6/Assemblies/`. Override the game path if yours differs:

```bash
dotnet build Source/RimArt/RimArt.csproj \
  -p:RimWorldManaged="/path/to/RimWorld/RimWorldWin64_Data/Managed"
```

Harmony is referenced with `ExcludeAssets="runtime"` so `0Harmony.dll` is never copied
into `Assemblies/` — shipping a second copy alongside the Harmony mod causes load errors.

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

Confirmed in-game (1.6.4871, alongside ~40 other mods including Vanilla Psycasts Expanded):

- the four genes appear under *special abilities*, archite gene behind Ignore restrictions
- the stasis field renders, and projectiles visibly halt in mid-air inside it
- no noticeable frame cost with a field up, despite the prefix sitting on `Thing.DoTick`
- the resonant marrow: notes are set on the struck part, a second blow in phase takes the part
  off, and the damage-layer postfix reads hit parts correctly off `DamageResult`
- the deferred plexus: wounds are held rather than applied, the carrier keeps moving while in
  debt, and settling delivers the whole bill at once - including the four-way prefix ordering on
  `Thing.TakeDamage` holding up with the other genes present

### Testing the involute organ

The pass-through cannot be tested in a fight, and that is a property of the design rather than a
gap in it: it fires only on the one part the hole is in, which is a few percent of incoming hits,
and the carrier is under fire the whole time you wait for it. So the test loop takes the combat
out.

Dev mode → the debug actions menu → **Ability Genes**, all pawn-targeted:

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
- **resonant marrow tuning.** Confirmed working in-game; what is still an open question is the
  numbers. `resonanceTicks` at 300 (5s) is a guess, not a measured value - too short and the
  second blow never lands, too long and every part on the field is live at once. `durationTicks`
  and the one-hour cooldown are untested against how often a player actually wants this
- **everything in the vector reflex organ.** It compiles and validates, and none of it has
  been in front of the game yet. The parts most likely to bite: whether refusing
  `CanReserve` produces job-giver spam in the log, whether a re-launched projectile behaves
  when it is spawned less than a cell from its new target, and whether 30 seconds of rooted
  invulnerability reads as a wall or as a win button
- **the panoply organ beyond its first look.** Rain has been cast in game and the blades land,
  plant and hold; what has not been tested is loose, grasp, the debt curve, or any of it in a
  fight. See its own *Known gaps* above
- **everything in the arc tendon.** It compiles and validates and has never been in front of the
  game, and unlike the rest of the mod it is talking to another mod's internals. The parts most
  likely to bite are in its own *Known gaps* above; the first thing to check is simply whether a
  three-target chain reads as one movement
- **everything in the involute organ.** It compiles and validates and has never been in front of
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
