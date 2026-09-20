#!/usr/bin/env python3
"""Author the Power Pole's clips for Melee Animation: the hands and body for Extend Thrust, Sweep
and the plant that starts Vault Strike.

Run: python3 make_power_pole_anim.py
Uses the JSON/curve helpers and the aim pivot of the throw animations (make_throw_anim.py), without
changing those clips.

The clips draw no pole. The C# draws it (Source/RimArt/PowerPole), long, from the same numbers, so
a clip only has to put two hands where that pole is: every constant below is copied from
PowerPoleGraphics.cs, PowerPoleThrust.cs, PowerPoleSweep.cs and PowerPoleStrike.cs. Change them
together.

Four clips per ability: east, north, south and west. The throws mirror their east clip for west,
but a mirrored Sweep would swing right to left while the pole swings left to right, so west is its
own clip here and nothing is mirrored. ThrowAimWorker turns everything under PawnALift by what is
left between the clip's direction and the real aim, at most 45 degrees.

Each clip starts with the ability's warmup (0.3 s on all three) and runs on the cast job's clock.
Thrust and Sweep end when the pole is back to its carried length. Plant ends at the warmup's end:
the wielder then leaves the map inside the vault's flyer, which no clip can follow.
"""
import json
import math
from pathlib import Path

from make_throw_anim import part, transform_curves, curve, HAND_SCALE

ROOT = Path(__file__).resolve().parent
STAMP = "2026-09-21T00:00:00.0000000Z"

# (suffix, Rot4, degrees of the clip's direction: 0 east, 90 north)
FACINGS = [("East", 1, 0.0), ("North", 0, 90.0), ("South", 2, -90.0), ("West", 3, 180.0)]

LIFT = 0.60            # SixPathsHeight.Lift: cells north per cell of height
HAND_HEIGHT = 0.5      # PowerPoleGraphics.HandHeight
REST_BACK = -0.45      # PowerPoleGraphics.RestBack: the carried staff's back end, from the feet
GRIPS = (0.05, 0.42)   # the two hands, cells from the feet along the pole (the sketches' stand-in hands)
BODY_SHARE = 0.4       # how much of the pole's slide the body leans with

# Extend Thrust: PowerPoleThrustTiming.
T_WINDUP, T_EXTEND, T_PUSH, T_HOLD, T_RETRACT = 0.3, 0.15, 0.22, 0.15, 0.3
PULL_BACK, LUNGE = 0.35, 0.25
# Sweep: PowerPoleSweepTiming, with the ability's 180 degree arc.
S_WINDUP, S_SWING, S_HOLD, S_RETRACT, S_HALF = 0.3, 0.4, 0.1, 0.25, 90.0
# Plant: PowerPoleStrikeTiming. The staff's tip goes to FOOT on the ground, its hand end to TOP.
P_WINDUP, FOOT, TOP_BACK, TOP_HEIGHT, REST_TIP, BOW = 0.3, 0.4, -0.1, 1.1, 0.75, 0.45
PLANT_GRIPS = (0.18, 0.5)   # cells from the hand end

FRAME = 1.0 / 30.0

# The hands' altitude above the clip's root, which is the pawn layer. The C# draws the pole on the
# overhead mote layer so that it passes over every pawn on its way: 5 altitude layers of 0.36585
# above the pawn layer, 1.829, plus up to 0.046 for the pole's own parts. A hand at the throws'
# 0.05 is under the pole, and the pole is wide enough to hide it. Altitude does not move anything
# on screen; it only decides what covers what.
HAND_Y = 1.95


def smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def ease_out(t):
    t = min(1.0, max(0.0, t))
    return 1 - (1 - t) ** 3


def times(length):
    count = int(round(length / FRAME))
    return [round(min(length, i * FRAME), 7) for i in range(count)] + [length]


def thrust_slide(t):
    """PowerPoleThrustTiming.BackAt minus the rest place: how far the pole, and the hands on it, have slid."""
    hit, retract = T_WINDUP + T_EXTEND, T_WINDUP + T_EXTEND + T_PUSH + T_HOLD
    if t < T_WINDUP:
        return -PULL_BACK * smooth(t / T_WINDUP)
    if t < hit:
        return -PULL_BACK + (PULL_BACK + LUNGE) * ease_out((t - T_WINDUP) / T_EXTEND)
    if t < retract:
        return LUNGE
    return LUNGE * (1 - smooth((t - retract) / T_RETRACT))


def sweep_phi(t):
    """PowerPoleSweepTiming.AngleAt: degrees from the aim, positive to the wielder's left."""
    swung, retract = S_WINDUP + S_SWING, S_WINDUP + S_SWING + S_HOLD
    if t < S_WINDUP:
        return S_HALF * smooth(t / S_WINDUP)
    if t < swung:
        return S_HALF - 2 * S_HALF * smooth((t - S_WINDUP) / S_SWING)
    if t < retract:
        return -S_HALF
    return -S_HALF * (1 - smooth((t - retract) / S_RETRACT))


