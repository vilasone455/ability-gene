#!/usr/bin/env python3
"""Textures for the Samehada kit: the item and the two ability icons. Requires Pillow.

Run from any directory: python3 make_samehada_textures.py

Samehada.png       256 px. The item on the floor and in menus, level with the tip to the right: a
                   dark wrapped grip with a bone pommel, the blade widening toward a blunt mouth with
                   two teeth, the grip half under a cream bandage with diagonal seams, the tip half
                   bare with rows of lit scales. Colours are lib/samehada.js's (Hide, HideLit, Scale,
                   ScaleLit, ScaleEdge, Bandage, BandageSeam, Bone). The held blade is not this
                   texture: Patch_Samehada_HeldDrawing draws it from code, longer with each charge.
IconSharkSkin.png  128 px each, white with the shape in the alpha, as Core's ability icons are.
IconFusion.png     Shark Skin: a blade pointing up-right with three scales standing off each side and
                   two torn strips falling away. Fusion: a shark's dorsal fin rising out of a wave line.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Samehada"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

# lib/samehada.js
HIDE, HIDE_LIT = (31, 36, 56), (61, 71, 102)
SCALE_C, SCALE_LIT, SCALE_EDGE = (66, 77, 107), (163, 184, 219), (13, 15, 28)
BANDAGE, SEAM, BANDAGE_LIT = (204, 194, 163), (133, 120, 97), (235, 227, 204)
BONE = (204, 199, 179)
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)


def canvas(size):
    return Image.new("RGBA", (size * SCALE, size * SCALE), CLEAR)


def finish(image, size, name):
    image.resize((size, size), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


def half_width(u):
    """lib/samehada.js halfWidth, as a share of the blade's length (0.17 of 1.0 at the widest)."""
    smooth = lambda t: (lambda c: c * c * (3 - 2 * c))(max(0.0, min(1.0, t)))
    if u < 0.8:
        return 0.07 + (0.17 - 0.07) * smooth(u / 0.8)
    return 0.17 + (0.13 - 0.17) * smooth((u - 0.8) / 0.2)


def scale_shape(draw, cx, cz, w, h, angle, colour):
    """The lib's scale mesh: a shield pointing along angle (radians, 0 = right)."""
    pts = [(0, 0.5), (0.45, 0.15), (0.35, -0.5), (-0.35, -0.5), (-0.45, 0.15)]
    ca, sa = math.cos(angle), math.sin(angle)
    out = []
    for x, z in pts:
        # z is along the point direction, x across it
        px, pz = x * w, z * h
        out.append((cx + pz * ca - px * sa, cz - (pz * sa + px * ca)))
    draw.polygon(out, fill=colour)


def weapon():
    size = 256
    s = size * SCALE
    image = canvas(size)
    d = ImageDraw.Draw(image)
    y = 0.5 * s
    grip0, grip1, tip = 0.05 * s, 0.25 * s, 0.97 * s
    length = tip - grip1
    k = length  # half-width unit: the lib's widths are in blade lengths of 1.0
    # Grip, pommel and wraps.
    d.rectangle([grip0, y - 0.03 * s, grip1, y + 0.03 * s], fill=HIDE + (255,))
    for i in range(4):
        x = grip0 + (i + 0.6) * (grip1 - grip0) / 4.5
        d.line([(x - 0.012 * s, y + 0.035 * s), (x + 0.012 * s, y - 0.035 * s)], fill=SEAM + (255,), width=int(0.012 * s))
    d.ellipse([grip0 - 0.035 * s, y - 0.035 * s, grip0 + 0.035 * s, y + 0.035 * s], fill=BONE + (255,))
    d.ellipse([grip0 - 0.018 * s, y - 0.02 * s, grip0 + 0.004 * s, y + 0.002 * s], fill=HIDE + (255,))
    # Blade outline: edge band, hide face, lit line.
    n = 40
    top = [(grip1 + length * i / n, y - (half_width(i / n) if i < n else 0.05) * k * 0.62) for i in range(n + 1)]
    bottom = [(grip1 + length * i / n, y + (half_width(i / n) if i < n else 0.05) * k * 0.62) for i in range(n + 1)]
    grow = lambda pts, f: [(x, y + (py - y) * f) for x, py in pts]
    d.polygon(grow(top, 1.12) + list(reversed(grow(bottom, 1.12))), fill=SCALE_EDGE + (255,))
    d.polygon(top + list(reversed(bottom)), fill=HIDE + (255,))
    d.polygon(grow(top, 0.5) + list(reversed(grow(top, 0.2))), fill=HIDE_LIT + (230,))
    # Scales on the bare tip half.
    wrapped = 0.5
    rows = 9
    for r in range(rows):
        u = wrapped + (r + 0.5) * (1 - wrapped) / rows
        if u > 0.96:
            continue
        x = grip1 + length * u
        w = half_width(u) * k * 0.62
        for j in (-1, 0, 1):
            if j and w < 0.06 * s:
                continue
            sz = min(0.075 * s, w * 0.9)
            cz = y + j * w * 0.55
            scale_shape(d, x + (0 if j == 0 else 0.02 * s), cz, sz, sz * 1.1, 0.0, SCALE_EDGE + (255,))
            scale_shape(d, x + (0 if j == 0 else 0.02 * s) + 0.004 * s, cz, sz * 0.78, sz * 0.9, 0.0,
                        (SCALE_LIT if (r + j) % 2 == 0 else SCALE_C) + (255,))
    # Bandage over the grip half, with seams and a frayed end.
    m = 20
    btop = [(grip1 + length * wrapped * i / m, y - (half_width(wrapped * i / m) * 1.08 + 0.01) * k * 0.62) for i in range(m + 1)]
    bbot = [(grip1 + length * wrapped * i / m, y + (half_width(wrapped * i / m) * 1.08 + 0.01) * k * 0.62) for i in range(m + 1)]
    d.polygon(btop + list(reversed(bbot)), fill=BANDAGE + (255,))
    d.polygon(grow(btop, 0.55) + list(reversed(grow(btop, 0.2))), fill=BANDAGE_LIT + (200,))
    seams = int(length * wrapped / (0.05 * s))
    for i in range(seams):
        x = grip1 + (i + 0.5) * 0.05 * s
        w = half_width((x - grip1) / length) * k * 0.62 * 1.1
        d.line([(x - w * 0.5, y + w), (x + w * 0.5, y - w)], fill=SEAM + (255,), width=int(0.01 * s))
    e = grip1 + length * wrapped
    d.line([(e, y - 0.03 * s), (e + 0.05 * s, y - 0.06 * s)], fill=BANDAGE + (255,), width=int(0.016 * s))
    d.line([(e, y + 0.03 * s), (e + 0.04 * s, y + 0.07 * s)], fill=BANDAGE + (255,), width=int(0.016 * s))
    # Teeth at the mouth.
    for j in (-1, 1):
        d.line([(tip - 0.05 * s, y + j * 0.04 * s), (tip - 0.005 * s, y + j * 0.055 * s)], fill=BONE + (255,), width=int(0.014 * s))
    finish(image, size, "Samehada.png")


