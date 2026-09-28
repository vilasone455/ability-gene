#!/usr/bin/env python3
"""Hero-form costume textures. Requires Pillow.

Run from any directory: python3 make_costume_textures.py

A costume is drawn over the Host while an Echo is manifested. It is only a picture: the Host keeps
wearing its own apparel, and that apparel's armour and insulation still count.

Echo/Costume/VergilCoat_<Body>_<facing>.png
    256 px, for Body in Thin, Male, Female, Fat, Hulk and facing in south, east, north (west is east
    mirrored, as for vanilla apparel). Vergil's long blue coat from Devil May Cry 3: gold trim on
    every edge, a white serpent pattern on the collar and down the wearer's right front, a gold
    lining that shows where the tail swings out, three split tails at the back, and under the open
    front a black ribbed vest, a brown belt with a silver buckle and dark trousers. Adult human
    Hosts are Thin in hero form, so the Thin set is the one normally seen; the other four fit Hosts
    of other races that keep their own body.

Echo/Costume/VergilCoatKneel_Thin.png
    256 px, facing south only: Vergil kneeling for Judgement Cut End's sheathe, facing the camera, his
    left knee up and his right knee on the ground, the coat skirt spread on the floor. The torso is the
    standing Thin torso moved down KNEEL_DROP units; the game hides the body and moves the head down by
    the same amount (Source/RimArt/Vergil/Kit/Patches_VergilPose.cs), so the head keeps its shape.

The costume is fitted to the vanilla body outlines in BODIES, measured from the game's
Naked_<Body>_<facing> textures (outer edge of the black outline, every 2 rows, on the 128 px
sheet). Every length below is in those 128 px units; the picture is drawn 8x larger and reduced.
"""
from pathlib import Path
import math

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Echo/Costume"
SIZE = 256
U = 8  # canvas pixels per 128 px unit: a 1024 px canvas, reduced to SIZE

INK = (16, 17, 22)
COAT, COAT_LIT, COAT_DARK = (36, 86, 158), (76, 140, 208), (16, 38, 88)
TRIM, TRIM_LIT = (206, 160, 64), (246, 214, 124)
LINING, LINING_DARK = (214, 136, 52), (140, 76, 26)
SERPENT = (228, 238, 244)
VEST, VEST_RIB = (27, 29, 35), (76, 84, 98)
BELT, BUCKLE = (98, 60, 32), (210, 216, 226)
TROUSERS, TROUSERS_SEAM = (40, 50, 46), (22, 28, 26)
SILVER = (208, 214, 224)

