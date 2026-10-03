#!/usr/bin/env python3
"""
Writes Tools/SoundLab/candidates.json: for each ability, its sound moments and about three
options per moment, each a list of layers (a SoundDef subSound: folder or clip, pitch, volume,
delay, loop).

    python3 Tools/SoundLab/make_candidates.py

Every path is checked against clips/index.json (run extract.py first) and the mod's Sounds/; a
path that does not exist stops the script. The options were chosen from clip names, the vanilla
SoundDefs that use them, length and brightness, not by ear: the lab is where they are judged.
"""
import json, pathlib, sys

WT = pathlib.Path(__file__).resolve().parent
idx = json.loads((WT / "clips/index.json").read_text())
folders = {}
clips = {}
for c in idx["clips"]:
    folders.setdefault(c["folder"].lower(), set()).add(c["source"])
    clips.setdefault((c["folder"] + "/" + c["clip"]).lower(), set()).add(c["source"])
# The mod's own sounds (make_sounds.py), as the game finds them: a path under Sounds/.
for f in (WT.parent.parent / "Sounds").rglob("*.wav"):
    rel = f.relative_to(WT.parent.parent / "Sounds").with_suffix("").as_posix()
    folders.setdefault(rel.rsplit("/", 1)[0].lower(), set()).add("RimArt")
    clips.setdefault(rel.lower(), set()).add("RimArt")
bad = []


def L(path, pitch=1.0, volume=50, delay=0.0, loop=False):
    p = path.lower()
    has_folder = any(k == p or k.startswith(p + "/") for k in folders)
    if has_folder:
        layer = {"folder": path}
    elif p in clips:
        layer = {"clip": path}
    else:
        bad.append(path)
        layer = {"folder": path}
    layer.update(pitch=pitch, volume=volume)
    if delay:
        layer["delay"] = delay
    if loop:
        layer["loop"] = True
    return layer


def O(label, *layers):
    return {"label": label, "layers": list(layers)}


def M(mid, label, when, *options, defName=None, duration=None):
    """duration: seconds the effect lasts in game; the code ends the sound then (a sustainer)."""
    m = {"id": mid, "label": label, "when": when, "options": list(options)}
    if defName:
        m["defName"] = defName
    if duration:
        m["duration"] = duration
    return m


A = {}


def ability(defName, note, *moments):
    A[defName] = {"note": note, "moments": list(moments)}


# Shared building blocks -------------------------------------------------------------------------
SKIP_IN = "Misc/Psycasts/Skip/Entry"          # Core: the skip psycast's departure
SKIP_OUT = "Misc/Psycasts/Skip/Exit"          # Royalty
BLADE_SWISH = "Pawn/Mechanoid/Scyther/Melee/Miss"
BLADE_HIT = "Pawn/Mechanoid/Scyther/Melee/HitPawn"
CLICK = "Interact/FlickSwitch"
WARMUP = "Item/Psychic_Artifact_Warmup_A01"
BESTOW = "Misc/Bestow_Warmup"
BOOM = "Weapon/Artillery/Mortar_Explode_Dry"
BIG_BOOM = "Explosion/GiantExplosion"
ZAP = "Misc/EMPDisabled"
PULSE = "Exotic/Crashed_Ship_Part/Psychic_Pulse"
SHOCKWAVE = "Impact/MechBand"

# Goku --------------------------------------------------------------------------------------------
ability("AG_GokuSolarFlare",
        "One flash, no build-up: the anime cue is a sharp bright burst. Plays once at the flash.",
        M("flash", "Flash", "when the flash goes off",
          O("EMP crackle up high + shield break sparkle", L(ZAP, 1.3, 55), L("Misc/EnergyShield/Broken", 1.2, 35)),
          O("Orbital targeter fire, bright", L("Weapon/OrbitalTargeter", 1.4, 50), L("Electricity/PowerOn/Small", 1.5, 40)),
          O("Charge shot + glass", L("Weapon/ChargeShotA", 1.5, 45), L("Buildings/GestatorGlassShattering", 1.6, 25, 0.03))))

ability("AG_GokuInstantTransmission",
        "Anime: a short 'shun'. Two moments: leaving (on the 0.5 s start) and arriving.",
        M("leave", "Leave", "fingers to forehead, Goku vanishes",
          O("Skip entry, fast", L(SKIP_IN, 1.6, 45)),
          O("Swish, high", L("Misc/Swish2", 1.8, 55), L(CLICK, 1.4, 25)),
          O("Mech jump prelaunch", L("Pawn/Abilities/LongJumpMechLauncher/PreLaunch", 1.3, 40))),
        M("arrive", "Arrive", "Goku and the passenger appear",
          O("Skip entry reversed feel: low + short", L(SKIP_IN, 1.9, 40), L("Impact/PunchMiss", 0.8, 35)),
          O("Swish, high", L("Misc/Swish2", 2.0, 50)),
          O("Skip exit (Royalty)", L(SKIP_OUT, 1.5, 45))))

ability("AG_GokuKamehameha",
        "Three moments: the held charge (loops until Fire), the beam (1.2 s), and the hit where the beam ends.",
        M("charge", "Charge (loop)", "channel 2.5 s, then hold until Fire",
          O("Psychic warmup + mech control hum", L(WARMUP, 0.8, 45), L("Pawn/Mechanitor/ControlTaking", 1.2, 30, loop=True)),
          O("Bestow warmup rising", L(BESTOW, 1.0, 45), L("Buildings/BurningPowerCell/Loop", 1.4, 20, loop=True)),
          O("Antigrav loop, pitched up (Odyssey)", L("Gravship/Gravship_Antigrav_Loop", 1.6, 40, loop=True))),
        M("fire", "Beam", "Fire: the beam runs 30 cells for 1.2 s",
          O("Orbital beam", L("Misc/OrbitalBeam", 1.1, 60, loop=True), L(SHOCKWAVE, 0.8, 40)),
          O("Charge lance + beam graser", L("Weapon/ChargeLance/Fire", 1.0, 55, loop=True), L("Weapon/Beamgraser/Fire", 0.8, 40, loop=True)),
          O("Flamethrower roar pitched up", L("Weapon/Flamethrower", 1.3, 55, loop=True), L(ZAP, 0.8, 30))),
        M("hit", "Beam end", "where the beam stops (wall or 30 cells)",
          O("Mortar dry, low", L(BOOM, 0.8, 45)),
          O("Rocket explosion", L("Weapon/RocketswarmLauncher/Explosion", 0.9, 45)),
          O("Mech band shockwave", L("Explosion/Mechband_Shockwave_Explosion_01a", 0.9, 45))))

ability("AG_GokuSpiritBomb",
        "Gather loop while channelling (no upper limit), a small cue each time a colonist lends energy, the throw, and the landing.",
        M("gather", "Gather (loop)", "channel, both hands up",
          O("Wind + reactor hum", L("Misc/Tornado", 1.3, 20, loop=True), L("Exotic/Ship_Reactor", 0.8, 30, loop=True)),
          O("Mech control loop, low", L("Pawn/Mechanitor/ControlTaking", 0.8, 40, loop=True)),
          O("Psycast casting loop (Royalty)", L("Misc/Psycasts/Psycast_Casting_Loop", 0.8, 40, loop=True))),
        M("lend", "Lend energy", "a colonist starts lending",
          O("Mech control grain", L("Pawn/Mechanitor/ControlTaking/Grains", 1.3, 35)),
          O("Psychic soothe pulse", L("Misc/Artifacts/Psychic_Soothe_Pulser", 1.4, 30)),
          O("Power on", L("Electricity/PowerOn/Small", 0.9, 35))),
        M("throw", "Throw", "Throw pressed: the bomb moves to the target",
          O("Drop pod fall", L("Misc/DropPodFall/Default", 0.8, 50)),
          O("Shuttle crash whoosh", L("Misc/Shuttle/Crash", 0.9, 45)),
          O("Mortar incoming, low", L("Weapon/Artillery/Mortar_Incoming_Alt2", 0.7, 50))),
        M("impact", "Impact", "the bomb lands",
          O("Giant explosion", L(BIG_BOOM, 0.9, 65), L("Misc/Emergence/Quake", 1.0, 40)),
          O("Planet-killer impact", L("Misc/PlanetkillerImpact", 1.0, 65)),
          O("Mortar dry, very low + rumble", L(BOOM, 0.55, 60), L("Ambience/Thunder/OnMap", 0.8, 40, 0.1))))

# Minato ------------------------------------------------------------------------------------------
FTG = [O("Skip entry, very fast", L(SKIP_IN, 2.0, 40), L(BLADE_SWISH, 1.2, 35, 0.05)),
       O("Swish + click", L("Misc/Swish2", 2.2, 50), L(CLICK, 1.0, 30)),
       O("Bionic slash miss (Royalty)", L("Impact/BionicSlash_Miss", 1.3, 45))]
ability("AG_ThunderGodJump",
        "The flash of the jump, then the cut (the melee hit already plays its own hit sound).",
        M("jump", "Jump", "Minato leaves and appears at the mark", *FTG))
ability("AG_ThunderGodChain",
        "Same jump sound as the single jump, once per jump (every 0.24 s), so it needs to be short.",
        M("jump", "Each jump", "every 0.24 s, up to 5", *FTG))
ability("AG_GuidingThunder",
        "Barrier up, and the redirect each time a shot is sent to the mark.",
        M("raise", "Barrier up", "cast: barrier for 6 s",
          O("Energy shield reset", L("Misc/EnergyShield/Reset", 1.2, 45)),
          O("Bullet shield reactivate", L("Misc/BulletShieldGenerator/Reactivate", 1.0, 45)),
          O("Broadshield startup (Royalty)", L("Buildings/Security", 1.0, 45))),
        M("redirect", "Shot sent away", "each shot that comes out at the mark",
          O("Deflect", L("Impact/Deflect", 1.3, 45)),
          O("Shield absorb + skip", L("Misc/EnergyShield/Absorb", 1.2, 35), L(SKIP_IN, 2.2, 25)),
          O("Swish, high", L("Misc/Swish2", 2.0, 45))))
ability("AG_Rasengan",
        "Anime: a whirring, rising spin, then a grinding impact. Forming is 0.6 s; the spin can loop if Minato has to walk to the target.",
        M("form", "Form (loop)", "0.6 s forming, loops while he closes in",
          O("Circular saw, high and light", L("Interact/Work/Construct/Circular_Saw/Saw_Circular_Cuts", 1.6, 25, loop=True), L(WARMUP, 1.6, 30)),
          O("Mech charger start + psychic hum", L("Buildings/Mechanoid/MechCharger/Start", 1.3, 40), L("Misc/Artifacts/Psychic_Animal_Pulser", 1.5, 30, loop=True)),
          O("Antigrav loop, fast (Odyssey)", L("Gravship/Gravship_Antigrav_Loop", 2.2, 40, loop=True))),
        M("hit", "Hit", "driven into the target, 3-tile throw",
          O("Thump cannon impact + shockwave", L("Impact/ThumpCannon", 1.1, 55), L(SHOCKWAVE, 1.1, 40)),
          O("Mortar dry, short and high", L(BOOM, 1.4, 45), L("Impact/BionicPunch_Hit", 0.9, 45)),
          O("Big animal hit + grind", L("Pawn/Animal/Melee_Big/Hit_Pawn", 0.8, 55), L("Interact/Work/Construct/Circular_Saw/Saw_Circular_Cuts", 1.2, 25))))

# Sasuke ------------------------------------------------------------------------------------------
ability("AG_SasukeAmenoyodomi",
        "A toggle; the sound that matters is a weapon stopping dead in the air.",
        M("toggle", "Toggle", "each click to the next state",
          O("Psychic effect, short", L("Misc/Psycasts/Psycast_Psychic_Effect", 1.4, 30)),
          O("Power on/off", L("Electricity/PowerOn/Small", 1.2, 35)),
          O("Click", L(CLICK, 0.8, 40))),
        M("hang", "Weapon stops in the air", "a thrown weapon stops over the cell",
          O("Shield absorb", L("Misc/EnergyShield/Absorb", 1.4, 40)),
          O("Metal sharp + hiss", L("Impact/MeleeHit_Metal_Sharp", 1.3, 40), L("Misc/Hiss", 1.5, 20)),
          O("Deflect, soft", L("Impact/Deflect", 0.9, 35))))
ability("AG_SasukeAmenotejikara",
        "Anime: an instant swap with a light 'ting'. Plays at both ends.",
        M("swap", "Swap", "both targets change places",
          O("Skip entry, very fast + ting", L(SKIP_IN, 2.2, 35), L("Impact/MeleeHit_Metal_Sharp", 1.8, 30)),
          O("Anchor clap style: puff", L("Impact/BeatFire", 1.4, 45), L(CLICK, 1.6, 30)),
          O("Swish + deflect", L("Misc/Swish2", 2.0, 40), L("Impact/Deflect", 1.6, 30))))
