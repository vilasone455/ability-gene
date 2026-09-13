# Tactical ability ideas

Design notes from the September 11, 2026 discussion. These are proposals, not implemented
features or tested CE compatibility claims. Numbers and research names are starting points.

## Direction and current preferences

- Add grounded tactical kits useful with Combat Extended (CE), without a large reality-bending theme.
- Give weaker existing traits a useful trick while preserving their drawbacks.
- The user favored **Wimp → Play Dead** and the broader idea of making bad traits interesting.
- Use XCOM 2 as inspiration, translating turn-based abilities into real-time decisions.
- Keep one source per kit, following [the roster](../REVAMP.md). Alternative sources below
  are options to choose between, not simultaneous acquisition systems for the same kit.
- Other pairings and priorities remain suggestions rather than approved implementation scope.

## Existing ability sources

| Source | Acquisition | Existing example |
|---|---|---|
| Gene | Acquire and implant a gene | Corrosive glands |
| Trait | Recruit or start with a pawn carrying the trait | Combat presence |
| Implant | Surgically install an implant obtained through crafting, trade, or rewards | Neural accelerator |
| Equipment | Wear the granting item | Stasis belt |
| Weapon trait | Equip a weapon carrying the special trait | Resonant / Arcing |
| Earned origin | Meet requirements and complete an awakening | Origin: Blade |

Possible future source categories mentioned: training, temporary drugs, and conditions
gained through events. None was selected. Crafting, trading, and quest rewards are acquisition
routes within a source, rather than separate ability sources.

## CE design rules

CE still uses randomness in projectile spread and aiming errors. Its ballistic simulation
does not make every shot deterministic or accurate. Angular deviation also produces a larger
offset over a longer distance.

For these proposals, preserve ammunition consumption, weapon firing limits, recoil/spread,
cover, penetration, projectile travel, and collision unless an ability explicitly changes
a relevant mechanic. A targeting benefit does not make bullets pass through walls.

The earlier proposal for an extra Squadsight distance penalty was withdrawn: let CE's existing
long-range behavior supply the difficulty first. Do not add a separate percentage hit roll.
CE targeting, suppression, stance, and aiming integrations still need implementation review
and in-game testing.

## Play Dead / Feign Death

**Preferred source: the existing Wimp trait.** Automatically grant the ability to carriers,
including existing colonists. Players gain access by starting with or recruiting a Wimp pawn.

The pawn deliberately collapses so enemies shift their attention to other threats. This gives
an exposed sniper or flanker an escape option and makes a weak combat trait more interesting.

| Part | Proposed behavior |
|---|---|
| Activation | Manual, while the pawn can still act and before actual incapacitation |
| Deception | Ordinary hostile targeting treats the pawn as no longer an active threat |
| While pretending | Cannot move, attack, reload, or use other abilities |
| Ending | Manually stand, with a short recovery delay before acting |
| Danger | Flying bullets, stray shots, and explosions can still injure the pawn |
| Health | No healing or immunity; genuine pain shock still prevents standing |

The tactical decision is when to stand up: wait for enemies to pass, let allies draw fire,
then retreat or attack from behind. Deception should not guarantee survival. Merely forcing
a downed state does not guarantee the desired targeting behavior; custom handling is needed.
Enemy types, pursuit behavior, duration, and cooldown remain unresolved.

Flavor option: “At the first sign of trouble, [PAWN] gives a convincing performance of
someone else's problem.”

Earlier alternatives, superseded by the preference for an existing trait:

- New natural trait **Death's understudy**.
- **Origin: Survivor**, earned after surviving separate battles in which enemies downed the
  pawn. Milestones would need to discourage deliberately injuring colonists.
- Origin flavor: “They have mistaken you for dead before. You learned what made them believe it.”

## XCOM 2-inspired abilities

These effects are proposed adaptations, not claims that XCOM 2 or CE already implements them
in this form.

### Squadsight

An ally spots a target, allowing the shooter to engage it beyond normal weapon range.
This is the core XCOM-inspired benefit; shared sight alone would have little value without
a tangible change to targeting in RimWorld.

