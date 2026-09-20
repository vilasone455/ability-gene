#!/usr/bin/env python3
"""Author the Paper Bomb kit's clips for Melee Animation: RimArt_TagThrow, RimArt_TagFlick,
RimArt_TagFan and RimArt_TagSeal.

Run: python3 make_paper_bomb_anim.py
Uses the rig, the facings and the aim pivot of the throw animations (make_throw_anim.py) and the
two-hand rig of the clap (make_clap_anim.py), without changing those clips.

The tag scroll stays in the off hand in every clip: a Scroll part on the body, under HandB, which
the throw rig braces across the chest. The main hand goes to the roll, takes what it needs, and
does the work. Each ability has its own motion; none reuses the grenade throw.

The three aimed clips have three files each: east (mirrored for west), north, south, and
ThrowAimWorker turns the hand and what it holds to the exact angle. Their numbers are the lab
sketches' (Tools/VfxLab/web/sketches/paper-bomb-*.js); change them together.

TagThrow  0.6 s. Tag Throw. The hand tears one tag off the roll (0.06), cocks beside the ear and
          whips forward like the kunai throw. The pin and tag leave at 0.30 (the sketch's Release).
TagFlick  2.8 s. Tag Line. The hand takes the strip's free end (0.08), swings back low and flicks
          it forward underhand; the end leaves at 0.30 (the sketch's Flick). The arm stays out over
          the running strip until 2.45, then comes back to the roll and chops down at 2.50 to tear
          the strip off (the sketch's 'Strip laid in' 2.5).
TagFan    1.2 s. Paper Shroud. The hand pulls a fan of tags across to the off shoulder, then sweeps
          forward and round to the throwing side. Tags leave all through the sweep: the first at
          0.35 (the sketch's Wind), the sixth at 0.80 (0.35 + 5 x 0.09), when the fan goes off.
TagSeal   0.95 s. The hand seal before Tag Line and Paper Shroud go off. Not aimed, so one clip,
          the pawn facing south where both hands show. The hands rise and meet at 0.35 (the lib's
          Seal), one 0.04 above the other, hold through the bursts, and part at 0.75.
"""
import json
from pathlib import Path

import make_throw_anim as throw
import make_clap_anim as clap

ROOT = Path(__file__).resolve().parent
STAMP = "2026-09-20T00:00:00.0000000Z"
SCROLL, TAG, TAG_PIN, TAG_FAN_TEXTURE = "RimArt/PaperBomb/TagScroll", "RimArt/PaperBomb/Tag", "RimArt/PaperBomb/TagPin", "RimArt/PaperBomb/Tags"

# Where the throw rig puts the off hand, and so the roll: forward 0.13, across the chest 0.17.
ROLL = (0.13, 0.12, 0.04)   # the main hand's pose when it is at the roll: (forward, side, lift)

THROW_LENGTH, THROW_TEAR, THROW_RELEASE = 0.6, 0.06, 0.30
FLICK_LENGTH, FLICK_TAKE, FLICK_RELEASE, FLICK_TORN = 2.8, 0.08, 0.30, 2.50
FAN_LENGTH, FAN_TAKE, FAN_RELEASE, FAN_LAST = 1.2, 0.10, 0.35, 0.80
SEAL_AT, SEAL_UNTIL, SEAL_LENGTH, SEAL_STACK = 0.35, 0.75, 0.95, 0.04

# Poses are (seconds, forward reach, shoulder-side offset, screen-space lift), as in
# make_throw_anim.py. Side below zero is the throwing side, above zero is across the chest.
TAG_THROW = {
    "name": "RimArt_TagThrow",
    "length": THROW_LENGTH,
    "release": THROW_RELEASE,
    "poses": [
        (0.00,  0.12, -0.23, 0.00),  # ready at the hip, same as the throw clips
        (THROW_TEAR, *ROLL),         # at the roll: pinch one tag
        (0.10,  0.06, -0.02, 0.14),  # torn off, coming up
        (0.19, -0.20, -0.22, 0.46),  # cocked beside the ear
        (0.25, -0.23, -0.21, 0.47),  # hold
        (0.27, -0.15, -0.20, 0.46),  # the whip starts
        (THROW_RELEASE, 0.15, -0.17, 0.43),  # let go mid-whip, at the hand's top speed
        (0.33,  0.44, -0.15, 0.38),  # the empty hand carries on to full extension
        (0.38,  0.50, -0.13, 0.28),
        (0.48,  0.28, -0.18, 0.08),
        (THROW_LENGTH, 0.12, -0.23, 0.00),
    ],
    "body_rot": [(0.00, 0.0), (0.21, -6.0), (THROW_RELEASE, 7.0), (0.38, 10.0), (THROW_LENGTH, 0.0)],
    "body_x": [(0.00, 0.0), (0.21, -0.05), (THROW_RELEASE, 0.07), (0.40, 0.09), (THROW_LENGTH, 0.0)],
    "body_lift": [(0.00, 0.0), (0.21, 0.02), (0.40, -0.02), (THROW_LENGTH, 0.0)],
    # The pin's direction in the clip's frame: 0 is up the screen, +90 is at the target.
    "wrist": [(0.0, 20.0), (THROW_TEAR, 40.0), (0.19, -30.0), (0.25, -35.0), (THROW_RELEASE, 90.0), (0.40, 110.0), (THROW_LENGTH, 20.0)],
    "behind": (0.36, 0.50),
    "texture": TAG_PIN,
    "spin": [(0.00, 0.0), (THROW_RELEASE, 0.0)],
    "item_pos": {"x": 0.0, "z": 0.05},
    "item_y": 0.04,
    "item_scale": 0.36,
    "held": (THROW_TEAR, THROW_RELEASE),
}