ability("AG_SasukeRaikoKusari",
        "Chidori through the net: an electric crackle when it links, and a crackle loop while it stands.",
        M("link", "Link", "the Chidori strings the weapons",
          O("Thunder lightning crack", L("Ambience/Thunder/lightning", 1.2, 45), L(ZAP, 1.0, 35)),
          O("EMP crackle, doubled", L(ZAP, 0.9, 50), L(ZAP, 1.3, 35, 0.08)),
          O("Charge rifle zaps", L("Weapon/ChargeRifle", 1.3, 40), L("Weapon/Beamgraser/Resolve", 1.2, 35))),
        M("loop", "Net (loop)", "while the net stands",
          O("Burning power cell sizzle", L("Buildings/BurningPowerCell/Loop", 1.3, 25, loop=True)),
          O("Beam graser fire, looped", L("Weapon/Beamgraser/Fire", 1.5, 20, loop=True)),
          O("Mech control loop, high", L("Pawn/Mechanitor/ControlTaking", 1.8, 25, loop=True))))
ability("AG_SasukeAmaterasu",
        "Black flame catching, then its own burn loop on the target.",
        M("ignite", "Ignite", "the flames light",
          O("Ignite + flamethrower start, low", L("Impact/Ignite", 0.8, 45), L("Weapon/Flamethrower", 0.6, 35)),
          O("Fire spew resolve, low", L("Pawn/Abilities/FireSpew/Resolve", 0.7, 45)),
          O("Incendiary mortar, dark", L("Weapon/Artillery/Mortar_Explode_Incendiary", 0.8, 40))),
        M("burn", "Burn (loop)", "while a pawn burns (20 s)",
          O("Fire burning, low", L("Misc/Fire", 0.7, 30, loop=True)),
          O("Burning corpse", L("Interact/Work/Cremate/Burning_Corpse", 0.8, 30, loop=True)),
          O("Flamethrower, very low", L("Weapon/Flamethrower", 0.5, 20, loop=True))))

# Pain --------------------------------------------------------------------------------------------
# One moment per sound marker in the Pain sketches (Tools/VfxLab/web/sketches/pain-*.js); its defName
# is the SoundDef the cast will play. The kit's sound so far is Shinra Tensei's: the Gravity Well hum's
# clip (PAIN_HUM) low for the charge, and the dry mortar far down for the push; options that reuse
# them keep the four abilities sounding like one hero.
PAIN_HUM = "Misc/Artifacts/Psychic_Animal_Pulser"
BIG_HIT = "Pawn/Animal/Melee_Big/Hit_Pawn"
STONE_HIT = "Impact/PunchHitBuilding/Stone"
STONE_BIG = "Impact/BuildingDestroyed/Stone/Big"
METAL_SHARP = "Impact/MeleeHit_Metal_Sharp"
ability("AG_ShinraTensei",
        "Already in game: AG_ShinraCharge and AG_ShinraRelease, set by hand before the lab. The first option of each is that sound, so picking it keeps it.",
        M("charge", "Charge", "the charged push starts its hold",
          O("In game now: animal pulser, very low", L(PAIN_HUM, 0.585, 18)),
          O("Animal pulser, low, louder", L(PAIN_HUM, 0.6, 30)),
          O("Animal pulser + psychic warmup", L(PAIN_HUM, 0.6, 25), L(WARMUP, 0.9, 20)),
          O("Psycast casting loop, low (Royalty)", L("Misc/Psycasts/Psycast_Casting_Loop", 0.7, 30)),
          defName="AG_ShinraCharge"),
        M("release", "Push", "the wave leaves him (tap and charged)",
          O("In game now: mortar dry, very low + animal pulser", L(BOOM, 0.415, 38), L(PAIN_HUM, 1.115, 26)),
          O("In game now + mech band shock (Biotech)", L(BOOM, 0.415, 35), L(PAIN_HUM, 1.115, 22), L(SHOCKWAVE, 0.7, 25)),
          O("Thump cannon + animal pulser", L("Impact/ThumpCannon", 0.8, 45), L(PAIN_HUM, 1.1, 25)),
          O("Psychic pulse psycast + mortar dry (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Pulse", 0.7, 40), L(BOOM, 0.45, 30)),
          defName="AG_ShinraRelease"))
ability("AG_PainBanshoTenin",
        "Warm-up 0.4 s (palm up, the core forms), the pull (0.15 s tug, then 0.34 s of flight back-first), and either the face-down slam or, when a pawn stands in the line, the two of them colliding.",
        M("cast", "Palm up", "warm-up 0.4 s: the black core forms at the fingertips",
          O("Animal pulser, low (Shinra's charge clip)", L(PAIN_HUM, 0.8, 30)),
          O("Psycast psychic effect, low (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Effect", 0.8, 35)),
          O("Soothe pulser, low", L("Misc/Artifacts/Psychic_Soothe_Pulser", 0.7, 30)),
          O("Swish, low + animal pulser", L("Misc/Swish2", 0.6, 30), L(PAIN_HUM, 1.0, 25)),
          defName="AG_PainBanshoCast"),
        M("pull", "Pull", "the pull takes hold: tug, lift, 0.34 s of flight to his hand",
          O("Animal pulser + swish, low", L(PAIN_HUM, 0.9, 30), L("Misc/Swish1", 0.7, 45, 0.15)),
          O("Longjump, low", L("Pawn/Abilities/Longjump/Jump", 0.8, 40)),
          O("Psychic pulse, low", L(PULSE, 0.7, 45)),
          O("Animal pulser (the Gravity Well hum's clip)", L(PAIN_HUM, 0.7, 45)),
          O("Psychic pulse psycast (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Pulse", 0.8, 45)),
          defName="AG_PainBanshoPull"),
        M("hit", "Blocked", "a standing pawn in the line: the two collide, 8 blunt each",
          O("Punch hit pawn, low", L("Impact/PunchHitPawn", 0.8, 50)),
          O("Big animal hit", L(BIG_HIT, 0.9, 50)),
          O("Big hit + punch", L(BIG_HIT, 1.0, 45), L("Impact/PunchHitPawn", 0.9, 35, 0.02)),
          O("Bionic punch, low", L("Impact/BionicPunch_Hit", 0.7, 45)),
          defName="AG_PainBanshoHit"),
        M("slam", "Slam", "caught by the head and pushed face-down: plates tip up, dust",
          O("Big animal hit + stone", L(BIG_HIT, 0.8, 55), L(STONE_HIT, 0.8, 40)),
          O("Thump cannon impact", L("Impact/ThumpCannon", 1.0, 55)),
          O("Rock collapse, short", L("Misc/RockCollapse", 1.2, 50)),
          O("Mortar dry, very low + big hit (Shinra's push body)", L(BOOM, 0.5, 35), L(BIG_HIT, 0.8, 45)),
          O("Stone punch + rock chunks", L(STONE_HIT, 0.7, 50), L("Interact/Haul/Drop/ChunkRock", 0.9, 35, 0.08)),
          O("Emergence end, small", L("Misc/Emergence/end_small", 1.0, 50)),
          O("Zeus hammer, low (Royalty)", L("Impact/ZeusHammer", 0.7, 45)),
          defName="AG_PainBanshoSlam"))
ability("AG_PainBlackReceiver",
        "Each rod: it grows out of the palm (0.3 s; 0.2 s when stabbing), is thrown at 30 cells/s and goes in; the third in one pawn pins it on its back. Each breaks after 8 s, and all at once when Pain goes down. A throw happens three times in 1.4 s, so these are short.",
        M("grow", "Rod grows", "a rod grows out of his palm",
          O("Mono sword handling, short (Royalty)", L("UI/WeaponHandling/HandleMonoSword", 1.4, 30)),
          O("Animal pulser, high and quiet", L(PAIN_HUM, 1.6, 20)),
          O("Piercing spine, low (Biotech)", L("Pawn/Abilities/PiercingSpine", 0.7, 25)),
          O("Weapon handling, small, low", L("UI/WeaponHandling/HandleWeapon_SmallA", 0.7, 35)),
          defName="AG_PainReceiverGrow"),
        M("throw", "Throw", "the rod leaves his hand",
          O("Piercing spine launch", L("Pawn/Abilities/PiercingSpine", 1.0, 45)),
          O("Bow shot", L("Weapon/BowA", 0.9, 50)),
          O("Swish + spiner", L("Misc/Swish1", 1.4, 40), L("Weapon/Spiner", 0.8, 30)),
          O("Bow, short and low", L("Weapon/BowB", 0.8, 45)),
          O("Spiner", L("Weapon/Spiner", 1.0, 45)),
          defName="AG_PainReceiverThrow"),
        M("hit", "Rod in", "the rod enters a pawn (first and second rod)",
          O("Bullet flesh + metal sharp", L("Impact/Bullet_Flesh", 0.9, 45), L(METAL_SHARP, 0.9, 35)),
          O("Scyther hit", L(BLADE_HIT, 0.8, 45)),
          O("Bio bite (wet)", L("Pawn/Abilities/Bloodfeed", 1.2, 35)),
          O("Metal sharp, low", L(METAL_SHARP, 0.8, 45)),
          O("Bullet flesh + animal pulser blip", L("Impact/Bullet_Flesh", 0.9, 45), L(PAIN_HUM, 1.8, 15)),
          defName="AG_PainReceiverHit"),
        M("pin", "Pinned", "the third rod: the pawn falls on its back, the rods go into the floor",
          O("Metal sharp + body on stone", L(METAL_SHARP, 0.8, 45), L(STONE_HIT, 0.8, 40, 0.15)),
          O("Scyther hit + big hit", L(BLADE_HIT, 0.8, 40), L(BIG_HIT, 0.8, 40, 0.15)),
          O("Metal sharp + stone + animal pulser, low", L(METAL_SHARP, 0.8, 40), L("Impact/MeleeHit_Stone", 0.7, 40, 0.15), L(PAIN_HUM, 0.6, 20)),
          O("Metal bullet + stone block drop", L("Impact/Bullet_Metal", 0.8, 35), L("Interact/Work/Construct/Stone/StoneBlock_Drop", 0.8, 45, 0.15)),
          defName="AG_PainReceiverPin"),
        M("break", "Rod breaks", "the rod shrinks from the knob down in 0.35 s, shedding dark flakes",
          O("Shield broken, low and quiet", L("Misc/EnergyShield/Broken", 0.6, 25)),
          O("Glass shattering, low and quiet", L("Buildings/GestatorGlassShattering", 0.6, 25)),
          O("Light stone chunk, high", L("Interact/Work/Construct/Stone/Stone_Chunk_Light", 1.4, 25)),
          O("Hiss", L("Misc/Hiss", 0.8, 25)),
          O("Animal pulser falling + hiss", L(PAIN_HUM, 0.5, 20), L("Misc/Hiss", 1.2, 15)),
          defName="AG_PainReceiverBreak"))
ability("AG_PainChibakuTensei",
        "Warm-up 0.8 s (the core forms between his hands, thrown up), the core climbing and coming down over the cell, 3 s of ground torn up and pulled in, the ball formed (holds 12 s), 0.4 s of cracking, the burst.",
        M("cast", "Core forms", "warm-up 0.8 s between cupped hands, thrown up in the last 0.18 s",
          O("Psychic warmup, fast", L(WARMUP, 1.4, 40)),
          O("Psycast psychic effect (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Effect", 0.9, 40)),
          O("Animal pulser, low", L(PAIN_HUM, 0.7, 35)),
          O("Fire spew warmup, low (the glow)", L("Pawn/Abilities/FireSpew/Warmup", 0.8, 35)),
          O("Mech resurrect warmup (Biotech)", L("Pawn/Abilities/MechResurrect/Warmup", 1.0, 35)),
          defName="AG_PainChibakuCast"),
        M("launch", "Core flies", "the core climbs from his hand to 5 cells over the cell",
          O("Drop pod leaving", L("Misc/DropPodLeaving", 0.8, 50)),
          O("Mech launcher launch, low (Biotech)", L("Pawn/Abilities/LongJumpMechLauncher/Launch", 0.8, 40)),
          O("Swish, low + animal pulser", L("Misc/Swish1", 0.6, 45), L(PAIN_HUM, 1.2, 25)),
          O("Mortar fire, low", L("Weapon/Artillery/Mortar_Fire", 0.6, 35)),
          defName="AG_PainChibakuLaunch"),
        M("pull", "Tear (3 s)", "3 s of ground torn up in plates and pulled into the core",
          O("Emergence quake + tornado", L("Misc/Emergence/Quake", 0.8, 55, loop=True), L("Misc/Tornado", 0.8, 25, loop=True)),
          O("Tunnel rumble", L("Misc/Tunnel", 0.7, 50, loop=True)),
          O("Stone buildings breaking", L(STONE_BIG, 0.7, 45), L("Impact/BuildingDestroyed/Stone/Medium", 0.8, 40, 0.8), L(STONE_BIG, 0.6, 45, 1.7)),
          O("Quake + stone breaking + animal pulser", L("Misc/Emergence/Quake", 0.8, 50), L("Impact/BuildingDestroyed/Stone/Medium", 0.8, 35, 0.6), L(STONE_BIG, 0.7, 35, 1.6), L(PAIN_HUM, 0.6, 25)),
          O("Neuroquake, low (Royalty)", L("Misc/Psycasts/Neuroquake", 0.8, 45)),
          defName="AG_PainChibakuPull"),
        M("formed", "Ball formed", "the last plates arrive and the ball closes; a big shake",
          O("Emergence end, large", L("Misc/Emergence/end_large", 0.9, 55)),
          O("Rock collapse + mortar, low", L("Misc/RockCollapse", 0.6, 55), L(BOOM, 0.5, 45)),
          O("Bridge collapse", L("Misc/BridgeCollapse", 0.7, 55)),
          O("Big stone building down, low + mortar", L(STONE_BIG, 0.7, 45), L(BOOM, 0.5, 35)),
          O("Thump cannon impact, low", L("Impact/ThumpCannon", 0.8, 50)),
          defName="AG_PainChibakuFormed"),
        M("crack", "Cracking", "0.4 s: cracks widen and fill with light, chips spat out",
          O("Light stone chunks, low + glass", L("Interact/Work/Construct/Stone/Stone_Chunk_Light", 0.8, 40), L("Buildings/GestatorGlassShattering", 0.6, 20)),
          O("Stone hammer, fast", L("Interact/Work/Construct/Stone/Hammer_Stone", 1.2, 40)),
          O("Small stone building down", L("Impact/BuildingDestroyed/Stone/Small", 0.9, 40)),
          O("Rock collapse, high", L("Misc/RockCollapse", 1.4, 40)),
          O("Shield broken, low + stone", L("Misc/EnergyShield/Broken", 0.5, 25), L("Impact/BuildingDestroyed/Stone/Small", 1.0, 35)),
          defName="AG_PainChibakuCrack"),
        M("burst", "Burst", "a flash, the ball bursts into chunks that fall into the crater",
          O("Giant explosion, low", L(BIG_BOOM, 0.8, 55)),
          O("Mortar dry, low + stone", L(BOOM, 0.6, 55), L(STONE_BIG, 0.8, 40, 0.1)),
          O("Rocket explosion + rock collapse", L("Weapon/RocketswarmLauncher/Explosion", 0.8, 50), L("Misc/RockCollapse", 0.8, 40, 0.05)),
          O("Mech band shockwave + stone (Biotech)", L("Explosion/Mechband_Shockwave_Explosion_01a", 0.8, 45), L(STONE_BIG, 0.8, 40)),
          O("Shinra's push body, lower + stone", L(BOOM, 0.38, 50), L(PAIN_HUM, 1.1, 30), L(STONE_BIG, 0.8, 40, 0.05)),
          O("Drop pod impact, low + stone", L("Misc/DropPodImpact/Default", 0.7, 45), L(STONE_BIG, 0.8, 35, 0.05)),
          defName="AG_PainChibakuBurst"))

