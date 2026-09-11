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

The user selected these four additional concepts for this document: **Frost Bomb, Mimic
Beacon, Grapnel Launcher, and Paper Bomb**. This selects ideas for design notes, not
implementation. Effects, costs, durations, and research requirements below remain proposals.
Paper Bomb is the chosen name for the earlier explosive-tag concept.

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

### Grapnel Launcher

**Role:** reach cover quickly using a hook, cable, and powered pull.

- Target a valid solid anchor, such as a wall or rock, with a clear route to a valid landing cell.
- Pull the wearer physically along that route; do not teleport through intervening walls.
- Prevent shooting during travel and add a short recovery after landing.
- The wearer can still be hit during traversal.
- Validate the anchor, route, and landing cell. Exact range and allowed anchors remain open.

**Acquisition proposal:** wearable equipment crafted after machining-related research.
**Supply proposal:** reusable cable with a cooldown, avoiding ammunition restocking.

### Paper Bomb

**Role:** Naruto-inspired explosive tags for prepared ambushes and breaching.

- Attach a paper explosive tag to a nearby surface, such as a wall or door.
- Withdraw, then detonate it remotely.
- Placement exposes the pawn. The blast can injure allies and damage colony structures.
- Strong structures may survive; breaching must depend on the charge's damage rather than
  guaranteeing destruction of every wall or door.
- Group detonation, placement limits, blast size, and remote-trigger range remain open.

**Acquisition proposal:** craft consumable tags after a dedicated explosives research project;
specific ingredients and trade availability remain undecided.
**Supply proposal:** one tag consumed per explosion, with replacements crafted or acquired.
Reusable remote triggering does not itself replenish spent tags.

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