# Outer edge of each vanilla body (alpha > 50 %), every 2 rows: (y, left x, right x) on the 128 px sheet.
BODIES = {
    ('Thin', 'south'): [(43, 60, 68), (45, 55, 72), (47, 53, 74), (49, 52, 75), (51, 52, 76),
        (53, 51, 76), (55, 51, 76), (57, 51, 77), (59, 51, 77), (61, 51, 77), (63, 51, 77),
        (65, 51, 77), (67, 51, 77), (69, 51, 77), (71, 51, 77), (73, 51, 77), (75, 51, 77),
        (77, 51, 77), (79, 51, 77), (81, 51, 77), (83, 51, 77), (85, 51, 77), (87, 51, 77),
        (89, 51, 76), (91, 51, 76), (93, 51, 76), (95, 51, 76), (97, 52, 76), (99, 52, 75),
        (101, 53, 75), (103, 53, 74), (105, 55, 73), (107, 58, 70)],
    ('Thin', 'east'): [(45, 59, 70), (47, 56, 73), (49, 55, 75), (51, 54, 76), (53, 53, 77),
        (55, 52, 77), (57, 52, 77), (59, 51, 77), (61, 50, 76), (63, 50, 76), (65, 49, 76),
        (67, 49, 75), (69, 48, 75), (71, 48, 74), (73, 47, 74), (75, 47, 73), (77, 46, 72),
        (79, 46, 72), (81, 45, 71), (83, 45, 71), (85, 45, 70), (87, 44, 69), (89, 44, 69),
        (91, 44, 68), (93, 43, 68), (95, 43, 67), (97, 43, 66), (99, 43, 65), (101, 43, 65),
        (103, 45, 63), (105, 49, 62), (106, 51, 60)],
    ('Thin', 'north'): [(43, 60, 68), (45, 55, 72), (47, 53, 74), (49, 52, 75), (51, 52, 76),
        (53, 51, 76), (55, 51, 76), (57, 51, 77), (59, 51, 77), (61, 51, 77), (63, 51, 77),
        (65, 51, 77), (67, 51, 77), (69, 51, 77), (71, 51, 77), (73, 51, 77), (75, 51, 77),
        (77, 51, 77), (79, 51, 77), (81, 51, 77), (83, 51, 77), (85, 51, 77), (87, 51, 77),
        (89, 51, 76), (91, 51, 76), (93, 51, 76), (95, 51, 76), (97, 52, 76), (99, 52, 75),
        (101, 53, 75), (103, 53, 74), (105, 55, 73), (107, 58, 70)],
    ('Male', 'south'): [(44, 53, 75), (46, 46, 82), (48, 43, 85), (50, 41, 87), (52, 39, 88),
        (54, 39, 89), (56, 39, 89), (58, 38, 89), (60, 38, 89), (62, 38, 89), (64, 38, 89),
        (66, 38, 89), (68, 39, 89), (70, 39, 89), (72, 39, 88), (74, 40, 88), (76, 40, 87),
        (78, 41, 87), (80, 42, 86), (82, 42, 85), (84, 43, 85), (86, 44, 84), (88, 44, 84),
        (90, 44, 83), (92, 45, 83), (94, 45, 82), (96, 45, 82), (98, 45, 82), (100, 46, 82),
        (102, 47, 81), (104, 48, 79), (106, 52, 75), (107, 57, 71)],
    ('Male', 'east'): [(43, 54, 69), (45, 48, 75), (47, 46, 79), (49, 44, 81), (51, 43, 83),
        (53, 43, 84), (55, 43, 85), (57, 43, 86), (59, 43, 86), (61, 43, 86), (63, 43, 86),
        (65, 43, 86), (67, 44, 86), (69, 44, 85), (71, 44, 85), (73, 45, 84), (75, 45, 83),
        (77, 45, 82), (79, 45, 81), (81, 45, 80), (83, 46, 79), (85, 46, 78), (87, 45, 78),
        (89, 45, 77), (91, 45, 76), (93, 45, 76), (95, 44, 75), (97, 44, 75), (99, 45, 75),
        (101, 46, 74), (103, 48, 73), (105, 52, 71), (106, 57, 67)],
    ('Male', 'north'): [(44, 53, 75), (46, 46, 81), (48, 43, 85), (50, 41, 87), (52, 39, 88),
        (54, 39, 89), (56, 39, 89), (58, 38, 89), (60, 38, 89), (62, 38, 89), (64, 38, 89),
        (66, 38, 89), (68, 39, 89), (70, 39, 89), (72, 39, 88), (74, 40, 88), (76, 40, 87),
        (78, 41, 87), (80, 42, 86), (82, 42, 85), (84, 43, 85), (86, 44, 84), (88, 44, 84),
        (90, 44, 83), (92, 45, 83), (94, 45, 82), (96, 45, 82), (98, 45, 82), (100, 46, 82),
        (102, 47, 81), (104, 48, 79), (106, 52, 75), (107, 57, 71)],
    ('Female', 'south'): [(39, 56, 72), (41, 52, 76), (43, 51, 77), (45, 50, 78), (47, 50, 78),
        (49, 49, 79), (51, 49, 79), (53, 48, 80), (55, 47, 81), (57, 46, 82), (59, 46, 82),
        (61, 45, 83), (63, 45, 83), (65, 45, 83), (67, 45, 83), (69, 46, 82), (71, 47, 81),
        (73, 48, 80), (75, 49, 79), (77, 48, 80), (79, 46, 82), (81, 44, 84), (83, 43, 85),
        (85, 42, 86), (87, 41, 87), (89, 40, 88), (91, 40, 88), (93, 40, 88), (95, 40, 88),
        (97, 41, 87), (99, 41, 87), (101, 43, 85), (103, 44, 84), (105, 46, 82), (107, 48, 80),
        (109, 51, 77), (111, 55, 73), (112, 59, 69)],
    ('Female', 'east'): [(39, 61, 68), (41, 57, 72), (43, 55, 74), (45, 54, 75), (47, 52, 76),
        (49, 51, 76), (51, 50, 76), (53, 49, 77), (55, 48, 77), (57, 47, 78), (59, 47, 78),
        (61, 47, 79), (63, 48, 80), (65, 48, 81), (67, 48, 81), (69, 48, 81), (71, 47, 81),
        (73, 46, 81), (75, 44, 79), (77, 43, 76), (79, 41, 74), (81, 40, 74), (83, 38, 75),
        (85, 37, 76), (87, 37, 77), (89, 36, 77), (91, 36, 78), (93, 36, 78), (95, 36, 78),
        (97, 37, 78), (99, 37, 77), (101, 38, 77), (103, 39, 76), (105, 41, 74), (107, 43, 72),
        (109, 46, 70), (111, 51, 66), (112, 55, 63)],
    ('Female', 'north'): [(39, 56, 72), (41, 52, 76), (43, 51, 77), (45, 50, 78), (47, 50, 78),
        (49, 49, 79), (51, 49, 79), (53, 48, 80), (55, 47, 81), (57, 46, 82), (59, 46, 82),
        (61, 45, 83), (63, 45, 83), (65, 45, 83), (67, 45, 83), (69, 46, 82), (71, 47, 81),
        (73, 48, 80), (75, 49, 79), (77, 48, 80), (79, 46, 82), (81, 44, 84), (83, 43, 85),
        (85, 42, 86), (87, 41, 87), (89, 40, 88), (91, 40, 88), (93, 40, 88), (95, 40, 88),
        (97, 41, 87), (99, 41, 87), (101, 43, 85), (103, 44, 84), (105, 46, 82), (107, 48, 80),
        (109, 51, 77), (111, 55, 73), (112, 59, 69)],
    ('Fat', 'south'): [(38, 57, 68), (40, 48, 77), (42, 44, 82), (44, 41, 85), (46, 39, 87),
        (48, 36, 89), (50, 34, 91), (52, 33, 93), (54, 32, 94), (56, 31, 95), (58, 30, 95),
        (60, 30, 96), (62, 30, 96), (64, 29, 97), (66, 27, 98), (68, 26, 99), (70, 26, 100),
        (72, 25, 101), (74, 24, 101), (76, 24, 102), (78, 23, 102), (80, 23, 102), (82, 23, 102),
        (84, 23, 102), (86, 23, 102), (88, 23, 102), (90, 24, 102), (92, 24, 101), (94, 25, 101),
        (96, 26, 100), (98, 27, 99), (100, 28, 98), (102, 29, 96), (104, 31, 94), (106, 34, 92),
        (108, 37, 88), (110, 42, 83), (112, 52, 75)],
    ('Fat', 'east'): [(38, 57, 68), (40, 50, 76), (42, 46, 80), (44, 42, 84), (46, 39, 86),
        (48, 37, 89), (50, 35, 90), (52, 33, 92), (54, 32, 93), (56, 30, 94), (58, 29, 94),
        (60, 28, 95), (62, 27, 95), (64, 26, 95), (66, 26, 96), (68, 25, 96), (70, 24, 96),
        (72, 24, 96), (74, 23, 96), (76, 23, 96), (78, 23, 97), (80, 23, 98), (82, 23, 99),
        (84, 23, 99), (86, 23, 99), (88, 23, 100), (90, 24, 99), (92, 24, 99), (94, 25, 98),
        (96, 26, 98), (98, 27, 97), (100, 28, 96), (102, 29, 95), (104, 31, 93), (106, 33, 92),
        (108, 36, 89), (110, 40, 86), (112, 49, 82), (113, 54, 79)],
    ('Fat', 'north'): [(38, 58, 69), (40, 49, 78), (42, 45, 83), (44, 42, 86), (46, 40, 88),
        (48, 37, 90), (50, 35, 92), (52, 34, 94), (54, 33, 95), (56, 32, 96), (58, 31, 96),
        (60, 31, 97), (62, 31, 97), (64, 30, 98), (66, 28, 99), (68, 27, 100), (70, 27, 101),
        (72, 26, 102), (74, 25, 102), (76, 25, 103), (78, 24, 103), (80, 24, 103), (82, 24, 103),
        (84, 24, 103), (86, 24, 103), (88, 24, 103), (90, 25, 103), (92, 25, 102), (94, 26, 102),
        (96, 27, 101), (98, 28, 100), (100, 29, 99), (102, 30, 97), (104, 32, 95), (106, 35, 93),
        (108, 38, 89), (110, 43, 84), (112, 53, 76)],
    ('Hulk', 'south'): [(32, 59, 68), (34, 51, 77), (36, 47, 81), (38, 43, 84), (40, 41, 87),
        (42, 38, 90), (44, 36, 92), (46, 34, 94), (48, 33, 95), (50, 31, 97), (52, 30, 98),
        (54, 29, 99), (56, 28, 100), (58, 27, 101), (60, 27, 101), (62, 26, 101), (64, 26, 101),
        (66, 26, 101), (68, 26, 101), (70, 26, 101), (72, 26, 100), (74, 27, 100), (76, 27, 99),
        (78, 28, 98), (80, 29, 97), (82, 30, 96), (84, 31, 95), (86, 32, 94), (88, 33, 93),
        (90, 34, 92), (92, 35, 91), (94, 36, 90), (96, 37, 89), (98, 38, 88), (100, 39, 88),
        (102, 40, 87), (104, 41, 86), (106, 42, 85), (108, 42, 85), (110, 43, 84), (112, 44, 84),
        (114, 44, 83), (116, 44, 83), (118, 45, 82), (120, 46, 81), (122, 49, 79), (124, 52, 75),
        (125, 55, 72)],
    ('Hulk', 'east'): [(36, 52, 68), (38, 46, 74), (40, 43, 78), (42, 41, 81), (44, 39, 84),
        (46, 38, 86), (48, 37, 87), (50, 36, 89), (52, 36, 90), (54, 35, 91), (56, 35, 92),
        (58, 34, 93), (60, 34, 94), (62, 34, 94), (64, 34, 95), (66, 34, 95), (68, 34, 95),
        (70, 34, 95), (72, 33, 94), (74, 33, 94), (76, 33, 93), (78, 33, 93), (80, 33, 92),
        (82, 32, 91), (84, 32, 90), (86, 32, 89), (88, 32, 87), (90, 31, 86), (92, 31, 85),
        (94, 31, 83), (96, 30, 82), (98, 30, 81), (100, 29, 79), (102, 29, 78), (104, 28, 77),
        (106, 28, 75), (108, 27, 74), (110, 27, 73), (112, 28, 72), (114, 28, 71), (116, 29, 70),
        (118, 30, 69), (120, 33, 68), (122, 37, 65), (124, 45, 59)],
    ('Hulk', 'north'): [(32, 59, 68), (34, 51, 77), (36, 47, 81), (38, 43, 84), (40, 41, 87),
        (42, 38, 90), (44, 36, 92), (46, 34, 94), (48, 33, 95), (50, 31, 97), (52, 30, 98),
        (54, 29, 99), (56, 28, 100), (58, 27, 101), (60, 27, 101), (62, 26, 101), (64, 26, 101),
        (66, 26, 101), (68, 26, 101), (70, 26, 101), (72, 26, 100), (74, 27, 100), (76, 27, 99),
        (78, 28, 98), (80, 29, 97), (82, 30, 96), (84, 31, 95), (86, 32, 94), (88, 33, 93),
        (90, 34, 92), (92, 35, 91), (94, 36, 90), (96, 37, 89), (98, 38, 88), (100, 39, 88),
        (102, 40, 87), (104, 41, 86), (106, 42, 85), (108, 42, 85), (110, 43, 84), (112, 44, 84),
        (114, 44, 83), (116, 44, 83), (118, 45, 82), (120, 46, 81), (122, 49, 79), (124, 52, 75),
        (125, 55, 72)],
}


