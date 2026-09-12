#!/usr/bin/env python3
"""
Writes the grenade-throw animation that Melee Animation plays for this mod.

Melee Animation's own guide (Source/AnimationTutorial/AnimationTutorial.md in their repo)
animates in Unity and exports a json. That json is the real interface - their loader reads
nothing else - and it is a flat bag of keyframe curves, so it can be written directly. This
script does that, for three reasons.

It removes Unity from the build. Nobody has to install a six-gigabyte editor, open a scene
and hand-export a file to change how far back the arm winds.

It makes the animation reviewable. A keyframe table in a diff says "the wind-up got 80ms
longer"; a re-exported 40KB json says nothing at all.

And it makes the timing single-sourced. The release moment is a number here, and the same
number is the one the C# throws the grenade on - see ThrowAnimation.ReleaseFraction, which
this script prints so the two cannot drift apart silently.

What is given up is the visual preview. Melee Animation ships one anyway: dev mode ->
Melee Animation -> Open Debugger -> Animation Starter plays any loaded anim on any pawn.

Run:  python3 make_throw_anim.py
"""
import json
import math
import os
from datetime import datetime, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, "Animations")

# East also covers west through horizontal mirroring. North and south use their own
# facing and rotated choreography so the step, off hand and depth agree with the body.
FACINGS = [
    ("RimArt_ThrowGrenade", 1, 0.0),
    ("RimArt_ThrowGrenadeNorth", 0, 90.0),
    ("RimArt_ThrowGrenadeSouth", 2, -90.0),
]

LENGTH = 1.2

# The instant the grenade leaves the hand, in seconds from the start. Everything before this is
# wind-up, everything after is follow-through with the grenade part hidden.
RELEASE = 0.58

# Unity's exporter stamps every keyframe with these. They are the default unweighted tangent
# settings; reproduced so a file written here is byte-shaped like a file written there.
IN_WEIGHT = 0.333333343
OUT_WEIGHT = 0.333333343
TANGENT_MODE = 136

# Draw depth. Their renderer sorts on world y, so these are layer numbers wearing a position's
# clothes: the grenade sits just in front of the hands that are holding it.
HAND_Y = 0.05
GRENADE_Y = 0.06

# Their hand sprite is authored at this scale in every shipped animation.
HAND_SCALE = 0.175

GRENADE_TEXTURE = "RimArt/Frost/Bomb"


def catmull_tangents(points):
    """
    Smooth tangents across a keyframe list, so the arm accelerates into the throw instead of
    hitting each pose and stopping dead.

    Unity's curves are Hermite: without tangents every segment eases to a halt at both ends,
    which on a throw reads as a series of poses rather than one movement. The finite difference
    used here is what Unity's own 'Auto' tangent mode computes, and the endpoints rest
    at zero velocity so the animation blends into and out of the idle pose.
    """
    n = len(points)
    tangents = []
    for i, (t, v) in enumerate(points):
        prev_t, prev_v = points[i - 1] if i > 0 else (t, v)
        next_t, next_v = points[i + 1] if i < n - 1 else (t, v)
        dt = next_t - prev_t
        tangents.append(0.0 if i in (0, n - 1) or dt <= 0 else (next_v - prev_v) / dt)
    return tangents


def curve(points, smooth=True):
    """
    One animation curve from a list of (time, value) pairs.

    `smooth` off gives flat tangents, which is what a value that should hold and then jump
    wants - visibility, facing direction, anything that is a state rather than a motion.
    """
    tangents = catmull_tangents(points) if smooth else [0.0] * len(points)
    keyframes = []
    for (t, v), tangent in zip(points, tangents):
        key = {}
        # The exporter omits time on the first keyframe when it is zero. Matched here so the
        # file diffs cleanly against one exported from Unity.
        if t != 0:
            key["time"] = round(t, 7)
        key["value"] = round(v, 5)
        if tangent != 0.0:
            key["inTangent"] = round(tangent, 6)
            key["outTangent"] = round(tangent, 6)
        key["inWeight"] = IN_WEIGHT
        key["outWeight"] = OUT_WEIGHT
        key["tangentMode"] = TANGENT_MODE
        keyframes.append(key)

    # 8 is WrapMode.ClampForever: hold the first and last pose rather than looping. A throw
    # that loops is a juggling act.
    return {"Keyframes": keyframes, "PreWrapMode": 8, "PostWrapMode": 8}


