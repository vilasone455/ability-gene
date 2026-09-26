#!/usr/bin/env python3
"""Textures for the Frost Gun kit: the rifle, its bolt and Flash Freeze's icon. Requires Pillow.

Run from any directory: python3 make_frost_gun_textures.py

FrostGun.png          128 px. The held rifle, drawn the way Core draws a rifle: level, muzzle to the
                      right, stock at the left. A dark steel body and stock, a long barrel with three
                      cooling fins, a pale glass coolant tank under the barrel with a frost line in
                      it, and a cyan emitter ring at the muzzle. The ice colours are the picture's
                      (FrostGunGraphics: IceWhite, IceLit, Ice, IceDark), so gun and bolt match.
Bolt.png              64 px. The bolt in flight, pointing up the image, which the game turns along the
                      direction of travel: a white core and a cyan tail. Only a fallback: the bolt is
                      drawn from code (Projectile_FrostGun).
IconFlashFreeze.png   128 px, white with the shape in the alpha, as the kit's other ability icons are.
                      A faceted ice block with a figure's head inside it, and a beam coming in from the
                      bottom left.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/FrostGun"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

STEEL_DARK, STEEL, STEEL_LIT = (24, 28, 36), (66, 76, 92), (120, 134, 154)
ICE_WHITE, ICE_LIT, ICE, ICE_DARK = (240, 250, 255), (189, 237, 255), (128, 204, 247), (51, 117, 189)
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
    o = int(0.012 * s)
    # Stock: a slanted block at the left with a dark outline.
    stock = [(0.04 * s, 0.44 * s), (0.26 * s, 0.42 * s), (0.28 * s, 0.56 * s), (0.06 * s, 0.62 * s)]
    d.polygon([(x + (-o if x < 0.15 * s else o), y + (-o if y < 0.5 * s else o)) for x, y in stock], fill=STEEL_DARK)
    d.polygon(stock, fill=STEEL)
    d.line([(0.06 * s, 0.455 * s), (0.25 * s, 0.435 * s)], fill=STEEL_LIT, width=int(0.012 * s))
    # Grip under the body.
    grip = [(0.33 * s, 0.52 * s), (0.41 * s, 0.52 * s), (0.38 * s, 0.70 * s), (0.30 * s, 0.70 * s)]
    d.polygon(grip, fill=STEEL_DARK)
    # Body.
    d.rectangle([0.24 * s - o, 0.40 * s - o, 0.62 * s + o, 0.53 * s + o], fill=STEEL_DARK)
    d.rectangle([0.24 * s, 0.40 * s, 0.62 * s, 0.53 * s], fill=STEEL)
    d.rectangle([0.24 * s, 0.40 * s, 0.62 * s, 0.42 * s], fill=STEEL_LIT)
    # Coolant tank under the barrel: pale glass with a frost line.
    d.rounded_rectangle([0.44 * s - o, 0.52 * s - o, 0.80 * s + o, 0.63 * s + o], radius=0.05 * s, fill=STEEL_DARK)
    d.rounded_rectangle([0.44 * s, 0.52 * s, 0.80 * s, 0.63 * s], radius=0.05 * s, fill=ICE_DARK)
    d.rounded_rectangle([0.45 * s, 0.53 * s, 0.79 * s, 0.60 * s], radius=0.035 * s, fill=ICE)
    d.rectangle([0.47 * s, 0.54 * s, 0.77 * s, 0.555 * s], fill=ICE_WHITE)
    for i in range(3):
        x = (0.53 + i * 0.09) * s
        d.line([(x, 0.52 * s), (x, 0.63 * s)], fill=STEEL_DARK, width=int(0.012 * s))
    # Barrel with three cooling fins.
    d.rectangle([0.60 * s - o, 0.43 * s - o, 0.90 * s + o, 0.49 * s + o], fill=STEEL_DARK)
    d.rectangle([0.60 * s, 0.43 * s, 0.90 * s, 0.49 * s], fill=STEEL)
    d.rectangle([0.60 * s, 0.43 * s, 0.90 * s, 0.445 * s], fill=STEEL_LIT)
    for i in range(3):
        x = (0.64 + i * 0.06) * s
        d.rectangle([x, 0.395 * s, x + 0.025 * s, 0.525 * s], fill=STEEL_DARK)
        d.rectangle([x + 0.006 * s, 0.40 * s, x + 0.019 * s, 0.52 * s], fill=STEEL_LIT)
    # Emitter at the muzzle: a cyan ring and a white core.
    d.rectangle([0.89 * s, 0.415 * s, 0.95 * s, 0.505 * s], fill=STEEL_DARK)
    d.rectangle([0.905 * s, 0.425 * s, 0.94 * s, 0.495 * s], fill=ICE)
    d.rectangle([0.915 * s, 0.445 * s, 0.93 * s, 0.475 * s], fill=ICE_WHITE)
    # Sight on top.
    d.rectangle([0.40 * s, 0.35 * s, 0.52 * s, 0.40 * s], fill=STEEL_DARK)
    d.rectangle([0.41 * s, 0.36 * s, 0.51 * s, 0.385 * s], fill=ICE_DARK)
    finish(image, size, "FrostGun.png")


def bolt():
    size = 64
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    steps = 24
    for i in range(steps):
        u = i / steps
        y0, y1 = (0.30 + u * 0.62) * s, (0.30 + (u + 1 / steps) * 0.62) * s
        w = (0.12 - 0.09 * u) * s
        a = int(220 * (1 - u) ** 1.5)
        colour = tuple(int(ICE_LIT[k] + (ICE[k] - ICE_LIT[k]) * u) for k in range(3)) + (a,)
        d.rectangle([0.5 * s - w / 2, y0, 0.5 * s + w / 2, y1], fill=colour)
    dot(d, (0.5 * s, 0.24 * s), 0.13 * s, ICE + (110,))
    dot(d, (0.5 * s, 0.24 * s), 0.08 * s, ICE_LIT + (255,))
    dot(d, (0.5 * s, 0.22 * s), 0.05 * s, ICE_WHITE + (255,))
    finish(image, size, "Bolt.png")


def icon():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    clear = (0, 0, 0, 0)
    # The ice block: a tall hexagonal crystal.
    block = [(0.40 * s, 0.10 * s), (0.72 * s, 0.10 * s), (0.86 * s, 0.30 * s), (0.84 * s, 0.86 * s),
             (0.58 * s, 0.94 * s), (0.30 * s, 0.86 * s), (0.28 * s, 0.30 * s)]
    d.polygon(block, fill=WHITE)
    # Facet lines cut out of the alpha: the top rim and the front edge.
    w = int(0.022 * s)
    d.line([(0.28 * s, 0.30 * s), (0.58 * s, 0.38 * s), (0.86 * s, 0.30 * s)], fill=clear, width=w)
    d.line([(0.58 * s, 0.38 * s), (0.58 * s, 0.92 * s)], fill=clear, width=w)
    # A figure's head and shoulders inside, cut out.
    dot(d, (0.57 * s, 0.52 * s), 0.08 * s, clear)
    d.pieslice([0.43 * s, 0.62 * s, 0.71 * s, 0.98 * s], 180, 360, fill=clear)
    d.line([(0.58 * s, 0.60 * s), (0.58 * s, 0.92 * s)], fill=WHITE, width=int(0.012 * s))
    # The beam from the bottom left, ending in a star at the block.
    bar(d, (0.02 * s, 0.98 * s), (0.26 * s, 0.70 * s), 0.05 * s, WHITE)
    for a in range(4):
        ang = a * math.pi / 4
        bar(d, (0.20 * s - math.cos(ang) * 0.09 * s, 0.76 * s - math.sin(ang) * 0.09 * s),
            (0.20 * s + math.cos(ang) * 0.09 * s, 0.76 * s + math.sin(ang) * 0.09 * s), 0.025 * s, WHITE)
    finish(image, size, "IconFlashFreeze.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    gun()
    bolt()
    icon()
