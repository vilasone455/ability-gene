#!/usr/bin/env python3
"""Textures for the Chain Sickle kit: the weapon and the two ability icons. Requires Pillow.

Run from any directory: python3 make_chain_sickle_textures.py

ChainSickle.png  256 px. The held weapon, drawn the way Core draws a melee weapon: handle at the
                 bottom left, blade at the top right. A wooden handle with a dark end cap, a steel
                 blade hooking to the left off its top with a lit edge, and a short run of chain from
                 the handle's butt looping down to the iron weight. Wood, steel and iron colours are
                 lib/chain-sickle.js's, so the sickle in the hand and the sickle of a cast match.
IconSnag.png     128 px each, white with the shape in the alpha, as Core's ability icons are.
IconStake.png    Snag: a figure-eight coil (the wrap) at the top right with a chain of links running
                 to the bottom left. Stake: the weight driven into a floor line with cracks either
                 side and a taut chain rising from it.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/ChainSickle"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

WOOD, WOOD_DARK = (92, 56, 28), (41, 23, 10)
STEEL, STEEL_LIT, IRON, IRON_LIT, IRON_DARK = (168, 176, 189), (230, 235, 242), (77, 79, 87), (133, 138, 148), (23, 23, 28)
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


def links(draw, points, size, dark, lit):
    """Chain links along a polyline: alternating flat rings and edge-on bars, `size` long each."""
    k = 0
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        length = math.hypot(x1 - x0, y1 - y0)
        n = max(1, int(length / size))
        for i in range(n):
            u0, u1 = i / n, (i + 1) / n
            a = (x0 + (x1 - x0) * u0, y0 + (y1 - y0) * u0)
            b = (x0 + (x1 - x0) * u1, y0 + (y1 - y0) * u1)
            if k % 2 == 0:
                cx, cy = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
                r = size * 0.55
                draw.ellipse([cx - r, cy - r * 0.8, cx + r, cy + r * 0.8], outline=dark, width=max(1, int(size * 0.28)))
                if lit:
                    draw.arc([cx - r, cy - r * 0.8, cx + r, cy + r * 0.8], 200, 320, fill=lit, width=max(1, int(size * 0.14)))
            else:
                bar(draw, a, b, size * 0.28, dark)
            k += 1


def weapon():
    size = 256
    s = size * SCALE
    image = canvas(size)
    d = ImageDraw.Draw(image)
    butt, top = (0.22 * s, 0.78 * s), (0.60 * s, 0.40 * s)
    # Chain from the butt, sagging down and round to the weight at the bottom right.
    path = [butt, (0.20 * s, 0.86 * s), (0.30 * s, 0.93 * s), (0.46 * s, 0.93 * s), (0.60 * s, 0.88 * s)]
    links(d, path, 0.035 * s, IRON_DARK + (255,), IRON_LIT + (255,))
    wx, wy, wr = 0.66 * s, 0.86 * s, 0.055 * s
    d.ellipse([wx - wr, wy - wr, wx + wr, wy + wr], fill=IRON_DARK + (255,))
    d.ellipse([wx - wr * 0.8, wy - wr * 0.85, wx + wr * 0.7, wy + wr * 0.6], fill=IRON + (255,))
    d.ellipse([wx - wr * 0.5, wy - wr * 0.6, wx - wr * 0.05, wy - wr * 0.2], fill=IRON_LIT + (255,))
    # The handle, outline first.
    bar(d, butt, top, 0.075 * s, WOOD_DARK + (255,))
    bar(d, (butt[0] + 0.01 * s, butt[1] - 0.01 * s), (top[0] - 0.01 * s, top[1] + 0.01 * s), 0.045 * s, WOOD + (255,))
    bar(d, butt, (butt[0] + 0.035 * s, butt[1] - 0.035 * s), 0.085 * s, IRON_DARK + (255,))   # end cap
    # The blade: an arc hooking left (counter-clockwise on screen) off the handle's top.
    ang = math.atan2(top[1] - butt[1], top[0] - butt[0])            # the handle's direction, image coords
    left = ang - math.pi / 2                                           # to the handle's left on screen
    R = 0.20 * s
    c = (top[0] + math.cos(left) * R, top[1] + math.sin(left) * R)
    inner, outer, n = [], [], 24
    for i in range(n + 1):
        u = i / n
        a = ang + math.pi / 2 + u * math.radians(-110)                # from the handle round to the left
        w = 0.07 * s * (1 - u * u) + 0.004 * s
        inner.append((c[0] + math.cos(a) * (R - w * 0.1), c[1] + math.sin(a) * (R - w * 0.1)))
        outer.append((c[0] + math.cos(a) * (R + w), c[1] + math.sin(a) * (R + w)))
    d.polygon(inner + outer[::-1], fill=IRON_DARK + (255,))
    shrink = [(o[0] + (i[0] - o[0]) * 0.15, o[1] + (i[1] - o[1]) * 0.15) for i, o in zip(inner, outer)]
    d.polygon([(i[0] + (o[0] - i[0]) * 0.12, i[1] + (o[1] - i[1]) * 0.12) for i, o in zip(inner, outer)] + shrink[::-1], fill=STEEL + (255,))
    edge = [(i[0] + (o[0] - i[0]) * 0.55, i[1] + (o[1] - i[1]) * 0.55) for i, o in zip(inner, outer)]
    d.polygon(edge + shrink[::-1], fill=STEEL_LIT + (255,))
    finish(image, size, "ChainSickle.png")


def icon(name, paint):
    size = 128
    image = canvas(size)
    paint(image, ImageDraw.Draw(image), size * SCALE)
    finish(image, size, name)


def snag(image, draw, s):
    # The wrap: two loops round a body at the top right.
    cx, cy = 0.68 * s, 0.34 * s
    for dy in (-0.07 * s, 0.07 * s):
        draw.ellipse([cx - 0.20 * s, cy + dy - 0.08 * s, cx + 0.20 * s, cy + dy + 0.08 * s], outline=WHITE, width=int(0.035 * s))
    # The weight riding the coil's end.
    wx, wy, r = cx + 0.20 * s, cy + 0.10 * s, 0.06 * s
    draw.ellipse([wx - r, wy - r, wx + r, wy + r], fill=WHITE)
    # The chain back to the holder at the bottom left.
    links(draw, [(0.10 * s, 0.90 * s), (cx - 0.18 * s, cy + 0.06 * s)], 0.07 * s, WHITE, None)


def stake(image, draw, s):
    floor = 0.72 * s
    draw.line([(0.08 * s, floor), (0.92 * s, floor)], fill=WHITE, width=int(0.03 * s))
    # The weight half in the floor, its spike below.
    cx, r = 0.5 * s, 0.12 * s
    draw.pieslice([cx - r, floor - r, cx + r, floor + r], 180, 360, fill=WHITE)
    draw.polygon([(cx - 0.04 * s, floor + 0.02 * s), (cx + 0.04 * s, floor + 0.02 * s), (cx, floor + 0.16 * s)], fill=WHITE)
    # Cracks either side.
    for side in (-1, 1):
        x0 = cx + side * (r + 0.03 * s)
        draw.line([(x0, floor + 0.03 * s), (x0 + side * 0.08 * s, floor + 0.09 * s), (x0 + side * 0.18 * s, floor + 0.11 * s)],
                  fill=WHITE, width=int(0.022 * s))
    # A taut chain rising from the weight.
    links(draw, [(cx, floor - r - 0.02 * s), (0.78 * s, 0.10 * s)], 0.07 * s, WHITE, None)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    weapon()
    icon("IconSnag.png", snag)
    icon("IconStake.png", stake)
