#!/usr/bin/env python3
"""
Writes the throw animations that Melee Animation plays for this mod: the grenade throw (frost
bomb, mimic beacon), the kunai throw and the makibishi scatter. Each is a set of three clips, one per facing.

Melee Animation's own guide (Source/AnimationTutorial/AnimationTutorial.md in their repo)
animates in Unity and exports a json. That json is the real interface - their loader reads
nothing else - and it is a flat bag of keyframe curves, so it can be written directly. This
script does that, for three reasons.

It removes Unity from the build. Nobody has to install a six-gigabyte editor, open a scene
and hand-export a file to change how far back the arm winds.

It makes the animation reviewable. A keyframe table in a diff says "the wind-up got 80ms
longer"; a re-exported 40KB json says nothing at all.

And it makes the timing single-sourced. The release moment is a number here, and the same
number is the one the C# throws on - see ThrowAnimation.Grenade, .Kunai and .Scatter, whose
release fractions this script prints and ApiChecks compares against the written json.

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

# Three clips per throw style, the usual 3/4 top-down set. East also covers west through
# horizontal mirroring. North and south use their own facing so the step, off hand and depth
# agree with the body.
#
# The clip is authored for its own direction only. The exact aim is applied at draw time by
# RimArt.MeleeAnimation.ThrowAimWorker, which turns everything under PawnALift (the throwing
# hand and the held item) around that part's position by the difference between the clip
# direction and the target, at most 45 degrees. The body keeps its sprite facing, the same way
# RimWorld draws a pawn aiming a gun diagonally: body in one of four facings, weapon at the real
# angle.
# (suffix, Rot4, arc turn in degrees)
FACINGS = [
    ("", 1, 0.0),
    ("North", 0, 90.0),
    ("South", 2, -90.0),
]

# Unity's exporter stamps every keyframe with these. They are the default unweighted tangent
# settings; reproduced so a file written here is byte-shaped like a file written there.
IN_WEIGHT = 0.333333343
OUT_WEIGHT = 0.333333343
TANGENT_MODE = 136

# Draw depth. Their renderer sorts on world y, so these are layer numbers wearing a position's
# clothes: the grenade sits just in front of the hands that are holding it.
HAND_Y = 0.05

# Their hand sprite is authored at this scale in every shipped animation.
HAND_SCALE = 0.175


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


# ---------------------------------------------------------------- grenade

# The instant the grenade leaves the hand, in seconds from the start. Everything before this is
# wind-up, everything after is follow-through with the grenade part hidden.
GRENADE_LENGTH = 1.2
GRENADE_RELEASE = 0.58

# Overhand poses: (seconds, forward reach, shoulder-side offset, screen-space lift).
# x/z carry the image on screen; y only controls draw order. Rotate forward/side
# with the facing, then add lift to screen z so "above the head" stays up in every clip.
GRENADE = {
    "name": "RimArt_ThrowGrenade",
    "length": GRENADE_LENGTH,
    "release": GRENADE_RELEASE,
    "poses": [
        (0.00,  0.12, -0.23,  0.00),  # ready at the hip
        (0.16, -0.08, -0.25,  0.28),  # bend the elbow and raise the hand
        (0.32, -0.24, -0.22,  0.64),  # cock the hand beside/behind the head
        (0.42, -0.26, -0.20,  0.72),  # hold the loaded pose briefly
        (0.50, -0.05, -0.18,  0.85),  # swing over the crown
        (GRENADE_RELEASE, 0.27, -0.14, 0.64),  # release with the hand still raised
        (0.70,  0.48, -0.10,  0.05),  # drive the empty hand down in front
        (0.82,  0.32, -0.12, -0.15),  # finish low, across the torso
        (1.02,  0.16, -0.20, -0.04),
        (GRENADE_LENGTH, 0.12, -0.23, 0.00),
    ],
    # Side-view lean: back while loading, forward after release. North/south instead
    # show the weight shift along their facing axis, without tilting sideways.
    "body_rot": [
        (0.00, 0.0),
        (0.32, -12.0),
        (0.42, -15.0),
        (GRENADE_RELEASE, 8.0),
        (0.72, 16.0),
        (0.94, 4.0),
        (GRENADE_LENGTH, 0.0),
    ],
    "body_x": [
        (0.00, 0.0),
        (0.40, -0.10),
        (GRENADE_RELEASE, 0.09),
        (0.72, 0.14),
        (0.94, 0.04),
        (GRENADE_LENGTH, 0.0),
    ],
    "body_lift": [(0.00, 0.0), (0.40, 0.04), (0.72, -0.06), (GRENADE_LENGTH, 0.0)],
    # Wrist rolls over with the throw instead of rotating around the pawn's waist.
    "wrist": [(0.0, -20.0), (0.42, -100.0), (GRENADE_RELEASE, -30.0),
              (0.82, 65.0), (GRENADE_LENGTH, -20.0)],
    # When a north-facing hand drops behind the torso. Only the low follow-through does;
    # screen height alone is not depth.
    "behind": (0.70, 0.94),
    "texture": "RimArt/Frost/Bomb",
    # A slow tumble of its own, on top of the arm's rotation which already carries it around the
    # arc. Small, because the arm is doing most of the turning now.
    "spin": [(0.00, 0.0), (GRENADE_RELEASE, -40.0)],
    "item_pos": {"x": 0.04, "z": 0.0},
    "item_y": 0.06,
    "item_scale": 0.42,
}


# ---------------------------------------------------------------- kunai

# A knife throw, not a lob. Half the length of the grenade clip. The hand comes up and cocks
# beside the ear with the blade pointing up and back, holds for 4 frames, then whips forward
# at shoulder height. The kunai leaves the hand with the arm extended and the blade pointing at
# the target, and the hand barely drops through the release: the grenade swings over the crown
# and finishes low, this one moves in a flat line. Short follow-through, small body lean.
KUNAI_LENGTH = 0.6
KUNAI_RELEASE = 0.3

KUNAI = {
    "name": "RimArt_ThrowKunai",
    "length": KUNAI_LENGTH,
    "release": KUNAI_RELEASE,
    "poses": [
        (0.00,  0.12, -0.23,  0.00),  # ready at the hip, same as the grenade clip
        (0.10,  0.00, -0.24,  0.30),  # hand comes up
        (0.18, -0.20, -0.22,  0.46),  # cocked beside the ear
        (0.24, -0.23, -0.21,  0.47),  # hold
        (KUNAI_RELEASE, 0.34, -0.16, 0.42),  # arm extended at shoulder height
        (0.38,  0.46, -0.13,  0.26),  # short follow-through, forward and down
        (0.48,  0.26, -0.18,  0.08),
        (KUNAI_LENGTH, 0.12, -0.23, 0.00),
    ],
    "body_rot": [
        (0.00, 0.0),
        (0.20, -6.0),
        (KUNAI_RELEASE, 7.0),
        (0.38, 10.0),
        (KUNAI_LENGTH, 0.0),
    ],
    "body_x": [
        (0.00, 0.0),
        (0.20, -0.05),
        (KUNAI_RELEASE, 0.07),
        (0.40, 0.09),
        (KUNAI_LENGTH, 0.0),
    ],
    "body_lift": [(0.00, 0.0), (0.20, 0.02), (0.40, -0.02), (KUNAI_LENGTH, 0.0)],
    # Blade direction in the clip's frame: 0 is up the screen, -90 is back, +90 is at the
    # target. Up and back while cocked, snapped round to point at the target on release.
    "wrist": [(0.0, 20.0), (0.18, -30.0), (0.24, -35.0), (KUNAI_RELEASE, 90.0),
              (0.40, 110.0), (KUNAI_LENGTH, 20.0)],
    "behind": (0.36, 0.50),
    "texture": "RimArt/Kunai/Kunai",
    # No tumble: a thrown knife that spins in the hand is one nobody is holding.
    "spin": [(0.00, 0.0), (KUNAI_RELEASE, 0.0)],
    # The texture's centre is the base of the blade. Shifted along the blade so the handle, not
    # the blade, sits in the hand, and drawn just behind the hand so the fingers cover the grip.
    "item_pos": {"x": 0.0, "z": 0.08},
    "item_y": 0.04,
    "item_scale": 0.40,
}

# ---------------------------------------------------------------- makibishi scatter

# An underhand toss of a handful of spikes at the ground a few cells away, not a throw at a
# target. The hand swings back past the hip, low, holds for 4 frames, then sweeps forward and up
# at waist height. The handful leaves the palm early in the forward swing, while the hand is still
# below the chest, and the arm follows through forward and up. No hand ever goes above the
# shoulder: the grenade clip lobs over the crown, the kunai clip whips from beside the ear.
SCATTER_LENGTH = 0.7
SCATTER_RELEASE = 0.3

SCATTER = {
    "name": "RimArt_ThrowScatter",
    "length": SCATTER_LENGTH,
    "release": SCATTER_RELEASE,
    "poses": [
        (0.00,  0.12, -0.23,  0.00),  # ready at the hip, same as the other clips
        (0.12, -0.10, -0.26, -0.02),  # swing back past the hip
        (0.20, -0.22, -0.24, -0.04),  # wound back, low
        (0.24, -0.23, -0.23, -0.04),  # hold
        (SCATTER_RELEASE, 0.20, -0.20, 0.10),  # sweeping forward at waist height: open the hand
        (0.40,  0.40, -0.15,  0.22),  # follow through forward and up, palm up
        (0.54,  0.26, -0.20,  0.10),
        (SCATTER_LENGTH, 0.12, -0.23, 0.00),
    ],
    "body_rot": [
        (0.00, 0.0),
        (0.20, -4.0),
        (SCATTER_RELEASE, 8.0),
        (0.42, 10.0),
        (SCATTER_LENGTH, 0.0),
    ],
    "body_x": [
        (0.00, 0.0),
        (0.20, -0.05),
        (SCATTER_RELEASE, 0.06),
        (0.42, 0.08),
        (SCATTER_LENGTH, 0.0),
    ],
    # A small crouch into the wind-back, standing up through the release.
    "body_lift": [(0.00, 0.0), (0.20, -0.03), (0.42, 0.0), (SCATTER_LENGTH, 0.0)],
    # Palm faces back on the wind-back and turns up and forward through the release.
    "wrist": [(0.0, 0.0), (0.20, -20.0), (SCATTER_RELEASE, 60.0), (0.42, 90.0), (SCATTER_LENGTH, 0.0)],
    # North-facing follow-through reaches past the body, away from the camera.
    "behind": (0.34, 0.54),
    "texture": "RimArt/Makibishi/Handful",
    "spin": [(0.00, 0.0), (SCATTER_RELEASE, 0.0)],
    # Sits in the palm, drawn in front of the hand.
    "item_pos": {"x": 0.0, "z": 0.04},
    "item_y": 0.06,
    "item_scale": 0.36,
}

# One full-sized folding weapon, with the release clock owned by its throw job.
FUMA = {
    **GRENADE,
    "name": "RimArt_ThrowFuma", "length": 1.2, "release": 0.8,
    "poses": [(0,0.12,-0.23,0), (0.20,0.04,-0.30,0.15),
              (0.42,-0.24,-0.40,0.20), (0.65,-0.30,-0.38,0.20),
              (0.80,0.40,-0.10,0.20), (0.96,0.42,0.20,0.12), (1.2,0.12,-0.23,0)],
    "body_rot": [(0,0),(.5,-12),(.8,10),(1.2,0)],
    "body_x": [(0,0),(.5,-.06),(.8,.10),(1.2,0)],
    "body_lift": [(0,0),(1.2,0)],
    "wrist": [(0,0),(.5,-25),(.8,65),(1.2,0)],
    "behind": (0.96,1.1), "spin": [(0,0),(.8,0)],
    "texture": "RimArt/Fuma/Ring", "item_scale": 1.4,
    "item_pos": {"x": 0, "z": 0}, "item_y": HAND_Y - 0.005,
}
STYLES = [GRENADE, KUNAI, SCATTER, FUMA]

ARC_SAMPLE = 0.02


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


def sample_arc(style, turn):
    """
    Project the hand poses into the pawn's facing and screen space, split into two parts.

    PawnALift carries what must not turn with the aim: the body's forward shift and the screen
    height of the hand. PawnAHolding, its child, carries the ground-plane reach and shoulder
    offset and the wrist angle, which is what the aim worker rotates. Their sum is the hand.
    """
    poses, length = style["poses"], style["length"]
    forward = [(p[0], p[1]) for p in poses]
    side = [(p[0], p[2]) for p in poses]
    lift = [(p[0], p[3]) for p in poses]
    facing_x, facing_z = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    lift_xs, lift_zs, xs, zs, rots = [], [], [], [], []
    steps = int(round(length / ARC_SAMPLE))
    times = sorted({round(min(length, i * ARC_SAMPLE), 7) for i in range(steps + 1)}
                   | {p[0] for p in poses})
    for t in times:
        step = hermite(style["body_x"], t)
        reach = hermite(forward, t)
        shoulder = hermite(side, t)
        height = hermite(lift, t) + hermite(style["body_lift"], t)
        lift_xs.append((t, step * facing_x))
        lift_zs.append((t, step * facing_z + height))
        xs.append((t, reach * facing_x - shoulder * facing_z))
        zs.append((t, reach * facing_z + shoulder * facing_x))
        rots.append((t, hermite(style["wrist"], t) - turn))
    return (lift_xs, lift_zs), (xs, zs, rots)


def item_active(style):
    """
    The thrown thing is drawn until the hand opens, then it is the projectile's problem. Held
    flat so the value does not ramp down across the release frame and leave a half-faded item.
    """
    release, length = style["release"], style["length"]
    return [
        (0.00, 1.0),
        (round(release - 1.0 / 60.0, 7), 1.0),
        (release, 0.0),
        (length, 0.0),
    ]


def build(style, name, direction, turn):
    (lift_x, lift_z), (holding_x, holding_z, holding_rot) = sample_arc(style, turn)
    facing_x, facing_z = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    body_x, body_lift = style["body_x"], style["body_lift"]
    body_times = sorted({t for t, _ in body_x} | {t for t, _ in body_lift})
    body_pos = {
        "x": [(t, hermite(body_x, t) * facing_x) for t in body_times],
        "z": [(t, hermite(body_x, t) * facing_z + hermite(body_lift, t))
              for t in body_times],
    }
    # Keep the raised hand visible beside the head. Only the north-facing follow-through
    # moves behind the torso.
    behind_from, behind_to = style["behind"]
    holding_y = [(0.0, 0.02), (style["release"], 0.02),
                 (behind_from, -0.12 if direction == 0 else 0.02),
                 (behind_to, -0.12 if direction == 0 else 0.02), (style["length"], 0.02)]

    body = part(
        1001, "BodyA", "BodyA",
        curves=transform_curves(pos=body_pos, rot={"y": [(t, v * facing_x) for t, v in style["body_rot"]]}),
        # Direction is a state, not a motion: a curve would interpolate the pawn through
        # north on its way from east to west.
        default_overrides={"PawnBody.Direction": float(direction)},
    )

    head = part(1002, "BodyA/HeadA", "HeadA", parent_id=1001)

    # Invisible parents. Their exporter gives these a fully transparent tint rather than
    # deactivating them, because a deactivated part takes its children with it.
    #
    # PawnALift is the aim pivot: the worker rotates its children around its position. Named,
    # because the worker finds it with GetPart, which matches CustomName.
    lift = part(
        1007, "PawnALift", "PawnALift",
        curves=transform_curves(pos={"x": lift_x, "z": lift_z}),
        default_overrides={"AnimatedPart.Tint.a": 0.0},
    )
    holding = part(
        1003, "PawnALift/PawnAHolding", parent_id=1007,
        curves=transform_curves(pos={"x": holding_x, "y": holding_y, "z": holding_z}, rot={"y": holding_rot}),
        default_overrides={"AnimatedPart.Tint.a": 0.0},
    )

    # Melee Animation fills in the hand texture and the pawn's own skin colour at runtime, in
    # ConfigureHandsForPawn. Both hands have to exist even when only one is shown: their
    # lookup of the off hand is guarded by the main hand's null check, so a rig with HandA and
    # no HandB throws inside their code rather than ours.
    hand_a = part(
        1004, "PawnALift/PawnAHolding/HandA", "HandA", parent_id=1003,
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
        1006, "PawnALift/PawnAHolding/Grenade", "Grenade", parent_id=1003,
        texture=style["texture"],
        curves={
            "GameObject.m_IsActive": curve(item_active(style), smooth=False),
            **transform_curves(rot={"y": style["spin"]}),
        },
        default_overrides={
            "Transform.m_LocalPosition.x": style["item_pos"]["x"],
            "Transform.m_LocalPosition.y": style["item_y"],
            "Transform.m_LocalPosition.z": style["item_pos"]["z"],
            "Transform.m_LocalScale.x": style["item_scale"],
            "Transform.m_LocalScale.y": style["item_scale"],
            "Transform.m_LocalScale.z": style["item_scale"],
        },
    )

    parts = [body, head, lift, holding, hand_a, hand_b, grenade]
    if style["name"] == "RimArt_ThrowFuma":
        # The ring and each blade have the same centred pivot. The fan opens smoothly
        # before release; all five visible pieces disappear on the release frame.
        for i in range(4):
            blade = part(1010+i, f"PawnALift/PawnAHolding/FumaBlade{i}",
                         f"FumaBlade{i}", parent_id=1003, texture="RimArt/Fuma/Blade",
                         curves={"GameObject.m_IsActive": curve(item_active(style), smooth=False),
                                 **transform_curves(rot={"y": [(0,142+8*i),(.15,142+8*i),(.48,90*i),(.8,90*i)]})},
                         default_overrides={"Transform.m_LocalPosition.y": HAND_Y-.01-i*.001,
                                            "Transform.m_LocalScale.x": 1.4,
                                            "Transform.m_LocalScale.y": 1.4,
                                            "Transform.m_LocalScale.z": 1.4})
            parts.append(blade)

    return {
        "ExportTimeUTC": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.0000000Z"),
        "Name": name,
        "Length": style["length"],
        "Bounds": bounds(lift_x, lift_z, holding_x, holding_z, body_pos),
        "Events": [],
        "Parts": parts,
    }


def bounds(lift_x, lift_z, holding_x, holding_z, body_pos):
    """
    The rectangle the animation plays inside, which their renderer uses for culling. Computed
    from the arm's travel with a cell of padding, so a throw at the edge of the screen does not
    vanish halfway through.

    The hand is PawnALift plus PawnAHolding, and the aim worker can turn PawnAHolding to any
    angle around PawnALift, so each sample counts as a circle of the holding offset's length.
    """
    xs = [v for _, v in body_pos["x"]]
    zs = [v for _, v in body_pos["z"]]
    for (_, lx), (_, lz), (_, hx), (_, hz) in zip(lift_x, lift_z, holding_x, holding_z):
        r = math.hypot(hx, hz)
        xs += [lx - r, lx + r]
        zs += [lz - r, lz + r]
    pad = 1.0
    x0, x1 = min(xs) - pad, max(xs) + pad
    z0, z1 = min(zs) - pad, max(zs) + pad
    return {"x": round(x0, 4), "y": round(z0, 4),
            "width": round(x1 - x0, 4), "height": round(z1 - z0, 4)}


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

        length, release = style["length"], style["release"]
        print(f"  {style['name']}")
        print(f"    length          {length}s ({round(length * 60)} ticks)")
        print(f"    release         {release}s ({round(release * 60)} ticks)")
        print(f"    ReleaseFraction {release / length:.4f}  <- must match ThrowAnimation")


if __name__ == "__main__":
    main()