def rig(name, direction, length, body_pos, lift_pos, holding, hands):
    """
    BodyA and HeadA; PawnALift, the invisible aim pivot; PawnAHolding under it, invisible, which
    carries the pole's direction; and the two hands. `hands` is one dict of curves and defaults per
    hand. Both hands sit under the pivot, because both are on the pole.
    """
    parts = [
        part(1001, "BodyA", "BodyA", curves=transform_curves(pos=body_pos), default_overrides={"PawnBody.Direction": float(direction)}),
        part(1002, "BodyA/HeadA", "HeadA", parent_id=1001),
        part(1007, "PawnALift", "PawnALift", curves=transform_curves(pos=lift_pos), default_overrides={"AnimatedPart.Tint.a": 0.0}),
        part(1003, "PawnALift/PawnAHolding", parent_id=1007, curves=holding, default_overrides={"AnimatedPart.Tint.a": 0.0}),
    ]
    for index, (hand_name, hand) in enumerate(zip(("HandA", "HandB"), hands)):
        parts.append(part(1004 + index, "PawnALift/PawnAHolding/" + hand_name, hand_name, parent_id=1003, curves=hand["curves"],
                          default_overrides={"Transform.m_LocalPosition.y": HAND_Y + index * 0.005,
                                             "Transform.m_LocalScale.x": HAND_SCALE, "Transform.m_LocalScale.y": HAND_SCALE,
                                             "Transform.m_LocalScale.z": HAND_SCALE, **hand.get("defaults", {})}))
    reach = 1.5
    return {"ExportTimeUTC": STAMP, "Name": name, "Length": length,
            "Bounds": {"x": -reach - 1, "y": -reach - 1, "width": 2 * (reach + 1), "height": 2 * (reach + 1)},
            "Events": [], "Parts": parts}


def thrust(name, direction, turn):
    length = T_WINDUP + T_EXTEND + T_PUSH + T_HOLD + T_RETRACT
    fx, fz = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    ts = times(length)
    step = [(t, BODY_SHARE * thrust_slide(t)) for t in ts]
    body = {"x": [(t, s * fx) for t, s in step], "z": [(t, s * fz) for t, s in step]}
    lift = {"x": body["x"], "z": [(t, s * fz + HAND_HEIGHT * LIFT) for t, s in step]}
    # A turn about +y is clockwise seen from above, so a pole along `turn` degrees is turned by -turn.
    holding = transform_curves(rot={"y": [(0.0, -turn), (length, -turn)]})
    # The hands slide with the pole; the body has already taken its share of that.
    hands = [{"curves": transform_curves(pos={"x": [(t, grip + (1 - BODY_SHARE) * thrust_slide(t)) for t in ts]})} for grip in reversed(GRIPS)]
    return rig(name, direction, length, body, lift, holding, hands)


def sweep(name, direction, turn):
    length = S_WINDUP + S_SWING + S_HOLD + S_RETRACT
    ts = times(length)
    still = [(0.0, 0.0), (length, 0.0)]
    lift = {"x": still, "z": [(0.0, HAND_HEIGHT * LIFT), (length, HAND_HEIGHT * LIFT)]}
    holding = transform_curves(rot={"y": [(t, -(turn + sweep_phi(t))) for t in ts]})
    hands = [{"curves": {}, "defaults": {"Transform.m_LocalPosition.x": grip}} for grip in reversed(GRIPS)]
    return rig(name, direction, length, {"x": still, "z": still}, lift, holding, hands)


def plant(name, direction, turn):
    """The staff goes from carried to planted: its tip to the ground ahead, its hand end up over the head."""
    length = P_WINDUP
    fx, fz = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    ts = times(length)
    still = [(0.0, 0.0), (length, 0.0)]

    def ends(t):
        k = smooth(t / P_WINDUP)
        tip = (REST_TIP + (FOOT - REST_TIP) * k, HAND_HEIGHT * (1 - k))
        top = (REST_BACK + (TOP_BACK - REST_BACK) * k, HAND_HEIGHT + (TOP_HEIGHT - HAND_HEIGHT) * k)
        return tip, top

    def grip(t, along_pole):
        tip, top = ends(t)
        run = math.hypot(tip[0] - top[0], tip[1] - top[1])
        share = along_pole / run
        return top[0] + (tip[0] - top[0]) * share, top[1] + (tip[1] - top[1]) * share

    # The pivot rides at the two hands' mean height, so the aim turn moves the reach and not the height.
    mean = [(t, sum(grip(t, g)[1] for g in PLANT_GRIPS) / 2) for t in ts]
    lift = {"x": [(t, BOW * abs(fz) * h) for t, h in mean], "z": [(t, h * LIFT) for t, h in mean]}
    hands = []
    for g in PLANT_GRIPS:
        xs, zs, rot = [], [], []
        for (t, h_mean) in mean:
            along, h = grip(t, g)
            xs.append((t, along * fx + BOW * abs(fz) * (h - h_mean)))
            zs.append((t, along * fz + (h - h_mean) * LIFT))
            tip, top = ends(t)
            sx = (tip[0] - top[0]) * fx + BOW * abs(fz) * (tip[1] - top[1])
            sz = (tip[0] - top[0]) * fz + (tip[1] - top[1]) * LIFT
            rot.append((t, -math.degrees(math.atan2(sz, sx))))
        hands.append({"curves": transform_curves(pos={"x": xs, "z": zs}, rot={"y": rot})})
    return rig(name, direction, length, {"x": still, "z": still}, lift, {}, hands)


CLIPS = [("RimArt_PoleThrust", thrust), ("RimArt_PoleSweep", sweep), ("RimArt_PolePlant", plant)]


def main():
    out = ROOT / "Animations"
    out.mkdir(exist_ok=True)
    for prefix, build in CLIPS:
        for suffix, direction, turn in FACINGS:
            clip = build(prefix + suffix, direction, turn)
            (out / f"{prefix}{suffix}.json").write_text(json.dumps(clip, indent=2) + "\n")
        print(f"Wrote {prefix}East/North/South/West.json: {clip['Length']:.2f} s")


if __name__ == "__main__":
    main()
