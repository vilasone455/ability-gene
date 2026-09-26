#!/usr/bin/env python3
"""Textures for the Bank Shot kit: the pistol, its bullet and the charge ability's icon. Requires Pillow.

Run from any directory: python3 make_bank_shot_textures.py

BankShot.png      128 px. The held gun, drawn the way Core draws a pistol: muzzle to the right, grip
                  down at the left. A long heavy slide over a squared frame, a wooden grip, a trigger
                  guard, and a hot orange band at the muzzle. Iron colours are the ones
                  lib/bank-shot.js draws the preview's pistol with (IronDark, Iron, IronLit); the band
                  is the tracer's hot colour, so the gun and its shot match.
Bullet.png        64 px. The bullet in flight, pointing up the image, which the game turns along the
                  direction of travel: a pale core with a warm tracer tail, the charged shot's colours.
IconBankShot.png  128 px, white with the shape in the alpha, as the kit's other ability icons are. A
                  pistol at the bottom left, its shot running up to a wall on the right, back to a wall
                  on the left and on to a dot: two bounces and a hit.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/BankShot"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

IRON_DARK, IRON, IRON_LIT = (23, 23, 28), (77, 79, 87), (133, 138, 148)
WOOD, WOOD_DARK = (110, 70, 38), (64, 40, 20)
TRACER, TRACER_HOT, CORE = (255, 237, 158), (255, 148, 46), (255, 255, 255)
WHITE = (255, 255, 255, 255)


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


def dot(draw, centre, radius, colour):
    draw.ellipse([centre[0] - radius, centre[1] - radius, centre[0] + radius, centre[1] + radius], fill=colour)


def gun():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    o = int(0.012 * s)  # outline
    # Grip, angled back under the frame, with a dark outline and a lighter inset.
    grip = [(0.20 * s, 0.50 * s), (0.36 * s, 0.50 * s), (0.31 * s, 0.80 * s), (0.13 * s, 0.80 * s)]
    d.polygon(grip, fill=WOOD_DARK)
    d.polygon([(0.215 * s, 0.53 * s), (0.335 * s, 0.53 * s), (0.295 * s, 0.77 * s), (0.155 * s, 0.77 * s)], fill=WOOD)
    for i in range(4):
        y = (0.57 + i * 0.05) * s
        d.line([(0.19 * s, y), (0.32 * s, y)], fill=WOOD_DARK, width=int(0.008 * s))
    # Trigger guard and trigger.
    d.arc([0.34 * s, 0.46 * s, 0.50 * s, 0.64 * s], 0, 180, fill=IRON_DARK, width=int(0.022 * s))
    bar(d, (0.42 * s, 0.50 * s), (0.40 * s, 0.58 * s), 0.02 * s, IRON)
    # Frame under the slide, then the slide and the barrel.
    d.rectangle([0.16 * s - o, 0.44 * s - o, 0.80 * s + o, 0.52 * s + o], fill=IRON_DARK)
    d.rectangle([0.16 * s, 0.44 * s, 0.80 * s, 0.52 * s], fill=IRON)
    d.rectangle([0.14 * s - o, 0.33 * s - o, 0.86 * s + o, 0.45 * s + o], fill=IRON_DARK)
    d.rectangle([0.14 * s, 0.33 * s, 0.86 * s, 0.45 * s], fill=IRON)
    d.rectangle([0.14 * s, 0.335 * s, 0.86 * s, 0.355 * s], fill=IRON_LIT)
    for i in range(5):  # slide serrations at the back
        x = (0.18 + i * 0.03) * s
        d.line([(x, 0.37 * s), (x, 0.43 * s)], fill=IRON_DARK, width=int(0.008 * s))
    d.rectangle([0.86 * s, 0.36 * s - o, 0.94 * s + o, 0.43 * s + o], fill=IRON_DARK)
    d.rectangle([0.86 * s, 0.36 * s, 0.94 * s, 0.43 * s], fill=IRON)
    # The hot band at the muzzle, and the sights.
    d.rectangle([0.80 * s, 0.33 * s, 0.84 * s, 0.45 * s], fill=TRACER_HOT)
    d.rectangle([0.81 * s, 0.34 * s, 0.83 * s, 0.35 * s], fill=TRACER)
    d.rectangle([0.84 * s, 0.30 * s, 0.87 * s, 0.33 * s], fill=IRON_DARK)
    d.rectangle([0.16 * s, 0.30 * s, 0.21 * s, 0.33 * s], fill=IRON_DARK)
    finish(image, size, "BankShot.png")


def bullet():
    size = 64
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    # A tracer tail fading down the image, a warm glow, and the pale core at the top.
    steps = 24
    for i in range(steps):
        u = i / steps
        y0, y1 = (0.30 + u * 0.62) * s, (0.30 + (u + 1 / steps) * 0.62) * s
        w = (0.10 - 0.07 * u) * s
        a = int(230 * (1 - u) ** 1.5)
        colour = tuple(int(TRACER[k] + (TRACER_HOT[k] - TRACER[k]) * u) for k in range(3)) + (a,)
        d.rectangle([0.5 * s - w / 2, y0, 0.5 * s + w / 2, y1], fill=colour)
    dot(d, (0.5 * s, 0.24 * s), 0.12 * s, TRACER_HOT + (120,))
    dot(d, (0.5 * s, 0.24 * s), 0.075 * s, TRACER + (255,))
    dot(d, (0.5 * s, 0.22 * s), 0.045 * s, CORE + (255,))
    finish(image, size, "Bullet.png")


def icon():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    # Walls: a short one on the right and one on the left.
    d.rectangle([0.86 * s, 0.30 * s, 0.94 * s, 0.62 * s], fill=WHITE)
    d.rectangle([0.06 * s, 0.10 * s, 0.14 * s, 0.40 * s], fill=WHITE)
    # The pistol at the bottom left, pointing up and to the right.
    bar(d, (0.10 * s, 0.86 * s), (0.30 * s, 0.72 * s), 0.08 * s, WHITE)
    bar(d, (0.13 * s, 0.86 * s), (0.10 * s, 0.96 * s), 0.06 * s, WHITE)
    # The shot: muzzle to the right wall, back to the left wall, on to the target dot.
    a, b, c, e = (0.34 * s, 0.69 * s), (0.85 * s, 0.46 * s), (0.15 * s, 0.25 * s), (0.62 * s, 0.10 * s)
    for p, q in ((a, b), (b, c), (c, e)):
        bar(d, p, q, 0.035 * s, WHITE)
    for p in (b, c):
        dot(d, p, 0.045 * s, WHITE)
    dot(d, e, 0.075 * s, WHITE)
    finish(image, size, "IconBankShot.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    gun()
    bullet()
    icon()
