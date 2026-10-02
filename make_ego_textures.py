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
SolemnLament.png 128 px. The held and dropped pair, level, muzzles to the right: the picture's two pistols
                 (EgoSolemnLamentShotGraphics.Pistol), part for part and in its colours. Each is a grip raked
                 back, a trigger guard, a slide 0.34 cells long and a barrel tip, with a lit line along the top
                 of the slide; the white gun is ash with a pale slide, the black gun soot with an ink slide and the
                 picture's grey outline 0.012 cells out. The white gun sits above, the black one below and 0.06
                 cells ahead, as a hand holds a pair. At the def's drawSize 0.8 each gun is the 0.32 cells long the
                 picture draws it. A 1 px dark outline goes round both.
ParadiseLost.png 128 px. The dropped staff and its icon, lying diagonally with the apple to the upper right: the
                 picture's staff (EgoParadiseLostStaffGraphics), part for part and in its colours, at the size the
                 picture draws it upright (2.2 lab cells x Lift 0.6 = 1.32 cells from the gold tip to the halo). The
                 white shaft with its grey outline and lit stripe, the gold tip, the snake in 3.5 loops with its
                 head at the apple, the fan of 4, 6 and 8 scale feathers beside the apple on the outer side, the gold
                 halo of 10 thorns above it, the red apple with its lit spot and stem. At the def's drawSize 1.5 the
                 whole staff fits across the diagonal. A 1 px dark outline goes round it.
Mimicry.png      128 px. The dropped sword and its icon, lying diagonally with the point to the upper right: the picture's
                 sword (EgoMimicrySwordGraphics) at size 1, part for part and in its colours, flesh side up. The black grip
                 with its lit line, red ring and end cap; the dark steel edge curving up into the point with its lit line;
                 the red muscle over the back half (thickest at the hilt, where its bulb wraps the blade) with its dark
                 underside band, lit ridge and four fibres, and three strands running on along the back; three bone spikes
                 on the back and two under the flesh; the big green-iris eye near the hilt and the small blue-iris eye
                 mid-blade; the sketch's black outlines 0.014 cells round steel and flesh. The sword is 1.41 cells from the
                 end cap to the point, the length the picture draws it, so at the def's drawSize 1.5 it fits across the
                 diagonal. A 1 px dark outline goes round it.