# ---- geometry ----

def interp(rows, y):
    """Left and right edge of a body row table at height y, clamped to its first and last row."""
    if y <= rows[0][0]:
        return rows[0][1], rows[0][2]
    for (ya, la, ra), (yb, lb, rb) in zip(rows, rows[1:]):
        if y <= yb:
            k = (y - ya) / (yb - ya)
            return la + (lb - la) * k, ra + (rb - ra) * k
    return rows[-1][1], rows[-1][2]


class Body:
    """One vanilla body silhouette and the heights the coat is laid out on."""

    def __init__(self, rows):
        self.rows = rows
        self.top, self.bottom = rows[0][0], rows[-1][0]
        self.height = self.bottom - self.top
        self.waist = self.top + 0.5 * self.height
        # Below here the vanilla outline rounds in; the coat hangs straight on past it instead.
        self.keep = self.bottom - 0.14 * self.height
        left, right = interp(rows, self.top + 0.4 * self.height)
        self.width = right - left

    def edges(self, y):
        """The body's edges, carried straight down (following its lean) past where it rounds in."""
        if y <= self.keep:
            return interp(self.rows, y)
        l0, r0 = interp(self.rows, self.waist)
        l1, r1 = interp(self.rows, self.keep)
        k = (y - self.keep) / (self.keep - self.waist)
        return l1 + (l1 - l0) * k, r1 + (r1 - r0) * k

    def centre(self, y):
        left, right = self.edges(y)
        return (left + right) / 2


def steps(a, b, step=0.5):
    n = max(1, int(math.ceil(abs(b - a) / step)))
    return [a + (b - a) * i / n for i in range(n + 1)]


def smooth(a, b, x):
    v = min(1.0, max(0.0, (x - a) / (b - a)))
    return v * v * (3 - 2 * v)


# ---- masks ----

def blank():
    return Image.new("L", (128 * U, 128 * U), 0)


def polygon(points):
    mask = blank()
    ImageDraw.Draw(mask).polygon([(x * U, y * U) for x, y in points], fill=255)
    return mask


def ellipse(cx, cy, rx, ry):
    mask = blank()
    ImageDraw.Draw(mask).ellipse([(cx - rx) * U, (cy - ry) * U, (cx + rx) * U, (cy + ry) * U], fill=255)
    return mask


def stroke(points, width):
    mask = blank()
    ImageDraw.Draw(mask).line([(x * U, y * U) for x, y in points], fill=255, width=max(1, round(width * U)),
                              joint="curve")
    return mask


def union(*masks):
    out = masks[0]
    for m in masks[1:]:
        out = ImageChops.lighter(out, m)
    return out


def inter(a, b):
    return ImageChops.darker(a, b)


def minus(a, b):
    return ImageChops.subtract(a, b)


def grow(mask, by):
    """Mask widened by about `by` units (a blur and a steep threshold)."""
    sigma = by * U / 1.42
    return mask.filter(ImageFilter.GaussianBlur(sigma)).point(lambda v: 0 if v < 12 else min(255, (v - 12) * 16))


def shrink(mask, by):
    sigma = by * U / 1.42
    return mask.filter(ImageFilter.GaussianBlur(sigma)).point(lambda v: 0 if v < 243 else 255)


def edge_light(mask, reach):
    """255 deep inside the mask, falling toward its edge over about `reach` units: the vanilla
    darkening at the rim of every piece of clothing."""
    blurred = mask.filter(ImageFilter.GaussianBlur(reach * U / 2))
    return ImageChops.multiply(blurred.point(lambda v: min(255, v * 2 - 128) if v > 64 else 0), mask)


def ramp(x0, x1, lo, hi):
    """Horizontal ramp from lo at x0 to hi at x1 (0..255), for light falling from one side."""
    row = Image.new("L", (128 * U, 1))
    px = row.load()
    for x in range(128 * U):
        k = min(1.0, max(0.0, (x / U - x0) / (x1 - x0)))
        px[x, 0] = round(lo + (hi - lo) * k)
    return row.resize((128 * U, 128 * U))


# ---- painting ----

def paint(image, mask, colour):
    image.paste(colour + (255,) if len(colour) == 3 else colour, mask=mask)


