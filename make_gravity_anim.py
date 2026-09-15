#!/usr/bin/env python3
"""Author Gravity Well's own one-hand gather, clench and recovery clip."""
import json
from pathlib import Path
from make_throw_anim import part, transform_curves, bounds, curve, HAND_SCALE

ROOT = Path(__file__).resolve().parent


def build():
    times = [0.0, 0.2, 0.5, 0.65, 0.8, 1.0, 1.2]
    body_pos = {"x": [(t, 0.0) for t in times],
                "z": list(zip(times, [0.0, -0.015, -0.035, -0.035, 0.015, 0.0, 0.0]))}
    parts = [part(1001, "BodyA", "BodyA", curves=transform_curves(pos=body_pos),
                  default_overrides={"PawnBody.Direction": 2.0}),
             part(1002, "BodyA/HeadA", "HeadA", parent_id=1001)]
    all_x, all_z = [], []
    for i, name in enumerate(["HandA", "HandB"]):
        xs = list(zip(times, [0.22, 0.26, 0.34, 0.32, 0.23, 0.22, 0.22] if i == 0 else [-0.22]*7))
        zs = list(zip(times, [0.04, 0.12, 0.22, 0.23, 0.13, 0.04, 0.04] if i == 0 else [0.04]*7))
        angles = list(zip(times, [90, 65, 20, 35, 100, 90, 90] if i == 0 else [90]*7))
        scales = list(zip(times, [HAND_SCALE, HAND_SCALE, HAND_SCALE, HAND_SCALE*0.85,
                                 HAND_SCALE*0.65, HAND_SCALE, HAND_SCALE] if i == 0 else [HAND_SCALE]*7))
        all_x.extend(xs); all_z.extend(zs)
        curves = transform_curves(pos={"x": xs, "z": zs}, rot={"y": angles})
        for axis in ("x", "y", "z"):
            curves[f"Transform.m_LocalScale.{axis}"] = curve(scales)
        parts.append(part(1003+i, name, name, curves=curves,
                          default_overrides={"Transform.m_LocalPosition.y": 0.055+i*0.005,
                                             "Transform.m_LocalScale.x": HAND_SCALE,
                                             "Transform.m_LocalScale.y": HAND_SCALE,
                                             "Transform.m_LocalScale.z": HAND_SCALE}))
    return {"ExportTimeUTC": "2026-09-14T00:00:00.0000000Z", "Name": "RimArt_GravityChannel",
            "Length": 1.2, "Bounds": bounds(all_x, all_z, [], [], body_pos), "Events": [], "Parts": parts}


if __name__ == "__main__":
    path = ROOT / "Animations/RimArt_GravityChannel.json"
    path.write_text(json.dumps(build(), indent=2) + "\n")
    print(f"Wrote {path.name}")
