#!/usr/bin/env python3
"""Textures for the Flame Gauntlet kit: the weapon, the two ability icons and the flame tongue.
Requires Pillow.

Run from any directory: python3 make_flame_gauntlet_textures.py

FlameGauntlet.png  256 px. The held weapon, drawn the way Core draws a melee weapon: the forearm
                   plate from the bottom left, the fist at the top right. Blackened iron with a lit
                   top strip, three brass seams, two elbow vents and four finger plates over the
                   knuckles. Colours are lib/flame-gauntlet.js's, so the gauntlet in the hand and the
                   gauntlet of a cast match. No fire on it: heat is drawn in game as a glow.
IconDevour.png     128 px each, white with the shape in the alpha, as Core's ability icons are.
IconRelease.png    Devour: an open claw at the bottom left with three arcs of flame curling into its
                   palm. Release: a fist at the bottom left and a fan of flame widening to the top right.
Tongue.png         64 x 128 px, white with a soft flame silhouette in the alpha: base at the bottom
                   centre, widest a third of the way up, pointed at the top. The sketch's tongue()
                   width curve. FlameGauntletGraphics tints and stretches it for every flame tongue,
                   so the port draws a quad where the sketch built a strip.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/FlameGauntlet"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

IRON, IRON_LIT, IRON_DARK = (41, 38, 41), (87, 82, 87), (13, 13, 15)
BRASS, BRASS_LIT = (158, 122, 56), (219, 184, 102)
EMBER = (219, 56, 13)
WHITE = (255, 255, 255, 255)


def canvas(width, height=None):
    return Image.new("RGBA", (width * SCALE, (height or width) * SCALE), (0, 0, 0, 0))


def finish(image, width, name, height=None):
    image.resize((width, height or width), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


def quad(draw, centre, length, width, degrees, colour):
    """A rectangle `length` along `degrees` (0 = right, 90 = up on the image) and `width` across."""
    r = math.radians(degrees)
    dx, dy = math.cos(r), -math.sin(r)
    nx, ny = -dy, dx
    cx, cy = centre
    hl, hw = length / 2, width / 2
    pts = [(cx + dx * hl + nx * hw, cy + dy * hl + ny * hw), (cx + dx * hl - nx * hw, cy + dy * hl - ny * hw),
           (cx - dx * hl - nx * hw, cy - dy * hl - ny * hw), (cx - dx * hl + nx * hw, cy - dy * hl + ny * hw)]
    draw.polygon(pts, fill=colour)


def along(p, degrees, distance, side=0.0):
    r = math.radians(degrees)
    dx, dy = math.cos(r), -math.sin(r)
    return (p[0] + dx * distance - dy * side, p[1] + dy * distance + dx * side)


def gauntlet():
    size = 256
    s = SCALE
    im = canvas(size)
    d = ImageDraw.Draw(im)
    deg = 45  # bottom left to top right
    wrist = (158 * s, 98 * s)
    arm_len, arm_w = 150 * s, 54 * s
    mid = along(wrist, deg, -arm_len / 2)
    quad(d, mid, arm_len + 10 * s, arm_w + 10 * s, deg, IRON_DARK)
    quad(d, mid, arm_len, arm_w, deg, IRON)
    quad(d, along(mid, deg, 0, arm_w * 0.22), arm_len * 0.92, arm_w * 0.32, deg, IRON_LIT)
    for i in range(3):
        quad(d, along(wrist, deg, -arm_len * (0.22 + i * 0.26)), 8 * s, arm_w + 8 * s, deg, BRASS)
    for k in (-1, 1):
        quad(d, along(wrist, deg, -arm_len * 0.85, k * arm_w * 0.28), 24 * s, 10 * s, deg, IRON_DARK)
    fist = along(wrist, deg, 18 * s)
    r = 44 * s
    d.ellipse([fist[0] - r, fist[1] - r * 0.92, fist[0] + r, fist[1] + r * 0.92], fill=IRON_DARK)
    r = 37 * s
    d.ellipse([fist[0] - r, fist[1] - r * 0.92, fist[0] + r, fist[1] + r * 0.92], fill=IRON)
    r = 20 * s
    d.ellipse([fist[0] - r - 4 * s, fist[1] - r - 6 * s, fist[0] + r - 4 * s, fist[1] + r - 6 * s], fill=IRON_LIT)
    for i in range(4):
        side = (i - 1.5) * 16 * s
        base = along(fist, deg, 14 * s, side)
        c = along(base, deg, 22 * s)
        quad(d, c, 50 * s, 18 * s, deg, IRON_DARK)
        quad(d, c, 44 * s, 12 * s, deg, IRON)
        quad(d, along(base, deg, 6 * s), 10 * s, 14 * s, deg, BRASS)
    finish(im, size, "FlameGauntlet.png")


def flame_polygon(base, height, width, degrees=90, bend=0.0, steps=24):
    """A flame silhouette: the sketch's tongue width curve along a line from `base`, bent sideways by `bend`."""
    left, right = [], []
    for i in range(steps + 1):
        u = i / steps
        w = width * math.sin(math.pi * min(1, 0.12 + 0.88 * u)) ** 0.9 * (1 - u * 0.45)
        p = along(base, degrees, height * u, bend * u * u * height)
        left.append(along(p, degrees + 90, w))
        right.append(along(p, degrees - 90, w))
    return left + right[::-1]


