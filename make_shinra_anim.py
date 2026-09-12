#!/usr/bin/env python3
"""Author three empty-hand push clips for Melee Animation, covering all four facings.

Run: python3 make_shinra_anim.py
Uses the same JSON/curve helpers as the grenade animation, without changing that clip.
"""
import json
import math
import re
from pathlib import Path

from make_throw_anim import part, transform_curves, bounds, HAND_SCALE

ROOT = Path(__file__).resolve().parent
TIMING = (ROOT / "Source/RimArt/Shinra/ShinraVfxTiming.cs").read_text()
PUSH = float(re.search(r"ChargeEnd = ([\d.]+)f", TIMING).group(1))
LENGTH = 1.35
FACINGS = [("RimArt_ShinraPush", 1, 0), ("RimArt_ShinraPushNorth", 0, 90),
           ("RimArt_ShinraPushSouth", 2, -90)]


def build(name, direction, turn):
    fx, fz = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    # Draw both hands back, drive the palms out together, briefly hold, then settle.
    # (seconds, forward reach, shoulder spread, screen-space lift, body lean)
    poses = [(0.0, 0.10, 0.22, 0.0, 0.0),
             (0.16, -0.02, 0.26, 0.10, -0.025),
             (0.27, -0.10, 0.28, 0.16, -0.055),
             (PUSH, 0.55, 0.23, 0.13, 0.045),
             (0.48, 0.62, 0.24, 0.10, 0.06),
             (0.76, 0.56, 0.24, 0.08, 0.04),
             (1.05, 0.29, 0.23, 0.03, 0.015),
             (LENGTH, 0.10, 0.22, 0.0, 0.0)]
    body_pos = {"x": [(t, lean*fx) for t, _, _, _, lean in poses],
                "z": [(t, lean*fz) for t, _, _, _, lean in poses]}
    body = part(1001, "BodyA", "BodyA", curves=transform_curves(pos=body_pos),
                default_overrides={"PawnBody.Direction": float(direction)})
    head = part(1002, "BodyA/HeadA", "HeadA", parent_id=1001)
    parts = [body, head]
    all_x, all_z = [], []
    for index, sign in enumerate((-1, 1)):
        xs = [(t, (reach+lean)*fx - sign*spread*fz) for t, reach, spread, lift, lean in poses]
        zs = [(t, (reach+lean)*fz + sign*spread*fx + lift) for t, reach, spread, lift, lean in poses]
        all_x.extend(xs)
        all_z.extend(zs)
        # North hands pass behind the torso; their extension carries them past its silhouette.
        depth = -0.07 if direction == 0 else 0.05 + index*0.005
        hand = part(1003+index, "HandA" if index == 0 else "HandB",
                    "HandA" if index == 0 else "HandB",
                    curves=transform_curves(pos={"x": xs, "z": zs},
                        rot={"y": [(t, -turn + sign*(8 if t < PUSH else 3)) for t, *_ in poses]}),
                    default_overrides={"Transform.m_LocalPosition.y": depth,
                        "Transform.m_LocalScale.x": HAND_SCALE,
                        "Transform.m_LocalScale.y": HAND_SCALE,
                        "Transform.m_LocalScale.z": HAND_SCALE})
        parts.append(hand)
    return {"ExportTimeUTC": "2026-09-12T00:00:00.0000000Z", "Name": name,
            "Length": LENGTH, "Bounds": bounds(all_x, all_z, body_pos), "Events": [], "Parts": parts}


def main():
    out = ROOT / "Animations"
    out.mkdir(exist_ok=True)
    for name, direction, turn in FACINGS:
        path = out / f"{name}.json"
        path.write_text(json.dumps(build(name, direction, turn), indent=2) + "\n")
        print(f"Wrote {path.name}")
    print(f"Both palms thrust at {PUSH}s, matching the pressure-shell release; recover by {LENGTH}s.")


if __name__ == "__main__":
    main()