def defaults(**overrides):
    """
    The full default-value block every part carries.

    Their loader falls back to DefaultValues for any property without a curve, and a property
    that is in neither is a null curve - which for scale means the part draws at zero size and
    is invisible. So every part gets the complete set, exactly as the Unity exporter writes it.
    """
    values = {
        "GameObject.m_IsActive": 1.0,
        "Transform.m_LocalPosition.x": 0.0,
        "Transform.m_LocalPosition.y": 0.0,
        "Transform.m_LocalPosition.z": 0.0,
        "Transform.localEulerAnglesRaw.x": 0.0,
        "Transform.localEulerAnglesRaw.y": 0.0,
        "Transform.localEulerAnglesRaw.z": 0.0,
        "Transform.m_LocalScale.x": 1.0,
        "Transform.m_LocalScale.y": 1.0,
        "Transform.m_LocalScale.z": 1.0,
        "AnimatedPart.DataA": 0.0,
        "AnimatedPart.DataB": 0.0,
        "AnimatedPart.DataC": 0.0,
        "AnimatedPart.Tint.r": 1.0,
        "AnimatedPart.Tint.g": 1.0,
        "AnimatedPart.Tint.b": 1.0,
        "AnimatedPart.Tint.a": 1.0,
        "AnimatedPart.FlipX": 0.0,
        "AnimatedPart.FlipY": 0.0,
        "AnimatedPart.SplitDrawMode": 0.0,
        "AnimatedPart.FrameIndex": 0.0,
        "PawnBody.Direction": 0.0,
    }
    values.update(overrides)
    return values


def part(part_id, path, custom_name=None, parent_id=0, texture=None,
         curves=None, default_overrides=None):
    model = {
        "ID": part_id,
        "Path": path,
        "CustomName": custom_name,
        "ParentID": parent_id,
        "TexturePath": texture,
        "TransparentByDefault": False,
        "Curves": curves or {},
        "DefaultValues": defaults(**(default_overrides or {})),
        "SplitDrawPivotPartID": 0,
        "SweepPaths": [],
    }
    # A curve always wins over a default, so carrying both is just noise in the file.
    for key in model["Curves"]:
        model["DefaultValues"].pop(key, None)
    return model


def transform_curves(pos=None, rot=None, smooth=True):
    """Position and rotation curves, skipping any axis that never moves."""
    curves = {}
    for axis, points in (pos or {}).items():
        curves[f"Transform.m_LocalPosition.{axis}"] = curve(points, smooth)
    for axis, points in (rot or {}).items():
        curves[f"Transform.localEulerAnglesRaw.{axis}"] = curve(points, smooth)
    return curves


# Overhand poses: (seconds, forward reach, shoulder-side offset, screen-space lift).
# x/z carry the image on screen; y only controls draw order. Rotate forward/side
# with the facing, then add lift to screen z so "above the head" stays up in every clip.
THROW_POSES = [
    (0.00,  0.12, -0.23,  0.00),  # ready at the hip
    (0.16, -0.08, -0.25,  0.28),  # bend the elbow and raise the hand
    (0.32, -0.24, -0.22,  0.64),  # cock the hand beside/behind the head
    (0.42, -0.26, -0.20,  0.72),  # hold the loaded pose briefly
    (0.50, -0.05, -0.18,  0.85),  # swing over the crown
    (RELEASE, 0.27, -0.14, 0.64), # release with the hand still raised
    (0.70,  0.48, -0.10,  0.05),  # drive the empty hand down in front
    (0.82,  0.32, -0.12, -0.15),  # finish low, across the torso
    (1.02,  0.16, -0.20, -0.04),
    (LENGTH, 0.12, -0.23, 0.00),
]
ARC_SAMPLE = 0.02

# Side-view lean: back while loading, forward after release. North/south instead
# show the weight shift along their facing axis, without tilting sideways.
BODY_ROT = [
    (0.00, 0.0),
    (0.32, -12.0),
    (0.42, -15.0),
    (RELEASE, 8.0),
    (0.72, 16.0),
    (0.94, 4.0),
    (LENGTH, 0.0),
]
BODY_X = [
    (0.00, 0.0),
    (0.40, -0.10),
    (RELEASE, 0.09),
    (0.72, 0.14),
    (0.94, 0.04),
    (LENGTH, 0.0),
]
BODY_LIFT = [(0.00, 0.0), (0.40, 0.04), (0.72, -0.06), (LENGTH, 0.0)]