def shade(image, mask, dark, base, lit, light=None, reach=3.0):
    """Fill mask with dark at its rim, base inside, and lit where `light` is bright."""
    inside = edge_light(mask, reach)
    layer = Image.composite(Image.new("RGB", image.size, base), Image.new("RGB", image.size, dark), inside)
    if light is not None:
        layer = Image.composite(Image.new("RGB", image.size, lit), layer, light)
    rgba = layer.convert("RGBA")
    image.paste(rgba, mask=mask)


def serpent(path, width=0.75, curl_every=6.0, curl_side=1, curl_size=1.5):
    """The white serpent pattern: a winding line along `path` with small curls off one side."""
    points, curls = [], []
    total = 0.0
    for (x0, y0), (x1, y1) in zip(path, path[1:]):
        seg = math.hypot(x1 - x0, y1 - y0)
        if seg == 0:
            continue
        nx, ny = -(y1 - y0) / seg, (x1 - x0) / seg
        for t in steps(0, 1, 0.25 / max(seg, 0.01))[:-1]:
            s = total + seg * t
            wave = 0.55 * math.sin(s * 2 * math.pi / 4.2)
            points.append((x0 + (x1 - x0) * t + nx * wave, y0 + (y1 - y0) * t + ny * wave))
            if curls == [] and s > curl_every * 0.5 or curls and s - curls[-1][0] >= curl_every:
                curls.append((s, points[-1], (nx, ny)))
        total += seg
    points.append(path[-1])
    mask = stroke(points, width)
    for _, (px, py), (nx, ny) in curls:
        sx, sy = nx * curl_side, ny * curl_side
        arc = []
        for i in range(9):
            a = i / 8 * 1.5 * math.pi
            r = curl_size * (1 - 0.45 * i / 8)
            arc.append((px + sx * (curl_size * 0.9 + r * math.cos(a) * 0.2) + sx * r * (1 - math.cos(a)) * 0.5,
                        py + sy * r * (1 - math.cos(a)) * 0.5 + r * math.sin(a) * 0.7))
        mask = union(mask, stroke(arc, width * 0.8))
    return mask


def finish(image, silhouette, name):
    """Ink outline round the silhouette, under everything, then reduce and save."""
    outline = grow(silhouette, 1.7)
    out = Image.new("RGBA", image.size, (0, 0, 0, 0))
    out.paste(INK + (255,), mask=outline)
    out.alpha_composite(image)
    OUT.mkdir(parents=True, exist_ok=True)
    out.resize((SIZE, SIZE), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


# ---- Vergil's coat ----

PAD = 1.7  # coat edge outside the body's outline
LAST_ROW = 125.5  # lowest hem, leaving room for the outline inside the sheet


def coat_sides(body, hem, flare_left, flare_right):
    """Left and right edge of the coat at height y: the body plus PAD, widening below the waist."""
    def at(y):
        left, right = body.edges(y)
        k = smooth(body.waist, hem, y) ** 1.2
        return left - PAD - flare_left * k, right + PAD + flare_right * k
    return at


def vergil_front(body, name):
    """South: the coat open down the front over the vest, belt and trousers."""
    w, c = body.width, body.centre
    hem = min(body.bottom + 5.5, LAST_ROW)
    side = coat_sides(body, hem, 2.4, 2.4)
    ys = steps(body.top - 0.5, hem)
    outer = [(side(y)[0], y) for y in ys] + [(side(y)[1], y) for y in reversed(ys)]
    full = polygon(outer)

    # The collar stands up and turns out at the shoulders; the head covers its middle, so only the
    # two points beside the jaw show.
    top = body.top
    collars = []
    for s in (-1, 1):
        edge = side(top + 9)[0 if s < 0 else 1]
        collars.append(polygon([(c(top) + s * 0.2 * w, top + 12), (edge, top + 14),
                                (edge + s * 7.0, top + 7.5), (edge + s * 5.6, top + 3.5),
                                (c(top) + s * 0.34 * w, top + 1)]))
    collar = union(*collars)

    lapel_end = top + 0.4 * body.height

    def opening_half(y):
        if y < lapel_end:
            return 0.13 * w + 0.17 * w * (1 - smooth(top, lapel_end, y))
        return 0.13 * w + 0.17 * w * smooth(body.waist, hem, y) ** 1.3
    oys = steps(top - 9, hem + 3)
    opening = polygon([(c(y) - opening_half(y), y) for y in oys] + [(c(y) + opening_half(y), y) for y in reversed(oys)])

    # What shows in the opening is cut to the body's own round bottom, so the trousers stop where
    # the body stops and the gap between the coat's front panels below is clear.
    brows = body.rows
    bys = steps(top, body.bottom)
    torso = polygon([(interp(brows, y)[0] - PAD * 0.6, y) for y in bys] +
                    [(interp(brows, y)[1] + PAD * 0.6, y) for y in reversed(bys)])
    inner = inter(opening, torso)
    panels = union(minus(full, opening), minus(collar, opening))
    silhouette = union(panels, inner)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))

    # Vest down to the belt, ribbed in chevrons; trousers below with a centre seam.
    vest = inter(inner, polygon([(0, 0), (128, 0), (128, body.waist - 1.2), (0, body.waist - 1.2)]))
    trousers = inter(inner, polygon([(0, body.waist + 1.2), (128, body.waist + 1.2), (128, 128), (0, 128)]))
    belt = minus(minus(inner, vest), trousers)
    shade(image, vest, INK, VEST, VEST, reach=1.5)
    for k in range(6):
        y = top + 12 + k * 2.6
        ribs = stroke([(c(y) - 4, y - 1.4), (c(y), y + 0.4), (c(y) + 4, y - 1.4)], 0.45)
        paint(image, inter(ribs, shrink(vest, 0.4)), VEST_RIB)
    shade(image, trousers, TROUSERS_SEAM, TROUSERS, TROUSERS, reach=1.5)
    paint(image, inter(stroke([(c(body.waist + 3), body.waist + 3), (c(body.bottom), body.bottom)], 0.5),
                       trousers), TROUSERS_SEAM)
    paint(image, belt, BELT)
    cy = body.waist
    paint(image, polygon([(c(cy) - 1.4, cy - 1.5), (c(cy) + 1.4, cy - 1.5), (c(cy) + 1.4, cy + 1.5),
                          (c(cy) - 1.4, cy + 1.5)]), BUCKLE)
    paint(image, polygon([(c(cy) - 0.6, cy - 0.7), (c(cy) + 0.6, cy - 0.7), (c(cy) + 0.6, cy + 0.7),
                          (c(cy) - 0.6, cy + 0.7)]), BELT)

    # The coat, lit from the viewer's left as the vanilla bodies are.
    light = inter(ramp(c(top) - w * 0.7, c(top) + w * 0.4, 150, 0), ellipse(c(top) - w * 0.3, top + 20, w * 0.45, 26)
                  .filter(ImageFilter.GaussianBlur(6 * U)))
    shade(image, panels, COAT_DARK, COAT, COAT_LIT, light)

    # Lapels: a lighter band along the opening, down to the lapel's end.
    for s in (-1, 1):
        lys = steps(top - 2, lapel_end)
        band = polygon([(c(y) + s * opening_half(y), y) for y in lys] +
                       [(c(y) + s * (opening_half(y) + 0.16 * w * (1 - smooth(top, lapel_end, y)) + 0.6), y)
                        for y in reversed(lys)])
        paint(image, inter(band, panels), COAT_LIT)
        paint(image, inter(stroke([(c(y) + s * (opening_half(y) + 0.16 * w * (1 - smooth(top, lapel_end, y)) + 0.6), y)
                                   for y in lys], 0.5), panels), COAT_DARK)

    # Sleeves: a seam from the shoulder, and a cuff with gold buttons at the hand.
    cuff_y = top + 0.64 * body.height
    for s in (-1, 1):
        def sleeve_x(y, s=s):
            left, right = side(y)
            return (right if s > 0 else left) - s * min(4.2, 0.2 * w)
        sys_ = steps(top + 8, cuff_y)
        paint(image, inter(stroke([(sleeve_x(y), y) for y in sys_], 0.5), panels), COAT_DARK)
        cuff = polygon([(sleeve_x(cuff_y - 2.2), cuff_y - 2.2), (side(cuff_y - 2.2)[s > 0], cuff_y - 2.2),
                        (side(cuff_y + 1.4)[s > 0], cuff_y + 1.4), (sleeve_x(cuff_y + 1.4), cuff_y + 1.4)])
        paint(image, inter(grow(cuff, 0.3), panels), COAT_DARK)
        paint(image, inter(cuff, panels), COAT)
        for k in (0, 1):
            bx = sleeve_x(cuff_y) + s * (1.2 + k * 1.7)
            paint(image, inter(ellipse(bx, cuff_y - 0.4, 0.6, 0.6), cuff), TRIM_LIT)

    # Serpent: head over the wearer's left shoulder (the viewer's right), tail down the wearer's
    # right front (the viewer's left) to the hem.
    for s, end, curl in ((-1, hem - 2.5, -1), (1, body.waist - 1, 1)):
        path = [(c(y) + s * (opening_half(y) + 2.2 + 0.16 * w * (1 - smooth(top, lapel_end, y))), y)
                for y in steps(top + 2, end, 3)]
        paint(image, inter(serpent(path, 0.7, 6.5, curl, 1.3), shrink(panels, 0.6)), SERPENT)
    for s in (-1, 1):
        edge = side(top + 9)[0 if s < 0 else 1]
        paint(image, inter(serpent([(edge + s * 5.6, top + 5.5), (edge + s * 0.8, top + 11.5)], 0.6, 99), collar),
              SERPENT)

    # Gold trim down both front edges and along the hem; a dark line where the coat meets the vest.
    trims = []
    for s in (-1, 1):
        trims.append(stroke([(c(y) + s * (opening_half(y) + 0.55), y) for y in steps(top - 8, hem + 1)], 1.0))
    hem_line = stroke([(side(hem - 0.6)[0] - 1, hem - 0.6), (side(hem - 0.6)[1] + 1, hem - 0.6)], 1.0)
    trim = inter(union(*trims, hem_line), panels)
    paint(image, trim, TRIM)
    paint(image, inter(trim, ellipse(c(top) - w * 0.35, top + 22, w * 0.3, 20)), TRIM_LIT)
    for s in (-1, 1):
        paint(image, inter(stroke([(c(y) + s * opening_half(y), y) for y in steps(top - 8, hem + 1)], 0.55),
                           grow(inner, 0.4)), INK)
    # Collar edges in gold.
    paint(image, inter(minus(collar, shrink(collar, 0.8)), grow(panels, 0.1)), TRIM)

    finish(image, silhouette, name)


