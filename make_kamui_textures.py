#!/usr/bin/env python3
"""Textures for Kamui's dimension, the Obito kit's pocket map. Requires Pillow.

Run from any directory: python3 make_kamui_textures.py

Atlas.png       512 x 512, the block field's colours in the "fight" palette (the anime seen from above)
AtlasStill.png  the same in the "still" palette (the darker Narutopedia picture)
Fade.png        64 x 64, white, alpha rising west to east: the dark past the map's edge
Top.png         flat fallback for the walkable terrain (the mod draws the field over it)
Void.png        flat fallback for the void terrain

The atlas is 8 x 8 cells of 64 px. Row 0: walkable tops in 4 shades (cols 0-3) and a top above the
walkable level (col 4). Row 1: tops 1 to 6 cells down (cols 0-5). Rows 2-4 by level column (col 0
walkable, col 1 above, cols 2-7 one to six cells down): 2 south faces, fading from the face colour at
the top of the cell to void at the bottom; 3 edge lines; 4 face corner lines, fading like the faces.
Row 5: col 0 void, col 1 the patch tile (alpha). The mesh samples each cell inside a 5 px border.

The formulas are the ones the lab sketch was tuned with (Tools/VfxLab/web/sketches/lib/kamui.js, the
Kamui dimension sketch), and hash, noise and fbm below are Tools/VfxLab/web/js/standins.js's, integer
overflow included, so the game draws the pixels the sketch was judged on. The colours were measured
from the references (browser research 2026-09-24, see the palettes in lib/kamui.js).
"""
from pathlib import Path
import math

from PIL import Image

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Kamui"
ATLAS_N, CELL, PAD = 8, 64, 5
SIDE = ATLAS_N * CELL


def rgb(r, g, b):
    return (r / 255, g / 255, b / 255)


PALETTES = {
    "Atlas": {"top": rgb(151, 171, 188), "edge": rgb(196, 222, 244), "face": rgb(80, 95, 105), "void": rgb(10, 16, 22)},
    "AtlasStill": {"top": rgb(85, 100, 122), "edge": rgb(113, 131, 155), "face": rgb(24, 32, 40), "void": rgb(7, 13, 20)},
}
SHADES = [0.95, 0.985, 1.015, 1.05]
# How far a block n cells down has gone toward void.
DOWN = [0, 0.52, 0.64, 0.74, 0.82, 0.88, 0.93]


def i32(v):
    v &= 0xFFFFFFFF
    return v - 0x100000000 if v & 0x80000000 else v


def hash01(x, y, seed):
    h = i32(x * 374761393 + y * 668265263 + seed * 144665)
    h = i32((h ^ ((h & 0xFFFFFFFF) >> 13)) * 1274126177)
    return ((h ^ ((h & 0xFFFFFFFF) >> 16)) & 0xFFFFFFFF) / 4294967295


def noise(x, y, period, seed):
    xi, yi = math.floor(x), math.floor(y)
    xf, yf = x - xi, y - yi
    s = lambda t: t * t * (3 - 2 * t)
    at = lambda i, j: hash01(i % period, j % period, seed)
    a, b, c, d = at(xi, yi), at(xi + 1, yi), at(xi, yi + 1), at(xi + 1, yi + 1)
    return a + (b - a) * s(xf) + (c - a) * s(yf) + (a - b - c + d) * s(xf) * s(yf)


def fbm(x, y, seed, octaves, period):
    v, amp, total = 0.0, 0.5, 0.0
    for o in range(octaves):
        v += noise(x * (1 << o), y * (1 << o), period * (1 << o), seed + o) * amp
        total += amp
        amp *= 0.5
    return v / total


def clamp(v):
    return 0.0 if v < 0 else 1.0 if v > 1 else v


def smooth(t):
    t = clamp(t)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    t = clamp(t)
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def mul(a, k):
    return tuple(x * k for x in a)


