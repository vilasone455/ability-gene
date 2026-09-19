#!/usr/bin/env python3
"""
Writes the two Rasengan clips that Melee Animation plays, three facings each.

  RimArt_RasenganForm    0.60 s  the warmup. The hand goes out low at the side, palm up, and
                                 holds there while the ball grows in it. The body settles back.
                                 The last pose is held for as long as the warmup lasts, because
                                 a finished clip clamps on its last frame.
  RimArt_RasenganThrust  0.85 s  starts from that same pose. Two frames of pull-back, then the
                                 palm is driven to full reach in 0.03 s: contact at 0.06 s. The
                                 arm keeps pressing for 0.30 s (the grind), the ball bursts at
                                 0.36 s, and the hand recoils and returns to the hip.

Two clips and not one because a teleported Rasengan has the jump between them: the pawn forms
the ball where it stands, arrives behind the target facing the other way, and thrusts there.
The touch-range version plays them back to back.

There is no run and no throw. Nothing leaves the hand, so neither clip has an item part: the
ball is drawn by the ability's own graphics at the hand's position. The rig is otherwise the
throw rig (PawnALift / PawnAHolding / HandA), so RimArt.MeleeAnimation.ThrowAimWorker can turn
the thrust toward the exact target the same way it turns a throw.

The contact and burst times are the sketch's Reach and Press constants
(Tools/VfxLab/web/sketches/kunai-rasengan.js). Change them in both places.

Run:  python3 make_rasengan_anim.py
"""
import json
import math
import os
from datetime import datetime, timezone

from make_throw_anim import (FACINGS, HAND_SCALE, HAND_Y, OUT_DIR, bounds, hermite, part,
                             sample_arc, transform_curves)

FORM_LENGTH = 0.6
THRUST_LENGTH = 0.85
CONTACT = 0.06   # the palm reaches the target
BURST = 0.36     # the ball goes off; CONTACT + the 0.30 s grind

# The pose the two clips share: the hand held out low at the side. (forward, side, lift)
HELD = (0.20, -0.27, 0.14)
HELD_BODY_X, HELD_BODY_ROT, HELD_BODY_LIFT, HELD_WRIST = -0.04, -5.0, -0.01, 70.0

# Poses: (seconds, forward reach, shoulder-side offset, screen-space lift), as in make_throw_anim.
FORM = {
    "name": "RimArt_RasenganForm",
    "length": FORM_LENGTH,
    "poses": [
        (0.00, 0.12, -0.23, 0.00),   # ready at the hip, same as the throw clips
        (0.12, 0.16, -0.26, 0.06),   # the hand turns palm up and moves out from the body
        (0.30, 0.20, -0.27, 0.12),   # held out low at the side; the ball is a third grown
        (0.45, 0.20, -0.27, 0.13),   # steadying it
        (FORM_LENGTH, *HELD),
    ],
    # Weight goes back and the torso turns a little away from the hand: bracing, not winding up.
    "body_rot": [(0.00, 0.0), (0.30, -4.0), (FORM_LENGTH, HELD_BODY_ROT)],
    "body_x": [(0.00, 0.0), (0.30, -0.03), (FORM_LENGTH, HELD_BODY_X)],
    "body_lift": [(0.00, 0.0), (FORM_LENGTH, HELD_BODY_LIFT)],
    "wrist": [(0.00, 20.0), (0.30, 60.0), (FORM_LENGTH, HELD_WRIST)],
}

THRUST = {
    "name": "RimArt_RasenganThrust",
    "length": THRUST_LENGTH,
    "poses": [
        (0.00, *HELD),
        (0.03, 0.05, -0.25, 0.20),          # two frames of pull-back
        (CONTACT, 0.55, -0.12, 0.26),       # palm driven to full reach at chest height
        (0.10, 0.62, -0.10, 0.26),          # pressing in
        (BURST, 0.66, -0.09, 0.26),         # still pressing when the ball goes off
        (0.42, 0.50, -0.12, 0.24),          # the burst throws the hand back
        (0.60, 0.30, -0.20, 0.10),
        (THRUST_LENGTH, 0.12, -0.23, 0.00),
    ],
    # The body goes in behind the palm: a 0.2-cell step forward and a lean over the front foot,
    # held through the grind.
    "body_rot": [(0.00, HELD_BODY_ROT), (0.03, -8.0), (CONTACT, 12.0), (BURST, 16.0),
                 (0.45, 6.0), (THRUST_LENGTH, 0.0)],
    "body_x": [(0.00, HELD_BODY_X), (0.03, -0.07), (CONTACT, 0.16), (BURST, 0.22),
               (0.45, 0.12), (THRUST_LENGTH, 0.0)],
    "body_lift": [(0.00, HELD_BODY_LIFT), (CONTACT, -0.04), (BURST, -0.05), (THRUST_LENGTH, 0.0)],
    "wrist": [(0.00, HELD_WRIST), (CONTACT, 90.0), (BURST, 90.0), (0.60, 40.0),
              (THRUST_LENGTH, 20.0)],
}
STYLES = [FORM, THRUST]