TAG_FLICK = {
    **TAG_THROW,
    "name": "RimArt_TagFlick",
    "length": FLICK_LENGTH,
    "release": FLICK_RELEASE,
    "poses": [
        (0.00,  0.12, -0.23,  0.00),
        (FLICK_TAKE, *ROLL),          # take the strip's free end at the roll
        (0.16, -0.18, -0.20, -0.02),  # swung back past the hip, low, the strip following
        (0.22, -0.20, -0.20, -0.03),  # hold
        (0.26, -0.10, -0.20, -0.01),  # the flick starts
        (FLICK_RELEASE, 0.12, -0.20, 0.06),  # the end leaves the fingers mid-swing
        (0.36,  0.40, -0.16,  0.18),
        (0.46,  0.46, -0.14,  0.22),  # arm out over the running strip, palm down
        (1.10,  0.44, -0.14,  0.20),
        (1.80,  0.46, -0.14,  0.21),
        (2.45,  0.44, -0.14,  0.20),
        (FLICK_TORN, 0.16, 0.10, 0.06),      # back at the roll: the strip is torn here
        (2.60,  0.20, -0.10, -0.04),  # the chop carries on down and out
        (FLICK_LENGTH, 0.12, -0.23, 0.00),
    ],
    "body_rot": [(0.00, 0.0), (0.20, -5.0), (FLICK_RELEASE, 8.0), (0.46, 6.0), (2.45, 6.0), (FLICK_LENGTH, 0.0)],
    "body_x": [(0.00, 0.0), (0.20, -0.05), (FLICK_RELEASE, 0.06), (0.46, 0.05), (2.45, 0.05), (FLICK_LENGTH, 0.0)],
    # A small crouch held while the strip runs.
    "body_lift": [(0.00, 0.0), (0.22, -0.03), (2.45, -0.03), (FLICK_LENGTH, 0.0)],
    "wrist": [(0.0, 0.0), (0.16, -30.0), (FLICK_RELEASE, 70.0), (0.46, 90.0), (2.45, 90.0), (2.60, 20.0), (FLICK_LENGTH, 0.0)],
    "behind": (2.54, 2.70),
    "texture": TAG,
    "spin": [(0.00, 0.0), (FLICK_RELEASE, 0.0)],
    "item_pos": {"x": 0.0, "z": 0.06},
    "item_scale": 0.30,
    "held": (FLICK_TAKE, FLICK_RELEASE),
}

TAG_FAN = {
    **TAG_THROW,
    "name": "RimArt_TagFan",
    "length": FAN_LENGTH,
    "release": FAN_RELEASE,
    "poses": [
        (0.00,  0.12, -0.23, 0.00),
        (FAN_TAKE, *ROLL),           # at the roll: take six tags
        (0.22, -0.02,  0.16, 0.12),  # pulled across to the off shoulder, fanned
        (0.30, -0.03,  0.17, 0.12),  # hold
        (FAN_RELEASE, 0.16, 0.10, 0.20),  # the sweep starts and the first tag leaves
        (0.50,  0.42,  0.00, 0.28),  # across the front
        (0.65,  0.48, -0.12, 0.27),
        (FAN_LAST, 0.40, -0.22, 0.20),    # round to the throwing side: the sixth tag leaves
        (0.95,  0.26, -0.22, 0.08),
        (FAN_LENGTH, 0.12, -0.23, 0.00),
    ],
    "body_rot": [(0.00, 0.0), (0.26, -6.0), (0.50, 4.0), (FAN_LAST, 9.0), (FAN_LENGTH, 0.0)],
    "body_x": [(0.00, 0.0), (0.26, -0.03), (0.50, 0.05), (FAN_LAST, 0.07), (FAN_LENGTH, 0.0)],
    "body_lift": [(0.00, 0.0), (FAN_LENGTH, 0.0)],
    # The fan turns with the sweep, clockwise from above for a right hand.
    "wrist": [(0.0, 0.0), (0.22, -60.0), (0.30, -65.0), (FAN_RELEASE, 20.0), (0.65, 90.0), (FAN_LAST, 125.0), (FAN_LENGTH, 0.0)],
    "behind": (0.86, 1.00),
    "texture": TAG_FAN_TEXTURE,
    "spin": [(0.00, 0.0), (FAN_LAST, 0.0)],
    "item_pos": {"x": 0.0, "z": 0.06},
    "item_scale": 0.34,
    "held": (FAN_TAKE, FAN_LAST),
}