- An allied pawn must maintain direct sight of the enemy.
- The shooter still requires a clear ballistic path from their own position.
- Extend targeting range up to a tuned limit; do not assume unlimited map-wide reach.
- Require standing still and a longer prepared shot. Moving cancels preparation.
- Use CE's existing long-range aiming and ballistics, with no additional distance penalty initially.
- Normal ammunition, cover, penetration, and projectile travel remain relevant.

Example: a scout spots an enemy from a flank while a sniper fires through an open lane from
a distant ridge.

Suggested source after the XCOM clarification: **Origin: Marksman**, earned at approximately
Shooting 14 plus a small set of marksmanship milestones. Exact milestones remain undefined.

Earlier alternative: an **ocular targeting implant**, crafted after Bionics and targeting
research, or obtained through combat traders and quests. That earlier version linked one
shooter to one spotter, required the spotter to track without firing, and granted improved
aiming plus modest extra range. Whether to keep those link restrictions is unresolved.
CE already has artillery spotting; this proposal concerns direct-fire weapons and needs its
own targeting integration.

### Overwatch

Prepare an ambush along a chosen firing lane. In real time, the reward is preparing before
an enemy appears and obtaining a faster first response.

1. Choose a cone or doorway to watch.
2. Stand still and prepare the weapon for several seconds.
3. Hold fire until an enemy enters the watched area with a valid firing path.
4. Automatically fire with a shortened aiming delay for the first shot or burst.
5. End Overwatch and resume normal combat.

- Ignore targets outside the watched cone while holding Overwatch.
- Moving, reloading, or changing the cone resets preparation.
- Require a loaded weapon and consume normal ammunition.
- Preserve CE shot error and collision: the shot can miss.
- Respect the weapon's mechanical firing limits.
- Display the cone when selected and show **Preparing → Ready → Triggered** status.

Suggested source: **Careful shooter**. Its usual long aiming time is spent preparing the
ambush earlier; keep the underlying trait drawback. The relationship with the Aim proposal
below remains open: they could form one trait kit if both are selected.

### Kill Zone

Maintain a watched cone for a limited duration and acquire successive targets faster.
Normal firing cycles and ammunition still apply; entering the cone must not create free
instantaneous shots.

| Ability | Intended distinction |
|---|---|
| Overwatch | One prepared interception, then ordinary combat |
| Kill Zone | Sustained lane control with faster acquisition of successive enemies |

Suggested source: a **targeting implant**, crafted and surgically installed. Exact research,
cost, duration, and acquisition bonuses remain open.

### Hunker Down

**Suggested source: Delicate.** Crouch tightly behind cover to reduce exposed body height.
Cannot shoot while hunkered; moving ends it. The pawn still takes the trait's extra damage
when hit. This requires a real CE targeting-profile effect, not only a visual animation.

### Steady Hands

**Suggested source: Slowpoke.** Standing still for several seconds gradually reduces weapon
sway; moving resets the benefit. The pawn remains slow to reposition but becomes useful in
a prepared firing position. This was the suggested next pairing after Play Dead.

### Aim

**Suggested source: Careful shooter.** Spend time hunkered down to prepare one shot with
reduced aiming error. Exact activation and its interaction with Overwatch are unresolved.

### Run and Gun

**Suggested source: Jogger.** Briefly sprint, then ready the weapon faster when stopping,
with increased sway on the first shot. This adaptation is for flanking and does not imply
continuous firing while moving. Jogger is a thematic source, not an example of a bad trait.

## Other trait ability ideas

| Existing trait | Proposed ability | Effect and tradeoff |
|---|---|---|
| Delicate | Careful Advance | Move slowly with a smaller targeting profile; cannot fire and still takes extra damage when hit |
| Slow learner | Rehearsed Shot | Spend extra time preparing one steady shot; rewards preparation rather than fast reaction |
| Pyromaniac | Firestarter | Deliberately ignite an adjacent flammable target; must approach it and risks spreading fire |

Careful Advance is an earlier alternative to Hunker Down for Delicate. Rehearsed Shot overlaps
with Aim and Steady Hands; decide their distinct roles before implementing multiple versions.
Do not give every weak trait a bonus solely as compensation: the action should fit the pawn's
personality and preserve the original drawback.

## Pointman kit