# Nakime ------------------------------------------------------------------------------------------
# One moment per sound marker in the Infinity Castle sketches (infinity-castle-*.js). Every command
# and the castle's opening and Release are one strum of the biwa, so AG_NakimeBiwaStrum is heard most.
# The game has no plucked string (Royalty's harp is a 14 s song): the biwa is make_sounds.py's BiwaStrum
# and BiwaNote. The castle is wood and paper: shoji doors, lacquer bars, wooden walls.
WOOD_HIT = "Impact/PunchHitBuilding/Wood"
DOOR_OPEN = "Misc/Door/ManualDoors/Open/Fast"
DOOR_SHUT = "Misc/Door/ManualDoors/Close/Fast"
ability("AG_Nakime_InfinityCastle",
        "Strum (opening the castle, every command, Release), a floor door opening under a pawn, Seal's bar, Shift's slide and the thud where rooms meet, Crush's four walls, the sunburn tick, and the biwa coming back.",
        M("strum", "Biwa strum", "every command, the opening and Release: one stroke across the strings",
          O("Synth: biwa strum", L("AG/BiwaStrum", 1.0, 50)),
          O("Synth: biwa strum, lower", L("AG/BiwaStrum", 0.85, 50)),
          O("Synth: two strokes (the anime's 'be-been')", L("AG/BiwaStrum", 1.0, 40), L("AG/BiwaStrum", 1.0, 45, 0.12)),
          O("Synth strum + soothe pulser (the castle answering)", L("AG/BiwaStrum", 1.0, 45), L("Misc/Artifacts/Psychic_Soothe_Pulser", 0.8, 20)),
          O("Synth strum + skip pulse (Royalty)", L("AG/BiwaStrum", 1.0, 45), L("Misc/Psycasts/Skip/Pulse", 1.2, 20)),
          O("No biwa: synth ting, low + wood knock", L("AG/Ting", 0.5, 35), L("Impact/MeleeHit_Wood", 1.2, 35)),
          defName="AG_NakimeBiwaStrum"),
        M("door", "Floor door", "a door snaps open in the floor (0.18 s); a pawn sinks in 0.4 s or rises",
          O("Door open, fast + low swish", L(DOOR_OPEN, 0.8, 45), L("Misc/Swish2", 0.6, 30, 0.1)),
          O("Door open, then shut", L(DOOR_OPEN, 0.9, 45), L(DOOR_SHUT, 0.8, 35, 0.5)),
          O("Fence gate", L("Misc/Door/FenceGates", 0.9, 45)),
          O("Wood knock + skip entry", L("Impact/MeleeHit_Wood", 1.0, 40), L(SKIP_IN, 0.9, 30, 0.05)),
          O("Dropped wood + swish down", L("Interact/Haul/Drop/Wood", 1.2, 35), L("Misc/Swish1", 0.7, 30, 0.08)),
          defName="AG_NakimeCastleDoor"),
        M("bar", "Seal bar", "Seal: the lacquer bar slides across the shut doorway and lands",
          O("Door shut, fast", L(DOOR_SHUT, 0.8, 45)),
          O("Dropped wood", L("Interact/Haul/Drop/Wood", 1.0, 45)),
          O("Wood hit + click", L("Impact/MeleeHit_Wood", 1.2, 40), L(CLICK, 0.7, 35, 0.02)),
          O("Wood construction finished", L("Interact/Work/Construct/Wood/Finish_Wood", 1.0, 40)),
          O("Stone block drop, high (a heavy bolt)", L("Interact/Work/Construct/Stone/StoneBlock_Drop", 1.3, 40)),
          defName="AG_NakimeCastleBar"),
        M("slide", "Room slides", "Shift: the room slides, speeding up, 0.85 s to the next room (longer into open void)",
          O("Quake, short + wood rummage", L("Misc/Emergence/Quake", 1.2, 35), L("Interact/Work/Construct/Wood/Rummage_Wood", 0.6, 30)),
          O("Wall raise (Royalty)", L("Misc/Psycasts/Wall_Raise", 1.0, 45)),
          O("Low swish + wood rummage", L("Misc/Swish1", 0.5, 40), L("Interact/Work/Construct/Wood/Rummage_Wood", 0.5, 30)),
          O("Drop pod leaving, low", L("Misc/DropPodLeaving", 0.6, 35)),
          defName="AG_NakimeCastleSlide"),
        M("thud", "Rooms meet", "Shift: the room's wall meets the other room's; dust, a small shake",
          O("Wood punch, low", L(WOOD_HIT, 0.6, 50)),
          O("Big hit on a building, low", L("Pawn/Animal/Melee_Big/Hit_Building", 0.7, 50)),
          O("Wood punch + quiet thump cannon", L(WOOD_HIT, 0.6, 45), L("Impact/ThumpCannon", 1.0, 25)),
          O("Mortar dry, very low + wood hit", L(BOOM, 0.5, 30), L("Impact/MeleeHit_Wood", 0.7, 45)),
          defName="AG_NakimeCastleThud"),
        M("crush", "Crush", "four walls slam 2 cells in (0.12 s); 15 blunt to everyone under them",
          O("Four wood slams", *[L(WOOD_HIT, 0.6 + 0.05 * i, 40, 0.015 * i) for i in range(4)]),
          O("Medium wood building down + thump", L("Impact/BuildingDestroyed/Wood/Medium", 0.8, 45), L("Impact/ThumpCannon", 1.0, 40)),
          O("Two big building hits + splinters", L("Pawn/Animal/Melee_Big/Hit_Building", 0.7, 50), L("Pawn/Animal/Melee_Big/Hit_Building", 0.8, 40, 0.03), L("Impact/BuildingDestroyed/Wood/Small", 1.0, 35, 0.04)),
          O("Mortar dry, low + wood + punch", L(BOOM, 0.6, 35), L("Impact/BuildingDestroyed/Wood/Medium", 0.9, 40), L("Impact/PunchHitPawn", 0.8, 35)),
          defName="AG_NakimeCastleCrush"),
        M("sunburn", "Sunburn", "once a second in daylight: an orange flash, embers, ash",
          O("Hiss", L("Misc/Hiss", 1.0, 30)),
          O("Inferno cannon fire, low and quiet", L("Weapon/InfernoCannon_Fire", 0.7, 25)),
          O("Beaten fire + hiss", L("Impact/BeatFire", 0.8, 35), L("Misc/Hiss", 1.2, 20)),
          defName="AG_NakimeSunBurn"),
        M("note", "Biwa returns", "she is up again: the biwa appears in her hands with one soft note",
          O("Synth: one biwa note", L("AG/BiwaNote", 1.0, 40)),
          O("Synth: one note, lower", L("AG/BiwaNote", 0.8, 40)),
          O("Synth note + shield reset shimmer", L("AG/BiwaNote", 1.0, 40), L("Misc/EnergyShield/Reset", 1.2, 15)),
          O("Synth: soft strum", L("AG/BiwaStrum", 1.2, 25)),
          defName="AG_NakimeBiwaNote"))

# Gojo --------------------------------------------------------------------------------------------
# One moment per sound marker in the Gojo sketches (gojo-blue-v2.js, gojo-red-v2.js, gojo-purple.js,
# gojo-unlimited-void-open.js and -inside.js); its defName is the SoundDef the cast will play. Blue runs
# on Gravity Well's code, so it plays Gravity Well's hum and implosion today: the first option of its
# pull and implosion is that sound. Red, Purple and the Void play nothing yet. Hollow Purple is a Red cast,
# so its sketch plays Red's charge and fire before its own moments. Blue sucks in (low, dark), Red pushes
# out (bright, sharp), Purple is both and erases; the Void is vast and quiet.
VOID_HUM = "Misc/Artifacts/Psychic_Soothe_Pulser"
ability("AG_GojoBlue",
        "In game now: Gravity Well's hum (AG_GravityHum, a sustainer, pitch rising as it eats) while it pulls and its implosion (AG_GravityImplode). The first option of the pull and the implosion is that sound, so picking it keeps it.",
        M("open", "Blue opens", "arm up and point (0.15 s); a star glint at the cell, the ball grows to the 1-cell core in 0.3 s, two arcs sweep round Gojo",
          O("Psychic warmup, fast + low swish", L(WARMUP, 1.5, 35), L("Misc/Swish2", 0.7, 30)),
          O("Animal pulser, quick and high (the hum's clip)", L(PAIN_HUM, 1.3, 30)),
          O("Skip entry, low", L(SKIP_IN, 0.7, 40)),
          O("Shield reset, low", L("Misc/EnergyShield/Reset", 0.7, 35)),
          O("Psycast psychic effect (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Effect", 1.1, 35)),
          O("Charge shot, low + soothe pulser", L("Weapon/ChargeShotA", 0.7, 35), L(VOID_HUM, 1.2, 20)),
          defName="AG_GojoBlueOpen"),
        M("pull", "Pull (3 s)", "3 s: a storm of blue fog turns over 4 cells, slabs and planks torn from the floor orbit the ball; small shakes at 1 and 2 s",
          O("In game now: Gravity Well hum (animal pulser, very low)", L(PAIN_HUM, 0.35, 12, loop=True)),
          O("Gravity Well hum, louder + tornado", L(PAIN_HUM, 0.45, 25, loop=True), L("Misc/Tornado", 0.9, 25, loop=True)),
          O("Tornado + wood and stone torn up at 1 s and 2 s", L("Misc/Tornado", 0.8, 35, loop=True), L("Impact/BuildingDestroyed/Wood/Small", 0.8, 30, 1.0), L("Impact/BuildingDestroyed/Stone/Small", 0.8, 30, 2.0)),
          O("Antigrav loop, low (Odyssey)", L("Gravship/Gravship_Antigrav_Loop", 0.7, 40, loop=True)),
          O("Wind + animal pulser, low", L("Ambience/Wind", 1.3, 40, loop=True), L(PAIN_HUM, 0.5, 20, loop=True)),
          O("Mech control loop, low (Biotech)", L("Pawn/Mechanitor/ControlTaking/Mech_Control_Taking_Loop_01a", 0.6, 35, loop=True)),
          O("Emergence quake + psychic pulse, low", L("Misc/Emergence/Quake", 0.7, 40, loop=True), L(PULSE, 0.6, 30, loop=True)),
          defName="AG_GojoBluePull"),
        M("implode", "Implosion", "everything rushes in (0.12 s), a white flash, 10-25 blunt within 2 cells, debris thrown out, a grey dust cloud",
          O("In game now: Gravity Well implosion (mortar dry, very low)", L(BOOM, 0.475, 28)),
          O("Mortar dry, low + big stone down", L(BOOM, 0.55, 45), L(STONE_BIG, 0.8, 35, 0.05)),
          O("Thump cannon impact + psychic pulse, low", L("Impact/ThumpCannon", 0.8, 50), L(PULSE, 0.5, 25)),
          O("Mech band shockwave, low (Biotech)", L("Explosion/Mechband_Shockwave_Explosion_01a", 0.7, 45)),
          O("Hellsphere vaporize, low", L("Explosion/Vaporize", 0.6, 40)),
          O("Drop pod impact + rock collapse", L("Misc/DropPodImpact/Default", 0.8, 45), L("Misc/RockCollapse", 0.8, 35, 0.05)),
          defName="AG_GojoBlueImplode"))
