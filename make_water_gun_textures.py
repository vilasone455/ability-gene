#!/usr/bin/env python3
"""Textures for the Water Gun kit: the weapon and the two ability icons. Requires Pillow.

Run from any directory: python3 make_water_gun_textures.py

WaterGun.png        256 px. The held weapon, drawn the way Core draws a gun: level, muzzle to the
                    right. A dark body with an orange stripe, an orange pump grip under it, a steel
                    nozzle, a pistol grip, and a short stub of hose leaving the back. Body, accent,
                    steel and hose are the colours lib/water-gun.js draws the gun with, so the gun
                    in the hand and the gun in a cast's picture match.
IconStreamShot.png  128 px each, white with the shape in the alpha, as Core's ability icons are.
IconHydroPump.png   Stream Shot: the gun's nozzle and one jet with a rounded front and two drops.
                    Hydro Pump: the nozzle and five jets fanning out into a cone, with the cone's
                    end line.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/WaterGun"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

# lib/water-gun.js: Gun, GunLit, GunDark, Accent, Steel.
GUN, GUN_LIT, GUN_DARK = (38, 43, 54), (77, 84, 102), (13, 13, 18)
ACCENT, STEEL = (242, 153, 41), (153, 161, 173)
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


def weapon():
    size = 256
    s = size * SCALE
    image = canvas(size)
    d = ImageDraw.Draw(image)
    edge = int(0.012 * s)
    # Hose stub leaving the back of the body, curling down.
    d.arc([0.02 * s, 0.40 * s, 0.26 * s, 0.70 * s], 180, 300, fill=GUN_DARK + (255,), width=int(0.05 * s))
    d.arc([0.02 * s, 0.40 * s, 0.26 * s, 0.70 * s], 180, 300, fill=GUN + (255,), width=int(0.03 * s))
    # Pistol grip, below the rear of the body.
    d.polygon([(0.22 * s, 0.52 * s), (0.34 * s, 0.52 * s), (0.30 * s, 0.74 * s), (0.18 * s, 0.74 * s)],
              fill=GUN + (255,), outline=GUN_DARK + (255,))
    # Pump grip under the front half.
    d.rounded_rectangle([0.46 * s, 0.53 * s, 0.68 * s, 0.62 * s], radius=int(0.02 * s), fill=ACCENT + (255,), outline=GUN_DARK + (255,), width=edge)
    for x in (0.50, 0.55, 0.60, 0.65):
        d.line([x * s, 0.545 * s, x * s, 0.605 * s], fill=GUN_DARK + (255,), width=int(0.008 * s))
    # Body.
    d.rounded_rectangle([0.14 * s, 0.40 * s, 0.70 * s, 0.54 * s], radius=int(0.04 * s), fill=GUN + (255,), outline=GUN_DARK + (255,), width=edge)
    d.rectangle([0.18 * s, 0.415 * s, 0.66 * s, 0.44 * s], fill=GUN_LIT + (255,))
    d.rectangle([0.26 * s, 0.46 * s, 0.50 * s, 0.50 * s], fill=ACCENT + (255,))
    # Steel nozzle and its bore.
    d.rectangle([0.70 * s, 0.435 * s, 0.94 * s, 0.505 * s], fill=STEEL + (255,), outline=GUN_DARK + (255,), width=edge)
    d.rectangle([0.94 * s, 0.445 * s, 0.97 * s, 0.495 * s], fill=GUN_DARK + (255,))
    finish(image, size, "WaterGun.png")


def icon(name, paint):
    size = 128
    image = canvas(size)
    paint(image, ImageDraw.Draw(image), size * SCALE)
    finish(image, size, name)


def nozzle(draw, s):
    """The gun's front, bottom left, pointing up-right."""
    bar(draw, (0.06 * s, 0.94 * s), (0.30 * s, 0.70 * s), 0.16 * s, WHITE)
    bar(draw, (0.28 * s, 0.72 * s), (0.38 * s, 0.62 * s), 0.08 * s, WHITE)


def jet(draw, a, b, width):
    """A jet from a to b that swells toward a rounded front."""
    steps = 24
    for k in range(steps):
        u = k / steps
        x, y = a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u
        r = width * (0.35 + 0.65 * u) / 2
        draw.ellipse([x - r, y - r, x + r, y + r], fill=WHITE)
    r = width * 0.62
    draw.ellipse([b[0] - r, b[1] - r, b[0] + r, b[1] + r], fill=WHITE)


def stream_shot(image, draw, s):
    nozzle(draw, s)
    jet(draw, (0.40 * s, 0.60 * s), (0.76 * s, 0.24 * s), 0.12 * s)
    for cx, cy, r in ((0.62 * s, 0.56 * s, 0.035 * s), (0.72 * s, 0.46 * s, 0.028 * s)):
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=WHITE)


def hydro_pump(image, draw, s):
    nozzle(draw, s)
    start = (0.40 * s, 0.60 * s)
    for k in range(5):
        a = math.radians(45 + (k - 2) * 13)
        end = (start[0] + math.cos(a) * 0.46 * s, start[1] - math.sin(a) * 0.46 * s)
        jet(draw, start, end, 0.065 * s)
    # The cone's end line.
    ends = [math.radians(45 + (k - 2) * 13) for k in (0, 4)]
    p = [(start[0] + math.cos(a) * 0.55 * s, start[1] - math.sin(a) * 0.55 * s) for a in ends]
    bar(draw, p[0], p[1], 0.03 * s, WHITE)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    weapon()
    icon("IconStreamShot.png", stream_shot)
    icon("IconHydroPump.png", hydro_pump)