def vergil_back(body, name):
    """North: the stand collar over the nape, a waist seam with two buttons, three split tails."""
    w, c = body.width, body.centre
    hem = min(body.bottom + 6, LAST_ROW)
    side = coat_sides(body, hem, 2.6, 2.6)
    top = body.top
    ys = steps(top + 3, hem)
    # Each tail's bottom hangs in a shallow curve.
    slits = [c(hem) - 0.19 * w, c(hem) + 0.19 * w]
    xs = [side(hem)[0]] + slits + [side(hem)[1]]

    def hem_at(x):
        for a, b in zip(xs, xs[1:]):
            if a <= x <= b:
                k = (x - a) / (b - a)
                return hem - 1.4 + 1.8 * math.sin(math.pi * k)
        return hem

    bottom = [(x, hem_at(x)) for x in steps(xs[0], xs[-1], 0.4)]
    outer = [(side(y)[0], y) for y in ys] + bottom + [(side(y)[1], y) for y in reversed(ys)]
    body_mask = polygon(outer)

    # The stand collar, wider than the neck and higher than the shoulders; drawn over the head
    # (vanilla shells use the same layer facing north), so it covers the nape.
    # Sized to the neck, not the body: the same collar on every body type.
    cw = 16.5
    cys = steps(0, 1, 0.05)
    rim = [(c(top) - 0.82 * cw * math.cos(math.pi * t), top - 3.2 - 4.2 * math.sin(math.pi * t)) for t in cys]
    collar = polygon([(c(top) - cw - 0.6, top + 8), (c(top) - cw, top + 2.5)] + rim +
                     [(c(top) + cw, top + 2.5), (c(top) + cw + 0.6, top + 8),
                      (side(top + 10)[1], top + 10), (side(top + 10)[0], top + 10)])
    shoulders = polygon([(c(top) - cw - 0.6, top + 6), (side(top + 13)[0], top + 13),
                         (side(top + 13)[1], top + 13), (c(top) + cw + 0.6, top + 6)])
    full = union(body_mask, collar, shoulders)

    # Slits: a narrow V opens at the bottom of each, so the three tails read apart.
    cuts = []
    for sx in slits:
        cuts.append(polygon([(sx - 0.15, body.waist + 7), (sx + 0.15, body.waist + 7),
                             (sx + 1.3, hem + 3), (sx - 1.3, hem + 3)]))
    coat = minus(full, union(*cuts))
    silhouette = coat

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    light = ellipse(c(top) - w * 0.15, top + 22, w * 0.42, 22).filter(ImageFilter.GaussianBlur(7 * U))
    shade(image, coat, COAT_DARK, COAT, COAT_LIT, light, reach=3.4)

    # Collar: its inner face shows along the top rim, lighter, with the serpent running round it.
    inner_rim = polygon([(x, y) for x, y in rim] + [(x, y + 2.6) for x, y in reversed(rim)])
    paint(image, inter(inner_rim, coat), COAT_LIT)
    paint(image, inter(stroke([(x, y + 2.6) for x, y in rim], 0.55), coat), COAT_DARK)
    paint(image, inter(serpent([(x, y + 1.3) for x, y in rim[::4]] + [rim[-1]], 0.6, 5.5, 1, 1.0),
                       shrink(inner_rim, 0.3)), SERPENT)
    paint(image, inter(stroke(rim, 0.9), coat), TRIM)
    # Where the collar meets the shoulders.
    seam = [(c(top) - cw * math.cos(math.pi * t), top + 6.5 - 2.0 * math.sin(math.pi * t)) for t in cys]
    paint(image, inter(stroke(seam, 0.55), coat), COAT_DARK)

    # Waist seam with two silver buttons.
    wy = body.waist + 2
    paint(image, inter(stroke([(side(wy)[0], wy), (side(wy)[1], wy)], 0.55), coat), COAT_DARK)
    for s in (-1, 1):
        bx = c(wy) + s * 0.19 * w
        paint(image, inter(grow(ellipse(bx, wy - 0.2, 0.8, 0.8), 0.35), coat), INK)
        paint(image, ellipse(bx, wy - 0.2, 0.8, 0.8), SILVER)

    # Slits: dark fold line, gold trim either side; gold hem along each tail.
    for sx in slits:
        paint(image, inter(stroke([(sx, wy + 1.5), (sx, body.waist + 7)], 0.55), coat), COAT_DARK)
        for s in (-1, 1):
            paint(image, inter(stroke([(sx + s * 0.25, body.waist + 7), (sx + s * 1.9, hem + 3)], 0.9), coat), TRIM)
    paint(image, inter(stroke([(x, y - 0.5) for x, y in bottom], 1.0), coat), TRIM)

    finish(image, silhouette, name)


