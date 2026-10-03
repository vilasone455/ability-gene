#!/usr/bin/env python3
"""Sprites for the Rainbow Crystal Staff's projectile. Requires Pillow.

Run from any directory: python3 make_rainbow_crystal_textures.py

All four are white with the shape in the alpha, coloured per draw and drawn additive (MoteGlow).
They are the formulas the Rainbow Crystal sketch (Tools/VfxLab/web/sketches/rainbow-crystal.js) was
tuned with, sampled the way the lab's pixels() samples them (u, v = pixel / size, v = 0 the top row),
with fbm from make_six_paths_textures.py, the lab's own noise.

Trail.png      256 px. A star's trail, tail at the left (u 0) to head at the right (u 1): two feathered
               strands 0.22 either side of the middle that draw in toward the head, over a soft glow,
               brightening as u^1.5. Stretched from tail to head, 0.6 cells across.
TrailCore.png  256 px. The trail's white core: a thin line, brightest at the head (u^2).
Star.png       128 px. The four-point star, as Terraria's explosion sprite: a long upright ray (0.48 of
               the picture each way), a shorter level one (0.3), a bright core and a halo.
Plus.png        64 px. The + sparkle: two crossed bars with soft ends and a bright middle.

The outermost ring of pixels is cleared in every picture, so a quad's edge never shows as a line
(only the two trail pictures' head ends had alpha there, up to 29 and 40 of 255). Inside that ring
the pixels equal the lab's generated ones exactly (checked against the sketch's textures, 2026-10-02).
"""
from pathlib import Path
import math

from PIL import Image

from make_six_paths_textures import fbm

OUT = Path(__file__).resolve().parent / "Textures/RimArt/RainbowCrystal"


def clamp01(x):
    return max(0.0, min(1.0, x))


def gauss(x, w):
    return math.exp(-(x * x) / (2 * w * w))


def inside(x, a, b):
    """0 at both edges of 0..1, rising over a at the start and b at the end."""
    return clamp01(x / a) * clamp01((1 - x) / b)


def trail(u, v):
    y, pull = v - 0.5, 0.45 + 0.55 * (1 - u)
    a = 0.45 * gauss(y, 0.17) + 0.35 * gauss(y, 0.04)
    for k, side in enumerate((-1, 1)):
        a += 0.95 * gauss(y - side * 0.22 * pull, 0.022 + 0.02 * (1 - u)) * (0.45 + 0.55 * fbm(u * 10, k * 4 + 0.5, 31 + k, 3, 10))
    return a * u ** 1.5 * inside(u, 0.02, 0.04) * inside(v, 0.04, 0.04)


def trail_core(u, v):
    return 1.2 * gauss(v - 0.5, 0.07) * u * u * inside(u, 0.02, 0.03)


def star(u, v):
    x, y = u - 0.5, v - 0.5
    r = math.hypot(x, y)

    def ray(along, across, length):
        left = clamp01(1 - abs(along) / length)
        return gauss(across, 0.006 + 0.026 * left) * left ** 1.5

    a = max(ray(y, x, 0.48), ray(x, y, 0.3), gauss(r, 0.045)) + 0.3 * gauss(r, 0.14)
    return a * clamp01((0.5 - max(abs(x), abs(y))) / 0.02)


def plus(u, v):
    x, y = u - 0.5, v - 0.5

    def bar(along, across):
        return gauss(across, 0.045) * clamp01(1 - abs(along) / 0.42)

    return 1.2 * max(bar(x, y), bar(y, x)) + 0.5 * gauss(math.hypot(x, y), 0.08)


def write(name, size, alpha):
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    last = size - 1
    for py in range(size):
        for px in range(size):
            edge = px in (0, last) or py in (0, last)
            # The lab's canvas stores value * 255 rounded half to even, as Python's round() does.
            a = 0 if edge else round(clamp01(alpha(px / size, py / size)) * 255)
            pixels[px, py] = (255, 255, 255, a)
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / name)
    print("wrote", OUT / name)


if __name__ == "__main__":
    write("Trail.png", 256, trail)
    write("TrailCore.png", 256, trail_core)
    write("Star.png", 128, star)
    write("Plus.png", 64, plus)
