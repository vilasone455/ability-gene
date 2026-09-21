#!/usr/bin/env python3
"""The two round textures Judgement Cut's ball of cut space is drawn from. Requires Pillow.

Run from any directory: python3 make_vergil_textures.py

Ball.png   white, opaque to 80 % of the radius then soft to nothing at the rim: the dark inside
Shell.png  white, clear in the middle, alpha rising as radius^3.2 toward the rim and soft over
           the last 7 %: the blue shell that brightens toward its edge

Both are the formulas of the VFX lab's "lab/vergil-ball" and "lab/vergil-shell" stand-ins
(Tools/VfxLab/web/sketches/lib/vergil.js), which the Judgement Cut sketch was tuned with, so
the game draws the pixels the sketch was judged on. A flat disc and a ring cannot make this
shape: the ball needs a soft edge and the shell needs a falloff that is clear at the centre.
"""
from pathlib import Path
import math

from PIL import Image

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Vergil"
SIZE = 256


def ease(a, b, x):
    """The lab's smoothstep, clamped."""
    v = min(1.0, max(0.0, (x - a) / (b - a)))
    return v * v * (3 - 2 * v)


def write(name, alpha_at):
    image = Image.new("RGBA", (SIZE, SIZE))
    pixels = image.load()
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = (x + 0.5) / SIZE, (y + 0.5) / SIZE
            r = math.hypot(u - 0.5, v - 0.5) * 2
            pixels[x, y] = (255, 255, 255, max(0, min(255, round(alpha_at(r) * 255))))
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / name)
    print(f"{OUT / name}  {SIZE}x{SIZE}")


write("Ball.png", lambda r: 1 - ease(0.8, 1, r))
write("Shell.png", lambda r: 0 if r >= 1 else pow(r, 3.2) * (1 - ease(0.93, 1, r)))