Grounded infantry support for advancing between cover, holding a lane, and extracting allies.

**Suggested source: combat coordination implant.** Research Bionics followed by proposed
Combat Coordination research, craft at a fabrication bench, and install surgically. Combat
suppliers and military quest rewards could provide the same implant earlier. Suggested price
tier: a standard bionic limb, allowing a small specialist squad.

| Ability | Proposed effect | Tactical use |
|---|---|---|
| Bound | Sprint to a reachable cell within roughly 7 tiles with reduced suppression buildup; cannot fire while moving | Cross a gap or retreat; movement is physical and the pawn can be hit |
| Brace | Remain planted for up to roughly 8 seconds with reduced recoil and suppression buildup; movement cancels it | Hold a lane at the cost of being stationary |
| Extract | Pick up an adjacent downed ally and carry them to a nearby selected cell faster than ordinary carrying; cannot shoot | Casualty extraction under fire |

All three build shared **exertion**. High exertion prevents further activation until brief
rest reduces it. Spending capacity to advance competes with saving it for a rescue. No damage
immunity, free ammunition, or armor bypass. CE recoil and suppression effects need explicit
integration and tuning. This was the first suggested kit before the discussion moved to traits.

## Sapper kit

**Suggested source: engineer's harness equipment.** Craft after proposed military engineering
research and wear it to gain the kit. Consumable charges must be replenished.

- Place a directional breaching charge.
- Deploy short-lived cover.
- Set a trip flare.

Costs, placement restrictions, effects, and duration remain undefined.

## Hemostatic organ

**Suggested source: gene.** Buy a genepack or extract the gene from an acquired carrier through
the game's applicable gene-acquisition mechanics, then implant it.

Temporarily reduce bleeding in the carrier or an adjacent ally. Wounds still require treatment;
this buys evacuation time rather than restoring health. Exact resource costs and effects remain open.

## Selected tactical items

The user selected these concepts for this document: **Frost Bomb, Mimic Beacon, Grapnel
Launcher, Kunai Paper Bomb, and Remote Toy Car**. This selects ideas for design notes, not
implementation. Effects, costs, durations, and research requirements below remain proposals.

Two later decisions are folded in. Paper Bomb was the chosen name for the earlier
explosive-tag concept, and it is now **thrown on a kunai** rather than placed by hand - which
exists to keep it distinct from the Remote Toy Car, whose first sketch was a strict superset
of a charge you place and detonate remotely. The two are now split on line of sight: the
kunai is thrown at what the pawn can see, the car is driven to what it cannot. Frost Bomb,
Remote Toy Car and Mimic Beacon are implemented; the Grapnel Launcher and the Kunai Paper Bomb
are not.

### Frost Bomb

**Role:** small-area control inspired by XCOM, creating time to flank, reload, or rescue.

- Throw a cryogenic grenade that bursts into a small freezing cloud.
- Briefly immobilize affected pawns and prevent attacks, then gradually restore movement and
  manipulation as they thaw.
- Affect allies as well as enemies. Frozen targets remain vulnerable to incoming damage.
- Proposed balance: low damage, a small radius, and a short full-freeze window. Large targets
  resist or thaw sooner; heavy mechs could suffer a shorter mechanical seizure.
- Bullets continue traveling through the area. This is distinct from the Stasis Belt's time field.
- With CE, integrate grenade flight and collision, then apply a custom frost effect at the
  actual detonation point. Do not add fragmentation by default.

**Acquisition proposal:** craft after dedicated Cryogenic Munitions research, buy limited stock
from combat suppliers, or obtain through quests.

**Supply remains open:** a consumable grenade was originally proposed. The user subsequently
expressed interest in XCOM-style replenishment without repeatedly buying or crafting bombs.
A reusable charge-based item with free replenishment after a sustained safe period at the
colony is an alternative. Do not assume either supply model has been approved.

**Implemented.** Shipped as `AG_FrostBomb`, a thrown grenade weapon behind *cryogenic
munitions* research. The burst is a stun that falls off from the centre plus an `AG_Frostbound`
hediff that thaws movement and manipulation back over about ten seconds; large targets and
mechanoids take proportionally less. Allies are caught. Bullets are unaffected, as specified.
The throw plays a hand-thrown animation through Melee Animation when that mod is present, and
launches instantly when it is not.

