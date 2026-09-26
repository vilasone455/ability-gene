#!/usr/bin/env python3
"""Textures for the Coil Gun kit: the rifle, its round's texture and Chain Arc's icon. Requires Pillow.

Run from any directory: python3 make_coil_gun_textures.py

CoilGun.png       128 px. The held gun, drawn the way Core draws a rifle: level, muzzle to the right.
                  A dark stock at the left, a receiver with a battery pack under it showing a blue
                  charge window, and a long barrel wrapped in four copper coils, ending in a muzzle
                  ring with a blue emitter at x = 0.95 of the image. At drawSize 1.25 that puts the
                  muzzle 0.95 cells from the holder's centre (CoilGunGraphics.MuzzleAlong).
Bolt.png          64 px. The round's def needs a texture; Projectile_CoilBolt draws the bolt in code
                  and never shows this. A soft blue-white streak pointing up the image.
IconChainArc.png  128 px, white with the shape in the alpha, as the kit weapons' other ability icons
                  are. A jagged bolt from the bottom left through three dots, the pawns it jumps to.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/CoilGun"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

METAL_DARK, METAL, METAL_LIT = (22, 24, 30), (62, 66, 76), (118, 124, 138)
STOCK, STOCK_LIT = (36, 38, 44), (58, 61, 70)
COPPER_DARK, COPPER, COPPER_LIT = (96, 50, 22), (176, 104, 48), (230, 158, 92)
CHARGE, CHARGE_LIT, CORE = (60, 140, 255), (150, 210, 255), (255, 255, 255)
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


def box(d, x0, y0, x1, y1, fill, s, outline=METAL_DARK, o=0.012):
    """A rectangle in image fractions with a dark outline."""
    k = o * s
    d.rectangle([x0 * s - k, y0 * s - k, x1 * s + k, y1 * s + k], fill=outline)
    d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], fill=fill)


def gun():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    # Stock: a slanted butt and a thin wrist into the receiver.
    butt = [(0.04 * s, 0.46 * s), (0.20 * s, 0.47 * s), (0.22 * s, 0.56 * s), (0.05 * s, 0.61 * s)]
    k = 0.012 * s
    d.polygon([(x - k if x < 0.1 * s else x + k, y - k if y < 0.5 * s else y + k) for x, y in butt], fill=METAL_DARK)
    d.polygon(butt, fill=STOCK)
    d.line([(0.06 * s, 0.475 * s), (0.19 * s, 0.482 * s)], fill=STOCK_LIT, width=int(0.012 * s))
    box(d, 0.20, 0.48, 0.30, 0.53, STOCK, s)
    # Grip under the receiver.
    grip = [(0.30 * s, 0.53 * s), (0.36 * s, 0.53 * s), (0.34 * s, 0.64 * s), (0.28 * s, 0.64 * s)]
    d.polygon([(0.29 * s, 0.52 * s), (0.37 * s, 0.52 * s), (0.35 * s, 0.65 * s), (0.27 * s, 0.65 * s)], fill=METAL_DARK)
    d.polygon(grip, fill=STOCK)
    # Receiver.
    box(d, 0.28, 0.44, 0.56, 0.54, METAL, s)
    d.rectangle([0.28 * s, 0.445 * s, 0.56 * s, 0.46 * s], fill=METAL_LIT)
    # Battery pack under the receiver, forward of the grip, with its blue charge window.
    box(d, 0.39, 0.54, 0.53, 0.62, METAL, s)
    d.rectangle([0.41 * s, 0.565 * s, 0.51 * s, 0.595 * s], fill=CHARGE)
    d.rectangle([0.41 * s, 0.565 * s, 0.51 * s, 0.575 * s], fill=CHARGE_LIT)
    # Sight rail on top.
    box(d, 0.33, 0.415, 0.50, 0.44, METAL_DARK, s, o=0.004)
    # Barrel: a rail with four copper coils round it.
    box(d, 0.56, 0.465, 0.92, 0.515, METAL, s)
    d.rectangle([0.56 * s, 0.468 * s, 0.92 * s, 0.478 * s], fill=METAL_LIT)
    for i in range(4):
        x = 0.585 + i * 0.075
        box(d, x, 0.435, x + 0.045, 0.545, COPPER, s, outline=COPPER_DARK, o=0.008)
        d.rectangle([x * s, 0.44 * s, (x + 0.045) * s, 0.455 * s], fill=COPPER_LIT)
        for j in range(3):  # windings
            xx = (x + 0.011 + j * 0.012) * s
            d.line([(xx, 0.445 * s), (xx, 0.54 * s)], fill=COPPER_DARK, width=max(1, int(0.004 * s)))
    # Muzzle ring and the blue emitter at its end.
    box(d, 0.905, 0.445, 0.945, 0.535, METAL_DARK, s, o=0.006)
    dot(d, (0.95 * s, 0.49 * s), 0.028 * s, CHARGE + (200,))
    dot(d, (0.95 * s, 0.49 * s), 0.014 * s, CORE + (255,))
    finish(image, size, "CoilGun.png")


def bolt():
    size = 64
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    steps = 24
    for i in range(steps):
        u = i / steps
        y0, y1 = (0.20 + u * 0.70) * s, (0.20 + (u + 1 / steps) * 0.70) * s
        w = (0.12 - 0.08 * u) * s
        a = int(220 * (1 - u) ** 1.4)
        colour = tuple(int(CHARGE_LIT[k] + (CHARGE[k] - CHARGE_LIT[k]) * u) for k in range(3)) + (a,)
        d.rectangle([0.5 * s - w / 2, y0, 0.5 * s + w / 2, y1], fill=colour)
    dot(d, (0.5 * s, 0.18 * s), 0.1 * s, CHARGE + (120,))
    dot(d, (0.5 * s, 0.18 * s), 0.05 * s, CORE + (255,))
    finish(image, size, "Bolt.png")


def icon():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    # Three pawns hit, as dots, and the bolt from the bottom left jumping through them.
    pawns = [(0.38 * s, 0.62 * s), (0.60 * s, 0.32 * s), (0.82 * s, 0.50 * s)]
    start = (0.06 * s, 0.92 * s)
    path = [start]
    points = [start] + pawns
    for a, b in zip(points, points[1:]):
        # Three kinks per jump, alternately to either side.
        for t, side in ((0.28, 1), (0.52, -1), (0.76, 1)):
            x, y = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
            dx, dy = b[0] - a[0], b[1] - a[1]
            n = math.hypot(dx, dy)
            path.append((x - dy / n * side * 0.07 * s, y + dx / n * side * 0.07 * s))
        path.append(b)
    for p, q in zip(path, path[1:]):
        bar(d, p, q, 0.045 * s, WHITE)
    for p in path:
        dot(d, p, 0.0225 * s, WHITE)
    for p in pawns:
        dot(d, p, 0.085 * s, WHITE)
    # A spark burst round the last pawn.
    c = pawns[-1]
    for k in range(6):
        a = k * math.pi / 3 + 0.3
        bar(d, (c[0] + math.cos(a) * 0.1 * s, c[1] + math.sin(a) * 0.1 * s), (c[0] + math.cos(a) * 0.14 * s, c[1] + math.sin(a) * 0.14 * s), 0.025 * s, WHITE)
    finish(image, size, "IconChainArc.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    gun()
    bolt()
    icon()
