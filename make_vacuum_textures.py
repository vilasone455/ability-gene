#!/usr/bin/env python3
"""Textures for the Vacuum kit: the held weapon and the three ability icons. Requires Pillow.

Run from any directory: python3 make_vacuum_textures.py

Vacuum.png       256 px. The held weapon, drawn the way Core draws a held weapon: level, the working
                 end to the right. The wand is a grey tube with a lit top edge and a teal grip band;
                 the wide floor head is a bar across its right end with a dark slot; a short stub of
                 hose leaves the grip and curls down. Hose, lit, dark, slot and teal are the colours
                 lib/vacuum.js draws the weapon with (Hose, HoseLit, HoseDark, Lip, Can), so the wand
                 in the hand and the wand in a cast's picture match.
IconSuck.png     128 px each, white with the shape in the alpha, as Core's ability icons are.
IconSpit.png     Suck: the floor head bottom left and three streaks converging into its slot.
IconDigest.png   Spit: the head bottom left and a lump leaving it on an arc, with two motion lines.
                 Digest: the canister from the front, its face a wide mouth with teeth, two eyes,
                 and three crumbs under it.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Vacuum"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

# lib/vacuum.js: Hose, HoseLit, HoseDark, Lip, Can, CanLit.
HOSE, HOSE_LIT, HOSE_DARK = (66, 66, 77), (112, 112, 128), (18, 18, 23)
LIP, CAN, CAN_LIT = (13, 13, 15), (31, 77, 84), (56, 117, 122)
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)


def canvas(size):
    return Image.new("RGBA", (size * SCALE, size * SCALE), CLEAR)


def finish(image, size, name):
    image.resize((size, size), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


def bar(draw, a, b, width, colour):
    """A straight bar from a to b with square ends."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dz)
    nx, nz = -dz / length * width / 2, dx / length * width / 2
    draw.polygon([(a[0] + nx, a[1] + nz), (b[0] + nx, b[1] + nz), (b[0] - nx, b[1] - nz), (a[0] - nx, a[1] - nz)], fill=colour)


def weapon():
    size = 256
    s = size * SCALE
    image = canvas(size)
    d = ImageDraw.Draw(image)
    edge = int(0.012 * s)
    # Hose stub leaving the grip end and curling down.
    d.arc([0.02 * s, 0.44 * s, 0.24 * s, 0.74 * s], 170, 300, fill=HOSE_DARK + (255,), width=int(0.055 * s))
    d.arc([0.02 * s, 0.44 * s, 0.24 * s, 0.74 * s], 170, 300, fill=HOSE + (255,), width=int(0.034 * s))
    # The wand: a tube from the grip to the head, widening a little toward the head.
    d.polygon([(0.12 * s, 0.465 * s), (0.80 * s, 0.455 * s), (0.80 * s, 0.545 * s), (0.12 * s, 0.535 * s)],
              fill=HOSE + (255,), outline=HOSE_DARK + (255,))
    d.rectangle([0.14 * s, 0.470 * s, 0.78 * s, 0.485 * s], fill=HOSE_LIT + (255,))
    # Grip end cap and the teal grip band.
    d.rounded_rectangle([0.08 * s, 0.445 * s, 0.16 * s, 0.555 * s], radius=int(0.015 * s), fill=HOSE_DARK + (255,))
    d.rectangle([0.20 * s, 0.458 * s, 0.34 * s, 0.542 * s], fill=CAN + (255,), outline=HOSE_DARK + (255,), width=edge)
    d.rectangle([0.21 * s, 0.463 * s, 0.33 * s, 0.478 * s], fill=CAN_LIT + (255,))
    # The floor head: a wide bar across the wand's end, with a lit edge and a dark slot facing out.
    d.rounded_rectangle([0.78 * s, 0.22 * s, 0.94 * s, 0.78 * s], radius=int(0.03 * s), fill=HOSE + (255,), outline=HOSE_DARK + (255,), width=edge)
    d.rectangle([0.795 * s, 0.24 * s, 0.82 * s, 0.76 * s], fill=HOSE_LIT + (255,))
    d.rounded_rectangle([0.875 * s, 0.26 * s, 0.915 * s, 0.74 * s], radius=int(0.012 * s), fill=LIP + (255,))
    finish(image, size, "Vacuum.png")


def icon(name, paint):
    size = 128
    image = canvas(size)
    paint(image, ImageDraw.Draw(image), size * SCALE)
    finish(image, size, name)


def head(draw, s):
    """The wand and its floor head, bottom left, the head facing up-right."""
    bar(draw, (0.04 * s, 0.96 * s), (0.28 * s, 0.72 * s), 0.10 * s, WHITE)
    # The head: a wide bar across the wand's end.
    bar(draw, (0.16 * s, 0.56 * s), (0.44 * s, 0.84 * s), 0.13 * s, WHITE)


def suck(image, draw, s):
    head(draw, s)
    # Three streaks converging into the head's slot, fat at the far end.
    mouth = (0.36 * s, 0.64 * s)
    for ang, length in ((20, 0.50), (45, 0.56), (70, 0.50)):
        a = math.radians(ang)
        far = (mouth[0] + math.cos(a) * length * s, mouth[1] - math.sin(a) * length * s)
        steps = 20
        for k in range(steps + 1):
            u = k / steps
            x, y = mouth[0] + (far[0] - mouth[0]) * (0.25 + 0.75 * u), mouth[1] + (far[1] - mouth[1]) * (0.25 + 0.75 * u)
            r = (0.012 + 0.03 * u) * s
            draw.ellipse([x - r, y - r, x + r, y + r], fill=WHITE)


def spit(image, draw, s):
    head(draw, s)
    # A lump on an arc out of the head, and two motion lines behind it.
    cx, cy, r = 0.74 * s, 0.30 * s, 0.13 * s
    draw.ellipse([cx - r, cy - r * 0.85, cx + r, cy + r * 0.85], fill=WHITE)
    for off in (-0.09, 0.09):
        a = (0.44 * s + off * s * 0.7, 0.58 * s + off * s * 0.7)
        b = (0.58 * s + off * s * 0.7, 0.44 * s + off * s * 0.7)
        bar(draw, a, b, 0.035 * s, WHITE)


def digest(image, draw, s):
    # The canister from the front: a body with a round top.
    draw.rounded_rectangle([0.16 * s, 0.24 * s, 0.84 * s, 0.84 * s], radius=int(0.12 * s), fill=WHITE)
    draw.ellipse([0.16 * s, 0.12 * s, 0.84 * s, 0.36 * s], fill=WHITE)
    # Eyes, cut out.
    for ex in (0.36, 0.64):
        draw.ellipse([(ex - 0.07) * s, 0.36 * s, (ex + 0.07) * s, 0.48 * s], fill=CLEAR)
    # A wide open mouth with teeth, cut out.
    draw.rounded_rectangle([0.26 * s, 0.56 * s, 0.74 * s, 0.74 * s], radius=int(0.06 * s), fill=CLEAR)
    for k in range(4):
        tx = 0.32 + k * 0.12
        draw.polygon([((tx - 0.04) * s, 0.56 * s), ((tx + 0.04) * s, 0.56 * s), (tx * s, 0.63 * s)], fill=WHITE)
    # Crumbs under it.
    for cx, cy, r in ((0.30, 0.93, 0.03), (0.50, 0.95, 0.025), (0.68, 0.92, 0.03)):
        draw.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=WHITE)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    weapon()
    icon("IconSuck.png", suck)
    icon("IconSpit.png", spit)
    icon("IconDigest.png", digest)
