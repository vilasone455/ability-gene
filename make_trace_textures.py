#!/usr/bin/env python3
"""Textures for Unlimited Blade Works, the Trace kit's pocket world. Requires Pillow.

Run from any directory: python3 make_trace_textures.py

Flame.png    128 x 128, white, alpha the shape of one standing flame (lib/ubw.js lab/ubw-flame)
Earth.png    256 x 256, the world's ground: red-brown dust over two sizes of dry cracks, a few pale
             pebbles (lib/ubw-pocket.js lab/ubw-earth); one whole copy per 8-cell tile of the floor
Fade.png     64 x 64, white, alpha rising west to east as a smoothstep (lib/ubw-pocket.js lab/ubw-fade):
             the soft inner edge of the white and the soot outside the wall of fire
Terrain.png  64 x 64, flat, the fallback for the AG_UbwEarth terrain (the mod draws Earth over it)

The formulas are the ones the lab sketches were tuned with, and hash, noise and fbm below are
Tools/VfxLab/web/js/standins.js's, integer overflow included, so the game draws the pixels the sketches
were judged on.
"""
from pathlib import Path
import math

from PIL import Image

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Trace"
EARTH_DARK, EARTH_LIT = (.22, .12, .08), (.58, .38, .25)


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
    at = lambda i, j: hash01(((i % period) + period) % period, ((j % period) + period) % period, seed)
    a, b, c, d = at(xi, yi), at(xi + 1, yi), at(xi, yi + 1), at(xi + 1, yi + 1)
    return a + (b - a) * s(xf) + (c - a) * s(yf) + (a - b - c + d) * s(xf) * s(yf)


def fbm(x, y, seed, octaves=4, period=8):
    v, amp, total = 0.0, 0.5, 0.0
    for o in range(octaves):
        v += noise(x * (1 << o), y * (1 << o), period * (1 << o), seed + o) * amp
        total += amp
        amp *= 0.5
    return v / total


def clamp(v):
    return 0.0 if v < 0 else 1.0 if v > 1 else v


def edge(lo, hi, x):
    t = clamp((x - lo) / (hi - lo))
    return t * t * (3 - 2 * t)


def border(u, v, period, seed):
    """Distance between the nearest two of a jittered grid's points: the cracks of a tileable Voronoi."""
    x, y = u * period, v * period
    xi, yi = math.floor(x), math.floor(y)
    d1 = d2 = 9.0
    for j in (-1, 0, 1):
        for i in (-1, 0, 1):
            cx, cy = xi + i, yi + j
            wx, wy = ((cx % period) + period) % period, ((cy % period) + period) % period
            d = math.hypot(cx + .15 + .7 * hash01(wx, wy, seed) - x, cy + .15 + .7 * hash01(wx, wy, seed + 1) - y)
            if d < d1:
                d2, d1 = d1, d
            elif d < d2:
                d2 = d
    return d2 - d1


def byte(v):
    # A canvas byte: clamped, rounded, as the lab writes its textures.
    return max(0, min(255, round(v * 255)))


def pixels(n, fn):
    image = Image.new("RGBA", (n, n))
    px = image.load()
    for y in range(n):
        for x in range(n):
            px[x, y] = tuple(byte(k) for k in fn(x / n, y / n))
    return image


def flame(u, v):
    y = 1 - v
    n = fbm(u * 4, v * 3, 91, 3, 4)
    hw = .38 * (math.sqrt(y / .22) if y < .22 else max(0, 1 - (y - .22) / .78) ** .9)
    dx = abs(u - .5 + (n - .5) * .22 * y)
    return (1, 1, 1, clamp((hw - dx) / (.3 * hw + .02)) * min(1, (1 - y) * 5))


def earth(u, v):
    n, fine = fbm(u * 4, v * 4, 301, 4, 4), fbm(u * 16, v * 16, 305, 2, 16)
    big, small = border(u, v, 3, 311), border(u, v, 7, 331)
    k = (1 - .36 * (1 - edge(0, .045, big))) * (1 - .15 * (1 - edge(0, .03, small))) * (.92 + .16 * fine)
    pebble = .18 if hash01(math.floor(u * 128), math.floor(v * 128), 337) > .995 else 0
    t = .2 + .6 * n
    return ((EARTH_DARK[0] + (EARTH_LIT[0] - EARTH_DARK[0]) * t) * k + pebble,
            (EARTH_DARK[1] + (EARTH_LIT[1] - EARTH_DARK[1]) * t) * k + pebble * .8,
            (EARTH_DARK[2] + (EARTH_LIT[2] - EARTH_DARK[2]) * t) * k + pebble * .6, 1)


def fade(u, v):
    return (1, 1, 1, u * u * (3 - 2 * u))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    pixels(128, flame).save(OUT / "Flame.png")
    pixels(256, earth).save(OUT / "Earth.png")
    pixels(64, fade).save(OUT / "Fade.png")
    flat = tuple(byte((EARTH_DARK[i] + EARTH_LIT[i]) / 2 * .9) for i in range(3)) + (255,)
    Image.new("RGBA", (64, 64), flat).save(OUT / "Terrain.png")
    for f in sorted(OUT.glob("*.png")):
        print(f"{f.relative_to(OUT.parent.parent.parent)}  {f.stat().st_size} bytes")


if __name__ == "__main__":
    main()
