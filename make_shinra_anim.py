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
import math
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
#
# The span is small on purpose. Melee Animation draws a hand sprite and no arm, so a hand
# carried well away from the torso is a mitt floating in open ground with nothing joining it
# to the pawn. The peak below puts each hand just outside the body silhouette and no further;
# the gesture is carried by the splay and the held height rather than by distance.
POSES = [
    (0.00, 0.22, 0.06, 0.15,  0.000),  # idle, hands at the sides
    (0.14, 0.15, 0.12, 0.05, -0.020),  # draw in and up toward the chest
    (0.27, 0.11, 0.15, 0.00, -0.045),  # loaded: hands close together, body settled back
    (PUSH, 0.34, 0.16, 0.95,  0.030),  # snap out, matching the pressure-shell release
    (0.48, 0.38, 0.16, 1.00,  0.040),  # peak span, just clear of the body
    (0.76, 0.36, 0.15, 1.00,  0.025),  # hold
    (1.05, 0.27, 0.11, 0.60,  0.010),  # recover
    (LENGTH, 0.22, 0.06, 0.15, 0.000),
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
    # The hands are driven directly, with no holding offset for the aim worker to swing.
    still = [(t, 0.0) for t, _ in all_x]
    return {"ExportTimeUTC": "2026-09-12T00:00:00.0000000Z", "Name": NAME,
            "Length": LENGTH, "Bounds": bounds(all_x, all_z, still, still, body_pos),
            "Events": [], "Parts": parts}


# ---------------------------------------------------------------- Shinra Tensei v2 (proposed)
#
# Two clips for the two versions sketched in Tools/VfxLab/web/sketches/pain-shinra-tensei.js. The
# game still plays RimArt_ShinraPush above until the port switches to these; nothing loads them yet.

# RimArt_ShinraTap, the one-click version: the west hand (HandA) sweeps from the hip out and up to
# shoulder height with the palm opening, bursts at TAP_BURST with a small lean back, holds, and comes
# back down. The east hand barely moves.
TAP_NAME = "RimArt_ShinraTap"
TAP_LENGTH = 0.70
TAP_BURST = 0.22
# (seconds, cast hand out, cast hand lift, cast hand splay, other hand out, other hand lift, body bob)
TAP = [
    (0.00, 0.22, 0.06, 0.15, 0.22, 0.06, 0.000),
    (0.12, 0.30, 0.20, 0.70, 0.21, 0.08, -0.010),
    (TAP_BURST, 0.37, 0.23, 1.00, 0.20, 0.09, 0.030),
    (0.40, 0.36, 0.21, 1.00, 0.20, 0.09, 0.020),
    (TAP_LENGTH, 0.22, 0.06, 0.15, 0.22, 0.06, 0.000),
]

# RimArt_ShinraCharge, the charged version with a hold that moves. From the anime's Konoha charge
# (arms down and out, then a level T, then a raised Y) and Naruto Mobile's spread arms: the hands open
# from the sides to down-and-out while the gesture starts, then the charge segment from HOLD to
# HOLD + CHARGE_SPAN stands for power 0 to 1 and the arms step up with the size: down and out, slowly
# rising (2 cells), a level T from a third (3 cells), a Y above the shoulders from two thirds, up on
# the toes (4 cells), each step with a small overshoot and a tremble that grows with the power. The
# controller seeks to HOLD + power * CHARGE_SPAN while the pawn charges. On release it jumps to the
# release segment of the size reached (RELEASES[i]) and plays it to its end: each one snaps from that
# size's pose to the T push, bursts after PUSH - HOLD, and recovers. One segment per size keeps the
# jump small (at most 0.06 cells, the slow rise inside a size).
CHARGE_NAME = "RimArt_ShinraCharge"
HOLD = 0.27
CHARGE_SPAN = 1.00
OPEN = [(0.00, 0.22, 0.06, 0.15, 0.000), (0.14, 0.26, 0.04, 0.40, -0.010), (HOLD, 0.30, 0.03, 0.70, -0.020)]
# Per size: (half-span, lift, splay, body) at the start and at the end of its third of the charge.
TIERS = [
    ((0.30, 0.03, 0.70, -0.020), (0.33, 0.09, 0.80, -0.020)),   # 2 cells: down and out
    ((0.40, 0.16, 0.95, -0.020), (0.40, 0.19, 0.95, -0.020)),   # 3 cells: level T
    ((0.36, 0.28, 1.00,  0.030), (0.37, 0.31, 1.00,  0.030)),   # 4 cells: Y, up on the toes
]
STEP = 0.15 / 3          # a step takes 0.15 s of a 3 s charge
STEP_OVER = 0.03
TREMBLE_LOW, TREMBLE_HIGH = 0.003, 0.02
KEY = 0.025
SEGMENT = LENGTH - HOLD  # release + burst + recovery, as in RimArt_ShinraPush
GAP = 0.02               # between segments, never played
RELEASES = [round(HOLD + CHARGE_SPAN + GAP + i * (SEGMENT + GAP), 4) for i in range(len(TIERS))]
CHARGE_LENGTH = round(RELEASES[-1] + SEGMENT, 4)


def lerp_pose(a, b, w):
    return tuple(x + (y - x) * w for x, y in zip(a, b))


def hold_pose(u):
    """(half-span, lift, splay, body) at power u."""
    n = len(TIERS)
    i = min(n - 1, int(u * n))
    w = min(1.0, u * n - i)
    pose = lerp_pose(TIERS[i][0], TIERS[i][1], w)
    since = w / n
    if i > 0 and since < STEP * 1.7:
        k = min(1.0, since / STEP)
        k = k * k * (3 - 2 * k)
        over = STEP_OVER * math.sin(math.pi * min(1.0, since / (STEP * 1.7)))
        pose = lerp_pose(TIERS[i - 1][1], pose, k)
        pose = (pose[0], pose[1] + over, pose[2], pose[3])
    return pose


def charge_poses():
    """(seconds, half-span, lift, splay, bob, body x) for RimArt_ShinraCharge."""
    poses = [(t, span, lift, splay, bob, 0.0) for t, span, lift, splay, bob in OPEN]
    steps = round(CHARGE_SPAN / KEY)
    for k in range(1, steps + 1):
        u = k / steps
        span, lift, splay, bob = hold_pose(u)
        amp = TREMBLE_LOW + (TREMBLE_HIGH - TREMBLE_LOW) * u
        shake = amp if k % 2 else -amp
        poses.append((round(HOLD + k * KEY, 4), round(span + shake, 4), round(lift + 0.7 * shake, 4),
                      splay, round(bob, 4), round(-0.4 * shake, 4)))
    after = [(t - HOLD, span, lift, splay, bob) for t, span, lift, splay, bob in POSES if t > HOLD]
    for start, tier in zip(RELEASES, TIERS):
        span, lift, splay, bob = tier[1]
        poses.append((start, span, lift, splay, bob, 0.0))
        poses += [(round(start + dt, 4), sp, li, sl, bo, 0.0) for dt, sp, li, sl, bo in after]
    return poses


def hands_clip(name, length, body_z, body_x, hand_a, hand_b):
    """A two-handed south-facing clip. hand_a/hand_b: lists of (seconds, x, z, rotation)."""
    body_pos = {"x": body_x, "z": body_z}
    body = part(1001, "BodyA", "BodyA", curves=transform_curves(pos=body_pos),
                default_overrides={"PawnBody.Direction": float(SOUTH)})
    head = part(1002, "BodyA/HeadA", "HeadA", parent_id=1001)
    parts = [body, head]
    all_x, all_z = [], []
    for index, keys in enumerate((hand_a, hand_b)):
        xs = [(t, x) for t, x, _, _ in keys]
        zs = [(t, z) for t, _, z, _ in keys]
        rot = [(t, r) for t, _, _, r in keys]
        all_x.extend(xs)
        all_z.extend(zs)
        name_ = "HandA" if index == 0 else "HandB"
        parts.append(part(1003 + index, name_, name_,
                          curves=transform_curves(pos={"x": xs, "z": zs}, rot={"y": rot}),
                          default_overrides={"Transform.m_LocalPosition.y": 0.05 + index * 0.005,
                              "Transform.m_LocalScale.x": HAND_SCALE,
                              "Transform.m_LocalScale.y": HAND_SCALE,
                              "Transform.m_LocalScale.z": HAND_SCALE}))
    still = [(t, 0.0) for t, _ in all_x]
    return {"ExportTimeUTC": "2026-09-29T00:00:00.0000000Z", "Name": name,
            "Length": length, "Bounds": bounds(all_x, all_z, still, still, body_pos),
            "Events": [], "Parts": parts}


def build_tap():
    # The cast hand is HandA on the west (x < 0); splay turns it from south to point west.
    a = [(t, -out, lift, 90.0 + 90.0 * splay) for t, out, lift, splay, _, _, _ in TAP]
    b = [(t, out2, lift2, 90.0 - 90.0 * 0.15) for t, _, _, _, out2, lift2, _ in TAP]
    return hands_clip(TAP_NAME, TAP_LENGTH, [(t, bob) for t, *_, bob in TAP], [(t, 0.0) for t, *_ in TAP], a, b)


def build_charge():
    poses = charge_poses()
    a = [(t, -span, lift, 90.0 + 90.0 * splay) for t, span, lift, splay, _, _ in poses]
    b = [(t, span, lift, 90.0 - 90.0 * splay) for t, span, lift, splay, _, _ in poses]
    return hands_clip(CHARGE_NAME, CHARGE_LENGTH, [(t, bob) for t, _, _, _, bob, _ in poses],
                      [(t, bx) for t, *_, bx in poses], a, b)


def main():
    out = ROOT / "Animations"
    out.mkdir(exist_ok=True)
    path = out / f"{NAME}.json"
    path.write_text(json.dumps(build(), indent=2) + "\n")
    print(f"Wrote {path.name}")
    span = max(span for _, span, _, _, _ in POSES)
    print(f"Arms reach the T at {PUSH}s, {span*2:.2f} cells across; recover by {LENGTH}s.")
    for clip_name, clip in ((TAP_NAME, build_tap()), (CHARGE_NAME, build_charge())):
        (out / f"{clip_name}.json").write_text(json.dumps(clip, indent=2) + "\n")
        print(f"Wrote {clip_name}.json ({clip['Length']:.2f}s)")
    print(f"Tap bursts at {TAP_BURST}s. Charge holds {HOLD}-{HOLD + CHARGE_SPAN:.2f}s (power 0-1); "
          f"release segments at {RELEASES} (2, 3, 4 cells), each bursting {PUSH - HOLD:.2f}s in.")


if __name__ == "__main__":
    main()
