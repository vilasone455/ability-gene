#!/usr/bin/env python3
"""Author the Anchor organ's Mark clips for Melee Animation: RimArt_MarkFlick and RimArt_MarkCatch.

Run: python3 make_mark_anim.py
Uses the rig, the facings and the aim pivot of the throw animations (make_throw_anim.py), without
changing those clips. Three clips each: east (mirrored for west), north, south. Unlike the clap,
Mark has a target within 9.9 cells, so the gesture has a direction; ThrowAimWorker turns the hand
and the card to the exact angle.

Both clips start with the warmup, and AG_AnchorMark's warmupTime is 0.5 s, so 0.50 is the moment
the mark is placed or lifted. Change that number and these clips together.

MarkFlick, placing a mark: a backhand card flick. The hand curls across the chest, snaps out,
and lets the card go at 0.38 with the hand at its top speed. The C# flies a card from there to
the target, landing at 0.50.

MarkCatch, lifting a mark: the hand goes out open and waits. The mark leaves the target at 0.50,
the C# flies it back, and it is in the fingers at 0.62; the hand then brings it in to the chest.
The card part is off until the catch, the reverse of a throw.
"""
import json
from pathlib import Path

import make_throw_anim as throw

ROOT = Path(__file__).resolve().parent
CARD = "RimArt/Anchor/CardBack"

FLICK_LENGTH, FLICK_RELEASE = 0.8, 0.38
CATCH_LENGTH, CATCH_TIME, CATCH_PUT_AWAY = 0.95, 0.62, 0.86

# Poses are (seconds, forward reach, shoulder-side offset, screen-space lift), as in
# make_throw_anim.py. Side below zero is the throwing side, above zero is across the chest.
# In the east clip the side offset is drawn up the screen like the lift, so the curl's lift stays
# at 0.08: side 0.10 plus lift 0.17 put the hand over the face.
FLICK = {
    "name": "RimArt_MarkFlick",
    "length": FLICK_LENGTH,
    "release": FLICK_RELEASE,
    "poses": [
        (0.00,  0.12, -0.23, 0.00),  # ready at the hip, same as the throw clips
        (0.14,  0.10, -0.08, 0.08),  # hand comes up in front of the chest
        (0.26, -0.02,  0.10, 0.08),  # curled across the chest, card by the off shoulder
        (0.30, -0.04,  0.12, 0.08),  # hold, 2 frames
        (0.34,  0.10,  0.04, 0.13),  # the snap starts
        (FLICK_RELEASE, 0.34, -0.08, 0.22),  # the card leaves mid-snap, at the hand's top speed
        (0.42,  0.50, -0.14, 0.27),  # the empty hand carries on to full extension
        (0.50,  0.48, -0.15, 0.25),  # and points at the target as the card lands
        (0.64,  0.28, -0.20, 0.10),
        (FLICK_LENGTH, 0.12, -0.23, 0.00),
    ],
    # A wrist motion, so the body barely moves: a small turn away and back.
    "body_rot": [(0.00, 0.0), (0.28, -4.0), (FLICK_RELEASE, 4.0), (0.50, 5.0), (FLICK_LENGTH, 0.0)],
    "body_x": [(0.00, 0.0), (0.28, -0.02), (FLICK_RELEASE, 0.03), (0.50, 0.04), (FLICK_LENGTH, 0.0)],
    "body_lift": [(0.00, 0.0), (FLICK_LENGTH, 0.0)],
    # The card's long edge in the clip's frame: 0 is up the screen, +90 is at the target. Curled
    # back while loaded, snapped round to the target on release.
    "wrist": [(0.0, 10.0), (0.26, -70.0), (0.30, -75.0), (FLICK_RELEASE, 90.0), (0.50, 100.0),
              (FLICK_LENGTH, 10.0)],
    "behind": (0.42, 0.60),
    "texture": CARD,
    # A quarter turn of its own through the snap: a flicked card leaves the fingers spinning.
    "spin": [(0.00, 0.0), (0.30, 0.0), (FLICK_RELEASE, 90.0)],
    # Held by its near end, drawn just behind the hand so the fingers cover the corner.
    "item_pos": {"x": 0.0, "z": 0.07},
    "item_y": 0.04,
    "item_scale": 0.18,
}

CATCH = {
    **FLICK,
    "name": "RimArt_MarkCatch",
    "length": CATCH_LENGTH,
    # The moment the card part comes on. ThrowAnimation's release fraction is this over the length.
    "release": CATCH_TIME,
    "poses": [
        (0.00,  0.12, -0.23, 0.00),
        (0.20,  0.30, -0.17, 0.22),  # reaching out
        (0.34,  0.48, -0.14, 0.30),  # arm out, hand open toward the mark
        (0.50,  0.50, -0.14, 0.30),  # held: the mark leaves the target here
        (CATCH_TIME, 0.46, -0.14, 0.30),  # the card arrives and the hand gives a little
        (0.68,  0.40, -0.14, 0.29),
        (0.82,  0.12, -0.06, 0.22),  # brought in to the chest
        (CATCH_LENGTH, 0.12, -0.23, 0.00),
    ],
    "body_rot": [(0.00, 0.0), (0.34, 4.0), (CATCH_TIME, 2.0), (CATCH_LENGTH, 0.0)],
    "body_x": [(0.00, 0.0), (0.34, 0.03), (CATCH_TIME, 0.02), (CATCH_LENGTH, 0.0)],
    "body_lift": [(0.00, 0.0), (CATCH_LENGTH, 0.0)],
    "wrist": [(0.0, 10.0), (0.34, 90.0), (CATCH_TIME, 90.0), (0.82, 20.0), (CATCH_LENGTH, 10.0)],
    "behind": (0.34, 0.70),
    "spin": [(0.00, 0.0), (CATCH_LENGTH, 0.0)],
}

STYLES = [FLICK, CATCH]


def caught_active():
    """Off until the catch, on until the hand is back at the chest, where it is put away."""
    tick = 1.0 / 60.0
    return [(0.0, 0.0), (round(CATCH_TIME - tick, 7), 0.0), (CATCH_TIME, 1.0),
            (round(CATCH_PUT_AWAY - tick, 7), 1.0), (CATCH_PUT_AWAY, 0.0), (CATCH_LENGTH, 0.0)]


def build(style, name, direction, turn):
    clip = throw.build(style, name, direction, turn)
    # The throw generator stamps the time it ran, which rewrites every clip on every deploy.
    clip["ExportTimeUTC"] = "2026-09-20T00:00:00.0000000Z"
    if style is CATCH:
        card = next(p for p in clip["Parts"] if p["CustomName"] == "Grenade")
        card["Curves"]["GameObject.m_IsActive"] = throw.curve(caught_active(), smooth=False)
    return clip


def main():
    out = ROOT / "Animations"
    out.mkdir(exist_ok=True)
    for style in STYLES:
        for suffix, direction, turn in throw.FACINGS:
            name = style["name"] + suffix
            (out / f"{name}.json").write_text(json.dumps(build(style, name, direction, turn), indent=2) + "\n")
            print(f"Wrote {name}.json  (Rot4 {direction})")
        print(f"  {style['name']}: length {style['length']}s, card {'caught' if style is CATCH else 'released'} at "
              f"{style['release']}s, ReleaseFraction {style['release'] / style['length']:.4f}  <- must match ThrowAnimation")


if __name__ == "__main__":
    main()