def build(style, name, direction, turn):
    (lift_x, lift_z), (holding_x, holding_z, holding_rot) = sample_arc(style, turn)
    facing_x, facing_z = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    body_x, body_lift = style["body_x"], style["body_lift"]
    body_times = sorted({t for t, _ in body_x} | {t for t, _ in body_lift})
    body_pos = {
        "x": [(t, hermite(body_x, t) * facing_x) for t in body_times],
        "z": [(t, hermite(body_x, t) * facing_z + hermite(body_lift, t)) for t in body_times],
    }
    body = part(1001, "BodyA", "BodyA",
                curves=transform_curves(pos=body_pos, rot={"y": [(t, v * facing_x) for t, v in style["body_rot"]]}),
                default_overrides={"PawnBody.Direction": float(direction)})
    head = part(1002, "BodyA/HeadA", "HeadA", parent_id=1001)
    # Same invisible parents as the throw rig. PawnALift is the aim pivot.
    lift = part(1007, "PawnALift", "PawnALift", curves=transform_curves(pos={"x": lift_x, "z": lift_z}),
                default_overrides={"AnimatedPart.Tint.a": 0.0})
    # The hand stays in front of the body in every facing: held out at the side it is outside
    # the torso's outline, and at full reach it is past it.
    holding = part(1003, "PawnALift/PawnAHolding", parent_id=1007,
                   curves=transform_curves(pos={"x": holding_x, "z": holding_z}, rot={"y": holding_rot}),
                   default_overrides={"AnimatedPart.Tint.a": 0.0, "Transform.m_LocalPosition.y": 0.02})
    hand_scale = {"Transform.m_LocalScale.x": HAND_SCALE, "Transform.m_LocalScale.y": HAND_SCALE,
                  "Transform.m_LocalScale.z": HAND_SCALE}
    hand_a = part(1004, "PawnALift/PawnAHolding/HandA", "HandA", parent_id=1003,
                  default_overrides={"Transform.m_LocalPosition.y": HAND_Y, **hand_scale})
    # The off hand is braced across the chest on the body, as in the throw clips. Both hands
    # have to exist: Melee Animation looks the off hand up inside the main hand's null check.
    hand_b = part(1005, "BodyA/HandB", "HandB", parent_id=1001,
                  default_overrides={
                      "Transform.m_LocalPosition.x": 0.13 * facing_x - 0.17 * facing_z,
                      "Transform.m_LocalPosition.y": -0.07 if direction == 0 else HAND_Y - 0.01,
                      "Transform.m_LocalPosition.z": 0.13 * facing_z + 0.17 * facing_x,
                      **hand_scale})
    return {
        "ExportTimeUTC": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.0000000Z"),
        "Name": name,
        "Length": style["length"],
        "Bounds": bounds(lift_x, lift_z, holding_x, holding_z, body_pos),
        "Events": [],
        "Parts": [body, head, lift, holding, hand_a, hand_b],
    }


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for style in STYLES:
        for suffix, direction, turn in FACINGS:
            name = style["name"] + suffix
            path = os.path.join(OUT_DIR, f"{name}.json")
            with open(path, "w", encoding="utf-8") as f:
                json.dump(build(style, name, direction, turn), f, indent=2)
                f.write("\n")
            print(f"Wrote {path}  (Rot4 {direction}, arc turned {turn:+.0f} deg)")
        print(f"  {style['name']}: length {style['length']}s ({round(style['length'] * 60)} ticks)")
    print(f"  thrust: contact {CONTACT}s ({round(CONTACT * 60)} ticks), burst {BURST}s ({round(BURST * 60)} ticks)")
    reach = max(p[1] for p in THRUST["poses"]) + max(v for _, v in THRUST["body_x"])
    print(f"  thrust: the palm ends {reach:.2f} cells in front of where the pawn stands")


if __name__ == "__main__":
    main()