def hermite(points, t):
    """Evaluates a control-point list the same way the exported curves are evaluated."""
    tangents = catmull_tangents(points)
    if t <= points[0][0]:
        return points[0][1]
    if t >= points[-1][0]:
        return points[-1][1]
    for i in range(len(points) - 1):
        t0, v0 = points[i]
        t1, v1 = points[i + 1]
        if t0 <= t <= t1:
            dt = t1 - t0
            u = (t - t0) / dt
            u2, u3 = u * u, u * u * u
            return ((2 * u3 - 3 * u2 + 1) * v0 + (u3 - 2 * u2 + u) * dt * tangents[i]
                    + (-2 * u3 + 3 * u2) * v1 + (u3 - u2) * dt * tangents[i + 1])
    return points[-1][1]


def sample_arc(turn):
    """Project the overhead hand poses into the pawn's facing and screen space."""
    forward = [(p[0], p[1]) for p in THROW_POSES]
    side = [(p[0], p[2]) for p in THROW_POSES]
    lift = [(p[0], p[3]) for p in THROW_POSES]
    facing_x, facing_z = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    xs, zs, rots = [], [], []
    steps = int(round(LENGTH / ARC_SAMPLE))
    times = sorted({round(min(LENGTH, i * ARC_SAMPLE), 7) for i in range(steps + 1)}
                   | {p[0] for p in THROW_POSES})
    # Wrist rolls over with the throw instead of rotating around the pawn's waist.
    wrist = [(0.0, -20.0), (0.42, -100.0), (RELEASE, -30.0),
             (0.82, 65.0), (LENGTH, -20.0)]
    for t in times:
        reach = hermite(forward, t) + hermite(BODY_X, t)
        shoulder = hermite(side, t)
        height = hermite(lift, t) + hermite(BODY_LIFT, t)
        xs.append((t, reach * facing_x - shoulder * facing_z))
        zs.append((t, reach * facing_z + shoulder * facing_x + height))
        rots.append((t, hermite(wrist, t) - turn))
    return xs, zs, rots


# The grenade is drawn until the hand opens, then it is the projectile's problem. Held flat so
# the value does not ramp down across the release frame and leave a half-faded grenade.
GRENADE_ACTIVE = [
    (0.00, 1.0),
    (round(RELEASE - 1.0 / 60.0, 7), 1.0),
    (RELEASE, 0.0),
    (LENGTH, 0.0),
]

# Slow tumble in the hand. It is barely visible and it is the difference between a grenade and
# a sticker of a grenade.
# A slow tumble of its own, on top of the arm's rotation which already carries it around the
# arc. Small, because the arm is doing most of the turning now.
GRENADE_SPIN = [
    (0.00, 0.0),
    (RELEASE, -40.0),
]