def icon(name, paint):
    size = 128
    s = size * SCALE
    image = canvas(size)
    paint(ImageDraw.Draw(image), s)
    finish(image, size, name)


def shark_skin(d, s):
    # A blade from bottom left to top right, scales standing off each side, two strips falling.
    a, b = (0.18 * s, 0.82 * s), (0.80 * s, 0.20 * s)
    ang = math.atan2(-(b[1] - a[1]), b[0] - a[0])
    n = 20
    ux, uz = math.cos(ang), -math.sin(ang)
    px, pz = -uz, ux
    left, right = [], []
    for i in range(n + 1):
        u = i / n
        w = (half_width(u) if i < n else 0.05) * 0.55 * s
        cx, cz = a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u
        left.append((cx + px * w, cz + pz * w))
        right.append((cx - px * w, cz - pz * w))
    d.polygon(left + list(reversed(right)), fill=WHITE)
    for i in range(3):
        u = 0.45 + i * 0.17
        w = half_width(u) * 0.55 * s
        cx, cz = a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u
        for side in (-1, 1):
            scale_shape(d, cx + px * side * (w + 0.06 * s), cz + pz * side * (w + 0.06 * s), 0.07 * s, 0.08 * s, ang + side * 0.5, WHITE)
    # Grip.
    d.line([(0.08 * s, 0.92 * s), a], fill=WHITE, width=int(0.05 * s))
    # Two torn strips falling away below the blade.
    d.line([(0.50 * s, 0.78 * s), (0.60 * s, 0.86 * s)], fill=WHITE, width=int(0.035 * s))
    d.line([(0.66 * s, 0.66 * s), (0.80 * s, 0.70 * s)], fill=WHITE, width=int(0.035 * s))


def fusion(d, s):
    # A dorsal fin: a curved triangle rising out of a wave line.
    fin = [(0.26 * s, 0.70 * s), (0.44 * s, 0.40 * s), (0.60 * s, 0.14 * s), (0.62 * s, 0.30 * s), (0.66 * s, 0.50 * s), (0.76 * s, 0.70 * s)]
    d.polygon(fin, fill=WHITE)
    # Waves: two arcs either side of the fin's base and one under it.
    width = int(0.05 * s)
    d.arc([0.04 * s, 0.64 * s, 0.30 * s, 0.84 * s], 200, 340, fill=WHITE, width=width)
    d.arc([0.70 * s, 0.64 * s, 0.96 * s, 0.84 * s], 200, 340, fill=WHITE, width=width)
    d.arc([0.14 * s, 0.76 * s, 0.86 * s, 0.96 * s], 200, 340, fill=WHITE, width=width)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    weapon()
    icon("IconSharkSkin.png", shark_skin)
    icon("IconFusion.png", fusion)


if __name__ == "__main__":
    main()
