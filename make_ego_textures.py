#!/usr/bin/env python3
"""Textures for the E.G.O. weapons (docs/ego-weapons.md). Requires Pillow.

Run from any directory: python3 make_ego_textures.py

MagicBullet.png  128 px. The held and dropped Magic Bullet, drawn the way Core draws a rifle: level, muzzle to
                 the right. It is the rifle the shot picture draws (EgoMagicBulletGraphics.Rifle), part for part
                 and in its colours: the navy stock, the black barrel and receiver, the gold band round the
                 chamber and the gold ring in front of it, and the lit line along the barrel's top. Lengths are
                 fractions of the rifle's length L = 1.4 cells and widths of W = 0.075 cells, as there; the gun
                 runs from 0.31 L behind the grip to the muzzle 0.72 L ahead of it (1.44 cells). At the def's
                 drawSize 1.55 it is drawn as long as the picture draws it, so the rifle does not change size when
                 a shot's picture takes over from Core. The one addition is a 1 px dark outline, so the thin gun
                 reads on the ground.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Ego"
SCALE = 4  # drawn this many times larger, then reduced, for smooth edges


def rgb(r, g, b, a=1.0):
    """A colour from the picture's 0..1 floats."""
    return (round(r * 255), round(g * 255), round(b * 255), round(a * 255))


# EgoMagicBulletGraphics' colours.
BARREL = rgb(0.07, 0.07, 0.09)
BARREL_LIT = rgb(0.30, 0.31, 0.36, 0.8)
GOLD = rgb(0.86, 0.68, 0.26)
NAVY = rgb(0.11, 0.14, 0.38)
OUTLINE = rgb(0.02, 0.02, 0.03)

# EgoMagicBulletTiming: the rifle's length and width, cells.
RIFLE_LEN, RIFLE_W = 1.4, 0.075
DRAW_SIZE = 1.55  # the def's drawSize: cells across the 128 px picture


def magic_bullet():
    size = 128
    s = size * SCALE
    per_cell = s / DRAW_SIZE
    L, W = RIFLE_LEN * per_cell, RIFLE_W * per_cell
    back, front = -0.31, 0.72  # the stock's end and the muzzle, in L from the grip
    grip_x = s / 2 - (back + front) / 2 * L  # the gun centred across the picture
    mid_y = s / 2

    def rect(centre, length, width, colour, draw, up=0.0):
        """EgoMagicBulletGraphics' Rect: a bar centred <centre> L ahead of the grip, <length> L long, <width> px wide."""
        x = grip_x + centre * L
        y = mid_y - up
        draw.rectangle([x - length * L / 2, y - width / 2, x + length * L / 2, y + width / 2], fill=colour)

    parts = [  # (centre, length, width, colour, up), in the picture's draw order
        (-0.17, 0.28, 1.15 * W, NAVY, 0.0),
        (0.28, 0.88, 0.6 * W, BARREL, 0.0),
        (0.02, 0.16, W, BARREL, 0.0),
        (0.02, 0.13, 0.55 * W, GOLD, 0.0),
        (0.11, 0.02, 0.95 * W, GOLD, 0.0),
    ]

    # The outline: the shapes' alpha grown by 1 px of the final picture, filled dark, under the gun.
    mask = Image.new("L", (s, s), 0)
    md = ImageDraw.Draw(mask)
    for c, length, width, _, up in parts:
        rect(c, length, width, 255, md, up)
    grown = mask.filter(ImageFilter.MaxFilter(2 * SCALE + 1))
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    image.paste(Image.new("RGBA", (s, s), OUTLINE), (0, 0), grown)

    d = ImageDraw.Draw(image)
    for c, length, width, colour, up in parts:
        rect(c, length, width, colour, d, up)
    # The lit line along the barrel's top: 0.012 cells wide, 0.012 cells up, from 0.3 L, 0.8 L long, 80 % alpha.
    lit = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    rect(0.30, 0.8, 0.012 * per_cell, BARREL_LIT, ImageDraw.Draw(lit), up=0.012 * per_cell)
    image = Image.alpha_composite(image, lit)

    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((size, size), Image.LANCZOS).save(OUT / "MagicBullet.png")
    print("wrote", OUT / "MagicBullet.png")


if __name__ == "__main__":
    magic_bullet()