def build(name, direction, turn):
    holding_x, holding_z, holding_rot = sample_arc(turn)
    facing_x, facing_z = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    body_times = sorted({t for t, _ in BODY_X} | {t for t, _ in BODY_LIFT})
    body_pos = {
        "x": [(t, hermite(BODY_X, t) * facing_x) for t in body_times],
        "z": [(t, hermite(BODY_X, t) * facing_z + hermite(BODY_LIFT, t))
              for t in body_times],
    }
    # Keep the raised hand visible beside the head. Only the north-facing low
    # follow-through moves behind the torso; screen height alone is not depth.
    holding_y = [(0.0, 0.02), (RELEASE, 0.02),
                 (0.70, -0.12 if direction == 0 else 0.02),
                 (0.94, -0.12 if direction == 0 else 0.02), (LENGTH, 0.02)]

    body = part(
        1001, "BodyA", "BodyA",
        curves=transform_curves(pos=body_pos, rot={"y": [(t, v * facing_x) for t, v in BODY_ROT]}),
        # Direction is a state, not a motion: a curve would interpolate the pawn through
        # north on its way from east to west.
        default_overrides={"PawnBody.Direction": float(direction)},
    )

    head = part(1002, "BodyA/HeadA", "HeadA", parent_id=1001)

    # An invisible parent. Their exporter gives these a fully transparent tint rather than
    # deactivating them, because a deactivated part takes its children with it.
    holding = part(
        1003, "PawnAHolding",
        curves=transform_curves(pos={"x": holding_x, "y": holding_y, "z": holding_z}, rot={"y": holding_rot}),
        default_overrides={"AnimatedPart.Tint.a": 0.0},
    )

    # Melee Animation fills in the hand texture and the pawn's own skin colour at runtime, in
    # ConfigureHandsForPawn. Both hands have to exist even when only one is shown: their
    # lookup of the off hand is guarded by the main hand's null check, so a rig with HandA and
    # no HandB throws inside their code rather than ours.
    hand_a = part(
        1004, "PawnAHolding/HandA", "HandA", parent_id=1003,
        default_overrides={
            "Transform.m_LocalPosition.y": HAND_Y,
            "Transform.m_LocalScale.x": HAND_SCALE,
            "Transform.m_LocalScale.y": HAND_SCALE,
            "Transform.m_LocalScale.z": HAND_SCALE,
        },
    )

    # Parented to the body, not to the throwing arm.
    #
    # Their rig hangs both hands off PawnAHolding because both hands hold the weapon in an
    # execution. A throw only has one hand on the grenade, and leaving the off hand on that
    # cluster made it orbit the pawn on the end of the same throwing path - two hands swinging
    # round in formation, which is the one thing a top-down camera would show clearly. It sits
    # on the body instead, braced across the chest, and only turns as the body turns.
    #
    # Parentage is free to change: Melee Animation finds both hands by name, in GetPart, and
    # never looks at where they sit in the hierarchy.
    hand_b = part(
        1005, "BodyA/HandB", "HandB", parent_id=1001,
        default_overrides={
            "Transform.m_LocalPosition.x": 0.13 * facing_x - 0.17 * facing_z,
            "Transform.m_LocalPosition.y": -0.07 if direction == 0 else HAND_Y - 0.01,
            "Transform.m_LocalPosition.z": 0.13 * facing_z + 0.17 * facing_x,
            "Transform.m_LocalScale.x": HAND_SCALE,
            "Transform.m_LocalScale.y": HAND_SCALE,
            "Transform.m_LocalScale.z": HAND_SCALE,
        },
    )

    # Deliberately not called ItemA. Their AddPawn looks up a part by that name and, finding
    # one, overwrites its texture with whatever melee weapon the pawn is carrying - which for a
    # grenade throw would put a sword in the pawn's hand. A part under any other name is left
    # alone, so the texture set here is the texture that draws.
    grenade = part(
        1006, "PawnAHolding/Grenade", "Grenade", parent_id=1003,
        texture=GRENADE_TEXTURE,
        curves={
            "GameObject.m_IsActive": curve(GRENADE_ACTIVE, smooth=False),
            **transform_curves(rot={"y": GRENADE_SPIN}),
        },
        default_overrides={
            "Transform.m_LocalPosition.x": 0.04,
            "Transform.m_LocalPosition.y": GRENADE_Y,
            "Transform.m_LocalScale.x": 0.42,
            "Transform.m_LocalScale.y": 0.42,
            "Transform.m_LocalScale.z": 0.42,
        },
    )

    parts = [body, head, holding, hand_a, hand_b, grenade]

    return {
        "ExportTimeUTC": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.0000000Z"),
        "Name": name,
        "Length": LENGTH,
        "Bounds": bounds(holding_x, holding_z, body_pos),
        "Events": [],
        "Parts": parts,
    }


def bounds(holding_x, holding_z, body_pos):
    """
    The rectangle the animation plays inside, which their renderer uses for culling. Computed
    from the arm's travel with a cell of padding, so a throw at the edge of the screen does not
    vanish halfway through.
    """
    xs = [v for _, v in holding_x] + [v for _, v in body_pos["x"]]
    zs = [v for _, v in holding_z] + [v for _, v in body_pos["z"]]
    pad = 1.0
    x0, x1 = min(xs) - pad, max(xs) + pad
    z0, z1 = min(zs) - pad, max(zs) + pad
    return {"x": round(x0, 4), "y": round(z0, 4),
            "width": round(x1 - x0, 4), "height": round(z1 - z0, 4)}


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    for name, direction, turn in FACINGS:
        path = os.path.join(OUT_DIR, f"{name}.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(build(name, direction, turn), f, indent=2)
            f.write("\n")
        print(f"Wrote {path}  (Rot4 {direction}, arc turned {turn:+.0f} deg)")

    release_fraction = RELEASE / LENGTH
    print(f"  length          {LENGTH}s ({round(LENGTH * 60)} ticks)")
    print(f"  release         {RELEASE}s ({round(RELEASE * 60)} ticks)")
    print(f"  ReleaseFraction {release_fraction:.4f}  <- must match ThrowAnimation.ReleaseFraction")


if __name__ == "__main__":
    main()