ability("AG_GojoRed",
        "Charge 0.5 s at the fingertip (the warm-up's last 0.5 s), the fire, the burst where it meets something (8 cells away it arrives 0.4 s after the fire), and the thrown pawn slamming into a wall or landing in the open.",
        M("charge", "Charge", "0.5 s: a flat red swirl spins at the fingertip, pink ribbon arcs sweep round Gojo, the orb grows white-hot",
          O("Fire spew warmup (the glow)", L("Pawn/Abilities/FireSpew/Warmup", 1.2, 35)),
          O("Psychic warmup, fast", L(WARMUP, 1.8, 35)),
          O("Charge rifle, slowed (a whine)", L("Weapon/ChargeRifle", 0.6, 35)),
          O("Jump pack prelaunch (Royalty)", L("Misc/JumpPack/JumpPack_PreLaunch", 1.0, 35)),
          O("Mech launcher prelaunch (Biotech)", L("Pawn/Abilities/LongJumpMechLauncher/PreLaunch", 1.0, 35)),
          O("Fire burst warmup, fast (Biotech)", L("Pawn/Abilities/FireBurst_Warmup", 1.6, 35)),
          defName="AG_GojoRedCharge"),
        M("fire", "Fire", "a pink-white flash at the finger, a red ring; Red flies at 20 cells/s",
          O("Charge rifle", L("Weapon/ChargeRifle", 1.0, 45)),
          O("Charge shot + swish", L("Weapon/ChargeShotA", 1.1, 45), L("Misc/Swish2", 1.3, 35)),
          O("Thump cannon fire", L("Weapon/ThumpCannon_Fire", 1.1, 45)),
          O("Orbital targeter fire", L("Weapon/OrbitalTargeter/OrbitalTargeter_Fire", 1.2, 40)),
          O("Beam graser resolve (Biotech)", L("Weapon/Beamgraser/Resolve", 1.0, 40)),
          O("Inferno cannon fire + swish", L("Weapon/InfernoCannon_Fire", 1.1, 40), L("Misc/Swish1", 1.4, 35)),
          O("Hellsphere cannon shot (Biotech)", L("Weapon/HellsphereCannon/Hellsphere_Cannon_Shot", 1.0, 45)),
          defName="AG_GojoRedFire"),
        M("burst", "Burst", "it meets a pawn, a thing or the cell: red wash, white flash, a shock front down its line; the others within 1.5 cells pushed 2 cells",
          O("Thump cannon impact", L("Impact/ThumpCannon", 1.0, 55)),
          O("Bionic punch + mortar dry, high", L("Impact/BionicPunch_Hit", 0.8, 45), L(BOOM, 0.9, 35)),
          O("Mech band shock (Biotech)", L(SHOCKWAVE, 0.8, 45)),
          O("Zeus hammer (Royalty)", L("Impact/ZeusHammer", 0.9, 50)),
          O("Rocket explosion, short", L("Weapon/RocketswarmLauncher/Explosion", 1.1, 45)),
          O("Big hit + punch", L(BIG_HIT, 0.9, 50), L("Impact/PunchHitPawn", 0.8, 40)),
          defName="AG_GojoRedBurst"),
        M("slam", "Slams into a wall", "the thrown pawn hits a wall 0.5 cells up: +10 blunt, cracks, a big dust cloud, chunks",
          O("Big hit on a building", L("Pawn/Animal/Melee_Big/Hit_Building", 0.8, 55)),
          O("Stone punch + rock chunks", L(STONE_HIT, 0.7, 50), L("Interact/Haul/Drop/ChunkRock", 0.9, 35, 0.06)),
          O("Big hit + small stone building down", L(BIG_HIT, 0.8, 50), L("Impact/BuildingDestroyed/Stone/Small", 0.9, 40, 0.02)),
          O("Rock collapse, short", L("Misc/RockCollapse", 1.1, 50)),
          O("Drop pod impact, high", L("Misc/DropPodImpact/Default", 1.2, 45)),
          O("Emergence end, small", L("Misc/Emergence/end_small", 1.1, 50)),
          defName="AG_GojoRedSlam"),
        M("land", "Lands in the open", "no wall: the thrown pawn touches down, bounces and skids to 6 cells",
          O("Punch hit pawn + ground", L("Impact/PunchHitPawn", 0.8, 45), L("Impact/Bullet_Ground", 0.7, 35, 0.05)),
          O("Mech launcher land (Biotech)", L("Pawn/Abilities/LongJumpMechLauncher/Land", 0.8, 45)),
          O("Melee dodge + ground (the skid)", L("Impact/MeleeDodge", 0.7, 40), L("Impact/Bullet_Ground", 0.8, 30, 0.1)),
          O("Jump pack land (Royalty)", L("Misc/JumpPack/JumpPack_Land", 0.9, 40)),
          O("Dropped load + swish", L("Interact/Haul/Drop/Standard", 0.7, 45), L("Misc/Swish1", 0.6, 30, 0.08)),
          defName="AG_GojoRedLand"))
ability("AG_GojoHollowPurple",
        "After Red's charge and fire: Red meets Blue and they merge (0.35 s), the ignition, the growth (0.3 s), the travel (a sustainer the code ends when it fades: 2.3 s for the sketch's 14 cells, 5 s for 30), a touch for each pawn it meets, and the fade.",
        M("merge", "Red meets Blue", "0.35 s: the red orb and the blue sphere spiral round each other and in; the world splits red and blue, then darkens",
          O("Skip pulse (Royalty)", L("Misc/Psycasts/Skip/Pulse", 0.9, 40)),
          O("Animal pulser + charge rifle whine", L(PAIN_HUM, 0.9, 35), L("Weapon/ChargeRifle", 0.7, 30)),
          O("Psychic shock lance", L("Misc/Artifacts/Psychic_Shock_Lance", 0.9, 40)),
          O("Psycast psychic effect, low (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Effect", 0.7, 40)),
          O("Two swishes crossing + shield absorb", L("Misc/Swish1", 0.8, 40), L("Misc/Swish2", 1.0, 35, 0.1), L("Misc/EnergyShield/Absorb", 0.7, 35, 0.2)),
          O("Mech resurrect warmup, fast (Biotech)", L("Pawn/Abilities/MechResurrect/Warmup", 1.4, 35)),
          defName="AG_GojoPurpleMerge"),
        M("ignite", "Ignition", "a white point and flash, a purple wash 18 cells across, 4 ripple rings, rays; the biggest shake; Purple grows to 1.5 cells",
          O("Giant explosion, low", L(BIG_BOOM, 0.7, 55)),
          O("Mech band shockwave + mortar dry, low (Biotech)", L("Explosion/Mechband_Shockwave_Explosion_01a", 0.7, 50), L(BOOM, 0.5, 40)),
          O("Psychic pulse psycast + thump (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Pulse", 0.6, 45), L("Impact/ThumpCannon", 0.7, 45)),
          O("Hellsphere vaporize", L("Explosion/Vaporize", 0.8, 50)),
          O("Planet-killer impact, low", L("Misc/PlanetkillerImpact", 0.8, 50)),
          O("Neuroquake, high (Royalty)", L("Misc/Psycasts/Neuroquake", 1.1, 45)),
          defName="AG_GojoPurpleIgnite"),
        M("travel", "Travel", "6 cells/s erasing a 3-cell lane: lightning crackles off it, a ripple ring and a small shake every 0.5 s",
          O("Orbital beam, low", L("Misc/OrbitalBeam/OrbitalBeam", 0.8, 50, loop=True)),
          O("Tornado, low + shock lance crackle", L("Misc/Tornado", 0.6, 45, loop=True), L("Misc/Artifacts/Psychic_Shock_Lance", 0.7, 25, loop=True)),
          O("Flamethrower roar, low", L("Weapon/Flamethrower/Flamthrower_Firing_Loop_A", 0.6, 45, loop=True)),
          O("Charge lance fire, low", L("Weapon/ChargeLance/Fire", 0.6, 45, loop=True)),
          O("Antigrav + engine loop (Odyssey)", L("Gravship/Gravship_Antigrav_Loop", 0.8, 40, loop=True), L("Gravship/Gravship_Engine_Loop", 0.8, 30, loop=True)),
          O("Burning power cell + tornado", L("Buildings/BurningPowerCell/Loop", 0.7, 30, loop=True), L("Misc/Tornado", 0.7, 35, loop=True)),
          O("Hellsphere cannon warmup, low (Biotech)", L("Weapon/HellsphereCannon/Hellsphere_Cannon_Warmup", 0.7, 45, loop=True)),
          O("Tunnel rumble + shock lance", L("Misc/Tunnel/TunnelLoop", 0.6, 40, loop=True), L("Misc/Artifacts/Psychic_Shock_Lance", 0.7, 25)),
          defName="AG_GojoPurpleTravel"),
        M("touch", "Touches a pawn", "a white flash; on the centre row the pawn dissolves into specks drawn into it, on a side row it takes 60 and falls",
          O("Hiss + shield absorb", L("Misc/Hiss", 0.7, 35), L("Misc/EnergyShield/Absorb", 0.8, 30)),
          O("Psychic entropy (Royalty)", L("Misc/Psycasts/Psychic_Entropy", 0.8, 40)),
          O("EMP crackle, low", L(ZAP, 0.6, 40)),
          O("Death acidifier", L("Misc/DeathAcidifier", 0.8, 35)),
          O("Glass, low + hiss", L("Buildings/GestatorGlassShattering", 0.6, 30), L("Misc/Hiss", 1.0, 25)),
          O("Shield broken, low", L("Misc/EnergyShield/Broken", 0.6, 35)),
          defName="AG_GojoPurpleTouch"),
        M("fade", "Fades", "it breaks into specks and fades in 0.5 s, a last ring; the dim lifts",
          O("Shield broken, very low", L("Misc/EnergyShield/Broken", 0.5, 35)),
          O("Soothe pulser, low", L(VOID_HUM, 0.7, 35)),
          O("Power off", L("Electricity/PowerOff/Small", 0.6, 40)),
          O("Hellsphere vaporize, quiet", L("Explosion/Vaporize", 1.0, 30)),
          O("Skip exit (Royalty)", L(SKIP_OUT, 0.8, 40)),
          defName="AG_GojoPurpleFade"))