STYLES = [TAG_THROW, TAG_FLICK, TAG_FAN]

# (seconds, half-span, lift, splay, body bob, contact), as in make_clap_anim.py.
TAG_SEAL = {
    "name": "RimArt_TagSeal",
    "poses": [
        (0.00, 0.22, 0.06, 0.15,  0.000, False),   # idle, hands at the sides
        (0.18, 0.20, 0.11, 0.30,  0.010, False),   # both hands come up
        (SEAL_AT, clap.TOGETHER, 0.13, 0.00, -0.010, True),    # the seal is made: ignition
        (SEAL_UNTIL, clap.TOGETHER, 0.13, 0.00, -0.010, False),  # held through the bursts
        (SEAL_LENGTH, 0.22, 0.06, 0.15, 0.000, False),
    ],
}


def held_active(style):
    """What the hand holds is off until the hand has been to the roll, and off again once it is let go."""
    tick = 1.0 / 60.0
    taken, gone = style["held"]
    return [(0.0, 0.0), (round(taken - tick, 7), 0.0), (taken, 1.0), (round(gone - tick, 7), 1.0), (gone, 0.0), (style["length"], 0.0)]


def scroll(parent_id, x, y, z, angle, scale=0.46):
    """The tag scroll, not called ItemA so Melee Animation leaves its texture alone (see make_throw_anim.py)."""
    return throw.part(1020, "BodyA/Scroll", "Scroll", parent_id=parent_id, texture=SCROLL,
                      default_overrides={"Transform.m_LocalPosition.x": x, "Transform.m_LocalPosition.y": y, "Transform.m_LocalPosition.z": z,
                                         "Transform.localEulerAnglesRaw.y": angle,
                                         "Transform.m_LocalScale.x": scale, "Transform.m_LocalScale.y": scale, "Transform.m_LocalScale.z": scale})


def build(style, name, direction, turn):
    clip = throw.build(style, name, direction, turn)
    # The throw generator stamps the time it ran, which rewrites every clip on every deploy.
    clip["ExportTimeUTC"] = STAMP
    item = next(p for p in clip["Parts"] if p["CustomName"] == "Grenade")
    item["Curves"]["GameObject.m_IsActive"] = throw.curve(held_active(style), smooth=False)
    # The roll sits in the off hand, one draw step under it so the fingers cover the rod.
    off = next(p for p in clip["Parts"] if p["CustomName"] == "HandB")["DefaultValues"]
    clip["Parts"].append(scroll(1001, off["Transform.m_LocalPosition.x"], off["Transform.m_LocalPosition.y"] - 0.004,
                                off["Transform.m_LocalPosition.z"], -turn))
    return clip


def build_seal():
    clip = clap.build(TAG_SEAL)
    clip["ExportTimeUTC"] = STAMP
    # One hand above the other while the seal is held, so it does not read as the Anchor's clap.
    upper = next(p for p in clip["Parts"] if p["CustomName"] == "HandB")
    zs = [(p[0], p[2] + (SEAL_STACK if p[5] or p[0] == SEAL_UNTIL else 0.0)) for p in TAG_SEAL["poses"]]
    upper["Curves"].update(throw.transform_curves(pos={"z": zs}))
    # The roll is tucked at the belt on the pawn's right, under both hands.
    clip["Parts"].append(scroll(1001, -0.17, 0.03, -0.04, 35.0, scale=0.40))
    return clip


def main():
    out = ROOT / "Animations"
    out.mkdir(exist_ok=True)
    for style in STYLES:
        for suffix, direction, turn in throw.FACINGS:
            name = style["name"] + suffix
            (out / f"{name}.json").write_text(json.dumps(build(style, name, direction, turn), indent=2) + "\n")
            print(f"Wrote {name}.json  (Rot4 {direction})")
        print(f"  {style['name']}: length {style['length']}s, release {style['release']}s, "
              f"ReleaseFraction {style['release'] / style['length']:.4f}  <- must match ThrowAnimation")
    (out / "RimArt_TagSeal.json").write_text(json.dumps(build_seal(), indent=2) + "\n")
    print(f"Wrote RimArt_TagSeal.json: {SEAL_LENGTH}s, seal made at {SEAL_AT}s")


if __name__ == "__main__":
    main()