**The supply question above is resolved, by being dissolved.** It first shipped as a worn cryo
bandolier granting an ability on a half-day cooldown — the plain timer this section warned
against. The user's later direction was that the thing should be an actual grenade rather than
an active ability, and reusable like the game's own. A reusable grenade has nothing to
replenish, so neither supply model applies: the objection recorded here was to buying and
crafting bombs one at a time, and an item that is never consumed satisfies it directly. The
balance lever moved to a six-second weapon cooldown, which is also bounded below by the throw
animation's 1.2-second clip.

**Combat Extended.** Now integrated rather than merely tolerated, but deliberately not on CE's
own terms: CE makes every hand grenade one-use ammo, and this one stays reusable so the item is
the same with or without that mod. `Patch_CombatExtended/` supplies CE's stats and melee tool.
Still **not** implemented is CE ballistic flight for the bomb — it keeps a vanilla projectile
class, because CE's verb casts what it spawns to `ProjectileCE` and adopting it would cost the
throw animation. That trade is recorded in the README, and it is the one piece of CE
integration left on the table.

### Mimic Beacon

**Role:** draw hostile fire away from allies during retreat, reloading, flanking, or rescue.
Earlier working name: Decoy Projector / False Contact.

- Throw a beacon to a nearby selected cell; it projects a stationary fake soldier.
- The projection cannot attack. Custom hostile-targeting behavior makes enemies prioritize it.
- End the projection after a short duration or when the physical emitter is destroyed.
- Proposed CE interaction: shots pass through the hologram and continue traveling; the emitter
  itself can be hit and destroyed. Positioning it beside allies can draw fire into them.
- Define which enemies can be deceived and when they recognize the decoy before implementation.

**Acquisition proposal:** craft after advanced electronics research, buy from combat traders,
or earn through quests.

**Supply proposal:** buy or craft a reusable device once, with limited combat charges that
replenish without material costs after a sustained safe period. The refill rule remains open;
a brief lull in the same fight must not reset charges. This is a custom mechanic, not CE's
ordinary inventory restocking.

**Implemented.** Shipped as `AG_MimicBeacon`, a thrown grenade weapon behind *holographic
projection* research. It stands `AG_MimicDecoy` where it lands: a copy of the thrower with 120
hit points that cannot move or act, holds for twenty seconds, and leaves nothing at all when the
timer runs out or the enemy destroys it.

**The two open questions in this entry are both closed.**

*Which enemies can be deceived, and when they recognise it* — all of them, and never. A
recognition mechanic is a second feature, and the decoy already has a defined failure case
without one: it is destructible, and a raid that shoots it simply gets it out of the way. If a
"mechs see through it" rule is ever wanted it is a filter on one list.

*Supply* — resolved by being dissolved, exactly as the frost bomb's was. The beacon is reusable
and never consumed, so the charge-and-replenish model this entry proposed has nothing to
replenish. The balance lever is a thirty-second weapon cooldown, and that number is derived
rather than picked: the decoy stands for twenty seconds, so anything shorter lets one pawn keep
a decoy up permanently.

**Two proposals here were dropped on purpose.** The projection is *not* a hologram that shots
pass through with a separate destructible emitter behind it. It is one solid object that absorbs
the rounds, because rounds spent on nothing is the entire product and rounds that pass through
and carry on are rounds still arriving somewhere. Dropping the emitter also removes a second
object to model and gives the "destroyed" ending an obvious meaning.

And the taunt is not custom targeting. It is `IAttackTarget.TargetPriorityFactor`, a multiplier
the game already applies inside its own target scorer, which means cover, distance,
friendly-fire avoidance and the engine's own five-second stickiness on an already-engaged target
all keep working. That stickiness is what implements "strong preference rather than compulsion"
without a line of code. Full derivation in the README.

**What it cost instead** was the picture: a Thing has no appearance, so the decoy is drawn by
running the thrower's own `PawnRenderer` at a second position. That is the same technique the
arc and the time lattice use for afterimages, and it is why the decoy ends the moment its
thrower stops standing. The projection reads as light rather than as a person by way of two
fields the render tree already carries - a blue `PawnDrawParms.tint` and
`PawnRenderFlags.Invisible`, which between them give a translucent rippling figure with no
shadow.

