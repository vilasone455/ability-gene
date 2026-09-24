#!/usr/bin/env python3
"""The four textures Unlimited Void is drawn with. Requires Pillow.

Run from any directory: python3 make_gojo_textures.py

Splatter.png   white ink blot with droplets: the white patches in the void and the dust's ink bits
HoleGas.png    white, the black hole's gas: a light rim hugging the disc, then feathery streaks
HoleWisps.png  white, the pale streaks laid over the gas, which turn faster
HoleRing.png   the thin ring round the hole, colours baked in: gold outside, white, blue inside,
               brightest and widest top left, faint on the east

All four are the formulas of the VFX lab's "lab/gojo-splatter", "lab/gojo-hole-gas",
"lab/gojo-hole-wisps" and "lab/gojo-hole-ring" (Tools/VfxLab/web/sketches/lib/unlimited-void.js),
which the Unlimited Void sketches were tuned with, so the game draws the pixels the sketches were
judged on. hash, noise and fbm are the lab's, from make_six_paths_textures.py. Pixel (x, y) is the
formula at (x / n, y / n) with row 0 at the top, as the lab's pixels() samples it, and a byte is
value * 255 rounded half to even, as a canvas stores it.
"""
from pathlib import Path
import math

from PIL import Image

from make_six_paths_textures import hash01, fbm

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Gojo"
TAU = math.pi * 2
DEG = math.pi / 180

# The black hole quads (lib/unlimited-void.js): the ring sits at RING_AT times the disc radius on a
# quad RING_QUAD times the disc radius across each way from the centre.
RING_AT, RING_QUAD = 2.2, 2.6


def clamp(v):
    return 0.0 if v < 0 else 1.0 if v > 1 else v


def ease(a, b, x):
    """The lab's ss(): smoothstep from a to b, clamped."""
    t = clamp((x - a) / (b - a))
    return t * t * (3 - 2 * t)


def byte(v):
    return round(clamp(v) * 255)


def write(name, size, colour_at):
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            r, g, b, a = colour_at(x / size, y / size)
            pixels[x, y] = (byte(r), byte(g), byte(b), byte(a))
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / name)
    print(f"{OUT / name}  {size}x{size}")


def splatter(u, v):
    r = math.hypot(u - 0.5, v - 0.5) * 2
    if r > 0.96:
        return 1, 1, 1, 0
    n = fbm(u * 4, v * 4, 313, 4, 4)
    m = fbm(u * 11, v * 11, 919, 3, 11)
    a = clamp((0.6 - r + (n - 0.5) * 0.75 + (m - 0.5) * 0.2) * 14)
    for i in range(16):
        ang = hash01(i, 1, 77) * TAU
        d = 0.3 + 0.14 * hash01(i, 2, 77)
        rr = 0.012 + 0.026 * hash01(i, 3, 77)
        a = max(a, clamp((rr - math.hypot(u - 0.5 - math.cos(ang) * d, v - 0.5 - math.sin(ang) * d)) / 0.006))
    return 1, 1, 1, a * clamp((0.96 - r) / 0.06)


def polar(f):
    """The lab's polarPixels: r 0 at the centre to 1 at the edge, phi 0..1 counter-clockwise from east."""
    def colour_at(u, v):
        dx, dz = u - 0.5, 0.5 - v
        r = math.hypot(dx, dz) * 2
        if r >= 1:
            return 0, 0, 0, 0
        return f(r, (math.atan2(dz, dx) / TAU + 1) % 1)
    return colour_at


def hole_gas(r, phi):
    n = fbm((phi + r * 0.55) * 12, r * 7, 404, 4, 12)
    rim = 0.85 * math.exp(-(((r - 0.56) / 0.04) ** 2)) * ease(0.47, 0.52, r)
    band = ease(0.5, 0.64, r) * (1 - ease(0.82, 0.99, r))
    return 1, 1, 1, clamp(rim + band * (0.2 + 0.8 * n * n))


def hole_wisps(r, phi):
    n = fbm((phi + r * 0.8) * 16, r * 11, 505, 4, 16)
    band = ease(0.53, 0.66, r) * (1 - ease(0.88, 0.98, r))
    return 1, 1, 1, band * ease(0.58, 0.86, n)


def hole_ring(r, phi):
    r0, deg = RING_AT / RING_QUAD, phi * 360
    round_ = 0.3 + 0.7 * (0.5 + 0.5 * math.cos((deg - 125) * DEG))
    w = 0.012 + 0.01 * round_
    d = (r - r0) / w
    if abs(d) > 5:
        return 0, 0, 0, 0
    east = 1 - 0.8 * math.exp(-(((math.fmod(deg + 180, 360) - 185) / 38) ** 2))
    t = clamp((d + 1.8) / 3.6)
    gold = clamp(0.2 + 0.8 * math.cos((deg - 110) * DEG))
    warm = (1, 0.78 + 0.1 * (1 - gold), 0.35 + 0.45 * (1 - gold))
    if t < 0.5:
        col = (0.45 + 0.55 * t * 2, 0.7 + 0.3 * t * 2, 1)
    else:
        col = tuple(1 + (w_ - 1) * (t - 0.5) * 2 for w_ in warm)
    a = clamp((math.exp(-d * d * 1.1) + 0.3 * math.exp(-d * d * 0.12)) * round_ * east)
    return col[0], col[1], col[2], a


if __name__ == "__main__":
    write("Splatter.png", 256, splatter)
    write("HoleGas.png", 512, polar(hole_gas))
    write("HoleWisps.png", 512, polar(hole_wisps))
    write("HoleRing.png", 512, polar(hole_ring))