ability("AG_GojoUnlimitedVoid",
        "Home map: the hand sign (0.6 s), the barrier closing (0.3 s) and shrinking into a ball (0.5 s), the ball breaking at the end. Pocket map: the rush in, the black hole opening at 1.35 s, the void's drone for the 10 s (a sustainer the end stops), a touch for each pawn Gojo spares, the collapse (0.7 s). The game plays a sound only on the map the camera is on.",
        M("sign", "Hand sign", "0.6 s: a hand rises, fingers cross, the blindfold comes down, the eyes light blue, light gathers at the hand",
          O("Psychic warmup", L(WARMUP, 1.0, 35)),
          O("Bestow warmup", L(BESTOW, 1.2, 35)),
          O("Word of warmup (Royalty)", L("Misc/Psycasts/WordOf/Warmup", 1.0, 35)),
          O("Soothe pulser warmup", L("Item/Psychic_Soothe_Pulser_Warmup_A", 1.1, 35)),
          O("Click + shield reset", L(CLICK, 0.8, 30), L("Misc/EnergyShield/Reset", 1.2, 25, 0.1)),
          O("Mech resurrect warmup (Biotech)", L("Pawn/Abilities/MechResurrect/Warmup", 1.0, 30)),
          defName="AG_GojoVoidSign"),
        M("open", "Barrier closes", "a dark sphere spreads over 9 cells in 0.3 s, everyone under it is taken, then it shrinks into a black ball over Gojo in 0.5 s",
          O("Psychic pulse psycast + mortar, very low (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Pulse", 0.6, 45), L(BOOM, 0.4, 35)),
          O("Neuroquake, low (Royalty)", L("Misc/Psycasts/Neuroquake", 0.7, 45)),
          O("Skip entry, low + thump", L(SKIP_IN, 0.5, 45), L("Impact/ThumpCannon", 0.6, 40)),
          O("Psychic pulse, low + quake", L(PULSE, 0.5, 45), L("Misc/Emergence/Quake", 1.0, 30)),
          O("Mech band shockwave, very low (Biotech)", L("Explosion/Mechband_Shockwave_Explosion_01a", 0.5, 45)),
          O("Insanity lance, low", L("Misc/Artifacts/Psychic_Insanity_Lance", 0.6, 40)),
          defName="AG_GojoVoidOpen"),
        M("break", "Ball breaks", "home map, at the end: cracks of light, a soft white flash, a ring out to 9 cells; everyone back 0.2 s later",
          O("Glass shattering, low", L("Buildings/GestatorGlassShattering", 0.6, 40)),
          O("Shield broken, low", L("Misc/EnergyShield/Broken", 0.6, 40)),
          O("Skip exit (Royalty)", L(SKIP_OUT, 1.0, 45)),
          O("Soothe pulser + glass", L(VOID_HUM, 0.9, 30), L("Buildings/GestatorGlassShattering", 0.8, 30)),
          O("Skip entry + shield broken", L(SKIP_IN, 0.8, 40), L("Misc/EnergyShield/Broken", 0.7, 30)),
          defName="AG_GojoVoidBreak"),
        M("enter", "Rush in", "pocket map: white, then speed lines rush out of the point for 1.6 s and the stars fly in",
          O("Drop pod fall, fast", L("Misc/DropPodFall/Default", 1.3, 40)),
          O("Mortar incoming, low + skip entry", L("Weapon/Artillery/Mortar_Incoming_Alt2", 0.6, 45), L(SKIP_IN, 0.9, 35)),
          O("Jump pack launch (Royalty)", L("Misc/JumpPack/JumpPack_Launch", 0.8, 45)),
          O("Mech launcher launch + psychic pulse (Biotech)", L("Pawn/Abilities/LongJumpMechLauncher/Launch", 0.7, 40), L(PULSE, 0.8, 30)),
          O("Shuttle crash whoosh", L("Misc/Shuttle/Crash", 1.0, 40)),
          O("Hellsphere vaporize, high (the white)", L("Explosion/Vaporize", 1.3, 40)),
          defName="AG_GojoVoidEnter"),
        M("hole", "Black hole opens", "a white light at the point, then the black hole opens over 0.7 s, its ring of gas turning",
          O("Soothe pulser, very low", L(VOID_HUM, 0.5, 45)),
          O("Mortar dry, very low (a far boom)", L(BOOM, 0.35, 45)),
          O("Planet-killer impact, very low", L("Misc/PlanetkillerImpact", 0.5, 45)),
          O("Psychic pulse psycast, very low (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Pulse", 0.45, 45)),
          O("Thump cannon, very low + soothe pulser", L("Impact/ThumpCannon", 0.5, 45), L(VOID_HUM, 0.6, 30)),
          defName="AG_GojoVoidHole"),
        M("drone", "The void (10 s)", "the whole domain: navy space, stars, the black hole turning; everyone frozen",
          O("Space ambience", L("Ambience/Amb_Space", 1.0, 50, loop=True)),
          O("Ship reactor, low", L("Exotic/Ship_Reactor", 0.7, 35, loop=True)),
          O("Crashed ship drone", L("Exotic/Crashed_Ship_Part/Drone", 0.8, 40, loop=True)),
          O("Sun blocker hum (Royalty)", L("Buildings/Exotic/SunBlocker_Ambience_01a", 1.0, 40, loop=True)),
          O("Band node, tuned (Biotech)", L("Buildings/Mechanoid/BandNode/Tuned/BandNode_Sustainer_Loop_01a", 0.8, 40, loop=True)),
          O("Meditation (Royalty)", L("Misc/Psycasts/Meditation", 0.8, 35, loop=True)),
          O("Space + shock lance shimmer (the information)", L("Ambience/Amb_Space", 1.0, 45, loop=True), L("Misc/Artifacts/Psychic_Shock_Lance", 0.6, 15, loop=True)),
          defName="AG_GojoVoidDrone"),
        M("touch", "Gojo spares a pawn", "0.3 s next to a frozen pawn: a blue ring opens, the specks stop, its colour comes back",
          O("Shield reset", L("Misc/EnergyShield/Reset", 1.2, 35)),
          O("Soothe pulser, high", L(VOID_HUM, 1.4, 30)),
          O("Power on, small", L("Electricity/PowerOn/Small", 0.8, 35)),
          O("Bullet shield reactivate", L("Misc/BulletShieldGenerator/Reactivate", 1.0, 35)),
          O("Band node tuned (Biotech)", L("Buildings/Mechanoid/BandNode/Tuned/BandNode_Tune_Complete_01a", 1.0, 35)),
          defName="AG_GojoVoidTouch"),
        M("collapse", "Collapse", "pocket map, at the end: the void collapses in 0.7 s, white, everyone goes home",
          O("Skip entry, low", L(SKIP_IN, 0.7, 45)),
          O("Drop pod leaving, low", L("Misc/DropPodLeaving", 0.7, 45)),
          O("Psycast psychic effect, low (Royalty)", L("Misc/Psycasts/Psycast_Psychic_Effect", 0.6, 45)),
          O("Shield broken, very low + skip entry", L("Misc/EnergyShield/Broken", 0.45, 35), L(SKIP_IN, 0.8, 35)),
          O("Mech band shock, low (Biotech)", L(SHOCKWAVE, 0.6, 40)),
          defName="AG_GojoVoidCollapse"))

# Itachi ------------------------------------------------------------------------------------------
ability("AG_ItachiFalseFace",
        "Genjutsu lands on everyone looking at him. A crow call is the Itachi signature; Odyssey has crows.",
        M("cast", "Cast", "the illusion takes hold",
          O("Insanity lance + low pulse", L("Misc/Artifacts/Psychic_Insanity_Lance", 0.8, 45), L(PULSE, 0.6, 25)),
          O("Crow call over a psychic pulse (Odyssey)", L("Pawn/Animal/Crow/Call", 0.9, 40), L(PULSE, 0.7, 30)),
          O("Word-of apply (Royalty)", L("Misc/Psycasts/WordOf/Apply", 0.8, 45))))
ability("AG_ItachiSusanoo",
        "Rise, the standing hum, the Yata Mirror turning a hit, and the Totsuka stab or seal.",
        M("rise", "Rise", "the Susanoo stands up",
          O("Emergence quake + fire", L("Misc/Emergence/Quake", 1.0, 45), L("Weapon/Flamethrower", 0.6, 30)),
          O("Wall raise (Royalty)", L("Misc/Psycasts/Wall_Raise", 0.7, 50)),
          O("Bestow warmup, fast", L(BESTOW, 1.5, 45))),
        M("loop", "Standing (loop)", "12 s while it stands",
          O("Ship reactor hum", L("Exotic/Ship_Reactor", 0.9, 25, loop=True)),
          O("Fire burning, low", L("Misc/Fire", 0.6, 25, loop=True)),
          O("Band node tuned hum", L("Buildings/Mechanoid/BandNode/Tuned", 0.8, 25, loop=True))),
        M("mirror", "Mirror turns a hit", "each hit from outside",
          O("Shield absorb, low", L("Misc/EnergyShield/Absorb", 0.8, 40)),
          O("Deflect", L("Impact/Deflect", 0.8, 40)),
          O("Metal blunt", L("Impact/MeleeHit_Metal_Blunt", 0.7, 45))),
        M("stab", "Totsuka stab / seal", "every 3 s on a target",
          O("Scyther hit + skip", L(BLADE_HIT, 0.8, 45), L(SKIP_IN, 0.8, 30)),
          O("Plasma sword (Royalty)", L("Impact/PlasmaSword", 0.8, 45)),
          O("Metal sharp + psychic effect", L("Impact/MeleeHit_Metal_Sharp", 0.7, 45), L("Misc/Artifacts/Psychic_Insanity_Lance", 1.2, 25))))
ability("AG_DispersalMurder",
        "Plays Longjump_Jump now. Crows are the missing part.",
        M("depart", "Scatter into crows", "Itachi breaks into the flock",
          O("Crow calls + longjump (Odyssey)", L("Pawn/Animal/Crow/Angry", 1.0, 40), L("Pawn/Animal/Crow/Call", 1.1, 30, 0.1), L("Pawn/Abilities/Longjump/Jump", 1.2, 30)),
          O("Longjump + sweeping (flaps stand-in)", L("Pawn/Abilities/Longjump/Jump", 1.2, 40), L("Interact/Work/Clean/Cleaning_Sweeping", 1.8, 30)),
          O("Crow calls only (Odyssey)", L("Pawn/Animal/Crow/Call", 1.0, 45))))

# Vergil ------------------------------------------------------------------------------------------
# One moment per sound marker in the Vergil sketches (Tools/VfxLab/web/sketches/vergil-*.js); its
# defName is the SoundDef the cast will play. The sheathe click is one sound for all three sheathes.
SHEATHE = "AG_VergilSheathe"
CUTS = [0.0, 0.1, 0.2, 0.3, 0.4]   # Judgement Cut's 5 damage ticks, burstSeconds 0.5 / 5 apart
ability("AG_VergilJudgementCut",
        "DMC: the draw and a ball of slices at the spot in the same instant, the ball breaking apart as it closes 0.5 s later, and the sheathe click with the break.",
        M("cut", "Draw + cuts", "after the 0.6 s warmup: the draw, then 5 hits 0.1 s apart",
          O("Deflect stings on the 5 hits", L("Impact/Deflect/Deflect_Metal", 1.3, 30), *[L("Impact/Deflect/Deflect_General", 1.1 + 0.1 * (i % 3), 30, d) for i, d in enumerate(CUTS)]),
          O("EMP crackle + 5 Scyther cuts", L(ZAP, 1.0, 25), *[L(BLADE_SWISH, 1.6 + 0.1 * (i % 3), 26, d) for i, d in enumerate(CUTS)]),
          O("Charge rifle zap + 5 metal stings", L("Weapon/ChargeRifle", 1.3, 35), *[L("Impact/MeleeHit_Metal_Sharp", 1.8 + 0.1 * (i % 3), 20, d) for i, d in enumerate(CUTS)]),
          O("Mech band shock (Biotech) + Scyther cuts", L("Impact/MechBand", 1.2, 35), *[L(BLADE_SWISH, 1.7, 28, d) for d in (0.1, 0.25, 0.4)]),
          O("Beam graser burst (Biotech) + deflects", L("Weapon/Beamgraser/Resolve", 1.3, 35), *[L("Impact/Deflect/Deflect_General", 1.3, 25, d) for d in (0.1, 0.25, 0.4)]),
          O("Plasma sword hits (Royalty)", *[L("Impact/PlasmaSword", p, 30, d) for p, d in ((1.2, 0.0), (1.4, 0.17), (1.3, 0.34))]),
          O("Psychic entropy + bionic slash misses (Royalty)", L("Misc/Psycasts/Psychic_Entropy", 1.2, 35), *[L("Impact/BionicSlash_Miss", 1.5 + 0.1 * (i % 3), 25, d) for i, d in enumerate(CUTS)]),
          O("Shield break + blade hits", L("Misc/EnergyShield/Broken", 1.0, 40), L(BLADE_HIT, 1.2, 35, 0.1), L(BLADE_HIT, 1.3, 35, 0.25), L(BLADE_HIT, 1.1, 35, 0.4)),
          O("Glass + swishes", L("Buildings/GestatorGlassShattering", 1.2, 35), L(BLADE_SWISH, 1.5, 35, 0.08), L(BLADE_SWISH, 1.7, 30, 0.22)),
          O("Bionic slash hits (Royalty)", L("Impact/BionicSlash_Hit", 1.2, 45), L("Impact/BionicSlash_Hit", 1.4, 35, 0.2)),
          O("Mono sword draw + bionic hits (Royalty)", L("UI/WeaponHandling/HandleMonoSword", 1.4, 35), L("Impact/BionicSlash_Hit", 1.3, 40, 0.1), L("Impact/BionicSlash_Hit", 1.5, 35, 0.3)),
          defName="AG_VergilJudgementCutOpen"),
        M("break", "The ball breaks", "0.5 s after the draw: it breaks into pieces that fall in, with a ring front and dust",
          O("Glass shattering", L("Buildings/GestatorGlassShattering", 1.0, 35)),
          O("Glass shattering, low", L("Buildings/GestatorGlassShattering", 0.8, 40)),
          O("Shield broken", L("Misc/EnergyShield/Broken", 1.0, 35)),
          O("Glass + shield broken", L("Buildings/GestatorGlassShattering", 1.1, 30), L("Misc/EnergyShield/Broken", 0.9, 25)),
          O("Mech band shock (Biotech)", L("Impact/MechBand", 1.0, 35)),
          defName="AG_VergilJudgementCutBreak"),
        M("sheathe", "Sheathe click", "as the ball closes; the same sound ends Yamato Dash and Judgement Cut End",
          O("Click", L(CLICK, 0.9, 45)),
          O("Grenade pin, low", L("Weapon/GrenadePin", 0.7, 45)),
          O("Trap arm", L("Misc/Trap", 1.3, 40)),
          O("Weapon handling, small", L("UI/WeaponHandling/HandleWeapon_SmallA", 1.0, 45)),
          O("Weapon handling, big low", L("UI/WeaponHandling/HandleWeapon_BigALow", 1.1, 45)),
          defName=SHEATHE))
ability("AG_VergilYamatoDash",
        "Dash (0.15 s), then 0.4 s later the sheathe click (AG_VergilSheathe) and the cuts on every mark.",
        M("dash", "Dash", "he crosses the ground in 0.15 s",
          O("Longjump + swish", L("Pawn/Abilities/Longjump/Jump", 1.6, 35), L(BLADE_SWISH, 1.3, 40)),
          O("Skip entry fast + swish", L(SKIP_IN, 2.0, 30), L("Misc/Swish2", 1.6, 45)),
          O("Bionic slash miss (Royalty)", L("Impact/BionicSlash_Miss", 1.0, 50)),
          defName="AG_VergilDash"),
        M("cuts", "Cuts land", "with the click, only when someone was marked",
          O("Blade hits", L(BLADE_HIT, 1.1, 40), L(BLADE_HIT, 1.3, 35, 0.07)),
          O("Glass", L("Buildings/GestatorGlassShattering", 1.3, 35)),
          O("Mono sword (Royalty)", L("Impact/MonoSword", 1.2, 40)),
          defName="AG_VergilDashCuts"))