**One number was wrong in the first draft and is worth recording.** The cooldown was set to
thirty seconds so that it would outlast the decoy. `RangedWeapon_Cooldown` is not a gate on the
item, though - it is a `Stance_Cooldown` on the thrower, and `Pawn_PathFollower` refuses to move
a pawn whose stance is busy, so thirty seconds meant a colonist who could neither walk nor shoot
for half a minute after covering their own retreat. It is eight now, and permanent uptime is
prevented by the one-decoy-per-thrower rule instead. Anything in this mod that needs a gate
longer than a few seconds has to hold it on the item, the way the stasis belt does.

### Grapnel Launcher

**Role:** reach cover quickly using a hook, cable, and powered pull.

- Target a valid solid anchor, such as a wall or rock, with a clear route to a valid landing cell.
- Pull the wearer physically along that route; do not teleport through intervening walls.
- Prevent shooting during travel and add a short recovery after landing.
- The wearer can still be hit during traversal.
- Validate the anchor, route, and landing cell. Exact range and allowed anchors remain open.

**Acquisition proposal:** wearable equipment crafted after machining-related research.
**Supply proposal:** reusable cable with a cooldown, avoiding ammunition restocking.

### Kunai Paper Bomb

**Role:** Naruto-inspired explosive tags for prepared ambushes and breaching, thrown rather
than placed by hand. Earlier form: a tag attached to an adjacent surface.

- Throw a tagged kunai at a target cell or surface within throwing range; it sticks where it
  lands.
- Withdraw, then detonate remotely, individually or as a group.
- The blast can injure allies and damage colony structures.
- Strong structures may survive; breaching must depend on the charge's damage rather than
  guaranteeing destruction of every wall or door.
- A placed tag is visible and can be shot or destroyed before it is triggered.
- Group detonation, placement limits, blast size, and remote-trigger range remain open. Also
  open: whether a kunai may stick to a *pawn*, and whether an unexploded one can be recovered.

**Why it is thrown.** The earlier hand-placed form justified itself with "placement exposes the
pawn" - the pawn had to walk to the wall. Throwing removes that exposure, so the cost has to
move: consumable tags, a throwing range well under gun range, and the tag being destructible
once it is stuck. Decide those before implementing, because without them the ranged form is
strictly better than the placed one it replaces.

**Implementation note.** This reuses the throw pipeline built for the frost bomb rather than
adding machinery. `MapComponent_Throws.Begin` already takes a projectile def and a hand
texture, and the Melee Animation clip was written so that a second thrown thing is a caller
rather than a change to the bridge. A tagged kunai is that second caller.

**Acquisition proposal:** craft consumable tags after a dedicated explosives research project;
specific ingredients and trade availability remain undecided.
**Supply proposal:** one tag consumed per explosion, with replacements crafted or acquired.
Reusable remote triggering does not itself replenish spent tags.

### Remote Toy Car

**Role:** deliver a charge to somewhere that cannot be thrown to - around a corner, through a
door, inside a room - and draw fire while it drives. In-fiction a child's toy repurposed as a
chassis and packed with chemfuel; the final label is open, and the joke belongs in the item
description rather than in the mechanic.

- Deploy the vehicle at the operator's feet.
- While the operator is controlling it, the operator cannot move or act.
- The vehicle works only within a limited range of its operator.
- Detonate it on command.
- The operator is defenceless for the whole drive, and is a legitimate target while driving.
  This is the point of the design, not a drawback to be softened.

**Boundary with the kunai above.** Line of sight is the split. A kunai is thrown at something
the pawn can see; the car is driven to something it cannot. If that distinction ever collapses -
a car with a long leash and a fast chassis reaching everything a throw would - then one of the
two is redundant and the other should be cut.

**Implementation direction.** The vehicle should be a `Pawn` of the player faction, shaped like
a Biotech mech. Pathing, damage, rendering and targeting then come from the game, and "remote
control" needs no control mode at all, because selecting a thing and right-clicking is already
how RimWorld is played. What remains to build is the operator lock, the tether, the detonate
gizmo, and the scribed link between the two. A `Thing` with custom movement instead would mean
reimplementing pathing and is not worth it.

