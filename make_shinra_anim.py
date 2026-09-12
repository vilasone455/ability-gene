#!/usr/bin/env python3
"""Author the empty-hand T-pose clip for Melee Animation.

Run: python3 make_shinra_anim.py
Uses the same JSON/curve helpers as the grenade animation, without changing that clip.

One clip, not one per facing. The wave is centred on the caster and radial, so the gesture
has no direction to carry: the arms go straight out to both sides. The pawn is drawn facing
south for the length of the clip, which is the one facing that shows both arms at full
extension instead of hiding one behind the torso.
"""
import json
import re
from pathlib import Path

from make_throw_anim import part, transform_curves, bounds, HAND_SCALE

ROOT = Path(__file__).resolve().parent
TIMING = (ROOT / "Source/RimArt/Shinra/ShinraVfxTiming.cs").read_text()
PUSH = float(re.search(r"ChargeEnd = ([\d.]+)f", TIMING).group(1))
LENGTH = 1.35
NAME = "RimArt_ShinraPush"
SOUTH = 2

# (seconds, half-span, screen-space lift, hand splay, body bob)
#
# half-span is each hand's distance from the body's centre line, straight out along x.
# lift is the height of both hands up the screen and holds through the extension: hands that
# descend while the arms open read as a strike toward the floor rather than a brace.
# splay turns each hand outward, 0 pointing south with the body, 1 pointing along its own arm.
POSES = [
    (0.00, 0.24, 0.06, 0.15,  0.000),  # idle, arms at the sides
    (0.14, 0.16, 0.12, 0.05, -0.020),  # draw in and up toward the chest
    (0.27, 0.12, 0.15, 0.00, -0.045),  # loaded: hands close together, body settled back
    (PUSH, 0.70, 0.16, 0.95,  0.030),  # snap to the T, matching the pressure-shell release
    (0.48, 0.76, 0.16, 1.00,  0.040),  # peak span
    (0.76, 0.72, 0.15, 1.00,  0.025),  # hold the T
    (1.05, 0.42, 0.11, 0.60,  0.010),  # recover
    (LENGTH, 0.24, 0.06, 0.15, 0.000),
]


def build():
    # The body carries only a small bob; the arms hold their own height so a lean cannot drag
    # the hands down with it.
    body_pos = {"x": [(t, 0.0) for t, *_ in POSES],
                "z": [(t, bob) for t, _, _, _, bob in POSES]}
    body = part(1001, "BodyA", "BodyA", curves=transform_curves(pos=body_pos),
                default_overrides={"PawnBody.Direction": float(SOUTH)})
    head = part(1002, "BodyA/HeadA", "HeadA", parent_id=1001)
    parts = [body, head]
    all_x, all_z = [], []
    for index, sign in enumerate((-1, 1)):
        xs = [(t, sign * span) for t, span, _, _, _ in POSES]
        zs = [(t, lift) for t, _, lift, _, _ in POSES]
        all_x.extend(xs)
        all_z.extend(zs)
        # 90 degrees is south, the way the body faces; each hand turns from there to point
        # along its own arm, west for the left hand and east for the right.
        rot = [(t, 90.0 - sign * 90.0 * splay) for t, _, _, splay, _ in POSES]
        hand = part(1003+index, "HandA" if index == 0 else "HandB",
                    "HandA" if index == 0 else "HandB",
                    curves=transform_curves(pos={"x": xs, "z": zs}, rot={"y": rot}),
                    default_overrides={"Transform.m_LocalPosition.y": 0.05 + index*0.005,
                        "Transform.m_LocalScale.x": HAND_SCALE,
                        "Transform.m_LocalScale.y": HAND_SCALE,
                        "Transform.m_LocalScale.z": HAND_SCALE})
        parts.append(hand)
    return {"ExportTimeUTC": "2026-09-12T00:00:00.0000000Z", "Name": NAME,
            "Length": LENGTH, "Bounds": bounds(all_x, all_z, body_pos), "Events": [], "Parts": parts}


def main():
    out = ROOT / "Animations"
    out.mkdir(exist_ok=True)
    path = out / f"{NAME}.json"
    path.write_text(json.dumps(build(), indent=2) + "\n")
    print(f"Wrote {path.name}")
    span = max(span for _, span, _, _, _ in POSES)
    print(f"Arms reach the T at {PUSH}s, {span*2:.2f} cells across; recover by {LENGTH}s.")


if __name__ == "__main__":
    main()
