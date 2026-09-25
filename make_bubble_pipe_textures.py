#!/usr/bin/env python3
"""Textures for the Bubble Pipe kit: the weapon and the two ability icons. Requires Pillow.

Run from any directory: python3 make_bubble_pipe_textures.py

BubblePipe.png         256 px. The held weapon, drawn the way Core draws a melee weapon: mouth end at
                       the bottom left, tip at the top right. A bamboo tube with three dark nodes, a
                       lit stripe along its upper side, a dark bore at the tip and one small bubble
                       just off the tip. Bamboo, node and film colours are lib/bubble-pipe.js's, so
                       the pipe in the hand and the pipe of a cast match. The soap jar is not in this
                       texture: it is drawn on the holder's hip by the kit.
IconDriftingBurst.png  128 px each, white with the shape in the alpha, as Core's ability icons are.
IconEyePop.png         Drifting Burst: a short pipe at the bottom left and four bubble rings fanning
                       out from its tip, growing with distance. Eye Pop: an eye with a bubble bursting
                       over it: a broken ring and specks.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/BubblePipe"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

BAMBOO, BAMBOO_DARK, BAMBOO_LIT = (189, 163, 92), (107, 87, 41), (224, 204, 133)
FILM, IRIS1, IRIS2 = (230, 245, 255), (255, 179, 217), (166, 242, 217)
WHITE = (255, 255, 255, 255)
CLEAR = (255, 255, 255, 0)


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


def ring(draw, centre, r, width, colour):
    draw.ellipse([centre[0] - r, centre[1] - r, centre[0] + r, centre[1] + r], outline=colour, width=int(width))


def weapon():
    size = 256
    s = size * SCALE
    # The pipe is drawn level, then turned 45 degrees onto the weapon diagonal.
    level = canvas(size)
    d = ImageDraw.Draw(level)
    mid, left, right, half = s / 2, 0.10 * s, 0.86 * s, 0.028 * s
    d.rounded_rectangle([left, mid - half, right, mid + half], radius=int(0.012 * s), fill=BAMBOO + (255,),
                        outline=BAMBOO_DARK + (255,), width=int(0.008 * s))
    d.rectangle([left + 0.01 * s, mid - half * 0.55, right - 0.01 * s, mid - half * 0.1], fill=BAMBOO_LIT + (255,))
    for k in (1, 2, 3):                                     # bamboo nodes
        x = left + (right - left) * k / 4
        d.rectangle([x - 0.008 * s, mid - half - 0.006 * s, x + 0.008 * s, mid + half + 0.006 * s], fill=BAMBOO_DARK + (255,))
    d.ellipse([right - 0.016 * s, mid - 0.016 * s, right + 0.016 * s, mid + 0.016 * s], fill=(20, 41, 46, 255))   # the bore
    image = canvas(size)
    image.alpha_composite(level.rotate(45, resample=Image.BICUBIC))
    # One small bubble just off the tip, on the diagonal.
    d = ImageDraw.Draw(image)
    c, r = (0.86 * s, 0.14 * s), 0.075 * s
    d.ellipse([c[0] - r, c[1] - r, c[0] + r, c[1] + r], fill=FILM + (40,))
    ring(d, c, r * 0.93, 0.010 * s, IRIS1 + (150,))
    ring(d, c, r * 0.84, 0.008 * s, IRIS2 + (130,))
    ring(d, c, r, 0.008 * s, FILM + (230,))
    d.arc([c[0] - r * 0.7, c[1] - r * 0.7, c[0] + r * 0.7, c[1] + r * 0.7], 200, 245, fill=(255, 255, 255, 230), width=int(0.014 * s))
    finish(image, size, "BubblePipe.png")


def icon(name, paint):
    size = 128
    image = canvas(size)
    paint(image, ImageDraw.Draw(image), size * SCALE)
    finish(image, size, name)


def drifting_burst(image, draw, s):
    bar(draw, (0.08 * s, 0.92 * s), (0.34 * s, 0.66 * s), 0.07 * s, WHITE)
    for k, (x, y, r) in enumerate(((0.45 * s, 0.55 * s, 0.07 * s), (0.64 * s, 0.60 * s, 0.09 * s),
                                   (0.56 * s, 0.33 * s, 0.10 * s), (0.82 * s, 0.30 * s, 0.12 * s))):
        ring(draw, (x, y), r, 0.028 * s, WHITE)
        draw.arc([x - r * 0.62, y - r * 0.62, x + r * 0.62, y + r * 0.62], 200, 250, fill=WHITE, width=int(0.03 * s))


def eye_pop(image, draw, s):
    cx, cy = 0.5 * s, 0.62 * s
    # The eye: an almond outline, an iris ring and a pupil.
    w, h = 0.40 * s, 0.20 * s
    top = [(cx - w + 2 * w * i / 20, cy - h * math.sin(math.pi * i / 20)) for i in range(21)]
    bottom = [(cx + w - 2 * w * i / 20, cy + h * math.sin(math.pi * i / 20)) for i in range(21)]
    draw.line(top + bottom + [top[0]], fill=WHITE, width=int(0.04 * s), joint="curve")
    ring(draw, (cx, cy), 0.11 * s, 0.035 * s, WHITE)
    draw.ellipse([cx - 0.045 * s, cy - 0.045 * s, cx + 0.045 * s, cy + 0.045 * s], fill=WHITE)
    # The bubble bursting over it: a ring torn open at the top, and specks thrown off the tear.
    bx, by, r = 0.5 * s, 0.30 * s, 0.20 * s
    draw.arc([bx - r, by - r, bx + r, by + r], 300, 240 + 360, fill=WHITE, width=int(0.03 * s))
    for k in range(7):
        a = math.radians(-150 + k * 20)
        d = r * (1.15 + 0.2 * (k % 2))
        x, y = bx + math.cos(a) * d, by + math.sin(a) * d
        q = 0.022 * s
        draw.ellipse([x - q, y - q, x + q, y + q], fill=WHITE)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    weapon()
    icon("IconDriftingBurst.png", drifting_burst)
    icon("IconEyePop.png", eye_pop)
