#!/usr/bin/env python3
"""Textures for the Paper Bomb kit: the weapon, the tag item and the three ability icons. Requires Pillow.

Run from any directory: python3 make_paper_bomb_textures.py

TagScroll.png    256 px. The held weapon, drawn the way Core draws a melee weapon: grip at the
                 bottom left, tip at the top right. A wooden rod with a roll of tags wound on its
                 upper two thirds, a red cord round the roll, and two loose tags hanging in a chain
                 from the middle of the roll. Paper, edge and ink are the colours lib/paper-bomb.js draws the
                 strip and the tags with, so the roll in the hand and the tags of a cast match.
Tags.png         128 px. The ammunition item on the ground: three tags fanned out. Also the fan of
                 tags in the hand in the Paper Shroud clip.
Tag.png          128 px each, for the hand in the animation clips (make_paper_bomb_anim.py), drawn
TagPin.png       the way the kunai texture is: up the image is the way it points. Tag: one tag.
                 TagPin: a steel pin with one tag tied behind it, what Tag Throw lets go of.
IconTagLine.png  128 px each, white with the shape in the alpha, as Core's ability icons are.
IconShroud.png   Tag Line: a strip of three tags and a spark at its near end. Shroud: a figure
IconTagThrow.png with three tags across its body. Tag Throw: a pin with a tag trailing and speed lines.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/PaperBomb"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges

PAPER, PAPER_SHADE, EDGE, INK = (237, 224, 191), (204, 189, 150), (84, 64, 43), (158, 31, 26)
WOOD, WOOD_DARK, STEEL = (122, 82, 46), (74, 48, 26), (51, 51, 61)
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


def tag(w, h, paper=PAPER + (255,), edge=EDGE + (255,), ink=INK + (255,)):
    """One tag, upright, w x h pixels plus a margin: border, inner frame, seal ring with a cross, two lines at each end."""
    m = int(w * 0.12)
    image = Image.new("RGBA", (w + 2 * m, h + 2 * m), (0, 0, 0, 0))
    d = ImageDraw.Draw(image)
    line = max(2, int(w * 0.045))
    d.rectangle([m, m, m + w, m + h], fill=paper, outline=edge, width=line)
    d.rectangle([m + w * 0.13, m + h * 0.06, m + w * 0.87, m + h * 0.94], outline=ink, width=line)
    cx, cy, r = m + w / 2, m + h / 2, w * 0.27
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=ink, width=int(line * 1.4))
    d.line([cx - r * 0.6, cy, cx + r * 0.6, cy], fill=ink, width=int(line * 1.4))
    d.line([cx, cy - r * 0.6, cx, cy + r * 0.6], fill=ink, width=int(line * 1.4))
    for k in (0.16, 0.24, 0.76, 0.84):
        d.line([m + w * 0.3, m + h * k, m + w * 0.7, m + h * k], fill=ink, width=line)
    return image


def paste(image, part, centre, degrees):
    part = part.rotate(degrees, resample=Image.BICUBIC, expand=True)
    image.alpha_composite(part, (int(centre[0] - part.width / 2), int(centre[1] - part.height / 2)))


def weapon():
    size = 256
    s = size * SCALE
    image = canvas(size)
    # Two loose tags first, so the roll overlaps where they leave it. They hang in a chain from the
    # middle of the roll, at right angles to the rod.
    paste(image, tag(int(0.15 * s), int(0.25 * s)), (0.80 * s, 0.70 * s), 38)
    paste(image, tag(int(0.15 * s), int(0.25 * s)), (0.66 * s, 0.54 * s), 45)
    # Rod and roll are drawn level, then turned 45 degrees onto the weapon diagonal.
    level = canvas(size)
    d = ImageDraw.Draw(level)
    mid = s / 2
    d.rounded_rectangle([0.06 * s, mid - 0.030 * s, 0.94 * s, mid + 0.030 * s], radius=int(0.02 * s), fill=WOOD + (255,), outline=WOOD_DARK + (255,), width=int(0.008 * s))
    left, right, half = 0.36 * s, 0.88 * s, 0.085 * s
    d.rounded_rectangle([left, mid - half, right, mid + half], radius=int(0.03 * s), fill=PAPER + (255,), outline=EDGE + (255,), width=int(0.010 * s))
    d.rectangle([left + 0.012 * s, mid + half * 0.30, right - 0.012 * s, mid + half - 0.012 * s], fill=PAPER_SHADE + (255,))
    for k in range(1, 5):                                   # the edges of the wound tags
        x = left + (right - left) * k / 5
        d.line([x, mid - half + 0.012 * s, x, mid + half - 0.012 * s], fill=INK + (255,), width=int(0.006 * s))
    d.rectangle([left + (right - left) * 0.42, mid - half, left + (right - left) * 0.58, mid + half], fill=INK + (255,))
    for x in (0.10, 0.14, 0.18, 0.22, 0.26):                # grip wrap
        d.line([x * s, mid - 0.030 * s, (x + 0.02) * s, mid + 0.030 * s], fill=WOOD_DARK + (255,), width=int(0.008 * s))
    image.alpha_composite(level.rotate(45, resample=Image.BICUBIC))
    finish(image, size, "TagScroll.png")


def item():
    size = 128
    s = size * SCALE
    image = canvas(size)
    for centre, degrees in (((0.36 * s, 0.54 * s), 24), ((0.64 * s, 0.54 * s), -22), ((0.50 * s, 0.50 * s), 2)):
        paste(image, tag(int(0.27 * s), int(0.62 * s)), centre, degrees)
    finish(image, size, "Tags.png")


def held():
    size = 128
    s = size * SCALE
    image = canvas(size)
    paste(image, tag(int(0.30 * s), int(0.70 * s)), (0.50 * s, 0.50 * s), 0)
    finish(image, size, "Tag.png")
    image = canvas(size)
    paste(image, tag(int(0.24 * s), int(0.52 * s)), (0.50 * s, 0.68 * s), 0)
    draw = ImageDraw.Draw(image)
    bar(draw, (0.50 * s, 0.46 * s), (0.50 * s, 0.10 * s), 0.035 * s, STEEL + (255,))
    draw.polygon([(0.50 * s, 0.03 * s), (0.46 * s, 0.12 * s), (0.54 * s, 0.12 * s)], fill=STEEL + (255,))
    finish(image, size, "TagPin.png")


def icon(name, paint):
    size = 128
    image = canvas(size)
    paint(image, ImageDraw.Draw(image), size * SCALE)
    finish(image, size, name)


def white_tag(w, h):
    """A tag for an icon: a white slip with its frame and seal cut out of the alpha."""
    image = Image.new("RGBA", (w, h), WHITE)
    d = ImageDraw.Draw(image)
    clear, line = (255, 255, 255, 0), max(2, int(w * 0.09))
    d.rectangle([w * 0.16, h * 0.08, w * 0.84, h * 0.92], outline=clear, width=line)
    cx, cy, r = w / 2, h / 2, w * 0.2
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=clear, width=line)
    return image


def tag_line(image, draw, s):
    for k in range(3):
        paste(image, white_tag(int(0.25 * s), int(0.15 * s)), ((0.36 + 0.22 * k) * s, (0.64 - 0.143 * k) * s), 33)
    centre = (0.13 * s, 0.80 * s)
    for degrees in range(0, 360, 45):
        r = math.radians(degrees)
        bar(draw, (centre[0] + 0.03 * s * math.cos(r), centre[1] + 0.03 * s * math.sin(r)),
            (centre[0] + 0.11 * s * math.cos(r), centre[1] + 0.11 * s * math.sin(r)), 0.03 * s, WHITE)


def shroud(image, draw, s):
    draw.ellipse([0.38 * s, 0.06 * s, 0.62 * s, 0.30 * s], outline=WHITE, width=int(0.04 * s))
    draw.rounded_rectangle([0.30 * s, 0.34 * s, 0.70 * s, 0.94 * s], radius=int(0.16 * s), outline=WHITE, width=int(0.04 * s))
    for centre, degrees in (((0.47 * s, 0.47 * s), -28), ((0.54 * s, 0.65 * s), 22), ((0.47 * s, 0.82 * s), -12)):
        paste(image, white_tag(int(0.34 * s), int(0.14 * s)), centre, degrees)


def tag_throw(image, draw, s):
    paste(image, white_tag(int(0.40 * s), int(0.20 * s)), (0.40 * s, 0.60 * s), 33)
    bar(draw, (0.56 * s, 0.49 * s), (0.92 * s, 0.25 * s), 0.045 * s, WHITE)
    draw.polygon([(0.97 * s, 0.22 * s), (0.86 * s, 0.22 * s), (0.92 * s, 0.32 * s)], fill=WHITE)
    for offset in (-0.20, 0.22):
        bar(draw, (0.10 * s, (0.80 + offset * 0.5) * s + 0.06 * s), (0.30 * s, (0.80 + offset * 0.5) * s - 0.07 * s), 0.03 * s, WHITE)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    weapon()
    item()
    held()
    icon("IconTagLine.png", tag_line)
    icon("IconShroud.png", shroud)
    icon("IconTagThrow.png", tag_throw)
