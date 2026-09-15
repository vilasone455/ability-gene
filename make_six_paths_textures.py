#!/usr/bin/env python3
"""Soft textures for the Six Paths slam. Requires Pillow.

Run from any directory: python3 make_six_paths_textures.py

SoftDisc.png  white, alpha (1 - r)^1.8: shadows, the impact flash, debris shadows
Puff.png      white, a radial falloff broken up by value noise: the impact dust

Both are the formulas of the VFX lab's "lab/soft-disc" and "lab/puff" stand-ins
(Tools/VfxLab/web/js/standins.js), which the Slam v2 sketch was tuned with. hash, noise and
fbm below are that file's, integer overflow included, so the game draws the pixels the sketch
was judged on.
"""
from pathlib import Path
import math

from PIL import Image

OUT = Path(__file__).resolve().parent / "Textures/RimArt/SixPaths"
SIZE = 128


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


def raster(alpha):
    image = Image.new("RGBA", (SIZE, SIZE))
    pixels = image.load()
    for py in range(SIZE):
        for px in range(SIZE):
            a = max(0.0, min(1.0, alpha(px / SIZE, py / SIZE)))
            # The lab writes a canvas byte as value * 255, truncated.
            pixels[px, py] = (255, 255, 255, int(a * 255))
    return image


def radial(u, v):
    return math.hypot(u - 0.5, v - 0.5) * 2


def soft_disc(u, v):
    return max(0.0, 1 - radial(u, v)) ** 1.8


def puff(u, v):
    return (1 - radial(u, v)) * 1.6 - 0.35 + (fbm(u * 4, v * 4, 71, 3, 4) - 0.5) * 0.9


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    raster(soft_disc).save(OUT / "SoftDisc.png")
    raster(puff).save(OUT / "Puff.png")
    print(f"wrote {OUT / 'SoftDisc.png'} and {OUT / 'Puff.png'}")


if __name__ == "__main__":
    main()