ability("AG_VergilSummonedSwords",
        "Summon; in fire mode each blade flying (once a second) and going in; in spin mode a cut every 0.9 s; every blade breaking when the time is up.",
        M("summon", "Summon", "eight blades rise over 0.5 s",
          O("Shield reset, bright", L("Misc/EnergyShield/Reset", 1.4, 40)),
          O("Mortar shield reactivate", L("Misc/MortarShieldGenerator/Reactivate", 1.3, 40)),
          O("Psychic warmup + glass", L(WARMUP, 1.8, 30), L("Buildings/GestatorGlassShattering", 1.8, 20)),
          defName="AG_VergilSwordsSummon"),
        M("fire", "Blade flies", "fire mode: one blade a second",
          O("Bow", L("Weapon/BowB", 1.3, 45)),
          O("Piercing spine", L("Pawn/Abilities/PiercingSpine", 1.4, 40)),
          O("Spiner", L("Weapon/Spiner", 1.2, 40)),
          defName="AG_VergilSwordFire"),
        M("hit", "Blade in", "a blade enters a pawn",
          O("Metal sharp", L("Impact/MeleeHit_Metal_Sharp", 1.2, 40)),
          O("Bullet flesh + shield absorb", L("Impact/Bullet_Flesh", 1.1, 40), L("Misc/EnergyShield/Absorb", 1.8, 20)),
          O("Scyther hit, high", L(BLADE_HIT, 1.4, 35)),
          defName="AG_VergilSwordHit"),
        M("spin", "Spin cut", "spin mode: one cut tick every 0.9 s on the pawns within 1.6 cells",
          O("Swish, fast", L("Misc/Swish1", 1.5, 35)),
          O("Scyther swish + hit", L(BLADE_SWISH, 1.5, 35), L(BLADE_HIT, 1.3, 25, 0.05)),
          O("Bionic slash miss, high (Royalty)", L("Impact/BionicSlash_Miss", 1.3, 40)),
          defName="AG_VergilSwordsSpin"),
        M("break", "Every blade breaks", "when the time is up, the blades stuck in pawns too",
          O("Glass shattering, high", L("Buildings/GestatorGlassShattering", 1.4, 35)),
          O("Shield broken, high", L("Misc/EnergyShield/Broken", 1.3, 35)),
          O("Glass + metal sharp", L("Buildings/GestatorGlassShattering", 1.6, 30), L("Impact/MeleeHit_Metal_Sharp", 1.5, 25, 0.04)),
          defName="AG_VergilSwordsBreak"))
ability("AG_VergilJudgementCutEnd",
        "Warmup (1 s), vanish with a storm of cuts (1.5 s), back kneeling, sheathe over 0.8 s; on the click (AG_VergilSheathe) everything is cut at once.",
        M("vanish", "Vanish + cut storm", "1.5 s while he is gone",
          O("Skip + 6 swishes", L(SKIP_IN, 1.2, 40), *[L(BLADE_SWISH, 1.3 + 0.1 * (i % 3), 30, 0.15 + 0.2 * i) for i in range(6)]),
          O("Glass + swishes + shield break", L("Buildings/GestatorGlassShattering", 1.0, 35), *[L(BLADE_SWISH, 1.5, 30, 0.2 + 0.25 * i) for i in range(5)], L("Misc/EnergyShield/Broken", 0.9, 35, 1.3)),
          O("Bionic slashes (Royalty)", *[L("Impact/BionicSlash_Miss", 1.1 + 0.1 * (i % 2), 35, 0.2 * i) for i in range(7)]),
          defName="AG_VergilCutEndVanish"),
        M("cuts", "Everything cut", "on the click, with the sheathe sound",
          O("Shield break + mortar", L("Misc/EnergyShield/Broken", 0.8, 45), L(BOOM, 0.9, 40)),
          O("Glass + blades", L("Buildings/GestatorGlassShattering", 0.9, 40), L(BLADE_HIT, 1.0, 40, 0.02), L(BLADE_HIT, 1.2, 40, 0.08)),
          O("Mech band shockwave", L("Explosion/Mechband_Shockwave_Explosion_01a", 1.0, 45)),
          defName="AG_VergilCutEndCuts"))

# Sato --------------------------------------------------------------------------------------------
ability("AG_SatoSever",
        "Cutting off a piece of himself and throwing it.",
        M("cut", "Cut + throw", "the part comes off and is thrown",
          O("Execute cut + swish", L("Interact/Execute/Cut", 1.0, 45), L("Misc/Swish1", 1.1, 35, 0.2)),
          O("Butcher meat + swish", L("Interact/Work/Butcher/Butcher_Meat", 1.1, 45), L("Misc/Swish1", 1.1, 35, 0.2)),
          O("Surgery + bite", L("Interact/Work/Surgery/Surgery_Loop", 1.2, 35), L("Impact/HumanBite_Hit", 0.8, 40))))
ability("AG_SatoHeadshotReset",
        "The shot to the temple, then the rise 6 s later (Satō's body re-forms).",
        M("shot", "Shot", "pistol to the temple",
          O("Revolver", L("Weapon/Revolver", 1.0, 55)),
          O("Autopistol + flesh", L("Weapon/Autopistol", 1.0, 50), L("Impact/Bullet_Flesh", 0.9, 40)),
          O("Revolver + gun tail", L("Weapon/Revolver", 0.95, 55), L("Weapon/Tails/Light", 1.0, 35))),
        M("rise", "Reset rise", "he rises healed",
          O("Resurrect cast", L("Pawn/Abilities/Resurrect", 0.9, 45)),
          O("Mech resurrect", L("Pawn/Abilities/MechResurrect", 1.0, 45)),
          O("Pollution ooze + surgery", L("Misc/PollutionSpreading", 0.8, 40), L("Interact/Work/Surgery/Surgery_Loop", 0.8, 30))))
ability("AG_SatoGrenadeReset",
        "The pin, a short hold, the blast (the explosion itself already has vanilla sound), then the rise at an anchor.",
        M("pin", "Pin", "he pulls the pin",
          O("Grenade pin", L("Weapon/GrenadePin", 1.0, 50)),
          O("Grenade pin + click", L("Weapon/GrenadePin", 0.9, 50), L(CLICK, 1.2, 30, 0.15)),
          O("Trap arm", L("Misc/Trap", 1.0, 45))),
        M("rise", "Rise at anchor", "the body re-forms at the anchor",
          O("Resurrect cast", L("Pawn/Abilities/Resurrect", 0.8, 45)),
          O("Pollution ooze", L("Misc/PollutionSpreading", 0.8, 50)),
          O("Deathrest casket exit", L("Buildings/DeathrestCasket/Exit", 0.9, 45))))
ability("AG_SatoTheGame",
        "Marking the next player to lose.",
        M("mark", "Mark", "the enemy is marked",
          O("Turret acquires target", L("Weapon/GunTurret/Status", 1.0, 45)),
          O("Poker chips (it is a game)", L("Interact/Joy/PokerChips", 1.0, 50)),
          O("Orbital targeter aim", L("Weapon/OrbitalTargeter", 1.2, 35))))
ability("AG_SatoBlackGhost",
        "Black matter pouring out and standing up, and the IBM tearing a limb off.",
        M("summon", "Summon", "the IBM stands up",
          O("Pollution ooze + apocriton call, low", L("Misc/PollutionSpreading", 0.7, 45), L("Pawn/Mechanoid/Apocriton/Call", 0.6, 35, 0.3)),
          O("Toxifier pollute + diabolus call", L("Buildings/Mechanoid/Toxifier/Pollute", 0.8, 45), L("Pawn/Mechanoid/Diabolus/Call", 0.8, 35, 0.3)),
          O("Emergence end + tesseron call", L("Misc/Emergence/end_small", 0.8, 45), L("Pawn/Mechanoid/Tesseron/Call", 0.7, 35, 0.2))),
        M("tear", "Tear a limb", "once per summon",
          O("Execute cut + flesh", L("Interact/Execute/Cut", 0.8, 50), L("Impact/Bullet_Flesh", 0.7, 45)),
          O("Butcher + big animal hit", L("Interact/Work/Butcher/Butcher_Meat", 0.8, 45), L("Pawn/Animal/Melee_Big/Hit_Pawn", 0.8, 45)),
          O("Bloodfeed bite, low", L("Pawn/Abilities/Bloodfeed", 0.7, 50))))

# Shikamaru ---------------------------------------------------------------------------------------
SLITHER = [O("Pollution spreading (ooze)", L("Misc/PollutionSpreading", 1.0, 45)),
           O("Acid spray cast, low", L("Pawn/Abilities/AcidSpray", 0.7, 40)),
           O("Cleaning fluid, low", L("Interact/Work/Clean/Cleaning_Fluid", 0.7, 40))]
ability("AG_ShadowImitation",
        "The shadow running over the ground, then catching.",
        M("run", "Shadow runs out", "cast: the shadow crosses the ground", *SLITHER),
        M("catch", "Caught", "the shadow holds the target",
          O("Roping", L("Pawn/Human/Roping", 0.9, 45)),
          O("Mech control resolve", L("Pawn/Mechanitor/ControlTaking/Resolve", 0.8, 40)),
          O("Psychic effect, low", L("Misc/Psycasts/Psycast_Psychic_Effect", 0.7, 40))))
ability("AG_ShadowSeam",
        "Two things sewn together.",
        M("sew", "Sew", "the seam forms",
          O("Roping, twice", L("Pawn/Human/Roping", 1.0, 45), L("Pawn/Human/Roping", 1.1, 40, 0.25)),
          O("Tailoring start + ooze", L("Interact/Work/Tailor/Tailoring_Start", 0.8, 40), L("Misc/PollutionSpreading", 1.0, 30)),
          O("Ooze + mech control grain", L("Misc/PollutionSpreading", 1.1, 40), L("Pawn/Mechanitor/ControlTaking/Grains", 0.8, 30))))
ability("AG_ShadowGrasp",
        "A hand of shadow dragging a thing over the ground.",
        M("drag", "Drag (loop)", "while the thing slides",
          O("Sweeping, low", L("Interact/Work/Clean/Cleaning_Sweeping", 0.7, 40, loop=True)),
          O("Cleaning dirt", L("Interact/Work/Clean/Cleaning_Dirt", 0.8, 35, loop=True)),
          O("Ooze, looped", L("Misc/PollutionSpreading", 0.9, 35, loop=True))))
ability("AG_ShadowDouble",
        "His shadow walks off and stands.",
        M("go", "Double goes out", "cast",
          O("Ooze + psychic effect", L("Misc/PollutionSpreading", 0.9, 40), L("Misc/Psycasts/Psycast_Psychic_Effect", 0.8, 30)),
          O("Acid spray resolve, low", L("Pawn/Abilities/AcidSpray", 0.6, 40)),
          O("Skip entry, low", L(SKIP_IN, 0.6, 35))))
ability("AG_ShadowNeckBind",
        "Hands climbing (2.1 s), then the choke while he channels.",
        M("climb", "Hands climb", "2.1 s up the body",
          O("Roping, slow", L("Pawn/Human/Roping", 0.8, 45)),
          O("Ooze + cloth", L("Misc/PollutionSpreading", 1.1, 40), L("Interact/Work/Tailor/Tailoring_Loop", 0.8, 30))),
        M("choke", "Choke (loop)", "up to 8 s",
          O("Mech control loop, low", L("Pawn/Mechanitor/ControlTaking", 0.6, 30, loop=True)),
          O("Roping, looped", L("Pawn/Human/Roping", 0.7, 30, loop=True)),
          O("Psychic animal pulser, low", L("Misc/Artifacts/Psychic_Animal_Pulser", 0.5, 30, loop=True))))

# Inumaki -----------------------------------------------------------------------------------------
WORD = [O("Throne speech, low + psychic pulse", L("Misc/ThroneSpeech_Male", 0.7, 50), L(PULSE, 0.8, 35)),
        O("Throne speech + mech band shockwave", L("Misc/ThroneSpeech_Male", 0.8, 50), L(SHOCKWAVE, 0.9, 35, 0.15)),
        O("No voice: shock lance + shockwave", L("Misc/Artifacts/Psychic_Shock_Lance", 0.8, 45), L(SHOCKWAVE, 0.8, 35))]
for d, lab in [("AG_Imperative_Stop", "stop"), ("AG_Imperative_Drop", "drop"), ("AG_Imperative_Come", "come"), ("AG_Imperative_Run", "run")]:
    ability(d, "LarynxExtension.wordSound is already there and empty; this fills it. One word, everyone in hearing obeys.",
            M("word", f"The word ({lab})", "Inumaki speaks", *WORD))