The operator lock should be a hediff rather than a job: it survives save/load and is visible in
the health tab.

**Open questions, each of which needs a defined answer or it becomes a bug report:** operator
downed or killed mid-drive; the player ordering the operator to move; the vehicle out of range;
the vehicle destroyed while controlled; save/load mid-drive; a caravan leaving with an active
link.

**Deliberately out of the first version:** vision. A drone that *sees* interacts with fog of
war, CE sightlines and Squadsight, and that is a much deeper problem than a drone that drives
and explodes. Ship the demolition form first.

**Pacing.** This is an XCOM idea in a real-time game, so time passes while the operator sits
helpless. A drive that takes thirty seconds is one the player pauses through, which is tedious
rather than tense. The chassis should be fast and the leash modest, so a run is five to ten
seconds. Fix that before tuning anything else, because it decides every other number.

**Acquisition proposal:** a crafted device, after machining or an electronics project.
**Supply proposal:** the chassis is reusable and is consumed only when detonated - drive it
back and you keep it. Retrieval costs time the fight may not give you, which prices the
decision without a timer.

## Makibishi

Chosen by the user on September 13, 2026, after the kunai belt, as a ninja tool that denies an
area. Nothing else in the mod does: the frost bomb acts only at the moment it bursts.

**Implemented** as the makibishi pouch. Decisions taken with the user:

| Topic | Decision |
|---|---|
| Carry | Waist pouch on the Belt layer, like the other belts. A thigh position would have allowed wearing it with the kunai belt; the user chose the waist because nobody carries a spike bag on the leg |
| Supply | Consumable makibishi items, one handful per throw, reloaded like the kunai belt. Spikes on the ground are not picked up again: there are too many of them |
| Shape | 3x3. The center "+" (5 cells) always, each corner 50%. The user's own design: a square's blocking with a scatter's look |
| Duration | 30 s |
| Pathing | Everyone avoids the patch if a detour exists and walks through if not |
| Effect | 35% per cell entered: 4 stab damage to a foot and Moving -30%, fading over 15 s |
| Friendly fire | Colonists and allies are hurt the same as enemies |

Rejected: a reusable grenade-slot item (endless physical spikes), a consumable stack in the grenade
slot (vanilla weapons do not stack, so every throw would need a manual re-equip), and hostiles not
seeing the patch like vanilla traps (the spikes are visible on screen).

Numbers, the pathing mechanism and the animation are in the README section *Makibishi pouch*.

## Suggested first slice

1. **Wimp → Play Dead**, the pairing the user favored.
2. Consider **Slowpoke → Steady Hands** and **Delicate → Hunker Down** next.
3. Develop **Careful shooter → Overwatch** as the prepared-ambush role.

This order is a recommendation, not authorization to implement all proposals.

## References

- [Current kit roster](../REVAMP.md) and [mod README](../README.md).
- [CE ranged weapon stats](https://github.com/CombatExtended-Continued/CombatExtended/blob/Development/Defs/Stats/Stats_Weapons_Ranged.xml): shot spread, sway, sights, range and lead error.
- [CE spotting equipment](https://github.com/CombatExtended-Continued/CombatExtended/blob/Development/Defs/ThingDefs_Misc/Weapons_Spotting.xml): existing artillery spotting context.
- [CE overview](https://github.com/CombatExtended-Continued/CombatExtended/blob/Development/README.md): weapon roles and combat systems.
- [RimWorld traits](https://rimworldwiki.com/wiki/Traits): existing trait context, including Wimp.
- [XCOM 2 manual](https://www.feralinteractive.com/en/manuals/xcom2/latest/steam/): Hunker Down and tactical context.
- [XCOM 2 Sharpshooter](https://xcom.fandom.com/wiki/Sharpshooter_Class_%28XCOM_2%29): Squadsight, Steady Hands, Aim, and Kill Zone inspiration.
- [XCOM 2 Ranger](https://xcom.fandom.com/wiki/Ranger_Class_%28XCOM_2%29): Run and Gun inspiration.
