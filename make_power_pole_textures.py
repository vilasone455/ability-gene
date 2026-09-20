#!/usr/bin/env python3
"""Textures for the Power Pole: the weapon and the three ability icons. Requires Pillow.

Run from any directory: python3 make_power_pole_textures.py

PowerPole.png    256 px. The carried staff, drawn the way Core draws a melee weapon: grip at the
                 bottom left, tip at the top right. Red shaft, dark outline, a lit strip on the
                 upper side and a darker ferrule at each end: the colours PowerPoleGraphics.cs
                 draws the extended pole with, so the staff in the hand and the pole of a cast
                 are the same object.
IconThrust.png   128 px each, white with the shape in the alpha, as Core's ability icons are.
IconSweep.png    Thrust: the pole and an arrowhead. Sweep: the pole and the arc it swings
IconStrike.png   through. Strike: the jump's arc, the pole coming down, and the impact lines.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/PowerPole"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

RED, RED_LIT, RED_DARK, FERRULE = (204, 33, 26), (250, 117, 92), (64, 10, 10), (117, 18, 15)


def canvas(size):
    return Image.new("RGBA", (size * SCALE, size * SCALE), (0, 0, 0, 0))


def finish(image, size, name):
    image.resize((size, size), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


def bar(draw, a, b, width, colour):
    """A straight bar from a to b with square ends."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dz)
    nx, nz = -dz / length * width / 2, dx / length * width / 2
    draw.polygon([(a[0] + nx, a[1] + nz), (b[0] + nx, b[1] + nz), (b[0] - nx, b[1] - nz), (a[0] - nx, a[1] - nz)], fill=colour)


def along(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def weapon():
    size = 256
    s = size * SCALE
    image = canvas(size)
    draw = ImageDraw.Draw(image)
    a, b = (0.12 * s, 0.88 * s), (0.88 * s, 0.12 * s)      # image rows count from the top
    width = 0.060 * s
    bar(draw, along(a, b, -0.012), along(b, a, -0.012), width + 0.016 * s, RED_DARK + (255,))
    bar(draw, a, b, width, RED + (255,))
    # The lit strip is on the upper side, 12% to 40% of the width out from the middle.
    dx, dz = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dz)
    ux, uz = dz / length, -dx / length                       # toward the top left
    shift = width * 0.26
    bar(draw, (a[0] + ux * shift, a[1] + uz * shift), (b[0] + ux * shift, b[1] + uz * shift), width * 0.28, RED_LIT + (255,))
    for start, end in ((0, 0.11), (0.89, 1)):
        bar(draw, along(a, b, start), along(a, b, end), width, FERRULE + (255,))
    finish(image, size, "PowerPole.png")


def icon(name, paint):
    size = 128
    image = canvas(size)
    paint(ImageDraw.Draw(image), size * SCALE)
    finish(image, size, name)


WHITE = (255, 255, 255, 255)


def thrust(draw, s):
    a, b = (0.10 * s, 0.50 * s), (0.74 * s, 0.50 * s)
    bar(draw, a, b, 0.10 * s, WHITE)
    draw.polygon([(0.70 * s, 0.30 * s), (0.94 * s, 0.50 * s), (0.70 * s, 0.70 * s)], fill=WHITE)
    for y in (0.30, 0.70):
        bar(draw, (0.16 * s, y * s), (0.46 * s, y * s), 0.035 * s, WHITE)


def sweep(draw, s):
    pivot = (0.22 * s, 0.78 * s)
    radius = 0.62 * s
    box = [pivot[0] - radius, pivot[1] - radius, pivot[0] + radius, pivot[1] + radius]
    draw.arc(box, 270, 360, fill=WHITE, width=int(0.05 * s))
    tip = (pivot[0] + radius * math.cos(math.radians(-20)), pivot[1] + radius * math.sin(math.radians(-20)))
    bar(draw, pivot, tip, 0.09 * s, WHITE)
    draw.polygon([(0.80 * s, 0.70 * s), (0.92 * s, 0.70 * s), (0.86 * s, 0.84 * s)], fill=WHITE)


def strike(draw, s):
    box = [0.08 * s, 0.22 * s, 0.72 * s, 1.20 * s]
    draw.arc(box, 190, 330, fill=WHITE, width=int(0.045 * s))
    bar(draw, (0.50 * s, 0.20 * s), (0.80 * s, 0.80 * s), 0.09 * s, WHITE)
    for degrees in (200, 250, 290, 340):
        r = math.radians(degrees)
        centre = (0.80 * s, 0.84 * s)
        bar(draw, (centre[0] + 0.07 * s * math.cos(r), centre[1] - 0.04 * s + 0.07 * s * math.sin(r)),
            (centre[0] + 0.17 * s * math.cos(r), centre[1] - 0.04 * s + 0.17 * s * math.sin(r)), 0.03 * s, WHITE)
    bar(draw, (0.62 * s, 0.90 * s), (0.98 * s, 0.90 * s), 0.035 * s, WHITE)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    weapon()
    icon("IconThrust.png", thrust)
    icon("IconSweep.png", sweep)
    icon("IconStrike.png", strike)