ability("AG_Imperative_Crush",
        "Heavier than the light words: 15 blunt to every listener.",
        M("word", "The word (crush)", "Inumaki speaks",
          O("Throne speech, lower + thump", L("Misc/ThroneSpeech_Male", 0.6, 55), L("Impact/ThumpCannon", 0.8, 45, 0.15)),
          O("Throne speech + stone breaking", L("Misc/ThroneSpeech_Male", 0.65, 55), L("Impact/BuildingDestroyed/Stone/Medium", 0.8, 45, 0.15)),
          O("No voice: shock lance + big animal hits", L("Misc/Artifacts/Psychic_Shock_Lance", 0.7, 45), L("Pawn/Animal/Melee_Big/Hit_Pawn", 0.7, 50, 0.1))))
ability("AG_Imperative_Explode",
        "The explosions already have vanilla sound; this is only the word before them.",
        M("word", "The word (explode)", "Inumaki speaks",
          O("Throne speech, lowest + pulse", L("Misc/ThroneSpeech_Male", 0.55, 55), L(PULSE, 0.6, 40)),
          O("Throne speech + insanity lance", L("Misc/ThroneSpeech_Male", 0.6, 55), L("Misc/Artifacts/Psychic_Insanity_Lance", 0.7, 35)),
          O("No voice: shock lance, low", L("Misc/Artifacts/Psychic_Shock_Lance", 0.6, 50))))

# Trace -------------------------------------------------------------------------------------------
ability("AG_Trace_UnlimitedBladeWorks",
        "Each 2 s verse, the world opening (fire ring in the source), and the world closing.",
        M("verse", "Verse", "each of up to three verses",
          O("Bestow warmup", L(BESTOW, 1.2, 40)),
          O("Psychic warmup", L(WARMUP, 0.9, 40)),
          O("Psycast casting loop (Royalty)", L("Misc/Psycasts/Psycast_Casting_Loop", 1.0, 40))),
        M("open", "World opens", "everyone is taken in",
          O("Flamethrower sweep + psychic pulse", L("Weapon/Flamethrower", 0.8, 45), L(PULSE, 0.7, 45)),
          O("Orbital beam + incendiary", L("Misc/OrbitalBeam", 0.8, 45), L("Weapon/Artillery/Mortar_Explode_Incendiary", 0.8, 40)),
          O("Skip pulse (Royalty) + fire", L("Misc/Psycasts/Skip/Pulse", 0.8, 50), L("Weapon/Flamethrower", 0.7, 35))),
        M("close", "World closes", "everyone is put back",
          O("Skip entry, low", L(SKIP_IN, 0.7, 45)),
          O("Psychic pulse, reversed feel", L(PULSE, 1.2, 40)),
          O("Glass + skip", L("Buildings/GestatorGlassShattering", 0.8, 35), L(SKIP_IN, 0.8, 35))))

# Obito -------------------------------------------------------------------------------------------
SWIRL_IN = [O("Skip entry, low", L(SKIP_IN, 0.7, 45)),
            O("Skip entry + low swish", L(SKIP_IN, 0.8, 40), L("Misc/Swish1", 0.5, 35)),
            O("Psychic pulse, low + swish", L(PULSE, 0.6, 35), L("Misc/Swish1", 0.6, 35))]
ability("AG_KamuiPhase",
        "Toggle on/off, and a hit passing through him into the dimension.",
        M("on", "Phase on / off", "the toggle",
          O("Skip entry, soft", L(SKIP_IN, 0.9, 30)),
          O("Psychic effect, low", L("Misc/Psycasts/Psycast_Psychic_Effect", 0.7, 35)),
          O("Swish, low", L("Misc/Swish1", 0.6, 35))),
        M("through", "Hit goes through", "each hit that passes into Kamui",
          O("Shield absorb, low", L("Misc/EnergyShield/Absorb", 0.6, 35)),
          O("Swish, low", L("Misc/Swish2", 0.7, 35)),
          O("Hiss, low", L("Misc/Hiss", 0.6, 30))))
ability("AG_KamuiWarp",
        "Winding into his eye (1 s), and coming out of the swirl.",
        M("in", "Wind in", "1 s into the eye", *SWIRL_IN),
        M("out", "Come out", "out of the swirl",
          O("Skip entry, normal", L(SKIP_IN, 1.0, 45)),
          O("Skip exit (Royalty)", L(SKIP_OUT, 0.9, 45)),
          O("Swish + skip", L("Misc/Swish1", 0.8, 35), L(SKIP_IN, 1.1, 35))))
ability("AG_KamuiStore",
        "A person or item winding into his eye (0.4 s), and being put back.",
        M("absorb", "Absorb", "0.4 s wind in", *SWIRL_IN),
        M("release", "Release", "put back out",
          O("Skip entry, normal", L(SKIP_IN, 1.1, 40)),
          O("Skip exit (Royalty)", L(SKIP_OUT, 1.0, 40)),
          O("Drop pod open, soft", L("Misc/DropPodOpen/Default", 1.2, 30))))
ability("AG_WoodRelease",
        "Branches bursting out (0.5 s warmup) and skewering the line.",
        M("burst", "Branches burst", "the line of branches",
          O("Tree felled + wood break", L("Interact/Work/Construct/Trees/Tree_Felled", 1.2, 45), L("Impact/BuildingDestroyed/Wood/Small", 1.1, 40)),
          O("Wood break, medium", L("Impact/BuildingDestroyed/Wood/Medium", 1.2, 50)),
          O("Rummage wood + tree chop", L("Interact/Work/Construct/Wood/Rummage_Wood", 1.3, 40), L("Interact/Work/Construct/Trees/Tree_Chop", 1.1, 40))),
        M("hit", "Skewer", "each pawn on the line",
          O("Bullet wood + flesh", L("Impact/Bullet_Wood", 0.9, 40), L("Impact/Bullet_Flesh", 0.8, 35)),
          O("Melee hit wood", L("Impact/MeleeHit_Wood", 0.8, 45)),
          O("Punch hit building wood", L("Impact/PunchHitBuilding/Wood", 0.8, 45))))

# Accelerator -------------------------------------------------------------------------------------
ability("AG_VectorReflection",
        "The world stops, and the rounds are sent back.",
        M("stop", "World stops", "the edit opens",
          O("Power off + skip, low", L("Electricity/PowerOff/Small", 0.7, 40), L(SKIP_IN, 0.6, 30)),
          O("Skip pulse (Royalty)", L("Misc/Psycasts/Skip/Pulse", 0.7, 45)),
          O("Geothermal stop", L("Electricity/GeothermalPlant/Stop", 1.2, 40))),
        M("release", "Rounds sent back", "the edit is applied",
          O("Deflects", L("Impact/Deflect", 1.0, 45), L("Impact/Deflect", 1.2, 35, 0.06)),
          O("Power on + swish", L("Electricity/PowerOn/Small", 1.0, 40), L("Misc/Swish2", 1.4, 40)),
          O("Mech band shockwave, high", L(SHOCKWAVE, 1.4, 40))))
ability("AG_VectorSurge",
        "Five game seconds of a crawling world: a slowed-down loop fits.",
        M("start", "Surge start", "the clock goes up",
          O("Psychic warmup, low", L(WARMUP, 0.6, 45)),
          O("Power off, slow", L("Electricity/PowerOff/Small", 0.5, 45)),
          O("Geothermal start", L("Electricity/GeothermalPlant/Start", 1.0, 40))),
        M("loop", "Surge (loop)", "while it lasts",
          O("Ship reactor, low", L("Exotic/Ship_Reactor", 0.6, 25, loop=True)),
          O("Mech booster working", L("Buildings/Mechanoid/MechBooster", 1.0, 30, loop=True)),
          O("Animal pulser, very low", L("Misc/Artifacts/Psychic_Animal_Pulser", 0.4, 30, loop=True))))
ability("AG_VectorShove",
        "One body thrown away.",
        M("shove", "Shove", "the push",
          O("Thump cannon impact + shockwave", L("Impact/ThumpCannon", 1.0, 50), L(SHOCKWAVE, 1.0, 35)),
          O("Big animal hit + swish", L("Pawn/Animal/Melee_Big/Hit_Pawn", 0.9, 50), L("Misc/Swish1", 0.8, 35)),
          O("Bionic punch hit, low", L("Impact/BionicPunch_Hit", 0.8, 50))))

# Todo --------------------------------------------------------------------------------------------
ability("AG_AnchorBlackFlash",
        "Plays the vanilla punch now. Black Flash is a punch with a black spark.",
        M("hit", "Black Flash hit", "the punch lands inside the window",
          O("Bionic punch + EMP crackle, low", L("Impact/BionicPunch_Hit", 0.8, 50), L(ZAP, 0.6, 40)),
          O("Thump cannon + zap", L("Impact/ThumpCannon", 1.1, 50), L("Weapon/ChargeRifle", 0.7, 35)),
          O("Big hit + shockwave + zap", L("Pawn/Animal/Melee_Big/Hit_Pawn", 0.9, 50), L(SHOCKWAVE, 0.8, 35), L(ZAP, 0.8, 30))))
ability("AG_AnchorMark",
        "The stone thrown and landing.",
        M("throw", "Stone lands", "the stone hits the ground",
          O("Stone impact (joy)", L("Interact/Joy/StoneImpact", 1.0, 50)),
          O("Swish + stone punch", L("Misc/Swish1", 1.2, 35), L("Impact/PunchHitBuilding/Stone", 1.0, 40, 0.3)),
          O("Chunk rock drop, light", L("Interact/Haul/Drop/ChunkRock", 1.4, 40))))

# Synthesized options (make_sounds.py, Sounds/AG/): added after the game-clip options ---------------
def add(defName, moment, *options):
    next(m for m in A[defName]["moments"] if m["id"] == moment)["options"].extend(options)


add("AG_GokuSolarFlare", "flash",
    O("Synth: solar flare", L("AG/SolarFlare", 1.0, 50)),
    O("Synth: solar flare + EMP crackle", L("AG/SolarFlare", 1.0, 45), L(ZAP, 1.3, 30)))
add("AG_GokuInstantTransmission", "leave", O("Synth: shun", L("AG/Shun", 1.0, 45)))
add("AG_GokuInstantTransmission", "arrive", O("Synth: shun, lower + punch miss", L("AG/Shun", 0.85, 45), L("Impact/PunchMiss", 0.8, 30)))
add("AG_GokuKamehameha", "charge",
    O("Synth: whine rising over the hum", L("AG/KiChargeStart", 1.0, 50), L("AG/KiCharge", 1.0, 30, loop=True)),
    O("Synth: hum only", L("AG/KiCharge", 1.0, 45, loop=True)))
add("AG_GokuKamehameha", "fire",
    O("Synth: beam roar + whoomp", L("AG/KiBeam", 1.0, 50, loop=True), L("AG/KiBeamFire", 1.0, 50)),
    O("Synth roar + orbital beam", L("AG/KiBeam", 1.0, 45, loop=True), L("Misc/OrbitalBeam", 1.1, 35, loop=True)))
add("AG_GokuSpiritBomb", "gather",
    O("Synth: airy chord", L("AG/SpiritGather", 1.0, 45, loop=True)),
    O("Synth: chord + wind", L("AG/SpiritGather", 1.0, 40, loop=True), L("Misc/Tornado", 1.3, 15, loop=True)))
for d in ("AG_ThunderGodJump", "AG_ThunderGodChain"):
    add(d, "jump", O("Synth: shun, fast", L("AG/Shun", 1.15, 45)))
add("AG_GuidingThunder", "redirect", O("Synth: shun, high", L("AG/Shun", 1.4, 35)))
add("AG_Rasengan", "form",
    O("Synth: spin (loop)", L("AG/Rasengan", 1.0, 45, loop=True)),
    O("Synth: wind-up + spin", L("AG/RasenganForm", 1.0, 50), L("AG/Rasengan", 1.0, 30, loop=True)))
add("AG_Rasengan", "hit",
    O("Synth: grind + thump", L("AG/RasenganHit", 1.0, 55)),
    O("Synth grind + thump cannon", L("AG/RasenganHit", 1.0, 50), L("Impact/ThumpCannon", 1.1, 40)))
add("AG_SasukeAmenotejikara", "swap",
    O("Synth: ting", L("AG/Ting", 1.0, 40)),
    O("Synth: shun + ting", L("AG/Shun", 1.2, 35), L("AG/Ting", 1.0, 35)))
add("AG_SasukeRaikoKusari", "link",
    O("Synth: Chidori crack", L("AG/ChidoriCrack", 1.0, 50)),
    O("Synth crack + thunder", L("AG/ChidoriCrack", 1.0, 45), L("Ambience/Thunder/lightning", 1.2, 30)))
add("AG_SasukeRaikoKusari", "loop", O("Synth: Chidori chirping", L("AG/Chidori", 1.0, 40, loop=True)))
add("AG_SasukeAmaterasu", "ignite", O("Synth: black flame catching", L("AG/BlackFlameIgnite", 1.0, 50)))
add("AG_SasukeAmaterasu", "burn",
    O("Synth: black flame", L("AG/BlackFlame", 1.0, 35, loop=True)),
    O("Synth: black flame, lower", L("AG/BlackFlame", 0.8, 35, loop=True)))
