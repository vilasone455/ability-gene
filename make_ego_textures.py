#!/usr/bin/env python3
"""Textures for the E.G.O. weapons (docs/ego-weapons.md). Requires Pillow.

Run from any directory: python3 make_ego_textures.py

MagicBullet.png  128 px. The held and dropped Magic Bullet, drawn the way Core draws a rifle: level, muzzle to
                 the right. It is drawn after the game's art of the rifle, part for part and in its colours:
                 the two grey blocks of the action with the gold strut slanting between them and its two
                 flared ends, the dark collar and the tall gold ring, the long dark barrel with the lit line
                 along its top, the black cap, and the two thin rods of the muzzle with the knob on the upper
                 one. Each part has an ink line round it, as the art has. The grip is the one part the art
                 does not show, the hand being round it: it is drawn as a blue pistol grip raked back under
                 the action, with a gold cap on its butt and a gold trigger ahead of it.
                 Lengths are fractions of the rifle's length L = 1.4 cells and heights of W = 0.075 cells, the
                 units EgoMagicBulletGraphics.Rifle uses. The gun runs from 0.31 L behind the grip to the
                 muzzle 0.72 L ahead of it (1.44 cells), as the shot picture's rifle does, so at the def's
                 drawSize 1.55 the rifle does not change size when a shot's picture takes over from Core.
                 The picture still draws its own five bars in its own colours; only the length is shared.
                 A 1 px dark outline goes round the whole gun, so the thin gun reads on the ground.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Ego"
SCALE = 8  # drawn this many times larger, then reduced, for smooth edges


def rgb(r, g, b, a=1.0):
    """A colour from 0..1 floats."""
    return (round(r * 255), round(g * 255), round(b * 255), round(a * 255))


# The colours of the rifle in the game's art.
INK = rgb(0.02, 0.02, 0.03)
BARREL = rgb(0.11, 0.11, 0.13)
SPLIT = rgb(0.03, 0.03, 0.04)  # the dark line between the barrel and the tube under it
LIT = rgb(0.51, 0.55, 0.58)  # the lit line along the barrel's top, and the blocks' lit tops
STEEL = rgb(0.14, 0.16, 0.17)
HAMMER = rgb(0.42, 0.45, 0.47)
COLLAR = rgb(0.18, 0.18, 0.15)
GOLD = rgb(0.97, 0.95, 0.37)
GOLD_SHADE = rgb(0.67, 0.66, 0.24)
GOLD_DARK = rgb(0.24, 0.23, 0.08)
BLUE_EDGE = rgb(0.22, 0.41, 0.84)
NAVY = rgb(0.08, 0.145, 0.31)
NAVY_SHADE = rgb(0.06, 0.11, 0.22)

# EgoMagicBulletTiming: the rifle's length and width, cells.
RIFLE_LEN, RIFLE_W = 1.4, 0.075
DRAW_SIZE = 1.55  # the def's drawSize: cells across the 128 px picture
INK_PX = 0.5  # the ink line round a part, px of the final picture
OUTLINE_PX = 1  # the outline round the whole gun, px of the final picture


def magic_bullet():
    size = 128
    s = size * SCALE
    per_cell = s / DRAW_SIZE
    L, W = RIFLE_LEN * per_cell, RIFLE_W * per_cell
    back, front = -0.31, 0.72  # the grip's butt and the muzzle, in L from the picture's grip
    grip_x = s / 2 - (back + front) / 2 * L  # the gun centred across the picture
    mid_y = s / 2  # the bore's axis

    gun = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    mask = Image.new("L", (s, s), 0)
    d, md = ImageDraw.Draw(gun), ImageDraw.Draw(mask)
    ink_w = round(2 * INK_PX * SCALE)

    def at(x, y):
        """A point <x> L ahead of the grip and <y> W above the bore's axis."""
        return (grip_x + x * L, mid_y - y * W)

    def poly(points, colour, ink=True, outlined=True):
        pts = [at(x, y) for x, y in points]
        if ink:
            d.line(pts + pts[:2], fill=INK, width=ink_w, joint="curve")
        d.polygon(pts, fill=colour)
        if outlined:
            md.polygon(pts, fill=255)

    def bar(x0, x1, y0, y1, colour, ink=True, outlined=True):
        """A level bar from <x0> to <x1> L and from <y0> to <y1> W."""
        poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], colour, ink, outlined)

    def oval(x, y, half_len, half_height, colour, ink=True):
        cx, cy = at(x, y)
        rx, ry = half_len * L, half_height * W
        if ink:
            g = ink_w / 2
            d.ellipse([cx - rx - g, cy - ry - g, cx + rx + g, cy + ry + g], fill=INK)
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=colour)
        md.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)

    # The frame the action's parts sit on: ink, as the art's is.
    poly([(-0.255, 0.55), (-0.113, 0.55), (-0.113, -0.40), (-0.200, -0.40), (-0.215, 0.0), (-0.255, 0.10)], INK)

    # The barrel: the lit line along its top, and the dark line that parts it from the tube under it.
    bar(-0.076, 0.556, -0.28, 0.28, BARREL)
    bar(-0.070, 0.550, 0.12, 0.28, LIT, ink=False)
    bar(-0.076, 0.556, 0.01, 0.12, SPLIT, ink=False)
    bar(0.554, 0.5725, -0.34, 0.34, INK)  # the cap at the barrel's end

    # The muzzle: two thin rods, the upper one longer and ending in the knob. They are ink, as the art's are,
    # and take no outline, so they stay thinner than the barrel and the gap between them stays open.
    bar(0.5725, 0.680, -0.29, -0.09, INK, ink=False, outlined=False)
    bar(0.5725, 0.702, 0.07, 0.27, INK, ink=False, outlined=False)
    bar(0.698, 0.720, -0.02, 0.36, INK, ink=False, outlined=False)

    # The action's two grey blocks. The upper one has the hammer at its rear and a lit top.
    poly([(-0.245, 0.49), (-0.245, 0.80), (-0.236, 0.95), (-0.152, 0.95), (-0.144, 0.80), (-0.144, 0.49)], STEEL)
    poly([(-0.243, 0.60), (-0.243, 0.80), (-0.235, 0.93), (-0.216, 0.93), (-0.216, 0.60)], HAMMER, ink=False)
    bar(-0.216, -0.154, 0.81, 0.93, LIT, ink=False)
    bar(-0.193, -0.138, -0.49, -0.03, STEEL)
    bar(-0.191, -0.140, -0.17, -0.05, LIT, ink=False)

    # The trigger: a small gold hook under the frame, ahead of the grip. It takes no outline, to stay thin.
    poly([(-0.197, -0.45), (-0.189, -0.45), (-0.190, -0.74), (-0.183, -0.92), (-0.190, -0.92), (-0.198, -0.76)],
         GOLD, outlined=False)

    # The blue grip. The art hides it in the hand and shows only its blue top under the action, so the rest is
    # drawn as a pistol grip: raked back about 30 degrees, a bright edge, the rear half the lighter face, and
    # a gold cap on the butt. Its butt is the gun's rear end.
    poly([(-0.203, 0.12), (-0.258, 0.12), (-0.272, -0.30), (-0.288, -0.80), (-0.302, -1.25), (-0.310, -1.56),
          (-0.264, -1.74), (-0.250, -1.25), (-0.236, -0.80), (-0.218, -0.40), (-0.203, -0.12)], BLUE_EDGE)
    poly([(-0.2075, 0.05), (-0.2535, 0.05), (-0.2675, -0.30), (-0.2835, -0.80), (-0.2975, -1.25), (-0.304, -1.38),
          (-0.259, -1.45), (-0.2545, -1.25), (-0.2405, -0.80), (-0.2225, -0.40), (-0.2075, -0.12)],
         NAVY_SHADE, ink=False)
    poly([(-0.2535, 0.05), (-0.2675, -0.30), (-0.2835, -0.80), (-0.2975, -1.25), (-0.304, -1.38),
          (-0.2815, -1.415), (-0.276, -1.25), (-0.262, -0.80), (-0.2423, -0.30), (-0.2305, 0.05)], NAVY, ink=False)
    poly([(-0.3048, -1.36), (-0.2554, -1.44), (-0.264, -1.74), (-0.310, -1.56)], GOLD)

    # The collar and the tall gold ring between the action and the barrel.
    bar(-0.116, -0.092, -0.40, 0.40, COLLAR)
    oval(-0.084, 0.0, 0.011, 0.57, GOLD)
    oval(-0.084, 0.0, 0.004, 0.36, GOLD_DARK, ink=False)

    # The gold strut slanting down the action from the rear, and its two flared ends.
    poly([(-0.250, 0.65), (-0.141, 0.22), (-0.141, 0.06), (-0.250, 0.49)], GOLD)
    poly([(-0.250, 0.65), (-0.141, 0.22), (-0.141, 0.17), (-0.250, 0.60)], GOLD_SHADE, ink=False)
    poly([(-0.266, 0.88), (-0.240, 0.57), (-0.266, 0.24)], GOLD)
    poly([(-0.152, 0.14), (-0.118, 0.62), (-0.118, -0.45)], GOLD)
    poly([(-0.143, 0.13), (-0.125, 0.39), (-0.125, -0.24)], GOLD_DARK, ink=False)

    # The outline: the parts' alpha grown by 1 px of the final picture, filled dark, under the gun.
    grown = mask.filter(ImageFilter.MaxFilter(2 * OUTLINE_PX * SCALE + 1))
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    image.paste(Image.new("RGBA", (s, s), INK), (0, 0), grown)
    image = Image.alpha_composite(image, gun)

    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((size, size), Image.LANCZOS).save(OUT / "MagicBullet.png")
    print("wrote", OUT / "MagicBullet.png")


if __name__ == "__main__":
    magic_bullet()