def icon_devour():
    size = 128
    s = SCALE
    im = canvas(size)
    d = ImageDraw.Draw(im)
    palm = (40 * s, 90 * s)
    # A spread claw: the palm and four fingers fanned toward the top right.
    r = 17 * s
    d.ellipse([palm[0] - r, palm[1] - r, palm[0] + r, palm[1] + r], fill=WHITE)
    for i in range(4):
        a = 45 + (i - 1.5) * 26
        quad(d, along(palm, a, 25 * s), 28 * s, 9 * s, a, WHITE)
    quad(d, along(palm, 225, 22 * s), 34 * s, 22 * s, 225, WHITE)
    # Three flames curling in from the top right toward the palm.
    for j, (start, bend) in enumerate((((112, 20), -0.35), ((118, 56), 0.2), ((80, 14), 0.3))):
        sx, sy = start[0] * s, start[1] * s
        deg = math.degrees(math.atan2(-(palm[1] - sy), palm[0] - sx))
        dist = math.hypot(palm[0] - sx, palm[1] - sy)
        d.polygon(flame_polygon((sx, sy), dist * 0.62, (8 - j) * s, deg, bend), fill=WHITE)
    finish(im, size, "IconDevour.png")


def icon_release():
    size = 128
    s = SCALE
    im = canvas(size)
    d = ImageDraw.Draw(im)
    fist = (30 * s, 98 * s)
    r = 19 * s
    d.ellipse([fist[0] - r, fist[1] - r, fist[0] + r, fist[1] + r], fill=WHITE)
    quad(d, along(fist, 225, 22 * s), 36 * s, 24 * s, 225, WHITE)
    # A fan of five flames widening from the knuckles to the top right.
    for k in range(5):
        deg = 45 + (k - 2) * 11
        base = along(fist, 45, 22 * s)
        length = (78 - abs(k - 2) * 10) * s
        d.polygon(flame_polygon(base, length, (7 + (2 - abs(k - 2)) * 2) * s, deg, 0.04 * (k - 2)), fill=WHITE)
    finish(im, size, "IconRelease.png")


def tongue():
    width, height = 64, 128
    im = Image.new("L", (width * SCALE, height * SCALE), 0)
    d = ImageDraw.Draw(im)
    base = (width * SCALE / 2, height * SCALE * 0.97)
    d.polygon(flame_polygon(base, height * SCALE * 0.95, width * SCALE * 0.44, 90, 0.0, 48), fill=255)
    im = im.filter(ImageFilter.GaussianBlur(3 * SCALE))
    out = Image.new("RGBA", im.size, (255, 255, 255, 0))
    out.putalpha(im)
    finish(out, width, "Tongue.png", height)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    gauntlet()
    icon_devour()
    icon_release()
    tongue()