add("AG_PainBanshoTenin", "pull", O("Synth: sucked in, low", L("AG/KamuiIn", 0.7, 40)))
add("AG_PainBanshoTenin", "cast", O("Synth: sucked in, very low", L("AG/KamuiIn", 0.6, 30)))
add("AG_PainBlackReceiver", "grow", O("Synth: ting, low", L("AG/Ting", 0.6, 25)))
add("AG_PainBlackReceiver", "throw", O("Synth: sword flies, low", L("AG/SwordFly", 0.8, 40)))
add("AG_PainBlackReceiver", "break", O("Synth: out of the swirl, soft", L("AG/KamuiOut", 0.7, 25)))
add("AG_PainChibakuTensei", "cast", O("Synth: whine rising, low", L("AG/KiChargeStart", 0.7, 35)))
add("AG_PainChibakuTensei", "launch", O("Synth: shun, low", L("AG/Shun", 0.6, 45)))
add("AG_Nakime_InfinityCastle", "sunburn", O("Synth: black flame catching, high (orange)", L("AG/BlackFlameIgnite", 1.4, 30)))
add("AG_GojoBlue", "open",
    O("Synth: sucked in, fast", L("AG/KamuiIn", 1.3, 40)),
    O("Synth: shun, low + sucked in", L("AG/Shun", 0.7, 35), L("AG/KamuiIn", 1.2, 35, 0.05)))
add("AG_GojoBlue", "pull",
    O("Synth: ki hum, low", L("AG/KiCharge", 0.6, 35, loop=True)),
    O("Synth: sucked in, slow, twice + Gravity Well hum", L("AG/KamuiIn", 0.5, 35), L("AG/KamuiIn", 0.55, 30, 1.5), L(PAIN_HUM, 0.4, 15, loop=True)))
add("AG_GojoBlue", "implode",
    O("Synth: whoomp, low + mortar", L("AG/KiBeamFire", 0.6, 45), L(BOOM, 0.5, 35)),
    O("Synth: Rasengan hit, low (grind + thump)", L("AG/RasenganHit", 0.7, 50)))
add("AG_GojoRed", "charge",
    O("Synth: whine rising, fast", L("AG/KiChargeStart", 1.6, 40)),
    O("Synth: Rasengan wind-up, high", L("AG/RasenganForm", 1.4, 35)))
add("AG_GojoRed", "fire",
    O("Synth: whoomp, high", L("AG/KiBeamFire", 1.3, 45)),
    O("Synth: shun + whoomp", L("AG/Shun", 1.2, 35), L("AG/KiBeamFire", 1.4, 40)))
add("AG_GojoRed", "burst",
    O("Synth: Rasengan hit (grind + thump)", L("AG/RasenganHit", 1.2, 50)),
    O("Synth: black flash, high", L("AG/BlackFlash", 1.2, 45)))
add("AG_GojoHollowPurple", "merge",
    O("Synth: sucked in", L("AG/KamuiIn", 1.0, 45)),
    O("Synth: whine rising + sucked in", L("AG/KiChargeStart", 1.2, 35), L("AG/KamuiIn", 1.1, 35)))
add("AG_GojoHollowPurple", "ignite",
    O("Synth: whoomp, low + shield break", L("AG/KiBeamFire", 0.7, 50), L("Misc/EnergyShield/Broken", 0.6, 30)),
    O("Synth: solar flare, low", L("AG/SolarFlare", 0.6, 45)))
add("AG_GojoHollowPurple", "travel",
    O("Synth: beam roar, low", L("AG/KiBeam", 0.6, 45, loop=True)),
    O("Synth: black flame, low (a crackling roar)", L("AG/BlackFlame", 0.6, 40, loop=True)))
add("AG_GojoHollowPurple", "touch", O("Synth: sucked in, fast", L("AG/KamuiIn", 1.5, 40)))
add("AG_GojoHollowPurple", "fade",
    O("Synth: out of the swirl, slow", L("AG/KamuiOut", 0.7, 40)),
    O("Synth: ting, low (the silence after)", L("AG/Ting", 0.5, 30)))
add("AG_GojoUnlimitedVoid", "sign",
    O("Synth: ting, low", L("AG/Ting", 0.6, 30)),
    O("Synth: whine rising, low", L("AG/KiChargeStart", 0.8, 30)))
add("AG_GojoUnlimitedVoid", "open",
    O("Synth: sucked in, slow", L("AG/KamuiIn", 0.6, 50)),
    O("Synth: sucked in + whoomp, low", L("AG/KamuiIn", 0.7, 45), L("AG/KiBeamFire", 0.5, 40, 0.3)))
add("AG_GojoUnlimitedVoid", "break",
    O("Synth: out of the swirl", L("AG/KamuiOut", 0.8, 45)),
    O("Synth: ting + glass", L("AG/Ting", 0.7, 35), L("Buildings/GestatorGlassShattering", 0.7, 25)))
add("AG_GojoUnlimitedVoid", "enter",
    O("Synth: shun, low + sucked in", L("AG/Shun", 0.7, 40), L("AG/KamuiIn", 0.9, 35, 0.1)),
    O("Synth: solar flare (the white)", L("AG/SolarFlare", 0.8, 40)))
add("AG_GojoUnlimitedVoid", "hole", O("Synth: whoomp, very low", L("AG/KiBeamFire", 0.45, 50)))
add("AG_GojoUnlimitedVoid", "drone",
    O("Synth: Susanoo hum", L("AG/SusanooHum", 0.8, 35, loop=True)),
    O("Synth: airy chord, low", L("AG/SpiritGather", 0.6, 35, loop=True)))
add("AG_GojoUnlimitedVoid", "touch", O("Synth: ting", L("AG/Ting", 1.0, 35)))
add("AG_GojoUnlimitedVoid", "collapse",
    O("Synth: sucked in", L("AG/KamuiIn", 0.8, 45)),
    O("Synth: out of the swirl, low", L("AG/KamuiOut", 0.6, 45)))
add("AG_DispersalMurder", "depart",
    O("Synth: wings", L("AG/CrowFlaps", 1.0, 50)),
    O("Synth wings + crow calls (Odyssey)", L("AG/CrowFlaps", 1.0, 45), L("Pawn/Animal/Crow/Call", 1.0, 35)))
add("AG_ItachiSusanoo", "loop", O("Synth: low hum", L("AG/SusanooHum", 1.0, 35, loop=True)))
add("AG_VergilJudgementCut", "cut",
    O("Synth: space slices", L("AG/SpaceSliceCluster", 1.0, 50)),
    O("Synth slices + shield break", L("AG/SpaceSliceCluster", 1.0, 45), L("Misc/EnergyShield/Broken", 1.0, 30)),
    O("Swish + synth slices", L("Misc/Swish2", 1.6, 40), L("AG/SpaceSliceCluster", 1.0, 45, 0.02)))
add("AG_VergilJudgementCut", "sheathe",
    O("Synth: ting", L("AG/Ting", 1.0, 40)),
    O("Click + synth ting", L(CLICK, 0.9, 45), L("AG/Ting", 1.2, 25, 0.02)))
add("AG_VergilYamatoDash", "dash",
    O("Synth: shun", L("AG/Shun", 1.1, 45)),
    O("Synth shun + swish", L("AG/Shun", 1.0, 40), L("Misc/Swish2", 1.6, 35)))
add("AG_VergilYamatoDash", "cuts",
    O("Synth: one slice", L("AG/SpaceSlice", 1.1, 40)),
    O("Synth: slices", L("AG/SpaceSliceCluster", 1.2, 40)))
add("AG_VergilSummonedSwords", "summon", O("Synth: glass arpeggio", L("AG/SwordSummon", 1.0, 45)))
add("AG_VergilSummonedSwords", "fire", O("Synth: sword flies", L("AG/SwordFly", 1.0, 45)))
add("AG_VergilSummonedSwords", "spin", O("Synth: sword flies, fast", L("AG/SwordFly", 1.4, 35)))
add("AG_VergilSummonedSwords", "break", O("Synth ting, high + glass", L("AG/Ting", 1.6, 30), L("Buildings/GestatorGlassShattering", 1.5, 30)))
add("AG_VergilJudgementCutEnd", "vanish",
    O("Synth: slice storm", L("AG/Shun", 0.8, 35), L("AG/SpaceSliceCluster", 1.0, 45, 0.1), L("AG/SpaceSliceCluster", 1.15, 40, 0.6)))
add("AG_VergilJudgementCutEnd", "cuts",
    O("Synth: slices + whoomp", L("AG/SpaceSliceCluster", 0.9, 50), L("AG/KiBeamFire", 1.2, 35)))
add("AG_ShadowImitation", "run", O("Synth: shadow run", L("AG/ShadowRun", 1.0, 50)))
add("AG_ShadowSeam", "sew", O("Synth: shadow run + roping", L("AG/ShadowRun", 1.1, 45), L("Pawn/Human/Roping", 1.0, 35, 0.4)))
add("AG_ShadowGrasp", "drag", O("Synth: shadow hold, faster", L("AG/ShadowHold", 1.3, 40, loop=True)))
add("AG_ShadowDouble", "go", O("Synth: shadow run, low", L("AG/ShadowRun", 0.8, 50)))
add("AG_ShadowNeckBind", "climb", O("Synth: shadow run, slow", L("AG/ShadowRun", 0.7, 45)))
add("AG_ShadowNeckBind", "choke", O("Synth: shadow hold", L("AG/ShadowHold", 1.0, 40, loop=True)))
add("AG_KamuiPhase", "on", O("Synth: swirl out, soft", L("AG/KamuiOut", 1.1, 30)))
add("AG_KamuiWarp", "in", O("Synth: sucked into the eye", L("AG/KamuiIn", 1.0, 50)))
add("AG_KamuiWarp", "out", O("Synth: out of the swirl", L("AG/KamuiOut", 1.0, 50)))
add("AG_KamuiStore", "absorb", O("Synth: sucked in, fast", L("AG/KamuiIn", 1.4, 45)))
add("AG_KamuiStore", "release", O("Synth: out, fast", L("AG/KamuiOut", 1.3, 45)))
add("AG_AnchorBlackFlash", "hit",
    O("Synth: black flash", L("AG/BlackFlash", 1.0, 55)),
    O("Synth + bionic punch", L("AG/BlackFlash", 1.0, 50), L("Impact/BionicPunch_Hit", 0.8, 40)))

# How long each looping moment lasts in game (s); the code ends the sustainer then, and the lab
# cuts the preview at the same time with a short fade.
DURATION = {
    ("AG_GokuKamehameha", "charge"): 3.5, ("AG_GokuKamehameha", "fire"): 1.2, ("AG_GokuSpiritBomb", "gather"): 6,
    ("AG_Rasengan", "form"): 1.5, ("AG_SasukeRaikoKusari", "loop"): 4, ("AG_SasukeAmaterasu", "burn"): 4,
    ("AG_PainChibakuTensei", "pull"): 3, ("AG_ItachiSusanoo", "loop"): 5, ("AG_ShadowGrasp", "drag"): 1.5,
    ("AG_ShadowNeckBind", "choke"): 4, ("AG_Trace_UnlimitedBladeWorks", "verse"): 2, ("AG_VectorSurge", "loop"): 5,
    ("AG_VergilJudgementCutEnd", "vanish"): 1.6, ("AG_GojoBlue", "pull"): 3, ("AG_GojoHollowPurple", "travel"): 5,
    ("AG_GojoUnlimitedVoid", "drone"): 10,
}
for (d, mid), sec in DURATION.items():
    next(m for m in A[d]["moments"] if m["id"] == mid)["duration"] = sec

# Group by hero in the lab: the AbilityDef file names are kit names (Anchor, Larynx, Vector...).
import re as _re
HERO = {"Anchor": "Todo", "Dispersal": "Itachi", "Itachi": "Itachi", "Larynx": "Inumaki", "ShadowPlexus": "Shikamaru",
        "Vector": "Accelerator", "Trace": "Shirou", "Goku": "Goku", "Minato": "Minato", "Sasuke": "Sasuke",
        "Pain": "Pain", "Shinra": "Pain", "Obito": "Obito", "Vergil": "Vergil", "Sato": "Satō", "Nakime": "Nakime",
        "Gojo": "Gojo", "Gojo_Void": "Gojo"}
for xml in (WT.parent.parent / "1.6/Defs/AbilityDefs").glob("*.xml"):
    kit = xml.stem.replace("AG_", "").replace("_Abilities", "")
    for d in _re.findall(r"<defName>([^<]+)</defName>", xml.read_text()):
        if d in A:
            A[d]["hero"] = HERO.get(kit, kit)
missing_hero = [d for d in A if "hero" not in A[d]]
assert not missing_hero, missing_hero

out = {"note": "Candidate sounds per ability moment, written by Claude from clip names, vanilla SoundDef use, length and brightness. Nothing here was listened to before it was written; pick by ear in the lab.",
       "abilities": A}
if bad:
    sys.exit("missing: " + ", ".join(sorted(set(bad))))
(WT / "candidates.json").write_text(json.dumps(out, indent=1) + "\n")
n_m = sum(len(a["moments"]) for a in A.values())
n_o = sum(len(m["options"]) for a in A.values() for m in a["moments"])
print(f"{len(A)} abilities, {n_m} moments, {n_o} options")
