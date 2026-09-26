#!/usr/bin/env python3
"""Textures for the resonance device and the Echo gizmos. Requires Pillow. Placeholders.

Run from any directory: python3 make_echo_textures.py

Device.png        256 px, drawn over the device's 2x2 cells. A dark octagonal base plate, four
                  steel pylons at the corners, and a round blue lens in the middle with a pale core.
IconManifest.png  128 px, white with the shape in the alpha, as the kit icons are. A standing figure
                  with a ring rising around it from the floor.
IconTrials.png    128 px. A checklist: three rows, the first two ticked.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Echo"
SCALE = 4

PLATE, PLATE_EDGE, STEEL, STEEL_LIT = (38, 40, 46), (70, 74, 84), (120, 126, 138), (180, 186, 196)
LENS, LENS_LIT, CORE = (40, 110, 200), (90, 160, 240), (215, 235, 255)
WHITE = (255, 255, 255, 255)


def canvas(size):
    return Image.new("RGBA", (size * SCALE, size * SCALE), (0, 0, 0, 0))


def finish(image, size, name):
    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((size, size), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


def octagon(cx, cy, r, s):
    return [((cx + r * math.cos(math.radians(22.5 + 45 * i))) * s,
             (cy + r * math.sin(math.radians(22.5 + 45 * i))) * s) for i in range(8)]


def circle(d, cx, cy, r, s, fill, outline=None, width=0):
    d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=fill,
              outline=outline, width=int(width * s) if width else 0)


def device():
    size = 256
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    d.polygon(octagon(0.5, 0.5, 0.47, s), fill=PLATE_EDGE)
    d.polygon(octagon(0.5, 0.5, 0.43, s), fill=PLATE)
    for cx, cy in ((0.2, 0.2), (0.8, 0.2), (0.2, 0.8), (0.8, 0.8)):
        circle(d, cx, cy, 0.075, s, STEEL)
        circle(d, cx - 0.015, cy - 0.015, 0.035, s, STEEL_LIT)
    circle(d, 0.5, 0.5, 0.24, s, PLATE_EDGE)
    circle(d, 0.5, 0.5, 0.2, s, LENS)
    circle(d, 0.47, 0.47, 0.13, s, LENS_LIT)
    circle(d, 0.46, 0.46, 0.06, s, CORE)
    finish(image, size, "Device.png")


def icon_manifest():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    circle(d, 0.5, 0.26, 0.09, s, WHITE)
    d.polygon([(0.38 * s, 0.38 * s), (0.62 * s, 0.38 * s), (0.58 * s, 0.66 * s), (0.42 * s, 0.66 * s)], fill=WHITE)
    d.rectangle([0.42 * s, 0.64 * s, 0.48 * s, 0.86 * s], fill=WHITE)
    d.rectangle([0.52 * s, 0.64 * s, 0.58 * s, 0.86 * s], fill=WHITE)
    for y, rx in ((0.86, 0.34), (0.66, 0.3), (0.46, 0.26)):
        d.ellipse([(0.5 - rx) * s, (y - rx * 0.28) * s, (0.5 + rx) * s, (y + rx * 0.28) * s],
                  outline=WHITE, width=int(0.022 * s))
    finish(image, size, "IconManifest.png")


def icon_trials():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    for i, ticked in enumerate((True, True, False)):
        y = 0.24 + i * 0.26
        d.rectangle([0.14 * s, (y - 0.08) * s, 0.3 * s, (y + 0.08) * s], outline=WHITE, width=int(0.025 * s))
        if ticked:
            d.line([(0.16 * s, y * s), (0.21 * s, (y + 0.06) * s), (0.33 * s, (y - 0.1) * s)],
                   fill=WHITE, width=int(0.04 * s))
        d.rectangle([0.4 * s, (y - 0.03) * s, 0.86 * s, (y + 0.03) * s], fill=WHITE)
    finish(image, size, "IconTrials.png")


if __name__ == "__main__":
    device()
    icon_manifest()
    icon_trials()
