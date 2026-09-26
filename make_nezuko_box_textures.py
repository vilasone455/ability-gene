#!/usr/bin/env python3
"""Textures for the sleeper box (Nezuko's box). Requires Pillow.

Run from any directory: python3 make_nezuko_box_textures.py

Box.png           128 px. The box on the ground, seen from the front as the item: orange-brown
                  vertical planks, black iron bands at three heights and on the corners, a door with a
                  latch, and a strap loop at each side. The colours are the picture's
                  (NezukoBoxGraphics: Wood, WoodLit, WoodDark, Grain, Iron, Strap), so the item and the
                  worn box match. On a pawn the box is drawn by code, not with this texture.
IconGoIn.png      128 px, white with the shape in the alpha, as the kit icons are. The box with its door
                  open and an arrow going into the doorway.
IconComeOut.png   128 px. The box with its top lid flipped up and a figure leaping out on an arc.
IconLetOut.png    128 px. The box with its door open and a figure standing beside it.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/NezukoBox"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

WOOD, WOOD_LIT, WOOD_DARK = (179, 94, 43), (219, 133, 69), (107, 48, 20)
GRAIN, IRON, IRON_LIT, STRAP = (92, 38, 15), (23, 23, 26), (77, 77, 84), (41, 31, 23)
WHITE = (255, 255, 255, 255)


def canvas(size):
    return Image.new("RGBA", (size * SCALE, size * SCALE), (0, 0, 0, 0))


def finish(image, size, name):
    image.resize((size, size), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


def rect(draw, x0, y0, x1, y1, colour, s):
    draw.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], fill=colour)


def box():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    x0, x1, y0, y1 = 0.26, 0.74, 0.08, 0.94
    # Outline, then the planks with grain lines.
    rect(d, x0 - 0.02, y0 - 0.02, x1 + 0.02, y1 + 0.02, IRON, s)
    rect(d, x0, y0, x1, y1, WOOD, s)
    rect(d, x0, y0, x0 + 0.06, y1, WOOD_LIT, s)
    for k in range(1, 4):
        gx = x0 + (x1 - x0) * k / 4
        rect(d, gx - 0.005, y0 + 0.02, gx + 0.005, y1 - 0.02, GRAIN, s)
    # The door: a seam and a slightly lighter panel, and the latch.
    dx0, dx1, dy0, dy1 = x0 + 0.07, x1 - 0.07, y0 + 0.07, y1 - 0.07
    rect(d, dx0 - 0.012, dy0 - 0.012, dx1 + 0.012, dy1 + 0.012, GRAIN, s)
    rect(d, dx0, dy0, dx1, dy1, WOOD, s)
    rect(d, dx0, dy0, dx0 + 0.04, dy1, WOOD_LIT, s)
    rect(d, dx1 - 0.07, 0.47, dx1 - 0.02, 0.56, IRON, s)
    # Iron bands at three heights and corner brackets.
    for v in (0.12, 0.5, 0.86):
        y = y0 + (y1 - y0) * v
        rect(d, x0, y - 0.022, x1, y + 0.022, IRON, s)
        rect(d, x0, y - 0.022, x1, y - 0.012, IRON_LIT, s)
    for cx in (x0, x1 - 0.06):
        for cy in (y0, y1 - 0.07):
            rect(d, cx, cy, cx + 0.06, cy + 0.07, IRON, s)
    # Strap loops at the sides.
    for sx, ex in ((x0 - 0.02, x0 - 0.09), (x1 + 0.02, x1 + 0.09)):
        d.arc([min(sx, ex) * s, 0.22 * s, max(sx, ex) * s + 0.07 * s, 0.62 * s], 90 if ex < sx else 270, 270 if ex < sx else 90, fill=STRAP, width=int(0.035 * s))
    finish(image, size, "Box.png")


def icon_box(d, s, door=False, lid=False):
    """The box outline in white: a tall block with bands; an open door leaves its doorway empty."""
    x0, x1, y0, y1 = 0.30, 0.62, 0.30, 0.92
    w = int(0.04 * s)
    d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], outline=WHITE, width=w)
    for v in (0.45, 0.77):
        d.rectangle([x0 * s, v * s - w / 2, x1 * s, v * s + w / 2], fill=WHITE)
    if door:
        # The doorway cleared and the door swung out to the right.
        d.rectangle([(x0 + 0.06) * s, (y0 + 0.06) * s, (x1 - 0.06) * s, (y1 - 0.06) * s], fill=(0, 0, 0, 0))
        d.polygon([(x1 * s, (y0 + 0.04) * s), ((x1 + 0.16) * s, (y0 + 0.10) * s), ((x1 + 0.16) * s, (y1 + 0.02) * s), (x1 * s, (y1 - 0.04) * s)], fill=WHITE)
    if lid:
        # The top lid flipped up and back from its hinge at the left.
        d.polygon([(x0 * s, y0 * s), ((x0 - 0.10) * s, (y0 - 0.18) * s), ((x0 - 0.04) * s, (y0 - 0.22) * s), ((x0 + 0.05) * s, (y0 - 0.02) * s)], fill=WHITE)
    return x0, x1, y0, y1


def figure(d, s, cx, cy, r=0.07):
    d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=WHITE)
    d.polygon([((cx - 0.08) * s, (cy + 0.09) * s), ((cx + 0.08) * s, (cy + 0.09) * s), ((cx + 0.06) * s, (cy + 0.30) * s), ((cx - 0.06) * s, (cy + 0.30) * s)], fill=WHITE)


def icon_go_in():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    x0, x1, y0, y1 = icon_box(d, s, door=True)
    # An arrow from the left into the doorway.
    ay = (y0 + y1) / 2
    d.rectangle([0.04 * s, (ay - 0.035) * s, (x0 + 0.06) * s, (ay + 0.035) * s], fill=WHITE)
    d.polygon([((x0 + 0.05) * s, (ay - 0.10) * s), ((x0 + 0.20) * s, ay * s), ((x0 + 0.05) * s, (ay + 0.10) * s)], fill=WHITE)
    finish(image, size, "IconGoIn.png")


def icon_come_out():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    x0, x1, y0, y1 = icon_box(d, s, lid=True)
    # The leap: a dashed arc from the top to the upper right, and the figure at its end.
    pts = []
    for i in range(13):
        u = i / 12
        x = (x0 + x1) / 2 + u * 0.36
        y = y0 - math.sin(u * math.pi) * 0.24 + u * 0.02
        pts.append((x * s, y * s))
    for i in range(0, 12, 2):
        d.line([pts[i], pts[i + 1]], fill=WHITE, width=int(0.03 * s))
    figure(d, s, 0.86, 0.22, 0.06)
    finish(image, size, "IconComeOut.png")


def icon_let_out():
    size = 128
    image = canvas(size)
    d = ImageDraw.Draw(image)
    s = size * SCALE
    icon_box(d, s, door=True)
    figure(d, s, 0.13, 0.52, 0.065)
    finish(image, size, "IconLetOut.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    box()
    icon_go_in()
    icon_come_out()
    icon_let_out()
