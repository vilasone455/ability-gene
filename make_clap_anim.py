#!/usr/bin/env python3
"""Author the two clap clips for Melee Animation: RimArt_Clap and RimArt_ClapTwice.

Run: python3 make_clap_anim.py
Uses the JSON/curve helpers of the throw animations, without changing those clips.

One clip per ability, not one per facing. The clap is map-wide and sightless, so the gesture
has no direction to carry. The pawn is drawn facing south for the length of the clip, which is
the one facing that shows both hands - same reasoning as make_shinra_anim.py.

The clip is meant to start with the warmup, so the last contact is the warmup's end and the
swap happens under the hands: 0.50 s for the clap, 0.75 s for the double clap. Those two
numbers are AG_AnchorClap's and AG_AnchorDoubleClap's warmupTime; change them together.
"""
import json
from pathlib import Path

from make_throw_anim import part, transform_curves, curve, HAND_SCALE, IN_WEIGHT, OUT_WEIGHT, TANGENT_MODE

ROOT = Path(__file__).resolve().parent
SOUTH = 2

# Half the distance between the two hand centres when the palms are together. The hand sprite
# is 0.175 cells across, so this overlaps them by about a third: two hands pressed flat, not
# one hand and not two hands with air between them.
TOGETHER = 0.06

# How much faster than the average closing speed the hands are moving when they meet. 1 would
# be a constant-speed close; 3 is the most a Hermite segment takes without overshooting.
STRIKE = 2.0

# (seconds, half-span, lift, splay, body bob, contact)
#
# half-span is each hand's distance from the body's centre line, straight out along x.
# lift is the height of both hands up the screen.
# splay turns each hand outward, 0 pointing south with the body, 1 pointing along its own arm.
# body bob is the body's shift up the screen. The pawn faces south, so positive is leaning back
# and negative is leaning into the clap.
# contact marks a key where the palms meet: the hands arrive at speed and stop dead, where
# every other key is smoothed through.
#
# The span never passes 0.36. Melee Animation draws a hand sprite and no arm, so a hand carried
# further from the torso is a mitt floating in open ground - see the note in make_shinra_anim.py.
CLAP = {
    "name": "RimArt_Clap",
    "poses": [
        (0.00, 0.22, 0.06, 0.15,  0.000, False),  # idle, hands at the sides
        (0.30, 0.34, 0.08, 0.35,  0.030, False),  # opened wide at chest height, body settled back
        (0.42, 0.34, 0.08, 0.35,  0.030, False),  # held wide: the beat before the strike
        (0.50, TOGETHER, 0.05, 0.00, -0.030, True),   # contact, 0.08 s close; the swap lands here
        (0.58, TOGETHER, 0.05, 0.00, -0.020, False),  # palms held together
        (0.75, 0.30, 0.13, 0.90, -0.010, False),  # the reveal: hands open, palms out, raised
        (0.95, 0.22, 0.06, 0.15,  0.000, False),
    ],
}

CLAP_TWICE = {
    "name": "RimArt_ClapTwice",
    "poses": [
        (0.00, 0.22, 0.06, 0.15,  0.000, False),
        (0.30, 0.34, 0.08, 0.35,  0.030, False),
        (0.42, 0.34, 0.08, 0.35,  0.030, False),
        (0.50, TOGETHER, 0.05, 0.00, -0.030, True),   # first contact: palm star only
        (0.54, TOGETHER, 0.05, 0.00, -0.020, False),
        (0.64, 0.17, 0.06, 0.20,  0.015, False),  # reopened a hand's width, not the full wind-up
        (0.75, TOGETHER, 0.05, 0.00, -0.035, True),   # second contact; the swap lands here
        (0.83, TOGETHER, 0.05, 0.00, -0.020, False),
        (1.00, 0.30, 0.13, 0.90, -0.010, False),  # the reveal
        (1.20, 0.22, 0.06, 0.15,  0.000, False),
    ],
}

CLIPS = [CLAP, CLAP_TWICE]


def span_curve(poses, sign):
    """
    The hand's x position. A contact key gets a steep tangent coming in and a flat one going
    out, which the shared curve() cannot write because it gives every key one tangent for both
    sides. Smoothed through, the hands would slow down into the clap and drift through each
    other after it.
    """
    points = [(t, sign * span) for t, span, *_ in poses]
    result = curve(points)
    for i, (pose, key) in enumerate(zip(poses, result["Keyframes"])):
        if not pose[5]:
            continue
        (t0, v0), (t1, v1) = points[i - 1], points[i]
        key["inTangent"] = round(STRIKE * (v1 - v0) / (t1 - t0), 6)
        key["outTangent"] = 0.0
        key["inWeight"], key["outWeight"], key["tangentMode"] = IN_WEIGHT, OUT_WEIGHT, TANGENT_MODE
    return result


def bounds(poses):
    """Hands' travel plus a cell of padding; their renderer culls on this rectangle."""
    span = max(p[1] for p in poses)
    lifts = [p[2] for p in poses] + [p[4] for p in poses]
    pad = 1.0
    return {"x": round(-span - pad, 4), "y": round(min(lifts) - pad, 4),
            "width": round(2 * (span + pad), 4), "height": round(max(lifts) - min(lifts) + 2 * pad, 4)}


def build(clip):
    poses = clip["poses"]
    body_pos = {"x": [(p[0], 0.0) for p in poses], "z": [(p[0], p[4]) for p in poses]}
    body = part(1001, "BodyA", "BodyA", curves=transform_curves(pos=body_pos),
                default_overrides={"PawnBody.Direction": float(SOUTH)})
    head = part(1002, "BodyA/HeadA", "HeadA", parent_id=1001)
    parts = [body, head]
    for index, sign in enumerate((-1, 1)):
        zs = [(p[0], p[2]) for p in poses]
        # 90 degrees is south, the way the body faces; each hand turns from there to point
        # along its own arm, west for the left hand and east for the right.
        rot = [(p[0], 90.0 - sign * 90.0 * p[3]) for p in poses]
        curves = transform_curves(pos={"z": zs}, rot={"y": rot})
        curves["Transform.m_LocalPosition.x"] = span_curve(poses, sign)
        name = "HandA" if index == 0 else "HandB"
        parts.append(part(1003 + index, name, name, curves=curves,
                          default_overrides={"Transform.m_LocalPosition.y": 0.05 + index * 0.005,
                                             "Transform.m_LocalScale.x": HAND_SCALE,
                                             "Transform.m_LocalScale.y": HAND_SCALE,
                                             "Transform.m_LocalScale.z": HAND_SCALE}))
    return {"ExportTimeUTC": "2026-09-19T00:00:00.0000000Z", "Name": clip["name"],
            "Length": poses[-1][0], "Bounds": bounds(poses), "Events": [], "Parts": parts}


def main():
    out = ROOT / "Animations"
    out.mkdir(exist_ok=True)
    for clip in CLIPS:
        path = out / f"{clip['name']}.json"
        path.write_text(json.dumps(build(clip), indent=2) + "\n")
        contacts = ", ".join(f"{p[0]:.2f}s" for p in clip["poses"] if p[5])
        print(f"Wrote {path.name}: {clip['poses'][-1][0]:.2f}s, palms meet at {contacts}")


if __name__ == "__main__":
    main()