def level_colours(pal, col):
    """Top, face and edge colour for an atlas level column: 0 walkable, 1 above, 2-7 one to six down."""
    if col == 0:
        return pal["top"], pal["face"], pal["edge"]
    if col == 1:
        return mix(pal["top"], pal["void"], 0.45), mix(pal["face"], pal["void"], 0.25), mix(pal["edge"], pal["void"], 0.5)
    t = DOWN[col - 1]
    return mix(pal["top"], pal["void"], t), mix(pal["face"], pal["void"], t), mix(pal["edge"], pal["void"], min(1.0, t + 0.1))


def atlas_colour(pal, col, row, lu, lv):
    """RGBA 0..1 for a point of a cell; lu, lv 0..1 inside the cell, lv 0 at the top (north)."""
    r = math.hypot(lu - 0.5, lv - 0.5) * math.sqrt(2)
    pool = 1 - 0.13 * smooth((r - 0.2) / 0.8)                      # lighter middle (Storm 4 from above)
    if row == 0 and col <= 4:
        base = mul(pal["top"], SHADES[col]) if col < 4 else level_colours(pal, 1)[0]
        return mul(base, pool) + (1.0,)
    if row == 1 and col <= 5:
        return mul(level_colours(pal, col + 2)[0], 0.6 + 0.4 * pool) + (1.0,)
    top, face, edge = level_colours(pal, col)
    down = clamp(lv) ** 0.85
    if row == 2:
        streak = 1 + (fbm(lu * 8, 0.5, 23 + col, 2, 8) - 0.5) * 0.12  # faint vertical streaks
        return mix(mul(face, streak), pal["void"], down) + (1.0,)
    if row == 3:
        return edge + (1.0,)
    if row == 4:
        return mix(mul(edge, 0.85), pal["void"], down) + (1.0,)
    if row == 5 and col == 0:
        return pal["void"] + (1.0,)
    if row == 5 and col == 1:
        # The patch tile: repeats exactly across the part the mesh samples (inside the border).
        iu, iv = (lu * CELL - PAD) / (CELL - 2 * PAD), (lv * CELL - PAD) / (CELL - 2 * PAD)
        m = fbm(iu * 3, iv * 3, 57, 3, 3)
        return mul(pal["top"], 0.72) + (0.28 * smooth((m - 0.55) / 0.2),)
    return None


def byte(v):
    # A canvas byte: clamped, rounded half to even (ToUint8Clamp), as the lab writes its textures.
    return max(0, min(255, round(v * 255)))


def atlas(pal):
    image = Image.new("RGBA", (SIDE, SIDE))
    px = image.load()
    for y in range(SIDE):
        for x in range(SIDE):
            u, v = x / SIDE, y / SIDE
            col, row = min(ATLAS_N - 1, math.floor(u * ATLAS_N)), min(ATLAS_N - 1, math.floor(v * ATLAS_N))
            c = atlas_colour(pal, col, row, u * ATLAS_N - col, v * ATLAS_N - row)
            px[x, y] = tuple(byte(k) for k in c) if c else (0, 0, 0, 0)
    return image


def fade():
    image = Image.new("RGBA", (64, 64))
    px = image.load()
    for y in range(64):
        for x in range(64):
            px[x, y] = (255, 255, 255, byte(smooth((x / 64 - 0.08) / 0.92)))
    return image


def flat(colour):
    return Image.new("RGBA", (64, 64), tuple(byte(k) for k in colour) + (255,))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, pal in PALETTES.items():
        atlas(pal).save(OUT / f"{name}.png")
    fade().save(OUT / "Fade.png")
    flat(PALETTES["Atlas"]["top"]).save(OUT / "Top.png")
    flat(PALETTES["Atlas"]["void"]).save(OUT / "Void.png")
    for f in sorted(OUT.glob("*.png")):
        print(f"{f.relative_to(OUT.parent.parent.parent)}  {f.stat().st_size} bytes")


if __name__ == "__main__":
    main()
