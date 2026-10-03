#!/usr/bin/env python3
"""Textures for the Last Prism kit: the weapon on the ground and two button icons. Requires Pillow.

Run from any directory: python3 make_last_prism_textures.py

LastPrism.png     256 px. The prism lying on the ground, seen from above at the game's slant: a pyramid
                  pointing to the top right, its three long faces in the picture's pale, lavender and slate
                  blue (LastPrismPyramidGraphics), the violet base at the bottom left, pale ridges and a navy
                  outline. In the hand and while firing the picture draws the prism instead.
IconFire.png      128 px each, white with the shape in the alpha, as Core's ability icons are.
IconRetarget.png  Fire: a small pyramid at the bottom left with a fan of six beams to the top right.
                  Retarget: a beam from the bottom left bending at a curved arrow to a ring at the top right.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/LastPrism"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

PALE, LAVENDER, SLATE = (242, 242, 255), (204, 179, 250), (117, 143, 204)
BASE, NAVY, RIDGE = (115, 82, 184), (31, 31, 87), (240, 242, 255)
WHITE = (255, 255, 255, 255)


def canvas(width):
    return Image.new("RGBA", (width * SCALE, width * SCALE), (0, 0, 0, 0))


def finish(image, width, name):
    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((width, width), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


def pt(x, y):
    return (x * SCALE, y * SCALE)


def prism():
    """The pyramid: tip at the top right, base triangle at the bottom left, three faces seen from above."""
    im = canvas(256)
    d = ImageDraw.Draw(im)
    tip = (214, 50)
    a, b, c = (52, 150), (96, 214), (118, 132)  # base corners: left, bottom, top (c is the near ridge's foot)
    faces = [((tip, a, c), PALE), ((tip, c, b), LAVENDER), ((tip, b, a), SLATE)]
    # The far face (tip, b, a) is drawn first; the two near faces cover it along the ridges.
    for poly, colour in (faces[2], faces[0], faces[1]):
        d.polygon([pt(*p) for p in poly], fill=colour)
    d.polygon([pt(*a), pt(*c), pt(*b)], fill=BASE)
    # The base is turned toward the viewer only a little: a thin violet sliver on the left edge.
    sliver = [a, ((a[0] + c[0]) / 2 - 6, (a[1] + c[1]) / 2 + 8), b]
    d.polygon([pt(*p) for p in sliver], fill=BASE)
    d.line([pt(*tip), pt(*c)], fill=RIDGE, width=5 * SCALE)
    d.line([pt(*c), pt(*a)], fill=RIDGE, width=3 * SCALE)
    d.line([pt(*c), pt(*b)], fill=RIDGE, width=3 * SCALE)
    d.line([pt(*p) for p in (tip, a, b, tip)], fill=NAVY, width=6 * SCALE, joint="curve")
    # A glint on the pale face.
    d.ellipse([pt(150, 82), pt(166, 98)], fill=(255, 255, 255, 230))
    finish(im, 256, "LastPrism.png")


def icon_fire():
    im = canvas(128)
    d = ImageDraw.Draw(im)
    tip = (50, 78)
    d.polygon([pt(14, 92), pt(30, 116), pt(*tip)], fill=WHITE)
    d.polygon([pt(14, 92), pt(36, 86), pt(*tip)], fill=(255, 255, 255, 170))
    for k in range(6):
        ang = math.radians(45 + (k - 2.5) * 9)
        end = (tip[0] + math.cos(ang) * 86, tip[1] - math.sin(ang) * 86)
        d.line([pt(*tip), pt(*end)], fill=WHITE, width=5 * SCALE)
    finish(im, 128, "IconFire.png")


def icon_retarget():
    im = canvas(128)
    d = ImageDraw.Draw(im)
    d.line([pt(10, 118), pt(54, 74)], fill=WHITE, width=9 * SCALE)
    # A curved arrow from the beam's line round to the new target.
    box = [pt(30, 22), pt(110, 102)]
    d.arc(box, start=200, end=320, fill=WHITE, width=7 * SCALE)
    head = (101, 36)
    d.polygon([pt(head[0] + 12, head[1] - 6), pt(head[0] - 6, head[1] - 10), pt(head[0] + 4, head[1] + 10)], fill=WHITE)
    d.ellipse([pt(84, 46), pt(120, 82)], outline=WHITE, width=6 * SCALE)
    d.ellipse([pt(97, 59), pt(107, 69)], fill=WHITE)
    finish(im, 128, "IconRetarget.png")


if __name__ == "__main__":
    prism()
    icon_fire()
    icon_retarget()
