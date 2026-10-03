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
    ("AG_VergilJudgementCutEnd", "vanish"): 1.6,
}
for (d, mid), sec in DURATION.items():
    next(m for m in A[d]["moments"] if m["id"] == mid)["duration"] = sec

# Group by hero in the lab: the AbilityDef file names are kit names (Anchor, Larynx, Vector...).
import re as _re
HERO = {"Anchor": "Todo", "Dispersal": "Itachi", "Itachi": "Itachi", "Larynx": "Inumaki", "ShadowPlexus": "Shikamaru",
        "Vector": "Accelerator", "Trace": "Shirou", "Goku": "Goku", "Minato": "Minato", "Sasuke": "Sasuke",
        "Pain": "Pain", "Shinra": "Pain", "Obito": "Obito", "Vergil": "Vergil", "Sato": "Satō"}
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