def vergil_side(body, name):
    """East (facing right): the collar behind the neck, the open front, the tail swinging back with
    its gold lining showing."""
    w = body.width
    top = body.top
    hem = min(body.bottom + 4, LAST_ROW - 3)
    tail = min(body.bottom + 7.5, LAST_ROW)

    def front(y):
        return body.edges(y)[1] + PAD + 0.6 * smooth(body.waist, hem, y)

    def back(y):
        return body.edges(y)[0] - PAD - 4.6 * smooth(body.waist, tail, y) ** 1.25

    fys = steps(top + 1, hem)
    bys = steps(top + 1, tail - 3)
    tip = (back(tail - 3) - 1.6, tail)
    # Hem: from the front bottom back to the tail's point in a long curve.
    hem_curve = []
    for t in steps(0, 1, 0.04):
        x = front(hem) + (tip[0] - front(hem)) * t
        y = hem + (tip[1] - hem) * t ** 1.6 + 1.2 * math.sin(math.pi * t)
        hem_curve.append((x, y))
    outer = [(front(y), y) for y in fys] + hem_curve + [(back(y), y) for y in reversed(bys)]
    coat_body = polygon(outer)

    # Collar standing up behind the neck, turned out at the top.
    cb = body.edges(top + 6)[0] - PAD - 2.5
    collar = polygon([(cb + 1.2, top + 9), (cb - 1.6, top - 3), (cb + 0.2, top - 5.8), (cb + 3.5, top - 5.4),
                      (cb + 7.5, top - 2.5), (cb + 10.5, top + 4), (cb + 7, top + 10)])
    full = union(coat_body, collar)

    # The open front: a strip of vest, belt and trousers between the coat's front edge and the chest.
    open_x = lambda y: front(y) - (3.4 if y < body.waist else 2.6 + 1.2 * smooth(body.waist, hem, y))
    oys = steps(top + 7, body.bottom + 1)
    strip = polygon([(open_x(y), y) for y in oys] + [(front(y) + 1, y) for y in reversed(oys)])
    brows = body.rows
    torso = polygon([(interp(brows, y)[0] - PAD * 0.6, y) for y in steps(top, body.bottom)] +
                    [(interp(brows, y)[1] + PAD * 0.6, y) for y in reversed(steps(top, body.bottom))])
    inner = inter(inter(strip, torso), full)
    coat = minus(full, inner)
    silhouette = full

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    vest = inter(inner, polygon([(0, 0), (128, 0), (128, body.waist - 1.2), (0, body.waist - 1.2)]))
    trousers = inter(inner, polygon([(0, body.waist + 1.2), (128, body.waist + 1.2), (128, 128), (0, 128)]))
    paint(image, vest, VEST)
    paint(image, trousers, TROUSERS)
    paint(image, minus(minus(inner, vest), trousers), BELT)

    # Lining: the inside of the tail, seen under its swung-out back edge.
    lining = inter(minus(coat, shrink(coat, 1.9)),
                   polygon([(0, body.waist + 9), (body.centre(body.waist + 9), body.waist + 9),
                            (body.centre(tail) + 2, tail + 3), (0, tail + 3)]))
    light = ramp(body.edges(top + 20)[0], body.edges(top + 20)[1] + 3, 0, 140)
    shade(image, coat, COAT_DARK, COAT, COAT_LIT, light, reach=3.0)
    shade(image, lining, LINING_DARK, LINING, LINING, reach=1.2)

    # Sleeve down the side, a cuff with gold buttons at the hand.
    cuff_y = top + 0.64 * body.height
    sleeve = lambda y: body.centre(y) - 1.2 - 0.9 * smooth(top, cuff_y, y)
    sl = [(sleeve(y) - 3.2 + 1.2 * smooth(top + 10, top + 16, y), y) for y in steps(top + 10, cuff_y - 2)]
    sr = [(sleeve(y) + 3.2, y) for y in steps(top + 10, cuff_y - 2)]
    paint(image, inter(stroke(sl, 0.5), coat), COAT_DARK)
    paint(image, inter(stroke(sr, 0.5), coat), COAT_DARK)
    cuff = polygon([(sleeve(cuff_y) - 3.4, cuff_y - 2.2), (sleeve(cuff_y) + 3.4, cuff_y - 2.2),
                    (sleeve(cuff_y) + 3.4, cuff_y + 1.4), (sleeve(cuff_y) - 3.4, cuff_y + 1.4)])
    paint(image, inter(grow(cuff, 0.3), coat), COAT_DARK)
    paint(image, inter(cuff, coat), COAT)
    for k in (-1, 0, 1):
        paint(image, inter(ellipse(sleeve(cuff_y) + k * 1.9, cuff_y - 0.4, 0.6, 0.6), cuff), TRIM_LIT)

    # Lapel turned back on the chest, the serpent running down the front edge.
    lapel = polygon([(open_x(top + 7) - 0.4, top + 7), (open_x(top + 7) - 5.2, top + 8),
                     (open_x(top + 19) - 0.5, top + 19)])
    paint(image, inter(lapel, coat), COAT_LIT)
    paint(image, inter(stroke([(open_x(top + 7) - 5.2, top + 8), (open_x(top + 19) - 0.5, top + 19)], 0.5), coat),
          COAT_DARK)
    path = [(open_x(y) - 2.4, y) for y in steps(top + 9, hem - 2, 3)]
    paint(image, inter(serpent(path, 0.7, 6.5, -1, 1.3), shrink(coat, 0.7)), SERPENT)

    # Collar: lighter inner face at the top, serpent round it, gold rim.
    collar_face = inter(polygon([(cb - 1.6, top - 3), (cb + 0.2, top - 5.8), (cb + 3.5, top - 5.4),
                                 (cb + 7.5, top - 2.5), (cb + 10.5, top + 4), (cb + 7.2, top + 3),
                                 (cb + 4.5, top - 1.5), (cb + 0.6, top - 1.8)]), coat)
    paint(image, collar_face, COAT_LIT)
    paint(image, inter(serpent([(cb - 0.2, top - 3.4), (cb + 3.8, top - 3.6), (cb + 8.6, top + 1.8)], 0.55, 99),
                       collar_face), SERPENT)
    paint(image, inter(minus(collar, shrink(collar, 0.8)), coat), TRIM)

    # Gold trim down the front edge and along the hem.
    edge = [(open_x(y) - 0.5, y) for y in steps(top + 7, body.bottom + 1)] + [(front(hem) - 0.8, hem - 0.4)]
    paint(image, inter(stroke(edge, 1.0), coat), TRIM)
    paint(image, inter(stroke([(x, y - 0.5) for x, y in hem_curve], 1.0), coat), TRIM)
    paint(image, inter(stroke([(open_x(y), y) for y in steps(top + 7, body.bottom + 1)], 0.55), grow(inner, 0.4)),
          INK)

    finish(image, silhouette, name)