"""
import math
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


# EgoSolemnLamentGraphics' colours.
SL_WHITE = rgb(1.0, 1.0, 1.0, 0.9)
SL_PALE = rgb(0.90, 0.90, 0.93)
SL_ASH = rgb(0.50, 0.50, 0.55)
SL_ASH_LINE = rgb(0.50, 0.50, 0.55, 0.9)
SL_ASH_OUTLINE = rgb(0.50, 0.50, 0.55, 0.8)
SL_INK = rgb(0.05, 0.05, 0.06)
SL_SOOT = rgb(0.16, 0.16, 0.18)

# EgoSolemnLamentShotGraphics.Parts: four corners (along, up) in cells each, bottom-back, top-back, bottom-front,
# top-front: the grip, the trigger guard, the slide, the barrel tip.
SL_PARTS = [
    (-0.075, -0.115, -0.04, 0.005, 0.0, -0.12, 0.04, 0.005),
    (0.03, -0.045, 0.03, 0.0, 0.1, -0.045, 0.1, 0.0),
    (-0.05, 0.0, -0.05, 0.065, 0.29, 0.0, 0.29, 0.065),
    (0.29, 0.012, 0.29, 0.052, 0.32, 0.012, 0.32, 0.052),
]
SL_DRAW_SIZE = 0.8


def solemn_lament():
    size = 128
    s = size * SCALE
    per_cell = s / SL_DRAW_SIZE
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    mask = Image.new("L", (s, s), 0)
    d, md = ImageDraw.Draw(image), ImageDraw.Draw(mask)
    # The pair's extent: the white gun from -0.075 to 0.32 along and -0.12 to 0.065 up, the black one 0.06 ahead and 0.11 down.
    off_along, off_up = 0.06, -0.11
    lo_a, hi_a, lo_u, hi_u = -0.075, 0.32 + off_along, -0.12 + off_up, 0.065
    cx, cy = (lo_a + hi_a) / 2, (lo_u + hi_u) / 2

    def at(a, u, da, du):
        return (s / 2 + (a + da - cx) * per_cell, s / 2 - (u + du - cy) * per_cell)

    def quad(c, da, du, colour, grow=0.0):
        pts = [(c[0], c[1]), (c[4], c[5]), (c[6], c[7]), (c[2], c[3])]
        if grow:
            ma, mu = (c[0] + c[6]) / 2, (c[1] + c[7]) / 2
            out = []
            for a, u in pts:
                na, nu = a - ma, u - mu
                n = (na * na + nu * nu) ** 0.5 or 1.0
                out.append((a + na / n * grow, u + nu / n * grow))
            pts = out
        xy = [at(a, u, da, du) for a, u in pts]
        d.polygon(xy, fill=colour)
        md.polygon(xy, fill=255)

    def pistol(white, da, du):
        if not white:
            for c in SL_PARTS:
                quad(c, da, du, SL_ASH_OUTLINE, grow=0.012)
        for i, c in enumerate(SL_PARTS):
            colour = (SL_PALE if white else SL_INK) if i == 2 else (SL_ASH if white else SL_SOOT)
            quad(c, da, du, colour)
        quad((-0.045, 0.05, -0.045, 0.064, 0.285, 0.05, 0.285, 0.064), da, du, SL_WHITE if white else SL_ASH_LINE)

    # The black gun first, so the white one lies over it where they meet.
    pistol(False, off_along, off_up)
    pistol(True, 0.0, 0.0)

    grown = mask.filter(ImageFilter.MaxFilter(2 * OUTLINE_PX * SCALE + 1))
    base = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    base.paste(Image.new("RGBA", (s, s), INK), (0, 0), grown)
    image = Image.alpha_composite(base, image)

    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((size, size), Image.LANCZOS).save(OUT / "SolemnLament.png")
    print("wrote", OUT / "SolemnLament.png")


# EgoParadiseLostGraphics' colours.
PL_SHAFT = rgb(0.94, 0.93, 0.90)
PL_SHAFT_LINE = rgb(0.36, 0.35, 0.38)
PL_SNAKE = rgb(0.80, 0.80, 0.78)
PL_EYE = rgb(0.05, 0.05, 0.05)
PL_GOLD = rgb(0.96, 0.76, 0.26)
PL_APPLE = rgb(0.80, 0.07, 0.09)
PL_APPLE_LIT = rgb(1.0, 0.48, 0.42)
PL_RED_INK = rgb(0.07, 0.02, 0.03)
PL_STEM = rgb(0.28, 0.40, 0.20)
PL_FEATHER = (0.97, 0.96, 0.95)
PL_WING_GREY = (0.62, 0.62, 0.66)
PL_DRAW_SIZE = 1.5
PL_LIFT = 0.6  # EgoParadiseLostTiming.Lift: a lab cell up is 0.6 cells north on screen
PL_TILT = -45.0  # degrees the upright staff is turned: the apple ends up to the upper right


def lerp_colour(a, b, t):
    return rgb(*(x + (y - x) * t for x, y in zip(a, b)))


def paradise_lost():
    size = 128
    s = size * SCALE
    per_cell = s / PL_DRAW_SIZE
    # Drawn on a canvas twice as wide round the gold tip, then cut out round what was drawn, so the staff is centred.
    big = 2 * s
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    mask = Image.new("L", (big, big), 0)
    d, md = ImageDraw.Draw(image), ImageDraw.Draw(mask)
    ca, sa = math.cos(math.radians(PL_TILT)), math.sin(math.radians(PL_TILT))

    def at(x, y):
        """An upright staff point, <x> cells right of the shaft and <y> cells up from the gold tip, turned."""
        rx, ry = x * ca - y * sa, x * sa + y * ca
        return (big / 2 + rx * per_cell, big / 2 - ry * per_cell)

    def poly(points, colour):
        xy = [at(x, y) for x, y in points]
        d.polygon(xy, fill=colour)
        md.polygon(xy, fill=255)

    def line(points, width, colour):
        xy = [at(x, y) for x, y in points]
        w = max(1, round(width * per_cell))
        d.line(xy, fill=colour, width=w, joint="curve")
        md.line(xy, fill=255, width=w, joint="curve")

    def disc(x, y, rx, ry, colour):
        poly([(x + rx * math.cos(a / 24 * 2 * math.pi), y + ry * math.sin(a / 24 * 2 * math.pi)) for a in range(24)], colour)

    top = 2.2 * PL_LIFT
    # The gold tip, then the shaft with its outline and a lit stripe.
    poly([(-0.03, 0.07), (0.03, 0.07), (0.0, -0.05)], PL_GOLD)
    poly([(-0.042, 0.0), (0.042, 0.0), (0.042, top), (-0.042, top)], PL_SHAFT_LINE)
    poly([(-0.028, 0.0), (0.028, 0.0), (0.028, top), (-0.028, top)], PL_SHAFT)
    poly([(0.006, 0.0), (0.022, 0.0), (0.022, top), (0.006, top)], rgb(1.0, 1.0, 1.0, 0.7))

    # The snake: 3.5 wide loops down the whole shaft, its head at the apple.
    def snake_x(u):
        return 0.075 * math.sin(u * 3.5 * 2 * math.pi + 0.6)
    coil = [(snake_x(i / 56), (0.12 + (2.0 - 0.12) * i / 56) * PL_LIFT) for i in range(57)]
    line(coil, 0.056, PL_SHAFT_LINE)
    line(coil, 0.036, PL_SNAKE)
    hx, hy = snake_x(1.0) + 0.03, 2.04 * PL_LIFT
    disc(hx, hy, 0.048, 0.034, PL_SHAFT_LINE)
    disc(hx, hy, 0.039, 0.026, PL_SNAKE)
    disc(hx + 0.014, hy + 0.006, 0.009, 0.009, PL_EYE)

    # The fan of scale feathers beside the apple, outer rows first: 4, 6 and 8 feathers from 100 degrees down to 15 up.
    root = (0.03, 1.98 * PL_LIFT)
    for r in (2, 1, 0):
        n, reach, fl = 4 + 2 * r, (0.13, 0.23, 0.33)[r], 0.1 + 0.025 * r
        tone = lerp_colour(PL_WING_GREY, PL_FEATHER, 0.25 + 0.35 * r)
        for j in range(n):
            a = math.radians(-100 + 115 * j / (n - 1))
            dx, dy = math.cos(a), math.sin(a)
            nx, ny = -dy, dx
            bx, by = root[0] + dx * (reach - fl * 0.6), root[1] + dy * (reach - fl * 0.6)
            mx, my = bx + dx * fl * 0.5, by + dy * fl * 0.5
            ex, ey = bx + dx * fl, by + dy * fl
            for grow, colour in ((0.012, PL_SHAFT_LINE), (0.0, tone)):
                w = 0.042 + grow
                poly([(bx - dx * grow, by - dy * grow), (mx + nx * w, my + ny * w), (ex + dx * grow, ey + dy * grow), (mx - nx * w, my - ny * w)], colour)

    # The gold halo of 10 thorns round the shaft above the apple, then the apple with its lit spot and stem.
    halo = 2.3 * PL_LIFT
    for i in range(10):
        a = math.radians(i * 36 + 8)
        dx, dy = math.cos(a), math.sin(a)
        nx, ny = -dy, dx
        bx, by = dx * 0.12, halo + dy * 0.12
        poly([(bx + nx * 0.018, by + ny * 0.018), (bx - nx * 0.018, by - ny * 0.018), (dx * 0.2, halo + dy * 0.2)], PL_GOLD)
    ring = [(0.13 * math.cos(a / 32 * 2 * math.pi), halo + 0.13 * math.sin(a / 32 * 2 * math.pi)) for a in range(33)]
    line(ring, 0.03, PL_GOLD)
    apple = 2.12 * PL_LIFT
    disc(0.0, apple, 0.088, 0.082, PL_RED_INK)
    disc(0.0, apple, 0.075, 0.07, PL_APPLE)
    disc(-0.025, apple + 0.025, 0.025, 0.02, PL_APPLE_LIT)
    line([(0.0, apple + 0.06), (0.02, apple + 0.11)], 0.018, PL_STEM)

    grown = mask.filter(ImageFilter.MaxFilter(2 * OUTLINE_PX * SCALE + 1))
    base = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    base.paste(Image.new("RGBA", (big, big), INK), (0, 0), grown)
    image = Image.alpha_composite(base, image)
    x0, y0, x1, y1 = grown.getbbox()
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    image = image.crop((cx - s // 2, cy - s // 2, cx + s // 2, cy + s // 2))

    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((size, size), Image.LANCZOS).save(OUT / "ParadiseLost.png")
    print("wrote", OUT / "ParadiseLost.png")


MI_DRAW_SIZE = 1.5
MI_FLESH = rgb(0.60, 0.07, 0.06)
MI_FLESH_LIT = rgb(0.88, 0.24, 0.17, 0.85)
MI_FIBRE_BAND = rgb(0.30, 0.02, 0.03, 0.35)
MI_FIBRE = rgb(0.30, 0.02, 0.03, 0.75)
MI_STEEL = rgb(0.23, 0.21, 0.23)
MI_STEEL_LIT = rgb(0.66, 0.64, 0.68, 0.85)
MI_OUTLINE = rgb(0.06, 0.02, 0.02)
MI_BONE = rgb(0.86, 0.80, 0.66)
MI_BONE_LIT = rgb(0.97, 0.94, 0.85, 0.8)
MI_GRIP = rgb(0.08, 0.07, 0.08)
MI_GRIP_LIT = rgb(0.30, 0.29, 0.31)
MI_GRIP_RING = rgb(0.78, 0.12, 0.10)
MI_SCLERA = rgb(0.97, 0.94, 0.91)
MI_IRIS_GREEN = rgb(0.22, 0.72, 0.42)
MI_IRIS_BLUE = rgb(0.24, 0.40, 0.85)
MI_PUPIL = rgb(0.03, 0.02, 0.02)
# EgoMimicrySwordGraphics: (u along the blade from the guard, v across, + the back) and EgoMimicryTiming's grip.
MI_EDGE = [(0, -0.12), (0.2, -0.125), (0.5, -0.088), (0.77, -0.04), (0.92, 0.02), (1, 0.065)]
MI_BACK = [(0, 0.02), (0.5, 0.05), (0.8, 0.08), (0.93, 0.08), (1, 0.065)]
MI_TOP = [(0, 0.03), (0.04, 0.1), (0.12, 0.135), (0.25, 0.13), (0.42, 0.118), (0.58, 0.1), (0.7, 0.085), (0.78, 0.075)]
MI_BULB = [(0, -0.05), (0.04, -0.14), (0.1, -0.145), (0.2, -0.095)]
MI_FLESH_END = 0.78
MI_BLADE, MI_POMMEL, MI_GUARD = 1.09, -0.06, 0.22


def mimicry():
    size = 128
    s = size * SCALE
    per_cell = s / MI_DRAW_SIZE
    big = 2 * s
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    mask = Image.new("L", (big, big), 0)
    d, md = ImageDraw.Draw(image), ImageDraw.Draw(mask)
    dx, dy = math.cos(math.radians(45)), math.sin(math.radians(45))
    nx, ny = -dy, dx  # left of the blade: the back, the flesh side, up on the picture

    def pl(points, u):
        if u <= points[0][0]:
            return points[0][1]
        for (u0, v0), (u1, v1) in zip(points, points[1:]):
            if u <= u1:
                return v0 + (v1 - v0) * (u - u0) / (u1 - u0)
        return points[-1][1]

    def smooth(t):
        t = min(1.0, max(0.0, t))
        return t * t * (3 - 2 * t)

    edge = lambda u: pl(MI_EDGE, u)
    back = lambda u: pl(MI_BACK, u)
    top = lambda u: pl(MI_TOP, u)

    def low(u):
        a = edge(u) + 0.03 + (back(u) - 0.012 - edge(u) - 0.03) * smooth((u - 0.3) / 0.35)
        return min(a, pl(MI_BULB, u) if u < 0.2 else 9.0)

    def pt(a, v):
        """A point <a> cells along the sword from the hand and <v> across, on the canvas; the hand sits 0.6 cells before the middle."""
        x, y = (a - 0.6) * dx + v * nx, (a - 0.6) * dy + v * ny
        return (big / 2 + x * per_cell, big / 2 - y * per_cell)

    def bp(u, v):
        return pt(MI_GUARD + u * MI_BLADE, v)

    def poly(points, colour):
        d.polygon(points, fill=colour)
        md.polygon(points, fill=255)

    def band(us, lo, hi, colour):
        """A strip along the blade from lo(u) to hi(u)."""
        poly([bp(u, lo(u)) for u in us] + [bp(u, hi(u)) for u in reversed(us)], colour)

    def line(points, width, colour):
        w = max(1, round(width * per_cell))
        d.line(points, fill=colour, width=w, joint="curve")
        md.line(points, fill=255, width=w, joint="curve")

    def disc(c, rx, ry, colour):
        """An ellipse round canvas point <c>, rx along the blade and ry across, turned with it."""
        poly([(c[0] + (math.cos(t) * rx * dx + math.sin(t) * ry * nx) * per_cell,
               c[1] - (math.cos(t) * rx * dy + math.sin(t) * ry * ny) * per_cell)
              for t in (i / 24 * 2 * math.pi for i in range(24))], colour)

    steel = [i / 18 for i in range(19)]
    flesh = [i / 14 * MI_FLESH_END for i in range(15)]

    # The grip: the black rod, its lit line, the red ring at the guard, the end cap.
    poly([pt(MI_POMMEL, -0.024), pt(MI_GUARD, -0.024), pt(MI_GUARD, 0.024), pt(MI_POMMEL, 0.024)], MI_GRIP)
    poly([pt(MI_POMMEL + 0.02, 0.006), pt(MI_GUARD - 0.03, 0.006), pt(MI_GUARD - 0.03, 0.016), pt(MI_POMMEL + 0.02, 0.016)], MI_GRIP_LIT)
    poly([pt(MI_GUARD - 0.035, -0.036), pt(MI_GUARD + 0.005, -0.036), pt(MI_GUARD + 0.005, 0.036), pt(MI_GUARD - 0.035, 0.036)], MI_GRIP_RING)
    disc(pt(MI_POMMEL, 0.0), 0.038, 0.034, MI_GRIP)

    # The outlines, then the steel and its lit line, then the flesh and its bands.
    band(steel, lambda u: edge(u) - 0.014, lambda u: back(u) + 0.014, MI_OUTLINE)
    band(flesh, lambda u: low(u) - 0.014, lambda u: top(u) + 0.014, MI_OUTLINE)
    band(steel, edge, back, MI_STEEL)
    band(steel[2:], lambda u: edge(u) + 0.006, lambda u: edge(u) + 0.02, MI_STEEL_LIT)
    band(flesh, low, top, MI_FLESH)
    band(flesh, low, lambda u: low(u) + (top(u) - low(u)) * 0.3, MI_FIBRE_BAND)
    ridge = [u for u in flesh if 0.04 <= u <= 0.72]
    band(ridge, lambda u: top(u) - 0.03, lambda u: top(u) - 0.01, MI_FLESH_LIT)
    for i in range(4):
        fr, u0, u1 = 0.22 + i * 0.17, 0.03 + 0.03 * i, MI_FLESH_END - 0.04 - 0.05 * i
        us = [u0 + (u1 - u0) * j / 10 for j in range(11)]
        line([bp(u, low(u) + (top(u) - low(u)) * fr + 0.006 * math.sin(u * 40 + i * 2)) for u in us], 0.012, MI_FIBRE)
    for i in range(3):
        u0, u1 = MI_FLESH_END - 0.1 + i * 0.02, MI_FLESH_END + 0.05 + 0.04 * i
        um = (u0 + u1) / 2
        line([bp(u0, back(u0) + 0.03 - i * 0.01), bp(um, back(um) + 0.018 - i * 0.006), bp(u1, back(u1) + 0.004)], 0.022, MI_FLESH)

    # The bone spikes: three on the back, two small ones under the flesh near the hilt.
    def spike(u, base, length, lean, sgn):
        du = 0.022 / MI_BLADE
        apex_u, apex_v = u + lean * length / MI_BLADE, base + sgn * length
        poly([bp(u - du * 1.6, base), bp(u + du * 1.6, base), bp(apex_u, apex_v + sgn * 0.016)], MI_OUTLINE)
        poly([bp(u - du, base), bp(u + du, base), bp(apex_u, apex_v)], MI_BONE)
        poly([bp(u - du * 0.2, base), bp(u + du * 0.7, base), bp(apex_u, apex_v - sgn * 0.01)], MI_BONE_LIT)
    for u, length, lean in ((0.13, 0.065, 0.25), (0.2, 0.115, 0.35), (0.27, 0.07, 0.3)):
        spike(u, top(u) - 0.01, length, lean, 1)
    for u, length, lean in ((0.2, 0.045, 0.2), (0.36, 0.04, 0.25)):
        spike(u, low(u) + 0.01, length, lean, -1)

    # The eyes, half open as the sword rests: the big green one near the hilt, the small blue one mid-blade.
    for u, v, rx, rz, iris, ir in ((0.22, 0.02, 0.06, 0.045, MI_IRIS_GREEN, 0.024), (0.5, 0.054, 0.032, 0.024, MI_IRIS_BLUE, 0.013)):
        c = bp(u, v)
        disc(c, rx + 0.012, rz + 0.012, MI_OUTLINE)
        disc(c, rx, rz, MI_SCLERA)
        disc(c, ir, ir, iris)
        disc(c, ir * 0.45, ir * 0.45, MI_PUPIL)

    grown = mask.filter(ImageFilter.MaxFilter(2 * OUTLINE_PX * SCALE + 1))
    base = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    base.paste(Image.new("RGBA", (big, big), INK), (0, 0), grown)
    image = Image.alpha_composite(base, image)
    x0, y0, x1, y1 = grown.getbbox()
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    image = image.crop((cx - s // 2, cy - s // 2, cx + s // 2, cy + s // 2))

    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((size, size), Image.LANCZOS).save(OUT / "Mimicry.png")
    print("wrote", OUT / "Mimicry.png")


if __name__ == "__main__":
    magic_bullet()
    solemn_lament()
    paradise_lost()
    mimicry()