# ---- Vergil kneeling (Judgement Cut End) ----

# How far the kneeling torso and head sit below the standing ones, in 128 px units (1.5 cells a sheet):
# 14 units is 0.164 cells. Patches_VergilPose.cs moves the head down by the same amount.
KNEEL_DROP = 14
BOOT, BOOT_LIT = (22, 22, 28), (70, 74, 88)


def vergil_kneel_front(body, name):
    """South, kneeling: the standing torso moved down, the skirt spread on the floor, the wearer's left
    knee up (the viewer's right) with the shin and boot below it, the right knee on the ground."""
    w, D = body.width, KNEEL_DROP
    top, waist = body.top + D, body.waist + D

    def c(y):
        return body.centre(min(y - D, body.waist))

    def torso(y):
        left, right = body.edges(min(y - D, body.waist))
        return left - PAD, right + PAD

    cx = c(waist)
    hem_left, hem_right, hem_mid = 113.0, 112.0, 115.0
    far_left, far_right = cx - 0.95 * w, cx + 1.05 * w

    def side(y):
        left, right = torso(y)
        if y <= waist:
            return left, right
        k = smooth(waist, hem_left, y) ** 0.8
        return left + (far_left - left) * k, right + (far_right - right) * k

    ys = steps(top - 0.5, hem_left)
    bottom = []
    for x in steps(far_left, far_right, 0.5):
        k = (x - far_left) / (far_right - far_left)
        bottom.append((x, hem_left + (hem_right - hem_left) * k + (hem_mid - max(hem_left, hem_right)) * math.sin(math.pi * k)))
    outer = [(side(y)[0], y) for y in ys] + bottom + [(side(y)[1], y) for y in reversed(steps(top - 0.5, hem_right))]
    full = polygon(outer)

    collars = []
    for s_ in (-1, 1):
        edge = side(top + 9)[0 if s_ < 0 else 1]
        collars.append(polygon([(c(top) + s_ * 0.2 * w, top + 12), (edge, top + 14),
                                (edge + s_ * 7.0, top + 7.5), (edge + s_ * 5.6, top + 3.5),
                                (c(top) + s_ * 0.34 * w, top + 1)]))
    collar = union(*collars)

    lapel_end = top + 0.4 * body.height

    def opening_edge(y, s_):
        """The front opening's edge on side s_ (-1 the viewer's left): the standing lapels down to the
        waist, then parting round the legs, the viewer's right panel pushed out by the raised knee."""
        if y < lapel_end:
            half = 0.13 * w + 0.17 * w * (1 - smooth(top, lapel_end, y))
            return c(y) + s_ * half
        if y <= waist:
            return c(y) + s_ * 0.13 * w
        k = smooth(waist, hem_left, y) ** 0.9
        return cx + s_ * 0.13 * w + (s_ * (0.19 if s_ < 0 else 0.49)) * w * k

    oys = steps(top - 9, hem_mid + 3)
    opening = polygon([(opening_edge(y, -1), y) for y in oys] + [(opening_edge(y, 1), y) for y in reversed(oys)])

    # The legs, in the opening. Raised knee: a rounded knee toward the viewer, the shin under it, a boot.
    knee_x, knee_y = cx + 0.28 * w, waist + 8.5
    raised = union(polygon([(cx + 0.04 * w, waist + 1.5), (cx + 0.46 * w, waist + 2.5),
                            (knee_x + 0.26 * w, knee_y), (knee_x - 0.26 * w, knee_y)]),
                   ellipse(knee_x, knee_y, 0.26 * w, 4.4),
                   polygon([(knee_x - 0.13 * w, knee_y), (knee_x + 0.14 * w, knee_y),
                            (knee_x + 0.13 * w, 108), (knee_x - 0.11 * w, 108)]))
    boot = union(polygon([(knee_x - 0.15 * w, 105.5), (knee_x + 0.16 * w, 105.5),
                          (knee_x + 0.19 * w, 110.5), (knee_x - 0.17 * w, 110.5)]),
                 ellipse(knee_x + 0.01 * w, 110.6, 0.19 * w, 1.6))
    ground_x = cx - 0.26 * w
    grounded = union(polygon([(cx - 0.36 * w, waist + 1.5), (cx - 0.02 * w, waist + 1.5),
                              (ground_x + 0.17 * w, 108.5), (ground_x - 0.2 * w, 108.5)]),
                     ellipse(ground_x, 108.8, 0.21 * w, 3.1))
    legs = union(raised, grounded)

    torso_rows = [(y + D, l, r) for y, l, r in body.rows if y <= body.waist + 2]
    bys = steps(top, waist + 2)
    chest = polygon([(interp(torso_rows, y)[0] - PAD * 0.6, y) for y in bys] +
                    [(interp(torso_rows, y)[1] + PAD * 0.6, y) for y in reversed(bys)])
    lower = union(legs, boot, polygon([(cx - 0.36 * w, waist - 1), (cx + 0.46 * w, waist - 1),
                                       (cx + 0.46 * w, waist + 3), (cx - 0.36 * w, waist + 3)]))
    inner = inter(opening, union(chest, lower))
    panels = union(minus(full, opening), minus(collar, opening))
    # The raised knee pushes forward over the edge of the viewer's right panel.
    knee_front = minus(inter(ellipse(knee_x, knee_y, 0.26 * w, 4.4), full), inner)
    inner = union(inner, knee_front)
    panels = minus(panels, knee_front)
    silhouette = union(panels, inner)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))

    vest = inter(inner, polygon([(0, 0), (128, 0), (128, waist - 1.2), (0, waist - 1.2)]))
    below = inter(inner, polygon([(0, waist + 1.2), (128, waist + 1.2), (128, 128), (0, 128)]))
    belt = minus(minus(inner, vest), below)
    shade(image, vest, INK, VEST, VEST, reach=1.5)
    for k in range(6):
        y = top + 12 + k * 2.6
        ribs = stroke([(c(y) - 4, y - 1.4), (c(y), y + 0.4), (c(y) + 4, y - 1.4)], 0.45)
        paint(image, inter(ribs, shrink(vest, 0.4)), VEST_RIB)
    trousers = minus(below, boot)
    # Light on the top of both knees, from the viewer's left as on the coat.
    knee_light = union(ellipse(knee_x - 0.06 * w, knee_y - 1.6, 0.15 * w, 2.2),
                       ellipse(ground_x - 0.04 * w, 107.8, 0.11 * w, 1.4)).filter(ImageFilter.GaussianBlur(1.0 * U))
    shade(image, trousers, TROUSERS_SEAM, TROUSERS, (92, 108, 100), knee_light, reach=1.2)
    # The fold where the raised thigh meets the grounded one, and the shin's front crease.
    paint(image, inter(stroke([(cx + 0.02 * w, waist + 2), (cx + 0.04 * w, 106)], 0.5), trousers), TROUSERS_SEAM)
    paint(image, inter(stroke([(knee_x, knee_y + 3.5), (knee_x + 0.01 * w, 105)], 0.4), trousers), TROUSERS_SEAM)
    shade(image, inter(boot, inner), INK, BOOT, BOOT_LIT,
          ellipse(knee_x - 0.06 * w, 108.5, 0.1 * w, 1.6).filter(ImageFilter.GaussianBlur(0.8 * U)), reach=0.8)
    paint(image, belt, BELT)
    paint(image, polygon([(cx - 1.4, waist - 1.5), (cx + 1.4, waist - 1.5), (cx + 1.4, waist + 1.5),
                          (cx - 1.4, waist + 1.5)]), BUCKLE)
    paint(image, polygon([(cx - 0.6, waist - 0.7), (cx + 0.6, waist - 0.7), (cx + 0.6, waist + 0.7),
                          (cx - 0.6, waist + 0.7)]), BELT)

    light = inter(ramp(c(top) - w * 0.7, c(top) + w * 0.4, 150, 0), ellipse(c(top) - w * 0.3, top + 20, w * 0.45, 26)
                  .filter(ImageFilter.GaussianBlur(6 * U)))
    shade(image, panels, COAT_DARK, COAT, COAT_LIT, light)

    # Where the viewer's right panel folds over the raised knee, its gold lining shows.
    lining = inter(polygon([(opening_edge(104, 1) - 0.5, 102), (opening_edge(104, 1) + 0.16 * w, 101),
                            (opening_edge(112, 1) + 0.2 * w, 112.5), (opening_edge(112, 1) - 0.5, 113)]), panels)
    shade(image, lining, LINING_DARK, LINING, LINING, reach=0.8)
    # Folds where the skirt lies on the floor.
    for x0, x1 in ((cx - 0.55 * w, cx - 0.72 * w), (cx + 0.78 * w, cx + 0.9 * w)):
        paint(image, inter(stroke([(x0, waist + 7), (x1, 111.5)], 0.5), panels), COAT_DARK)

    for s_ in (-1, 1):
        lys = steps(top - 2, lapel_end)
        band = polygon([(opening_edge(y, s_), y) for y in lys] +
                       [(opening_edge(y, s_) + s_ * (0.16 * w * (1 - smooth(top, lapel_end, y)) + 0.6), y)
                        for y in reversed(lys)])
        paint(image, inter(band, panels), COAT_LIT)

    cuff_y = top + 0.64 * body.height
    for s_ in (-1, 1):
        def sleeve_x(y, s_=s_):
            left, right = torso(y)
            return (right if s_ > 0 else left) - s_ * min(4.2, 0.2 * w)
        paint(image, inter(stroke([(sleeve_x(y), y) for y in steps(top + 8, cuff_y)], 0.5), panels), COAT_DARK)

    for s_, end, curl in ((-1, hem_left - 3, -1), (1, waist - 1, 1)):
        path = [(opening_edge(y, s_) + s_ * (2.2 + 0.16 * w * (1 - smooth(top, lapel_end, y))), y)
                for y in steps(top + 2, end, 3)]
        paint(image, inter(serpent(path, 0.7, 6.5, curl, 1.3), shrink(panels, 0.6)), SERPENT)
    for s_ in (-1, 1):
        edge = side(top + 9)[0 if s_ < 0 else 1]
        paint(image, inter(serpent([(edge + s_ * 5.6, top + 5.5), (edge + s_ * 0.8, top + 11.5)], 0.6, 99), collar),
              SERPENT)

    trims = [stroke([(opening_edge(y, s_) + s_ * 0.55, y) for y in steps(top - 8, hem_mid + 1)], 1.0) for s_ in (-1, 1)]
    hem_line = stroke([(x, y - 0.6) for x, y in bottom], 1.0)
    trim = inter(union(*trims, hem_line), panels)
    paint(image, trim, TRIM)
    paint(image, inter(trim, ellipse(c(top) - w * 0.35, top + 22, w * 0.3, 20)), TRIM_LIT)
    for s_ in (-1, 1):
        paint(image, inter(stroke([(opening_edge(y, s_), y) for y in steps(top - 8, hem_mid + 1)], 0.55),
                           grow(inner, 0.4)), INK)
    paint(image, inter(minus(collar, shrink(collar, 0.8)), grow(panels, 0.1)), TRIM)
    # The knee's outline over the panel it covers.
    paint(image, inter(minus(grow(knee_front, 0.45), knee_front), panels), INK)

    finish(image, silhouette, name)


def main():
    for body in ("Thin", "Male", "Female", "Fat", "Hulk"):
        vergil_front(Body(BODIES[(body, "south")]), f"VergilCoat_{body}_south.png")
        vergil_side(Body(BODIES[(body, "east")]), f"VergilCoat_{body}_east.png")
        vergil_back(Body(BODIES[(body, "north")]), f"VergilCoat_{body}_north.png")
    # Hero form makes adult human Hosts Thin, so only the Thin kneel is made; other bodies keep the squash.
    vergil_kneel_front(Body(BODIES[("Thin", "south")]), "VergilCoatKneel_Thin.png")


if __name__ == "__main__":
    main()
