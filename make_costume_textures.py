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

Echo/Costume/AkatsukiCloak_<Body>_<facing>.png
    256 px, the same 5 body types x 3 facings. The Akatsuki cloak, shared by every Echo who wears it
    (the AG_EchoManifest_Akatsuki parent in AG_Echo_Hediffs.xml): a long black cloak, red clouds with a
    pale outline and pale curls, the red lining showing as a thin line down the front opening, a
    short slit at the front hem.

Echo/Costume/AkatsukiCollar_<facing>.png
    256 px, south, east, north: the cloak's high collar, drawn on the head (parent node Head, over the
    hair and beard), so it stays on the head when the pawn crawls or swims. It covers the chin; its
    red lining shows along the top rim and down the front. One set for every body type: it is fitted
    to the vanilla heads (chin at 87-92 on the 128 px head sheet, widest jaw 42-85), not the body.

Echo/Costume/ObitoMask_<facing>.png
    256 px, south, east, west, north: Obito's orange spiral mask, on the head over the hair (layer 63),
    under the Akatsuki collar. One spiral groove winds out from the eye hole on his right eye (the
    viewer's left facing south) and is fitted to the mask's outline, so its turns bunch up on the
    near side, as in the official art. The face-on drawing is projected onto the profiles: east shows
    his right side with the hole, west (its own picture, not east mirrored) his left side with only
    the outer turns; it is stored facing right, as the game mirrors every west picture. North is
    empty: the head hides the mask.

Echo/Costume/PainPiercings_<facing>.png
    256 px, south, east, north (west is east mirrored: the piercings are the same on both sides):
    Pain's piercings from the official art, on the head under the hair (layer 61). Six dark studs
    down the bridge of the nose in two columns, two short studs under the lower lip (just above the
    collar), and silver studs along each ear, at the head's sides face-on and in an arc along the
    ear's rim in profile.

Echo/Costume/MinatoHaori_<Body>_<facing>.png
    256 px, the same 5 body types x 3 facings. Minato's Hokage haori from the official art: white,
    short sleeves, open down the front, knee length, a tall collar with a lavender-grey inside, red
    flames round the hem with white curls cut into them, and 四代目火影 ("Fourth Hokage") in red down
    the back. Under it the green jōnin vest (zip, chest pouches) tied across by a red cord, blue
    trousers, grey forearm guards below the sleeves, white tape and a kunai holster on his right
    thigh, and below the hem the shins in white wraps and dark sandals.

Echo/Costume/MinatoHeadband_<facing>.png
    256 px, south, east, north (west is east mirrored: the knot is at the back): the forehead
    protector, on the head over the hair (layer 63). A blue band just above the brows, the steel
    plate with a rivet in each corner and the Leaf symbol engraved on it; the knot and two loose ends
    at the back.

Echo/Costume/SasukeOutfit_<Body>_<facing>.png
    256 px, the same 5 body types x 4 facings: Sasuke's outfit from the end of the Fourth War, from
    the official art. A lavender-grey top zipped up to a high collar, short sleeves, the Uchiha crest
    (traced from the wiki's Uchiha_Symbol.svg) on the back under the collar. The arms are not drawn,
    as on the other costumes: the top covers the body, with a seam to the elbow for the short sleeve.
    A blue cloth from the waist to the knee, folded over at the top, with the thick purple rope wound
    round it twice and knotted a little to his right of the middle: two frayed ends hang down his
    right thigh and a big loop down his left. Below the knee the dark trousers bloused over grey shin
    wraps, and sandals. West is its own picture (his left side shows the loop, the right side the
    ends), stored facing right because the game mirrors every west picture. The art's colours are
    pushed toward blue so they do not read beige and pink in the game's warm light.

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


def finish(image, silhouette, name, outline_within=None, outline_width=1.7):
    """Ink outline round the silhouette, under everything, then reduce and save. With outline_within,
    the outline is drawn only inside that mask (for an edge that joins another piece)."""
    outline = grow(silhouette, outline_width)
    if outline_within is not None:
        outline = union(inter(outline, outline_within), silhouette)
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


# ---- the Akatsuki cloak ----

AK_CLOTH, AK_CLOTH_LIT, AK_CLOTH_DARK = (34, 33, 42), (74, 72, 90), (12, 12, 17)
AK_LINING = (186, 32, 42)
AK_CLOUD, AK_CLOUD_DARK = (184, 36, 46), (126, 22, 30)
AK_CLOUD_EDGE = (236, 232, 226)
AK_LEGS = (24, 25, 31)
AK_FLARE = 4.2  # how far the hem stands out past the body on each side, front and back: a bell, not a tube

# The cloud, traced from an anime frame of the Akatsuki symbol (385 px wide, centred on (282.5, 410)):
# a pointed wisp to the left, round lobes to the right, pale curls inside four of them.
CLOUD_W, CLOUD_CX, CLOUD_CY = 385.0, 282.5, 410.0
CLOUD_LOBES = [(250, 332, 57, 57), (345, 322, 57, 57), (418, 392, 55, 55), (388, 468, 62, 62),
               (282, 490, 62, 62), (200, 445, 70, 85)]
CLOUD_CORE = [(200, 380), (250, 332), (345, 322), (418, 392), (388, 468), (282, 490), (200, 445)]
CLOUD_TAIL = [(90, 300), (120, 300), (160, 306), (200, 322), (222, 352), (175, 425), (150, 382),
              (134, 346), (112, 318)]
# Curls: centre, start and end angle in degrees (y down, so a rising angle turns clockwise), start and
# end radius.
CLOUD_CURLS = [(250, 336, 150, 470, 46, 15), (345, 326, 160, 470, 46, 15), (388, 468, 330, 70, 50, 17),
               (268, 508, 150, 290, 34, 24)]


def cloud(cx, cy, width, mirror=False):
    """The cloud's fill and its curls as masks, `width` units wide and centred on (cx, cy); mirror
    turns the wisp to the right."""
    s = width / CLOUD_W
    sx = -1 if mirror else 1

    def at(x, y):
        return cx + sx * (x - CLOUD_CX) * s, cy + (y - CLOUD_CY) * s
    fill = union(polygon([at(x, y) for x, y in CLOUD_CORE]), polygon([at(x, y) for x, y in CLOUD_TAIL]),
                 *[ellipse(*at(x, y), rx * s, ry * s) for x, y, rx, ry in CLOUD_LOBES])
    curls = []
    for x, y, a0, a1, r0, r1 in CLOUD_CURLS:
        points = []
        for t in steps(0, 1, 0.04):
            a = math.radians(a0 + (a1 - a0) * t)
            r = r0 + (r1 - r0) * t
            points.append(at(x + r * math.cos(a), y + r * math.sin(a)))
        curls.append(stroke(points, max(0.4, 0.04 * width)))
    return fill, union(*curls)


def paint_cloud(image, cx, cy, width, within, mirror=False):
    """A red cloud with its pale outline and curls, cut to `within` (the panel it is printed on)."""
    fill, curls = cloud(cx, cy, width, mirror)
    edge = max(0.5, 0.05 * width)
    paint(image, inter(grow(fill, edge), within), AK_CLOUD_EDGE)
    shade(image, inter(fill, within), AK_CLOUD_DARK, AK_CLOUD, AK_CLOUD, reach=0.8)
    paint(image, inter(inter(curls, shrink(fill, edge * 0.3)), within), AK_CLOUD_EDGE)


def cloud_width(body):
    """The clouds are large, as in the anime: about 45 % of a Thin front, a little less on wide bodies."""
    return 0.34 * body.width + 5.0


def half_of(line, top, bottom, s):
    """Everything on side s (-1 the viewer's left) of the line x = line(y)."""
    ys = steps(top, bottom)
    outer = 0 if s < 0 else 128
    return polygon([(line(y), y) for y in ys] + [(outer, bottom), (outer, top)])


def cloak_outline(side, top, hem):
    ys = steps(top, hem)
    return polygon([(side(y)[0], y) for y in ys] + [(side(y)[1], y) for y in reversed(ys)])


def sleeve_seams(image, body, side, within):
    """A seam from each shoulder down to the cuff, and the cuff's hem across the sleeve."""
    w, top = body.width, body.top
    cuff_y = top + 0.64 * body.height
    for s in (-1, 1):
        def sleeve_x(y, s=s):
            left, right = side(y)
            return (right if s > 0 else left) - s * min(4.2, 0.2 * w)
        paint(image, inter(stroke([(sleeve_x(y), y) for y in steps(top + 8, cuff_y)], 0.5), within), AK_CLOTH_DARK)
        outer = side(cuff_y + 0.8)[1 if s > 0 else 0]
        paint(image, inter(stroke([(sleeve_x(cuff_y), cuff_y), (outer, cuff_y + 0.8)], 0.55), within),
              AK_CLOTH_DARK)


def akatsuki_front(body, name):
    """South: the cloak closed down the front, the red lining showing along the closing line, a short
    slit at the hem, four clouds (two cut by the cloak's edge)."""
    w, c, top, h = body.width, body.centre, body.top, body.height
    hem = min(body.bottom + 6, LAST_ROW)
    side = coat_sides(body, hem, AK_FLARE, AK_FLARE)
    cloak = cloak_outline(side, top - 0.5, hem)

    slit_top = hem - 0.1 * h
    slit = inter(polygon([(c(slit_top), slit_top), (c(hem) + 1.4, hem + 1), (c(hem) - 1.4, hem + 1)]), cloak)
    panels = minus(cloak, slit)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    # Lit from the viewer's left, as the vanilla bodies are.
    light = inter(ramp(c(top) - w * 0.7, c(top) + w * 0.4, 150, 0), ellipse(c(top) - w * 0.3, top + 20, w * 0.45, 26)
                  .filter(ImageFilter.GaussianBlur(6 * U)))
    shade(image, panels, AK_CLOTH_DARK, AK_CLOTH, AK_CLOTH_LIT, light)
    paint(image, slit, AK_LEGS)
    # Long folds from the waist to the hem.
    for k in (-0.3, 0.26):
        x0 = c(body.waist) + k * w
        paint(image, inter(stroke([(x0, body.waist + 5), (x0 + k * 5, hem - 0.5)], 0.45), shrink(panels, 0.6)),
              AK_CLOTH_DARK)
    sleeve_seams(image, body, side, panels)

    left, right = (inter(panels, half_of(c, top - 12, hem + 4, s)) for s in (-1, 1))
    cw = cloud_width(body)
    paint_cloud(image, c(top) - 0.2 * w, top + 0.42 * h, cw, left)
    paint_cloud(image, side(top + 0.6 * h)[1] - 0.04 * w, top + 0.6 * h, cw, right)
    paint_cloud(image, c(top) + 0.22 * w, hem - 0.13 * h, cw, right, mirror=True)
    paint_cloud(image, side(hem)[0] + 0.04 * w, hem - 0.02 * h, cw, left, mirror=True)

    # The red lining along the closing line, and along both edges of the slit.
    paint(image, inter(stroke([(c(y), y) for y in steps(top - 0.5, slit_top)], 0.7), cloak), AK_LINING)
    for s in (-1, 1):
        paint(image, inter(stroke([(c(slit_top), slit_top), (c(hem) + s * 1.4, hem + 1)], 0.6), cloak), AK_LINING)

    finish(image, cloak, name)


def akatsuki_back(body, name):
    """North: the plain back with four clouds, the top under the collar and the head."""
    w, c, top, h = body.width, body.centre, body.top, body.height
    hem = min(body.bottom + 6, LAST_ROW)
    side = coat_sides(body, hem, AK_FLARE, AK_FLARE)
    cloak = cloak_outline(side, top - 0.5, hem)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    light = ellipse(c(top) - w * 0.15, top + 22, w * 0.42, 22).filter(ImageFilter.GaussianBlur(7 * U))
    shade(image, cloak, AK_CLOTH_DARK, AK_CLOTH, AK_CLOTH_LIT, light, reach=3.4)
    for k in (-0.3, 0.04, 0.3):
        x0 = c(body.waist) + k * w
        paint(image, inter(stroke([(x0, body.waist + 4), (x0 + k * 5, hem - 0.5)], 0.45), shrink(cloak, 0.6)),
              AK_CLOTH_DARK)
    sleeve_seams(image, body, side, cloak)

    cw = cloud_width(body)
    paint_cloud(image, c(top) + 0.14 * w, top + 0.42 * h, cw, cloak)
    paint_cloud(image, side(top + 0.46 * h)[0] + 0.02 * w, top + 0.46 * h, cw, cloak, mirror=True)
    paint_cloud(image, c(top) - 0.24 * w, top + 0.74 * h, cw, cloak, mirror=True)
    paint_cloud(image, c(top) + 0.26 * w, hem - 0.06 * h, cw, cloak)

    finish(image, cloak, name)


def akatsuki_side(body, name):
    """East (facing right): the cloak hanging a little behind the body, the red lining along the front
    edge, the wide sleeve down the side, a cloud on the sleeve and one on the skirt."""
    w, top, h = body.width, body.top, body.height
    hem = min(body.bottom + 5, LAST_ROW - 1.5)

    def front(y):
        return body.edges(y)[1] + PAD + 1.6 * smooth(body.waist, hem, y)

    def back(y):
        return body.edges(y)[0] - PAD - 4.6 * smooth(body.waist, hem, y) ** 1.2

    # The hem runs from the front down to the back, which hangs a little lower.
    hem_curve = [(front(hem) + (back(hem) - front(hem)) * t, hem + 1.5 * t + 0.7 * math.sin(math.pi * t))
                 for t in steps(0, 1, 0.04)]
    cloak = polygon([(front(y), y) for y in steps(top + 1, hem)] + hem_curve +
                    [(back(y), y) for y in reversed(steps(top + 1, hem + 1.5))])

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    light = ramp(body.edges(top + 20)[0], body.edges(top + 20)[1] + 3, 0, 140)
    shade(image, cloak, AK_CLOTH_DARK, AK_CLOTH, AK_CLOTH_LIT, light, reach=3.0)
    for x0, x1 in ((back(body.waist + 6) + 3, back(hem) + 2), (body.centre(body.waist + 8) + 2, body.centre(hem) + 3)):
        paint(image, inter(stroke([(x0, body.waist + 6), (x1, hem)], 0.45), shrink(cloak, 0.6)), AK_CLOTH_DARK)

    # The sleeve down the side, widening to the cuff.
    cuff_y = top + 0.64 * h
    sleeve = lambda y: body.centre(y) - 1.2 - 0.9 * smooth(top, cuff_y, y)
    spread = lambda y: 3.2 + 0.9 * smooth(top + 12, cuff_y, y)
    sl = [(sleeve(y) - spread(y) + 1.2 * smooth(top + 10, top + 16, y), y) for y in steps(top + 10, cuff_y)]
    sr = [(sleeve(y) + spread(y), y) for y in steps(top + 10, cuff_y)]
    arm = polygon(sl + list(reversed(sr)))
    paint(image, inter(stroke(sl, 0.5), cloak), AK_CLOTH_DARK)
    paint(image, inter(stroke(sr, 0.5), cloak), AK_CLOTH_DARK)
    paint(image, inter(stroke([sl[-1], sr[-1]], 0.55), cloak), AK_CLOTH_DARK)

    cw = cloud_width(body)
    paint_cloud(image, sleeve(top + 0.47 * h), top + 0.47 * h, cw * 0.85, inter(grow(arm, 0.2), cloak))
    paint_cloud(image, body.centre(hem - 0.1 * h) - 0.15 * w, hem - 0.1 * h, cw, minus(cloak, grow(arm, 0.4)),
                mirror=True)

    # The red lining along the front edge, where the cloak closes.
    paint(image, inter(stroke([(front(y) - 0.7, y) for y in steps(top + 7, hem)], 0.7), cloak), AK_LINING)

    finish(image, cloak, name)


# The collar is drawn in head space: the vanilla head sheet, 128 units, the head's middle at x 63.5,
# the chin at 87-92, the widest jaw (HeavyJaw) 42-85 at y 80-86. Its lower edge (96) is on the body
# cloak's shoulders for every body type (the Thin cloak is 49-79 there), so it is not outlined.
HEAD_CX = 63.5
COLLAR_BOTTOM = 96.0


def collar_straight(name, rim_side, rim_mid, front):
    """South (front=True) or north: the collar flaring out beside the jaw, its red lining along the top
    rim. From the front, the red lining also runs down the middle, and the two sides part in a small V
    at the top."""
    cx, bottom = HEAD_CX, COLLAR_BOTTOM
    # Just wider than the jaw at the rim, narrowing straight to the Thin cloak's shoulders (half 14.7).
    half_top, half_bottom = 23.5, 14.5

    def rim(x):
        k = (x - cx) / half_top
        return rim_side + (rim_mid - rim_side) * max(0.0, 1 - k * k)

    def half(y):
        return half_top + (half_bottom - half_top) * (y - rim_side) / (bottom - rim_side)

    ys = steps(rim_side, bottom)
    shape = polygon([(x, rim(x)) for x in steps(cx - half_top, cx + half_top, 0.4)] +
                    [(cx + half(y), y) for y in ys] + [(cx - half(y), y) for y in reversed(ys)])
    notch = polygon([(cx - 1.4, rim(cx) - 1), (cx + 1.4, rim(cx) - 1), (cx, rim(cx) + 2.4)]) if front else blank()
    collar = minus(shape, notch)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    light = ramp(cx - half_top, cx + half_top * 0.6, 170, 0)
    shade(image, collar, AK_CLOTH_DARK, AK_CLOTH, AK_CLOTH_LIT, light, reach=2.2)
    # Folds round the neck.
    for s in (-1, 1):
        x0 = cx + s * 0.55 * half_top
        paint(image, inter(stroke([(x0, rim(x0) + 2.2), (cx + s * 0.6 * half_bottom, bottom - 1)], 0.45),
                           shrink(collar, 0.5)), AK_CLOTH_DARK)
    # The red lining turned over along the top rim.
    band = polygon([(x, rim(x) - 0.2) for x in steps(cx - half_top - 1, cx + half_top + 1, 0.4)] +
                   [(x, rim(x) + 1.0) for x in reversed(steps(cx - half_top - 1, cx + half_top + 1, 0.4))])
    paint(image, inter(band, collar), AK_LINING)
    if front:
        paint(image, inter(stroke([(cx, rim(cx)), (cx, bottom + 2)], 0.7), collar), AK_LINING)
        for s in (-1, 1):
            paint(image, inter(stroke([(cx + s * 1.4, rim(cx) - 1), (cx, rim(cx) + 2.4)], 0.6), grow(collar, 0.1)),
                  AK_LINING)

    top_part = polygon([(0, 0), (128, 0), (128, bottom - 2.5), (0, bottom - 2.5)])
    finish(image, collar, name, outline_within=top_part)


def collar_side(name):
    """East (facing right): the collar round the chin and the nape, sloping back from the chin to the
    chest, the red lining along the top rim and down the front edge where it closes."""
    back_x, front_x, rim_back, rim_front, sag = 45.5, 87.5, 78.0, 79.0, 1.2
    bottom = COLLAR_BOTTOM

    def rim(x):
        t = (x - back_x) / (front_x - back_x)
        return rim_back + (rim_front - rim_back) * t + sag * math.sin(math.pi * t)

    # Round the chin (the HeavyJaw chin reaches 86-88 at y 79-85), then back to the chest; the back
    # edge stays inside the Thin cloak (its back is at 41.6 here).
    front_edge = [(front_x, rim_front), (88.8, 82.0), (87.8, 86.0), (84.5, 89.5), (78.5, 92.5), (72.0, 95.0),
                  (68.5, bottom + 0.5)]
    back_edge = [(44.0, bottom + 0.5), (44.5, 88.0), (45.5, 80.0)]
    collar = polygon([(x, rim(x)) for x in steps(back_x, front_x, 0.4)] + front_edge[1:] + back_edge)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    light = ramp(back_x, front_x, 0, 150)
    shade(image, collar, AK_CLOTH_DARK, AK_CLOTH, AK_CLOTH_LIT, light, reach=2.2)
    paint(image, inter(stroke([(64, rim(64) + 2.2), (60, bottom - 1)], 0.45), shrink(collar, 0.5)), AK_CLOTH_DARK)
    band = polygon([(x, rim(x) - 0.2) for x in steps(back_x - 1, front_x + 1, 0.4)] +
                   [(x, rim(x) + 1.0) for x in reversed(steps(back_x - 1, front_x + 1, 0.4))])
    paint(image, inter(band, collar), AK_LINING)
    paint(image, inter(stroke([(x - 0.8, y) for x, y in front_edge], 0.7), collar), AK_LINING)

    top_part = polygon([(0, 0), (128, 0), (128, bottom - 2.5), (0, bottom - 2.5)])
    finish(image, collar, name, outline_within=top_part)


# ---- Obito's spiral mask ----

MASK_ORANGE, MASK_LIT, MASK_DARK = (228, 132, 52), (248, 178, 100), (168, 82, 30)
MASK_GROOVE = (48, 24, 12)
MASK_HOLE_DARK = (14, 10, 10)
SHARINGAN = (206, 26, 32)

# Face-on, in head space: an egg, widest in its upper third, from the hairline to the chin (the
# collar covers its bottom). The vanilla eyes are at x 52-59, y 67-72 on every head type; the hole is
# on Obito's right eye, the viewer's left.
MASK_CX, MASK_TOP, MASK_BOTTOM, MASK_HALF = 63.5, 46.0, 90.0, 18.0
MASK_EYE, MASK_HOLE_R = (55.5, 69.5), 2.3
SPIRAL_TURNS, SPIRAL_START = 5.5, -60.0  # turns out to the rim; the groove leaves the hole up and right
# The average head's face front facing east (Male_Average_Normal_east), (y, x); the mask stands 1.3 off it.
FACE_FRONT_EAST = [(43, 77), (46, 81), (49, 84), (52, 85), (55, 87), (58, 87.5), (61, 88), (64, 87.5),
                   (67, 87), (70, 87), (73, 86), (76, 86), (79, 85), (82, 84), (85, 81), (88, 77), (91, 73)]
# In profile the hole is drawn over the vanilla eye (x 74-81 average, 70-77 female): x 75.5.
MASK_EYE_EAST_X = 75.5
MASK_SIDE_CURVE = 0.65  # how the face-on width folds into the profile: s ** this, s 0 at the middle, 1 at the rim


def mask_half(y):
    """Half the mask's width at height y, face-on (0 above and below it)."""
    mid, half_h = (MASK_TOP + MASK_BOTTOM) / 2, (MASK_BOTTOM - MASK_TOP) / 2
    t = (y - mid) / half_h
    if abs(t) >= 1:
        return 0.0
    return MASK_HALF * math.sqrt(1 - t * t) * (1 - 0.12 * t)


def mask_outline():
    ys = steps(MASK_TOP, MASK_BOTTOM, 0.25)
    return [(MASK_CX - mask_half(y), y) for y in ys] + [(MASK_CX + mask_half(y), y) for y in reversed(ys)]


def mask_edge(x0, y0, theta):
    """Where a ray from (x0, y0) at angle theta leaves the mask."""
    dx, dy = math.cos(theta), math.sin(theta)
    lo, hi = 0.0, 60.0
    for _ in range(40):
        mid = (lo + hi) / 2
        x, y = x0 + dx * mid, y0 + dy * mid
        if abs(x - MASK_CX) < mask_half(y):
            lo = mid
        else:
            hi = mid
    return x0 + dx * lo, y0 + dy * lo


def mask_spiral():
    """The groove, face-on: from the hole's rim out to the mask's rim in SPIRAL_TURNS turns, clockwise on
    screen, each point the same fraction of the way from the hole to the rim along its direction."""
    hx, hy = MASK_EYE
    total = SPIRAL_TURNS * 2 * math.pi
    points = []
    n = int(SPIRAL_TURNS * 240)
    for i in range(n + 1):
        phi = total * i / n
        theta = math.radians(SPIRAL_START) + phi
        ex, ey = mask_edge(hx, hy, theta)
        reach = math.hypot(ex - hx, ey - hy)
        start = min(0.95, (MASK_HOLE_R + 0.2) / max(reach, 0.01))
        f = start + (1 - start) * phi / total
        points.append((hx + (ex - hx) * f, hy + (ey - hy) * f))
    return points


def mask_hole_ring(r):
    hx, hy = MASK_EYE
    return [(hx + r * math.cos(a), hy + r * math.sin(a)) for a in steps(0, 2 * math.pi, 0.1)]


def face_front_east(y):
    rows = [(yy, xx, xx) for yy, xx in FACE_FRONT_EAST]
    return interp(rows, y)[0] + 1.3


def side_depth(y):
    """How far back from the front the mask's rim is in profile at height y: enough at eye level to put
    the hole over the vanilla eye, rounding off to 0 at the top and bottom."""
    hx, hy = MASK_EYE
    s = (MASK_CX - hx) / mask_half(hy)
    full = (face_front_east(hy) - MASK_EYE_EAST_X) / s ** MASK_SIDE_CURVE
    return full * (mask_half(y) / MASK_HALF) ** 0.5


def to_side(x, y, west):
    """A face-on point on the visible half (x left of the middle for east, right of it for west) in
    profile, face to the right. The west picture is stored this way too: the render tree always draws
    west with a mirrored mesh, even when a _west texture exists, so it comes out facing left."""
    half = mask_half(y)
    s = min(1.0, abs(x - MASK_CX) / half) if half > 0 else 0.0
    return face_front_east(y) - side_depth(y) * s ** MASK_SIDE_CURVE, y


def visible_runs(points, west):
    """The parts of a face-on polyline on the half a profile shows, each cut where it crosses the middle."""
    runs, run = [], []
    shown = (lambda x: x >= MASK_CX) if west else (lambda x: x <= MASK_CX)
    for a, b in zip(points, points[1:]):
        if shown(a[0]):
            run.append(a)
            if not shown(b[0]):
                k = (MASK_CX - a[0]) / (b[0] - a[0])
                run.append((MASK_CX, a[1] + (b[1] - a[1]) * k))
                runs.append(run)
                run = []
        elif shown(b[0]):
            k = (MASK_CX - a[0]) / (b[0] - a[0])
            run = [(MASK_CX, a[1] + (b[1] - a[1]) * k)]
    if shown(points[-1][0]):
        run.append(points[-1])
    if len(run) > 1:
        runs.append(run)
    return runs


def mask_front(name):
    """South: the whole mask face-on, the hole with the Sharingan in it."""
    mask = polygon(mask_outline())
    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    light = ellipse(MASK_CX - 6, MASK_TOP + 12, 12, 13).filter(ImageFilter.GaussianBlur(4 * U))
    shade(image, mask, MASK_DARK, MASK_ORANGE, MASK_LIT, light, reach=2.4)
    paint(image, inter(stroke(mask_spiral(), 0.5), shrink(mask, 0.3)), MASK_GROOVE)
    hx, hy = MASK_EYE
    paint(image, ellipse(hx, hy, MASK_HOLE_R + 0.5, MASK_HOLE_R + 0.5), MASK_GROOVE)
    paint(image, ellipse(hx, hy, MASK_HOLE_R, MASK_HOLE_R), MASK_HOLE_DARK)
    paint(image, ellipse(hx + 0.2, hy + 0.1, 1.15, 1.15), SHARINGAN)
    paint(image, ellipse(hx + 0.2, hy + 0.1, 0.42, 0.42), MASK_HOLE_DARK)
    finish(image, mask, name)


def mask_side(name, west):
    """East or west: the face-on mask folded onto the profile. East shows the hole; west only the outer
    turns of the groove, which wind round to the hole on the far side."""
    ys = steps(MASK_TOP, MASK_BOTTOM, 0.25)
    front = [to_side(MASK_CX, y, west) for y in ys]
    rim = [to_side(MASK_CX + (1 if west else -1) * mask_half(y), y, west) for y in reversed(ys)]
    mask = polygon(front + rim)
    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    fx = to_side(MASK_CX, 66, west)[0]
    light = ellipse(fx - 4, MASK_TOP + 13, 8, 12).filter(ImageFilter.GaussianBlur(4 * U))
    shade(image, mask, MASK_DARK, MASK_ORANGE, MASK_LIT, light, reach=2.0)
    for run in visible_runs(mask_spiral(), west):
        paint(image, inter(stroke([to_side(x, y, west) for x, y in run], 0.5), shrink(mask, 0.3)), MASK_GROOVE)
    if not west:
        hx, hy = MASK_EYE
        ring = lambda r: polygon([to_side(x, y, False) for x, y in mask_hole_ring(r)])
        paint(image, ring(MASK_HOLE_R + 0.5), MASK_GROOVE)
        paint(image, ring(MASK_HOLE_R), MASK_HOLE_DARK)
        ix, iy = to_side(hx + 0.2, hy + 0.1, False)
        paint(image, ellipse(ix, iy, 0.9, 1.15), SHARINGAN)
        paint(image, ellipse(ix, iy, 0.33, 0.42), MASK_HOLE_DARK)
    finish(image, mask, name)


def mask_back(name):
    """North: nothing. Graphic_Multi would put the south picture on the back of the head if this were
    missing."""
    OUT.mkdir(parents=True, exist_ok=True)
    Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0)).save(OUT / name)
    print("wrote", OUT / name)


# ---- Pain's piercings ----

STUD, STUD_LIT = (40, 40, 48), (150, 154, 168)
EAR_STUD, EAR_STUD_LIT, EAR_STUD_DARK = (178, 182, 194), (236, 238, 244), (86, 90, 102)
# Placed to suit both the vanilla heads and Facial Animation's (a popular face mod, in the user's
# game), whose heads are narrower and draw ears. Face-on: the nose studs sit between the eyes (x 52-59
# and 68-75, y 67-72 on both), two columns of three from eye level down; the ear studs on the rim of
# Facial Animation's ears (its head is 44-83 at y 68), 2 units inside the vanilla outline (42-85). A
# little larger than life so they read at the game's zoom.
NOSE_COLUMNS, NOSE_ROWS = (62.05, 64.95), (70.2, 72.8, 75.4)
LIP_STUDS, LIP_TOP, LIP_BOTTOM = (61.5, 65.5), 79.4, 81.3
EAR_X, EAR_ROWS = (44.0, 83.0), (66.4, 68.6, 70.8, 73.0)
# Profile: along the rim of Facial Animation's ear (a C round (58.8, 69) opening to the face), top to
# bottom round the back; the nose studs on its face front (x 82-83 at y 68-76), which is 3-4 units
# inside the vanilla face front (86-87), in front of the vanilla eye (74-81).
EAR_SIDE_CENTRE, EAR_SIDE_R, EAR_SIDE_ANGLES = (58.8, 69.0), 4.3, (290, 250, 210, 170, 130)
NOSE_SIDE = [(83.3, 70.2), (82.9, 72.8), (82.8, 75.4)]


def paint_stud(image, x, y, rx, ry, base, lit, dark=None):
    """A stud: a small dome, darker at its rim when `dark` is given, a glint at its upper left."""
    if dark is not None:
        paint(image, ellipse(x, y, rx + 0.25, ry + 0.25), dark)
    paint(image, ellipse(x, y, rx, ry), base)
    paint(image, ellipse(x - rx * 0.35, y - ry * 0.35, rx * 0.38, ry * 0.38), lit)


def piercings_front(name):
    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    for x in NOSE_COLUMNS:
        for y in NOSE_ROWS:
            paint_stud(image, x, y, 0.95, 0.62, STUD, STUD_LIT)
    for x in LIP_STUDS:
        paint(image, polygon([(x - 0.5, LIP_TOP), (x + 0.5, LIP_TOP), (x, LIP_BOTTOM)]), STUD)
        paint(image, ellipse(x, LIP_TOP + 0.05, 0.55, 0.3), STUD_LIT)
    for x in EAR_X:
        for y in EAR_ROWS:
            paint_stud(image, x, y, 0.72, 0.72, EAR_STUD, EAR_STUD_LIT, EAR_STUD_DARK)
    save_piece(image, name)


def piercings_side(name):
    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    cx, cy = EAR_SIDE_CENTRE
    for a in EAR_SIDE_ANGLES:
        x = cx + EAR_SIDE_R * math.cos(math.radians(a))
        y = cy + EAR_SIDE_R * math.sin(math.radians(a))
        paint_stud(image, x, y, 0.72, 0.72, EAR_STUD, EAR_STUD_LIT, EAR_STUD_DARK)
    for x, y in NOSE_SIDE:
        paint_stud(image, x, y, 0.62, 0.58, STUD, STUD_LIT)
    save_piece(image, name)


def piercings_back(name):
    """North: only the ear studs on the head's outline; the hair covers them when it hangs that low."""
    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    for x in EAR_X:
        for y in EAR_ROWS[:3]:
            paint_stud(image, x, y + 0.5, 0.72, 0.72, EAR_STUD, EAR_STUD_LIT, EAR_STUD_DARK)
    save_piece(image, name)


def save_piece(image, name):
    """Reduce and save a piece that brings its own edges (no ink outline round it)."""
    OUT.mkdir(parents=True, exist_ok=True)
    image.resize((SIZE, SIZE), Image.LANCZOS).save(OUT / name)
    print("wrote", OUT / name)


# ---- Minato's Hokage haori ----

MN_WHITE, MN_WHITE_LIT, MN_WHITE_DARK = (226, 224, 222), (248, 247, 244), (150, 146, 168)
MN_INNER = (118, 112, 148)  # the haori's inside and the collar's inner face, in shade
MN_FLAME, MN_FLAME_DARK = (204, 64, 50), (146, 38, 32)
MN_VEST, MN_VEST_LIT, MN_VEST_DARK = (120, 146, 100), (152, 176, 128), (74, 94, 60)
MN_BLUE, MN_BLUE_LIT, MN_BLUE_DARK = (42, 72, 146), (68, 102, 178), (22, 38, 86)
MN_GUARD, MN_GUARD_LIT, MN_GUARD_DARK = (88, 88, 108), (132, 132, 150), (52, 52, 68)
MN_TAPE, MN_TAPE_LINE = (234, 232, 224), (160, 158, 156)
MN_SANDAL = (36, 38, 56)
MN_HOLSTER = (32, 34, 44)
MN_CORD = (178, 58, 42)
MN_KANJI = (190, 36, 32)
MN_FLARE = 1.6  # the haori hangs nearly straight: a little wider at the hem than at the waist
# Flame heights along a hem, as fractions of the band's height, repeated.
FLAME_HEIGHTS = (1.0, 0.62, 0.86, 0.5, 0.94, 0.7, 0.8, 0.56)

# 四代目火影 ("Fourth Hokage"), written down the back: each character as strokes on a 10 x 10 grid, y down.
KANJI = [
    # 四
    [[(1, 1.4), (1, 9)], [(1, 1.4), (9, 1.4), (9, 9)], [(1, 8.6), (9, 8.6)],
     [(4, 1.4), (4, 4.4), (2.8, 6.8)], [(6, 1.4), (6, 5.8), (7.6, 5.8)]],
    # 代
    [[(3, 0.3), (0.6, 4.2)], [(2, 3), (2, 9.8)], [(3.8, 3.4), (9.4, 2.6)],
     [(6, 0.3), (6.6, 4.6), (8, 7.8), (9.6, 9.4), (9.8, 7.6)], [(7.8, 0.6), (8.8, 1.6)]],
    # 目
    [[(2.4, 0.4), (2.4, 9.6)], [(2.4, 0.4), (7.6, 0.4), (7.6, 9.6)], [(2.4, 3.5), (7.6, 3.5)],
     [(2.4, 6.5), (7.6, 6.5)], [(2.4, 9.4), (7.6, 9.4)]],
    # 火
    [[(1.6, 3), (2.6, 5.6)], [(8.6, 2.6), (7.2, 5.2)], [(5, 0.3), (5, 4.8), (3.4, 8), (0.6, 9.8)],
     [(5.2, 5.2), (7.2, 8.2), (9.6, 9.8)]],
    # 影: 日 over 京 on the left, 彡 on the right
    [[(0.8, 0.4), (0.8, 3.4)], [(0.8, 0.4), (5.2, 0.4), (5.2, 3.4)], [(0.8, 1.9), (5.2, 1.9)],
     [(0.8, 3.4), (5.2, 3.4)], [(0.2, 4.7), (5.8, 4.7)], [(1.2, 5.8), (1.2, 7.4)],
     [(1.2, 5.8), (4.8, 5.8), (4.8, 7.4)], [(1.2, 7.4), (4.8, 7.4)], [(3, 7.4), (3, 9.8)],
     [(1.6, 8.4), (0.6, 9.6)], [(4.4, 8.4), (5.4, 9.4)],
     [(9.4, 0.8), (6.8, 3)], [(9.4, 3.8), (6.6, 6.4)], [(9.8, 6.4), (6.2, 9.8)]],
]


class Haori:
    """Heights the three facings share, from the official art: the haori ends at the knee, so the
    trousers, the shin wraps and the sandals show below it."""

    def __init__(self, body):
        top, h = body.top, body.height
        self.elbow = top + 0.34 * h  # the short sleeve's edge
        self.wrist = top + 0.55 * h  # the forearm guard's end, just above the flames
        self.hem = body.bottom - 0.15 * h
        self.flames = 0.26 * h  # the flame band's height at its tallest tongue
        self.wraps = body.bottom - 0.09 * h  # shin wraps from here to the sandal
        self.sandal = body.bottom - 0.035 * h


def band(y0, y1):
    return polygon([(0, y0), (128, y0), (128, y1), (0, y1)])


def body_outline(body, pad=PAD * 0.6):
    """The vanilla body's own outline, rounded bottom and all, a little outside it."""
    ys = steps(body.top, body.bottom)
    return polygon([(interp(body.rows, y)[0] - pad, y) for y in ys] +
                   [(interp(body.rows, y)[1] + pad, y) for y in reversed(ys)])


def leg_gap(body):
    """A narrow notch up from the bottom, so the two legs read apart below the haori."""
    c, b = body.centre, body.bottom
    return polygon([(c(b) - 0.9, b + 2), (c(b) + 0.9, b + 2), (c(b), b - 0.05 * body.height)])


def paint_legs(image, body, lay, within, light=None, tape_side=0):
    """Trousers, shin wraps and sandals, cut to `within`. tape_side -1 or 1 wraps the white tape and the
    kunai holster round that leg's thigh (-1 the viewer's left)."""
    c, w = body.centre, body.width
    shade(image, within, MN_BLUE_DARK, MN_BLUE, MN_BLUE_LIT, light, reach=1.6)
    crotch = body.waist + 0.3 * (body.bottom - body.waist)
    paint(image, inter(stroke([(c(crotch), crotch), (c(body.bottom), body.bottom)], 0.5), within), MN_BLUE_DARK)
    if tape_side:
        s = tape_side
        y = body.waist + 0.28 * (body.bottom - body.waist)
        leg = inter(within, half_of(c, body.waist, body.bottom, s))
        holster = polygon([(c(y) + s * 0.17 * w, y - 1.2), (c(y) + s * 0.29 * w, y - 1.2),
                           (c(y) + s * 0.29 * w, y + 5.2), (c(y) + s * 0.17 * w, y + 5.6)])
        paint(image, inter(grow(holster, 0.35), leg), INK)
        paint(image, inter(holster, leg), MN_HOLSTER)
        for k in (0, 1):
            ty = y + k * 1.9
            paint(image, inter(minus(band(ty - 0.55, ty + 0.55), holster), leg), MN_TAPE)
    wraps = inter(within, band(lay.wraps, lay.sandal))
    paint(image, wraps, MN_TAPE)
    for k in range(3):
        y = lay.wraps + (k + 0.6) * (lay.sandal - lay.wraps) / 3
        paint(image, inter(stroke([(0, y + 0.3), (128, y - 0.3)], 0.3), wraps), MN_TAPE_LINE)
    paint(image, inter(within, band(lay.sandal, 128)), MN_SANDAL)


def flames(x0, x1, hem_at, height, phase=0):
    """The flame band along a hem from x0 to x1 (hem_at(x) its height there): a red strip at the hem,
    pointed tongues rising from it and leaning each way in turn, and between the tongues white tongues
    that reach down into the red and end in a curl, as on the Hokage haori. Returns the red and the
    white masks."""
    lo, hi = min(x0, x1), max(x0, x1)
    n = max(2, round((hi - lo) / 4.2))
    sp = (hi - lo) / n
    tongues = []
    for i in range(n):
        xi = lo + (i + 0.5) * sp
        tall = height * FLAME_HEIGHTS[(i + phase) % len(FLAME_HEIGHTS)]
        turn = 1 if (i + phase) % 2 else -1
        base, tip = hem_at(xi) - 0.25 * height, hem_at(xi) - tall
        bw = 0.56 * sp
        left, right = [], []
        for t in steps(0, 1, 0.04):
            y = base + (tip - base) * t
            # The tongue sways one way and flicks back at the tip; its sides ripple a little.
            x = xi + turn * 0.3 * sp * math.sin(0.9 * math.pi * t)
            half = bw * (1 - t) ** 0.9 * (1 + 0.12 * math.sin(2.5 * math.pi * t))
            left.append((x - half, y))
            right.append((x + half, y))
        tongues.append(polygon(left + list(reversed(right))))
    xs = steps(lo - 1, hi + 1, 0.4)
    strip = polygon([(x, hem_at(x) - 0.3 * height) for x in xs] + [(hi + 1, 128), (lo - 1, 128)])
    red = union(strip, *tongues)

    whites = []
    for i in range(1, n):
        xv = lo + i * sp
        turn = 1 if (i + phase) % 2 else -1
        top_y = hem_at(xv) - 0.75 * height
        end_y = hem_at(xv) - (0.24 + 0.06 * ((i + phase) % 3)) * height
        left, right = [], []
        for t in steps(0, 1, 0.04):
            y = top_y + (end_y - top_y) * t
            x = xv + turn * 0.16 * sp * math.sin(math.pi * t)
            half = 0.1 * sp * (1 - t) + 0.06 * sp
            left.append((x - half, y))
            right.append((x + half, y))
        whites.append(polygon(left + list(reversed(right))))
        # The curl: on round from the tongue's end, under and up the other side, tightening.
        r = 0.17 * sp
        cx = xv + turn * r
        curl = []
        for t in steps(0, 1, 0.03):
            a = math.radians(180 - 300 * t) if turn > 0 else math.radians(300 * t)
            rr = r * (1 - 0.4 * t)
            curl.append((cx + rr * math.cos(a), end_y + rr * math.sin(a)))
        whites.append(stroke(curl, 0.11 * sp + 0.1))
    return red, union(*whites)


def paint_flames(image, x0, x1, hem_at, height, within, phase=0):
    red, white = flames(x0, x1, hem_at, height, phase)
    shade(image, inter(red, within), MN_FLAME_DARK, MN_FLAME, MN_FLAME, reach=1.0)
    paint(image, inter(inter(white, red), within), MN_WHITE)


def kanji(cx, top, size):
    """四代目火影 down a column centred on cx, from `top`, each character `size` units square."""
    gap = 0.12 * size
    masks = []
    for i, strokes in enumerate(KANJI):
        y0 = top + i * (size + gap)
        for points in strokes:
            masks.append(stroke([(cx - size / 2 + x * size / 10, y0 + y * size / 10) for x, y in points],
                                max(0.5, 0.1 * size)))
    return union(*masks)


def kanji_size(body, lay, top):
    """As large as the back allows: about a fifth of its width, and all five above the flames."""
    room = (lay.hem - lay.flames - 1.5) - top
    return max(4.0, min(6.5, 0.21 * body.width, room / (5 + 4 * 0.12)))


def haori_arms(image, body, lay, side, within):
    """South and north: a seam from each shoulder down the short sleeve to its edge at the elbow,
    the haori's shaded inside just under that edge, and the grey forearm guard from there to the
    wrist, a pale band at its end."""
    top, w = body.top, body.width
    fw = min(4.2, 0.16 * w + 0.6)
    for s in (-1, 1):
        def outer(y, s=s):
            return side(y)[1 if s > 0 else 0] - s * 0.5

        def inner(y, s=s):
            return outer(y) - s * fw
        paint(image, inter(stroke([(inner(y), y) for y in steps(top + 7, lay.elbow)], 0.45), within), MN_WHITE_DARK)
        ys = steps(lay.elbow, lay.wrist)
        guard = polygon([(inner(y) + s * 0.3 * smooth(lay.wrist - 2, lay.wrist, y), y) for y in ys] +
                        [(outer(lay.wrist) - s * 0.5 * fw, lay.wrist + 1.0)] +
                        [(outer(y), y) for y in reversed(ys)])
        guard = inter(guard, within)
        paint(image, inter(grow(guard, 0.35), within), MN_GUARD_DARK)
        light = ramp(outer(lay.elbow) if s < 0 else inner(lay.elbow), inner(lay.elbow) if s < 0 else outer(lay.elbow),
                     120 if s < 0 else 0, 0 if s < 0 else 60)
        shade(image, guard, MN_GUARD_DARK, MN_GUARD, MN_GUARD_LIT, light, reach=1.2)
        paint(image, inter(band(lay.wrist - 2.0, lay.wrist - 1.1), guard), MN_GUARD_LIT)
        # The sleeve's edge, and its shaded inside showing just under it.
        cuff = [(inner(lay.elbow) - s * 0.4, lay.elbow - 0.3), (outer(lay.elbow) + s * 0.4, lay.elbow + 0.5)]
        paint(image, inter(stroke([(x, y + 0.7) for x, y in cuff], 0.9), within), MN_INNER)
        paint(image, inter(stroke(cuff, 0.5), within), MN_WHITE_DARK)


def minato_front(body, name):
    """South: the haori open down the front over the green vest and blue trousers, short sleeves over
    grey forearm guards, the flame band at the hem; below it the legs in trousers, shin wraps and
    sandals, the tape and kunai holster on his right thigh (the viewer's left)."""
    w, c, top, h = body.width, body.centre, body.top, body.height
    lay = Haori(body)
    side = coat_sides(body, lay.hem, MN_FLARE, MN_FLARE)
    haori = cloak_outline(side, top - 0.5, lay.hem)

    def opening_half(y):
        return 0.2 * w + 0.07 * w * smooth(top, lay.hem, y)
    oys = steps(top - 12, lay.hem + 3)
    opening = polygon([(c(y) - opening_half(y), y) for y in oys] + [(c(y) + opening_half(y), y) for y in reversed(oys)])
    panels = minus(haori, opening)

    # The collar stands up round the neck, open at the front; the head covers all but its outer edges
    # beside the jaw. Sized to the neck, not the body: the same on every body type.
    collars, faces = [], []
    for s in (-1, 1):
        one = polygon([(c(top) + s * 9, top + 12), (c(top) + s * 17, top + 13), (c(top) + s * 21.5, top + 5),
                       (c(top) + s * 20.5, top - 3), (c(top) + s * 11, top - 1)])
        collars.append(one)
        faces.append(inter(one, half_of(lambda y: c(top) + s * 16, top - 12, top + 14, -s)))
    collar = minus(union(*collars), opening)

    under = body_outline(body)
    gap = leg_gap(body)
    silhouette = minus(union(panels, collar, under), gap)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    # The vest down to the waist: a zip down the middle, two scroll pouches each side on the chest,
    # a darker band at its hem.
    vest_end = body.waist - 0.8
    vest = inter(under, band(0, vest_end))
    shade(image, vest, MN_VEST_DARK, MN_VEST, MN_VEST_LIT, ellipse(c(top) - 0.2 * w, top + 12, 0.35 * w, 14)
          .filter(ImageFilter.GaussianBlur(4 * U)), reach=1.5)
    paint(image, inter(band(vest_end - 1.6, vest_end), vest), MN_VEST_DARK)
    paint(image, inter(stroke([(c(y), y) for y in steps(top, vest_end)], 0.45), vest), MN_VEST_DARK)
    pw = min(2.6, 0.085 * w)
    py0, py1 = top + 0.2 * h, top + 0.35 * h
    for s in (-1, 1):
        for k in (0, 1):
            x0 = c(py0) + s * (0.8 + k * (pw + 0.4))
            pouch = polygon([(x0, py0), (x0 + s * pw, py0), (x0 + s * pw, py1), (x0, py1)])
            paint(image, inter(grow(pouch, 0.3), vest), MN_VEST_DARK)
            paint(image, inter(pouch, vest), MN_VEST_LIT)
            paint(image, inter(stroke([(x0, py0 + 1.0), (x0 + s * pw, py0 + 1.0)], 0.35), vest), MN_VEST_DARK)
    legs = minus(inter(under, band(vest_end, 128)), gap)
    paint_legs(image, body, lay, legs, tape_side=-1)

    # The haori, lit from the viewer's left as the vanilla bodies are.
    light = inter(ramp(c(top) - w * 0.7, c(top) + w * 0.4, 150, 0), ellipse(c(top) - w * 0.3, top + 20, w * 0.45, 26)
                  .filter(ImageFilter.GaussianBlur(6 * U)))
    shade(image, union(panels, collar), MN_WHITE_DARK, MN_WHITE, MN_WHITE_LIT, light, reach=2.4)
    paint(image, inter(union(*faces), collar), MN_INNER)
    haori_arms(image, body, lay, side, panels)

    # The flames, one run on each front panel from its outer edge to the opening.
    for s in (-1, 1):
        outer = side(lay.hem)[0 if s < 0 else 1]
        paint_flames(image, outer, c(lay.hem) + s * opening_half(lay.hem), lambda x: lay.hem, lay.flames,
                     inter(panels, half_of(c, top, 128, s)), phase=0 if s < 0 else 3)

    # The front edges, and the red cord tying the two fronts across the chest.
    for s in (-1, 1):
        paint(image, inter(stroke([(c(y) + s * opening_half(y), y) for y in steps(top - 8, lay.hem + 1)], 0.55),
                           grow(under, 0.4)), INK)
    cy = top + 0.24 * h
    paint(image, inter(stroke([(c(cy) - opening_half(cy) - 0.4, cy), (c(cy) + opening_half(cy) + 0.4, cy + 0.2)], 0.5),
                       grow(vest, 0.5)), MN_CORD)
    for s in (-1, 1):
        paint(image, ellipse(c(cy) + s * (opening_half(cy) + 0.4), cy + 0.1, 0.75, 0.75), MN_CORD)

    finish(image, silhouette, name)


def minato_back(body, name):
    """North: the stand collar over the nape, 四代目火影 in red down the back, flames right round the
    hem; below it the shins in their wraps. Drawn over the head facing north, as vanilla shells are."""
    w, c, top, h = body.width, body.centre, body.top, body.height
    lay = Haori(body)
    side = coat_sides(body, lay.hem, MN_FLARE, MN_FLARE)
    left, right = side(lay.hem)

    def hem_at(x):
        return lay.hem + 0.9 * math.sin(math.pi * min(1.0, max(0.0, (x - left) / (right - left))))
    ys = steps(top + 3, lay.hem)
    bottom = [(x, hem_at(x)) for x in steps(left, right, 0.4)]
    back = polygon([(side(y)[0], y) for y in ys] + bottom + [(side(y)[1], y) for y in reversed(ys)])

    # The stand collar, sized to the neck, not the body: the same on every body type. Its rim is highest
    # at the middle of the back and it flares out a little at the top.
    cw = 16.0
    cys = steps(0, 1, 0.05)
    rim = [(c(top) - 0.9 * cw * math.cos(math.pi * t), top - 1.4 - 2.8 * math.sin(math.pi * t)) for t in cys]
    collar = polygon([(c(top) - cw - 0.6, top + 8), (c(top) - 0.9 * cw - 0.4, top + 1)] + rim +
                     [(c(top) + 0.9 * cw + 0.4, top + 1), (c(top) + cw + 0.6, top + 8),
                      (side(top + 10)[1], top + 10), (side(top + 10)[0], top + 10)])
    shoulders = polygon([(c(top) - cw - 0.6, top + 6), (side(top + 13)[0], top + 13),
                         (side(top + 13)[1], top + 13), (c(top) + cw + 0.6, top + 6)])
    haori = union(back, collar, shoulders)
    gap = leg_gap(body)
    legs = minus(inter(body_outline(body), band(lay.hem - 1, 128)), gap)
    silhouette = minus(union(haori, legs), gap)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    paint_legs(image, body, lay, legs)
    light = ellipse(c(top) - w * 0.15, top + 22, w * 0.42, 22).filter(ImageFilter.GaussianBlur(7 * U))
    shade(image, haori, MN_WHITE_DARK, MN_WHITE, MN_WHITE_LIT, light, reach=3.0)
    # The collar's inner face along its rim, and the seam where it meets the shoulders.
    inner_rim = polygon([(x, y) for x, y in rim] + [(x, y + 2.4) for x, y in reversed(rim)])
    paint(image, inter(inner_rim, haori), MN_INNER)
    seam = [(c(top) - cw * math.cos(math.pi * t), top + 6.5 - 2.0 * math.sin(math.pi * t)) for t in cys]
    paint(image, inter(stroke(seam, 0.5), haori), MN_WHITE_DARK)
    haori_arms(image, body, lay, side, haori)

    ktop = top + 8
    paint(image, inter(kanji(c(ktop), ktop, kanji_size(body, lay, ktop)), shrink(haori, 0.8)), MN_KANJI)
    paint_flames(image, left, right, hem_at, lay.flames, haori, phase=1)

    finish(image, silhouette, name)


def minato_side(body, name):
    """East (facing right): the collar behind the neck, the open front showing a strip of vest and
    trousers, the short sleeve and the grey forearm guard down the side, the flames along the hem;
    below it the shins in their wraps."""
    w, top, h = body.width, body.top, body.height
    lay = Haori(body)
    hem = lay.hem

    def front(y):
        return body.edges(y)[1] + PAD + 0.8 * smooth(body.waist, hem, y)

    def back(y):
        return body.edges(y)[0] - PAD - 2.8 * smooth(body.waist, hem, y) ** 1.2

    x_front, x_back = front(hem), back(hem)

    def hem_at(x):
        t = min(1.0, max(0.0, (x - x_front) / (x_back - x_front)))
        return hem + 1.0 * t + 0.5 * math.sin(math.pi * t)
    hem_curve = [(x, hem_at(x)) for x in steps(x_front, x_back, 0.4)]
    back_hem = hem_at(x_back)
    outer = polygon([(front(y), y) for y in steps(top + 1, hem)] + hem_curve +
                    [(back(y), y) for y in reversed(steps(top + 1, back_hem))])

    # The collar standing up behind the neck, turned out at the top; on wide bodies it stays by the neck
    # rather than at the back's edge.
    cb = max(body.edges(top + 6)[0] - PAD - 2.5, body.centre(top) - 15.5)
    collar_points = [(cb + 1.2, top + 9), (cb - 1.8, top - 3.5), (cb + 0.2, top - 6.5), (cb + 3.6, top - 6.1),
                     (cb + 7.5, top - 2.5), (cb + 10.5, top + 4), (cb + 7, top + 10)]
    collar = polygon(collar_points)
    full = union(outer, collar)

    # The open front: a strip of vest and trousers between the haori's front edge and the chest.
    def open_x(y):
        return front(y) - (3.2 if y < body.waist else 2.4 + 0.8 * smooth(body.waist, hem, y))
    oys = steps(top + 7, hem + 1)
    strip = polygon([(open_x(y), y) for y in oys] + [(front(y) + 1, y) for y in reversed(oys)])
    torso = body_outline(body)
    inner = inter(inter(strip, torso), full)
    haori = minus(full, inner)
    legs = inter(torso, band(hem - 1, 128))
    silhouette = union(full, legs)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    vest_end = body.waist - 0.8
    paint(image, inter(inner, band(0, vest_end)), MN_VEST)
    paint(image, inter(inner, band(vest_end - 1.6, vest_end)), MN_VEST_DARK)
    paint_legs(image, body, lay, union(inter(inner, band(vest_end, 128)), legs))

    light = ramp(body.edges(top + 20)[0], body.edges(top + 20)[1] + 3, 0, 140)
    shade(image, haori, MN_WHITE_DARK, MN_WHITE, MN_WHITE_LIT, light, reach=2.4)
    # Collar: its inner face at the top.
    face = inter(polygon([(cb - 1.8, top - 3.5), (cb + 0.2, top - 6.5), (cb + 3.6, top - 6.1), (cb + 7.5, top - 2.5),
                          (cb + 10.5, top + 4), (cb + 7.2, top + 3), (cb + 4.5, top - 1.5), (cb + 0.6, top - 1.8)]),
                 haori)
    paint(image, face, MN_INNER)

    # The arm down the side: the short sleeve to the elbow, the guard below it to the wrist.
    def arm(y):
        return body.centre(y) - 1.2 - 0.9 * smooth(top, lay.wrist, y)

    def spread(y):
        return 3.2 + 0.8 * smooth(top + 12, lay.elbow, y)
    sl = [(arm(y) - spread(y) + 1.2 * smooth(top + 10, top + 16, y), y) for y in steps(top + 10, lay.elbow)]
    sr = [(arm(y) + spread(y), y) for y in steps(top + 10, lay.elbow)]
    for line in (sl, sr):
        paint(image, inter(stroke(line, 0.45), haori), MN_WHITE_DARK)
    gys = steps(lay.elbow, lay.wrist)
    guard = polygon([(arm(y) - 2.3, y) for y in gys] + [(arm(lay.wrist), lay.wrist + 1.2)] +
                    [(arm(y) + 2.3, y) for y in reversed(gys)])
    guard = inter(guard, haori)
    paint(image, inter(grow(guard, 0.35), haori), MN_GUARD_DARK)
    shade(image, guard, MN_GUARD_DARK, MN_GUARD, MN_GUARD_LIT, ramp(arm(lay.wrist) - 2, arm(lay.wrist) + 2.5, 0, 90),
          reach=1.2)
    paint(image, inter(band(lay.wrist - 2.0, lay.wrist - 1.1), guard), MN_GUARD_LIT)
    cuff = [sl[-1], sr[-1]]
    paint(image, inter(stroke([(x, y + 0.7) for x, y in cuff], 0.9), haori), MN_INNER)
    paint(image, inter(stroke(cuff, 0.5), haori), MN_WHITE_DARK)

    paint_flames(image, x_back, x_front, hem_at, lay.flames, haori, phase=2)
    paint(image, inter(stroke([(open_x(y), y) for y in steps(top + 7, hem + 1)], 0.55), grow(inner, 0.4)), INK)

    finish(image, silhouette, name)


# ---- Minato's forehead protector ----

MN_BAND, MN_BAND_LIT, MN_BAND_DARK = (40, 56, 112), (72, 94, 152), (20, 28, 62)
MN_PLATE, MN_PLATE_LIT, MN_PLATE_DARK = (184, 192, 204), (238, 242, 248), (110, 118, 132)
MN_ENGRAVE = (58, 64, 78)
# In head space. Face-on: the band round the forehead just above the brows (the eyes are at y 67-72 on
# every head type), its ends 1 unit inside the vanilla outline (41-87 at y 56-64) so it also sits on
# Facial Animation's narrower heads; the plate over the middle of the forehead. The band's ends sit
# BAND_SAG lower than its middle, as a band round a head does seen from a little above.
BAND_TOP, BAND_BOTTOM, BAND_HALF, BAND_SAG = 57.4, 61.6, 21.5, 1.0
PLATE_TOP, PLATE_BOTTOM, PLATE_HALF = 54.6, 62.4, 10.5


def band_sag(x):
    return BAND_SAG * ((x - HEAD_CX) / BAND_HALF) ** 2


def leaf_symbol(cx, cy, size, width):
    """The Leaf village symbol, `size` wide: an arc from a short tick at the upper right over the top
    to a point at the lower left, and from that point a spiral round the bottom winding in to the
    middle."""
    k = size
    ox, oy = cx + 0.1 * k, cy
    r = 0.38 * k

    def at(a, rr):
        return ox + rr * math.cos(math.radians(a)), oy + rr * math.sin(math.radians(a))
    tip = (ox - r - 0.2 * k, oy + 0.34 * k)
    tick = [(at(-40, r)[0] + 0.12 * k, at(-40, r)[1] - 0.12 * k)]
    arc = [at(a, r) for a in steps(-40, -180, 4)]
    outer = tick + arc + [(ox - r - 0.06 * k, oy + 0.18 * k), tip]
    spiral = [tip] + [at(a, 0.34 * k - (0.3 * k) * (135 - a) / 555) for a in steps(135, -420, 5)]
    return union(stroke(outer, width), stroke(spiral, width))


def headband_front(name):
    """South: the blue band across the forehead, the steel plate on it with a rivet in each corner and
    the Leaf symbol engraved in the middle."""
    cx = HEAD_CX
    xs = steps(cx - BAND_HALF, cx + BAND_HALF, 0.4)
    cloth = polygon([(x, BAND_TOP + band_sag(x)) for x in xs] + [(x, BAND_BOTTOM + band_sag(x)) for x in reversed(xs)])
    pxs = steps(cx - PLATE_HALF, cx + PLATE_HALF, 0.4)
    plate = grow(shrink(polygon([(x, PLATE_TOP + band_sag(x)) for x in pxs] +
                                [(x, PLATE_BOTTOM + band_sag(x)) for x in reversed(pxs)]), 0.8), 0.8)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    shade(image, cloth, MN_BAND_DARK, MN_BAND, MN_BAND_LIT, ramp(cx - BAND_HALF, cx + BAND_HALF * 0.3, 140, 0), reach=1.2)
    light = inter(ramp(cx - PLATE_HALF, cx + PLATE_HALF * 0.5, 200, 0), band(0, PLATE_TOP + 3.5))
    shade(image, plate, MN_PLATE_DARK, MN_PLATE, MN_PLATE_LIT, light, reach=1.2)
    for sx in (-1, 1):
        for y in (PLATE_TOP + 1.5, PLATE_BOTTOM - 1.5):
            x = cx + sx * (PLATE_HALF - 1.5)
            paint(image, ellipse(x, y + band_sag(x), 0.5, 0.5), MN_PLATE_DARK)
    paint(image, leaf_symbol(cx, (PLATE_TOP + PLATE_BOTTOM) / 2 - 0.2, 5.4, 0.5), MN_ENGRAVE)
    finish(image, union(cloth, plate), name, outline_width=0.8)


def headband_tails(knot, ends, width):
    """The two loose ends hanging from the knot, cut square at the bottom."""
    tails = []
    kx, ky = knot
    for ex, ey in ends:
        dx, dy = ex - kx, ey - ky
        n = math.hypot(dx, dy)
        nx, ny = -dy / n * width / 2, dx / n * width / 2
        tails.append(polygon([(kx - nx * 0.8, ky - ny * 0.8), (kx + nx * 0.8, ky + ny * 0.8),
                              (ex + nx, ey + ny), (ex - nx, ey - ny)]))
    return tails


def headband_side(name):
    """East: the band round the head from the knot at the back to the plate on the forehead, the plate
    seen edge-on standing just off the face, the two loose ends hanging from the knot. West is this
    mirrored by the game: the knot is at the back of the head, so both sides look alike."""
    back_x, front_x, drop = 40.2, 88.0, 1.2  # the band is a little lower at the back

    def top_at(x):
        return BAND_TOP + drop * (front_x - x) / (front_x - back_x)
    xs = steps(back_x, front_x, 0.4)
    cloth = polygon([(x, top_at(x)) for x in xs] + [(x, top_at(x) + BAND_BOTTOM - BAND_TOP) for x in reversed(xs)])
    # The plate's side: from where it bends round the forehead to its front edge, bowed forward.
    pys = steps(PLATE_TOP, PLATE_BOTTOM, 0.4)
    mid = (PLATE_TOP + PLATE_BOTTOM) / 2
    plate = polygon([(82.0, PLATE_TOP + 0.3)] +
                    [(88.6 + 0.4 * (1 - ((y - mid) / (mid - PLATE_TOP)) ** 2), y) for y in pys] +
                    [(82.0, PLATE_BOTTOM - 0.1)])
    knot_at = (back_x - 0.6, top_at(back_x) + 2.4)
    knot = ellipse(*knot_at, 1.7, 1.9)
    tails = headband_tails(knot_at, [(33.8, 72.5), (36.8, 74.5)], 2.3)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    for tail in tails:
        shade(image, tail, MN_BAND_DARK, MN_BAND, MN_BAND, reach=0.8)
    paint(image, inter(stroke([(knot_at[0] - 0.4, knot_at[1] + 1), (35.4, 72.0)], 0.3), tails[0]), MN_BAND_DARK)
    shade(image, cloth, MN_BAND_DARK, MN_BAND, MN_BAND_LIT, ramp(back_x, front_x, 0, 120), reach=1.2)
    shade(image, knot, MN_BAND_DARK, MN_BAND, MN_BAND_LIT, reach=0.8)
    shade(image, plate, MN_PLATE_DARK, MN_PLATE, MN_PLATE_LIT, ramp(82, 89, 0, 190), reach=1.0)
    for y in (PLATE_TOP + 1.5, PLATE_BOTTOM - 1.5):
        paint(image, ellipse(83.8, y, 0.5, 0.5), MN_PLATE_DARK)
    finish(image, union(cloth, plate, knot, *tails), name, outline_width=0.8)


def headband_back(name):
    """North: the band across the back of the head, the knot in the middle, the two loose ends hanging
    from it (the haori's collar covers their tips)."""
    cx = HEAD_CX
    xs = steps(cx - BAND_HALF, cx + BAND_HALF, 0.4)
    cloth = polygon([(x, BAND_TOP + band_sag(x)) for x in xs] + [(x, BAND_BOTTOM + band_sag(x)) for x in reversed(xs)])
    knot_at = (cx + 0.4, (BAND_TOP + BAND_BOTTOM) / 2 + 0.3)
    knot = ellipse(*knot_at, 2.0, 1.9)
    tails = headband_tails(knot_at, [(cx - 3.2, 74.5), (cx + 2.8, 76.0)], 2.4)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    for tail in tails:
        shade(image, tail, MN_BAND_DARK, MN_BAND, MN_BAND, reach=0.8)
    shade(image, cloth, MN_BAND_DARK, MN_BAND, MN_BAND_LIT, ramp(cx - BAND_HALF, cx + BAND_HALF * 0.3, 120, 0), reach=1.2)
    shade(image, knot, MN_BAND_DARK, MN_BAND, MN_BAND_LIT, reach=0.8)
    paint(image, inter(stroke([(knot_at[0] - 1.2, knot_at[1] - 0.8), (knot_at[0] + 1.0, knot_at[1] + 0.9)], 0.3), knot),
          MN_BAND_DARK)
    finish(image, union(cloth, knot, *tails), name, outline_width=0.8)


# ---- Sasuke's Fourth War outfit ----

# The game's light is warm: the art's lavender-grey, blue and purple are pushed toward blue so they
# do not read beige and pink in it (as Vergil's coat blue was).
SK_SHIRT, SK_SHIRT_LIT, SK_SHIRT_DARK = (156, 160, 198), (192, 196, 228), (100, 104, 140)
SK_ZIP = (228, 228, 240)
SK_CLOTH, SK_CLOTH_LIT, SK_CLOTH_DARK = (78, 92, 158), (112, 128, 194), (42, 48, 92)
SK_ROPE, SK_ROPE_LIT, SK_ROPE_DARK = (124, 108, 176), (162, 148, 212), (70, 58, 116)
SK_NAVY, SK_NAVY_LIT, SK_NAVY_DARK = (49, 52, 78), (74, 80, 114), (27, 29, 46)  # trousers
SK_WRAP, SK_WRAP_LIT, SK_WRAP_DARK = (92, 92, 102), (124, 124, 134), (56, 56, 66)
SK_SANDAL = (34, 34, 42)
SK_CREST_RED, SK_CREST_RED_DARK, SK_CREST_WHITE = (214, 30, 34), (150, 18, 24), (244, 242, 238)
SK_FLARE = 1.0  # the waist cloth hangs nearly straight

# The Uchiha crest, from the wiki's Uchiha_Symbol.svg (150 x 200, y down): a red dome whose lower edge
# arches up, a gap, a white bowl under a parallel arch, and the handle, all outlined in black. Cubic
# Béziers (start, control, control, end); a straight edge has its controls on its ends.
CREST_RED = [((75.0, 2.86), (35.18, 2.86), (2.86, 34.40), (2.86, 73.26)),
             ((2.86, 73.26), (2.86, 79.38), (3.66, 85.31), (5.16, 90.98)),
             ((5.16, 90.98), (21.63, 75.02), (46.80, 64.81), (75.0, 64.81)),
             ((75.0, 64.81), (103.20, 64.81), (128.37, 75.02), (144.83, 90.98)),
             ((144.83, 90.98), (146.34, 85.31), (147.14, 79.38), (147.14, 73.26)),
             ((147.14, 73.26), (147.14, 34.40), (114.82, 2.86), (75.0, 2.86))]
CREST_WHITE = [((75.0, 77.38), (47.79, 77.38), (24.03, 88.96), (11.25, 106.16)),
               ((11.25, 106.16), (21.86, 125.69), (41.59, 139.76), (64.91, 142.95)),
               ((64.91, 142.95), (64.91, 142.95), (64.91, 197.14), (64.91, 197.14)),
               ((64.91, 197.14), (64.91, 197.14), (85.09, 197.14), (85.09, 197.14)),
               ((85.09, 197.14), (85.09, 197.14), (85.09, 142.95), (85.09, 142.95)),
               ((85.09, 142.95), (108.41, 139.76), (128.14, 125.69), (138.75, 106.16)),
               ((138.75, 106.16), (125.97, 88.96), (102.21, 77.38), (75.0, 77.38))]
CREST_W, CREST_H, CREST_LINE = 150.0, 200.0, 5.72


def bezier(segments, at):
    points = []
    for p0, p1, p2, p3 in segments:
        for t in steps(0, 1, 0.05):
            u = 1 - t
            points.append(at(u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                             u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))
    return points


def paint_crest(image, cx, top, width):
    """The Uchiha crest `width` units wide with its top at `top`, outlined as in the symbol."""
    s = width / CREST_W

    def at(x, y):
        return cx + (x - CREST_W / 2) * s, top + y * s
    red, white = bezier(CREST_RED, at), bezier(CREST_WHITE, at)
    shade(image, polygon(red), SK_CREST_RED_DARK, SK_CREST_RED, SK_CREST_RED, reach=0.8)
    paint(image, polygon(white), SK_CREST_WHITE)
    line = max(0.3, CREST_LINE * s)
    paint(image, union(stroke(red, line), stroke(white, line)), INK)


def along(path, pitch):
    """Points every `pitch` units along a polyline, each with its unit tangent."""
    out, carry = [], pitch / 2
    for (x0, y0), (x1, y1) in zip(path, path[1:]):
        seg = math.hypot(x1 - x0, y1 - y0)
        if seg == 0:
            continue
        tx, ty = (x1 - x0) / seg, (y1 - y0) / seg
        d = carry
        while d < seg:
            out.append((x0 + tx * d, y0 + ty * d, tx, ty))
            d += pitch
        carry = d - seg
    return out


def lines(segments, width):
    """Many short strokes in one mask."""
    mask = blank()
    draw = ImageDraw.Draw(mask)
    for (x0, y0), (x1, y1) in segments:
        draw.line([(x0 * U, y0 * U), (x1 * U, y1 * U)], fill=255, width=max(1, round(width * U)))
    return mask


def paint_rope(image, path, width, within=None):
    """The thick purple rope along `path`: twisted strands, shaded round, lit along its top, with a
    slanting groove between the strands every 0.62 x its width. Returns its mask."""
    mask = stroke(path, width)
    if within is not None:
        mask = inter(mask, within)
    light = stroke([(x, y - 0.22 * width) for x, y in path], 0.3 * width).filter(ImageFilter.GaussianBlur(0.1 * width * U))
    shade(image, mask, SK_ROPE_DARK, SK_ROPE, SK_ROPE_LIT, light, reach=0.7 * width)
    half = 0.5 * width
    # Each groove crosses the rope, leaning along it by 0.45 of the half width.
    grooves = [((x + ty * half - tx * 0.45 * half, y - tx * half - ty * 0.45 * half),
                (x - ty * half + tx * 0.45 * half, y + tx * half + ty * 0.45 * half))
               for x, y, tx, ty in along(path, 0.62 * width)]
    paint(image, inter(lines(grooves, 0.16 * width + 0.12), shrink(mask, 0.12)), SK_ROPE_DARK)
    return mask


def loop_path(top, length, width, lean):
    """A loop of rope hanging from the knot at `top`: an oval `length` long, narrow where it leaves the
    knot, its bottom moved `lean` sideways."""
    x0, y0 = top
    points = []
    for t in steps(0, 1, 0.02):
        a = 2 * math.pi * t
        u = (1 - math.cos(a)) / 2  # 0 at the knot, 1 at the bottom
        points.append((x0 + lean * u + math.sin(a) * width / 2 * min(1.0, 0.3 + 1.4 * u), y0 + length * u))
    return points


def paint_end(image, top, length, lean, width):
    """A loose end hanging from the knot `length` down, swinging `lean` sideways, bound near its tip
    and frayed below into a tassel as long as the rope is thick. Returns its mask."""
    tassel = 1.2 * width
    path = [(top[0] + lean * math.sin(0.5 * math.pi * t), top[1] + (length - tassel) * t) for t in steps(0, 1, 0.05)]
    rope = paint_rope(image, path, width)
    x1, y1 = path[-1]
    y2 = y1 + tassel
    half0, half1 = 0.45 * width, 0.6 * width
    fringe = [(x1 + half1 - 2 * half1 * i / 6, y2 - (0.3 * width if i % 2 else 0)) for i in range(7)]
    tip = polygon([(x1 - half0, y1 - 0.3), (x1 + half0, y1 - 0.3)] + fringe)
    shade(image, tip, SK_ROPE_DARK, SK_ROPE, SK_ROPE_LIT, ramp(x1 - half1, x1 + half1, 160, 0), reach=0.35 * width)
    strands = [((x1 + (k - 0.5) * 1.5 * half0, y1 + 0.3 * width), (x1 + (k - 0.5) * 2 * half1, y2 - 0.35 * width))
               for k in (0.2, 0.4, 0.6, 0.8)]
    paint(image, inter(lines(strands, 0.1 * width + 0.1), tip), SK_ROPE_DARK)
    paint(image, inter(band(y1 - 0.25 * width, y1 + 0.1 * width), grow(tip, 0.2)), SK_ROPE_DARK)
    return union(rope, tip)


def bowed(a, b, bend):
    """A curve from a to b bowed `bend` units to the left of the line from a to b."""
    (ax, ay), (bx, by) = a, b
    n = math.hypot(bx - ax, by - ay)
    cx, cy = (ax + bx) / 2 + (by - ay) / n * bend, (ay + by) / 2 - (bx - ax) / n * bend
    return [((1 - t) ** 2 * ax + 2 * (1 - t) * t * cx + t * t * bx, (1 - t) ** 2 * ay + 2 * (1 - t) * t * cy + t * t * by)
            for t in steps(0, 1, 0.05)]


def paint_knot(image, kx, ky, rw, squeeze=1.0):
    """The knot, a wad of rope about 2.4 x its thickness across (squeeze narrows it, for a profile),
    two twisted turns crossing over it. Returns its mask."""
    rx, ry = 1.2 * rw * squeeze, 1.25 * rw
    base = ellipse(kx, ky, rx, ry)
    shade(image, base, SK_ROPE_DARK, SK_ROPE, SK_ROPE_LIT,
          ellipse(kx - 0.3 * rx, ky - 0.4 * ry, 0.6 * rx, 0.5 * ry).filter(ImageFilter.GaussianBlur(0.3 * rw * U)),
          reach=0.5 * rw)
    turns = [paint_rope(image, bowed((kx + ax * rx, ky + ay * ry), (kx + bx * rx, ky + by * ry), bend * ry), 0.8 * rw,
                        within=grow(base, 0.3))
             for (ax, ay), (bx, by), bend in (((-0.95, -0.5), (0.9, 0.4), 0.3), ((-0.75, 0.7), (0.8, -0.55), -0.25))]
    return union(base, *turns)


def paint_rope_turns(image, x_left, x_right, top, width, sag, tilt=0.0):
    """The two turns of rope round the waist from x_left to x_right, the lower under the upper, sagging
    `sag` in the middle (a band round the body seen from a little above) and dropping `tilt` toward
    x_right. Returns the mask."""
    masks = []
    mid, half = (x_left + x_right) / 2, (x_right - x_left) / 2
    for k in (1, 0):
        y0 = top + width * (0.5 + 0.9 * k)
        path = [(x, y0 + sag * (1 - ((x - mid) / half) ** 2) + tilt * (x - x_left) / (x_right - x_left))
                for x in steps(x_left, x_right, 0.4)]
        masks.append(paint_rope(image, path, width))
    return union(*masks)


def paint_fold(image, lay, fold, light=None):
    """The top of the waist cloth, folded over above the rope: the cloth with a lit rim along its top."""
    shade(image, fold, SK_CLOTH_DARK, SK_CLOTH, SK_CLOTH_LIT, light, reach=0.8)
    paint(image, inter(band(lay.fold, lay.fold + 0.7), fold), SK_CLOTH_LIT)


def sasuke_legs(image, lay, legs, light=None):
    """Below the waist cloth: the trousers bloused over the top of the shin wraps, the grey wraps and
    the dark sandals, cut to `legs`. Returns the mask they cover (the blouse stands out a little)."""
    blouse = inter(grow(inter(legs, band(lay.wraps - 2.4, lay.wraps + 0.6)), 0.7), band(lay.wraps - 3.0, lay.wraps + 1.3))
    wraps = inter(legs, band(lay.wraps, lay.sandal))
    shade(image, wraps, SK_WRAP_DARK, SK_WRAP, SK_WRAP_LIT, light, reach=1.2)
    for k in range(3):
        y = lay.wraps + 1.2 + k * (lay.sandal - lay.wraps - 1.2) / 3
        paint(image, inter(stroke([(0, y + 0.4), (128, y - 0.4)], 0.3), wraps), SK_WRAP_DARK)
    paint(image, inter(legs, band(lay.sandal, 128)), SK_SANDAL)
    trousers = union(inter(legs, band(0, lay.wraps)), blouse)
    shade(image, trousers, SK_NAVY_DARK, SK_NAVY, SK_NAVY_LIT, light, reach=1.4)
    paint(image, inter(stroke([(0, lay.wraps + 0.9), (128, lay.wraps + 0.9)], 0.45), blouse), SK_NAVY_DARK)
    return union(legs, blouse)


class Outfit:
    """What the three facings share, from the official art: the waist cloth folded over at the top,
    the rope round it twice (each turn 14 % of the waist's width), the cloth to the knee, then the
    bloused trousers, shin wraps and sandals (the heights of Minato's haori, which also ends at the
    knee). The arms are not drawn, as on the other costumes: the top covers the body, and a seam from
    each shoulder to the elbow marks the short sleeve."""

    def __init__(self, body):
        lay = Haori(body)
        self.elbow, self.hem, self.wraps, self.sandal = lay.elbow, lay.hem, lay.wraps, lay.sandal
        self.rope_w = min(4.6, 0.14 * body.width)
        self.fold = body.waist - 2.8  # the top of the waist cloth, folded over above the rope
        self.rope = body.waist - 0.6  # the top of the rope
        self.knot = self.rope + 0.95 * self.rope_w  # the knot's middle, between the two turns
        self.skirt = self.rope + 2 * self.rope_w  # below the rope: the cloth's folds start here
        self.fall = self.hem - self.knot  # from the knot to the knee: the ends and the loop hang down it


def sleeve_seams_to_elbow(image, body, lay, side, within, top_y):
    """South and north: a seam from each shoulder down to the elbow, where the short sleeve ends."""
    w = body.width
    for s in (-1, 1):
        x = lambda y, s=s: side(y)[1 if s > 0 else 0] - s * min(4.2, 0.2 * w)
        paint(image, inter(stroke([(x(y), y) for y in steps(top_y, lay.elbow)], 0.45), within), SK_SHIRT_DARK)


def sasuke_front(body, name):
    """South: the grey top zipped to its high collar; the blue cloth from the waist to the knee, folded
    over at the top and open over the trousers just right of the middle; the purple rope round it
    twice, knotted a little to his right of the middle, two frayed ends hanging from the knot down his
    right thigh (the viewer's left) and a big loop down his left; below, the bloused trousers, shin
    wraps and sandals."""
    w, c, top, h = body.width, body.centre, body.top, body.height
    lay = Outfit(body)
    rw = lay.rope_w
    side = coat_sides(body, lay.hem, SK_FLARE, SK_FLARE)

    shirt = cloak_outline(side, top - 0.5, lay.fold + 0.5)
    # The high collar stands up round the neck, open at the throat; the head covers all but its outer
    # edges beside the jaw. Sized to the neck, as Minato's: the same on every body type.
    collars, faces = [], []
    for s in (-1, 1):
        one = polygon([(c(top) + s * 9, top + 12), (c(top) + s * 17, top + 13), (c(top) + s * 21.5, top + 5),
                       (c(top) + s * 20.5, top - 3), (c(top) + s * 11, top - 1)])
        collars.append(one)
        faces.append(inter(one, half_of(lambda y: c(top) + s * 16, top - 12, top + 14, -s)))
    collar = union(*collars)

    fold = cloak_outline(side, lay.fold, lay.rope + rw)
    cloth = cloak_outline(side, lay.rope + rw, lay.hem)
    gap = leg_gap(body)
    legs = minus(inter(body_outline(body), band(lay.hem - 1, 128)), gap)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    legs = sasuke_legs(image, lay, legs)

    light = inter(ramp(c(top) - w * 0.7, c(top) + w * 0.4, 150, 0), ellipse(c(top) - w * 0.3, top + 20, w * 0.45, 26)
                  .filter(ImageFilter.GaussianBlur(6 * U)))
    shade(image, union(shirt, collar), SK_SHIRT_DARK, SK_SHIRT, SK_SHIRT_LIT, light, reach=2.4)
    paint(image, inter(union(*faces), collar), SK_SHIRT_DARK)
    # The zip from the collar to the waist.
    zip_line = [(c(y), y) for y in steps(top + 2, lay.fold)]
    paint(image, inter(stroke(zip_line, 0.8), shirt), SK_SHIRT_DARK)
    paint(image, inter(stroke(zip_line, 0.32), shirt), SK_ZIP)
    sleeve_seams_to_elbow(image, body, lay, side, shirt, top + 7)

    # The cloth: two panels meeting under the knot, the trousers showing between them lower down; the
    # panel on the viewer's right shows its paler lining along its edge.
    shade(image, cloth, SK_CLOTH_DARK, SK_CLOTH, SK_CLOTH_LIT,
          inter(ramp(c(lay.hem) - w * 0.7, c(lay.hem) + w * 0.5, 120, 0), cloth), reach=2.0)
    apex = (c(lay.knot) + 0.03 * w, lay.knot + 1.2 * rw)
    left, right = (c(lay.hem) - 0.05 * w, lay.hem + 2), (c(lay.hem) + 0.2 * w, lay.hem + 2)
    opening = inter(polygon([apex, right, left]), cloth)
    shade(image, opening, SK_NAVY_DARK, SK_NAVY, SK_NAVY, reach=1.0)
    paint(image, inter(stroke([apex, left], 0.45), cloth), SK_CLOTH_DARK)
    paint(image, inter(stroke([(x + 0.6, y) for x, y in (apex, right)], 0.55), cloth), SK_CLOTH_LIT)
    paint(image, inter(stroke([apex, right], 0.5), cloth), INK)
    for x0, x1 in ((c(lay.skirt) + 0.36 * w, c(lay.hem) + 0.4 * w), (c(lay.skirt) - 0.24 * w, c(lay.hem) - 0.28 * w),
                   (c(lay.skirt) - 0.42 * w, c(lay.hem) - 0.46 * w)):
        paint(image, inter(stroke([(x0, lay.skirt + 1), (x1, lay.hem - 0.5)], 0.4), minus(shrink(cloth, 0.6), opening)),
              SK_CLOTH_DARK)
    paint_fold(image, lay, fold)

    rope = paint_rope_turns(image, side(lay.rope)[0] + 0.4, side(lay.rope)[1] - 0.4, lay.rope, rw, 0.7)
    kx = c(lay.knot) - 0.06 * w
    tie = union(paint_rope(image, loop_path((kx + 0.55 * rw, lay.knot + 0.35 * rw), 0.62 * lay.fall, 2.4 * rw, 0.12 * w),
                           0.85 * rw),
                paint_end(image, (kx - 0.5 * rw, lay.knot + 0.7 * rw), lay.hem + 0.8 - lay.knot - 0.7 * rw, -0.16 * w, 0.95 * rw),
                paint_end(image, (kx, lay.knot + 0.8 * rw), lay.hem - 0.6 - lay.knot - 0.8 * rw, -0.02 * w, 0.9 * rw),
                paint_knot(image, kx, lay.knot, rw))

    finish(image, union(shirt, collar, fold, cloth, legs, rope, tie), name)


def sasuke_back(body, name):
    """North: the high collar over the nape, the Uchiha crest just under it, the plain back of the top,
    the cloth folded over at the waist, the rope round it twice (the knot is in front), the closed
    cloth to the knee and the legs below. Drawn over the head facing north, as Minato's."""
    w, c, top, h = body.width, body.centre, body.top, body.height
    lay = Outfit(body)
    rw = lay.rope_w
    side = coat_sides(body, lay.hem, SK_FLARE, SK_FLARE)

    ys = steps(top + 3, lay.fold + 0.5)
    back = polygon([(side(y)[0], y) for y in ys] + [(side(y)[1], y) for y in reversed(ys)])
    # The high collar and the shoulders, as on Minato's haori: sized to the neck, its rim highest at
    # the middle of the back.
    cw = 16.0
    cys = steps(0, 1, 0.05)
    rim = [(c(top) - 0.9 * cw * math.cos(math.pi * t), top - 1.4 - 2.8 * math.sin(math.pi * t)) for t in cys]
    collar = polygon([(c(top) - cw - 0.6, top + 8), (c(top) - 0.9 * cw - 0.4, top + 1)] + rim +
                     [(c(top) + 0.9 * cw + 0.4, top + 1), (c(top) + cw + 0.6, top + 8),
                      (side(top + 10)[1], top + 10), (side(top + 10)[0], top + 10)])
    shoulders = polygon([(c(top) - cw - 0.6, top + 6), (side(top + 13)[0], top + 13),
                         (side(top + 13)[1], top + 13), (c(top) + cw + 0.6, top + 6)])
    shirt = union(back, collar, shoulders)

    fold = cloak_outline(side, lay.fold, lay.rope + rw)
    cloth = cloak_outline(side, lay.rope + rw, lay.hem)
    gap = leg_gap(body)
    legs = minus(inter(body_outline(body), band(lay.hem - 1, 128)), gap)

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    legs = sasuke_legs(image, lay, legs)

    light = ellipse(c(top) - w * 0.15, top + 22, w * 0.42, 22).filter(ImageFilter.GaussianBlur(7 * U))
    shade(image, shirt, SK_SHIRT_DARK, SK_SHIRT, SK_SHIRT_LIT, light, reach=3.0)
    inner_rim = polygon([(x, y) for x, y in rim] + [(x, y + 2.4) for x, y in reversed(rim)])
    paint(image, inter(inner_rim, shirt), SK_SHIRT_DARK)
    seam = [(c(top) - cw * math.cos(math.pi * t), top + 6.5 - 2.0 * math.sin(math.pi * t)) for t in cys]
    paint(image, inter(stroke(seam, 0.5), shirt), SK_SHIRT_DARK)
    sleeve_seams_to_elbow(image, body, lay, side, shirt, top + 9)
    # The crest just under the collar, as large as the back allows above the folded cloth.
    crest_w = min(0.3 * w + 1.5, (lay.fold - top - 11) * CREST_W / CREST_H)
    paint_crest(image, c(top + 12), top + 9.5, crest_w)

    shade(image, cloth, SK_CLOTH_DARK, SK_CLOTH, SK_CLOTH_LIT,
          inter(ramp(c(lay.hem) - w * 0.6, c(lay.hem) + w * 0.4, 110, 0), cloth), reach=2.0)
    for k in (-0.3, 0.02, 0.32):
        x0 = c(lay.skirt) + k * w
        paint(image, inter(stroke([(x0, lay.skirt + 1), (x0 + k * 3, lay.hem - 0.5)], 0.4), shrink(cloth, 0.6)),
              SK_CLOTH_DARK)
    paint_fold(image, lay, fold)
    rope = paint_rope_turns(image, side(lay.rope)[0] + 0.4, side(lay.rope)[1] - 0.4, lay.rope, rw, 0.7)

    finish(image, union(shirt, fold, cloth, legs, rope), name)


def sasuke_side(body, name, west):
    """East (facing right): his right side, the collar standing up behind the neck, the short sleeve
    down the side to the elbow, the cloth folded over at the waist and the rope round it, the knot
    standing out in front and its two frayed ends hanging down the front of his right thigh. West
    shows his left side: the knot in front and the big loop hanging down the front of his left thigh,
    seen edge-on. West is its own picture but stored facing right, as the game mirrors every west
    picture."""
    top = body.top
    lay = Outfit(body)
    rw = lay.rope_w
    hem = lay.hem

    def shirt_front(y):
        return body.edges(y)[1] + PAD * 0.8

    def shirt_back(y):
        return body.edges(y)[0] - PAD * 0.8

    def front(y):
        return body.edges(y)[1] + PAD + 0.6 * smooth(body.waist, hem, y)

    def back(y):
        return body.edges(y)[0] - PAD - 2.2 * smooth(body.waist, hem, y) ** 1.2

    ys = steps(top + 1, lay.fold + 0.5)
    shirt = polygon([(shirt_front(y), y) for y in ys] + [(shirt_back(y), y) for y in reversed(ys)])
    # The collar standing up behind the neck, turned out a little at the top, as Minato's.
    cb = max(body.edges(top + 6)[0] - PAD - 2.5, body.centre(top) - 15.5)
    collar = polygon([(cb + 1.2, top + 9), (cb - 1.8, top - 3.5), (cb + 0.2, top - 6.5), (cb + 3.6, top - 6.1),
                      (cb + 7.5, top - 2.5), (cb + 10.5, top + 4), (cb + 7, top + 10)])

    x_front, x_back = front(hem), back(hem)

    def hem_at(x):
        k = min(1.0, max(0.0, (x - x_front) / (x_back - x_front)))
        return hem + 1.0 * k + 0.5 * math.sin(math.pi * k)
    ys = steps(lay.fold, hem)
    skirt = polygon([(front(y), y) for y in ys] + [(x, hem_at(x)) for x in steps(x_front, x_back, 0.4)] +
                    [(back(y), y) for y in reversed(steps(lay.fold, hem_at(x_back)))])
    fold = inter(skirt, band(0, lay.rope + rw))
    cloth = minus(skirt, fold)
    legs = inter(body_outline(body), band(hem - 1, 128))

    image = Image.new("RGBA", (128 * U, 128 * U), (0, 0, 0, 0))
    legs = sasuke_legs(image, lay, legs)
    light = ramp(body.edges(top + 20)[0], body.edges(top + 20)[1] + 3, 0, 140)
    shade(image, union(shirt, collar), SK_SHIRT_DARK, SK_SHIRT, SK_SHIRT_LIT, light, reach=2.4)
    # The short sleeve down the side, as the Akatsuki cloak's: its two edges and its hem at the elbow.
    arm = lambda y: body.centre(y) - 1.2 - 0.9 * smooth(top, lay.elbow, y)
    spread = lambda y: 3.2 + 0.8 * smooth(top + 12, lay.elbow, y)
    sl = [(arm(y) - spread(y) + 1.2 * smooth(top + 10, top + 16, y), y) for y in steps(top + 10, lay.elbow)]
    sr = [(arm(y) + spread(y), y) for y in steps(top + 10, lay.elbow)]
    for line in (sl, sr, [sl[-1], sr[-1]]):
        paint(image, inter(stroke(line, 0.45), shirt), SK_SHIRT_DARK)

    shade(image, cloth, SK_CLOTH_DARK, SK_CLOTH, SK_CLOTH_LIT, inter(light, cloth), reach=2.0)
    for x0, x1 in ((back(lay.skirt) + 3, back(hem) + 2), (body.centre(lay.skirt) + 1, body.centre(hem) + 2)):
        paint(image, inter(stroke([(x0, lay.skirt + 1), (x1, hem)], 0.4), shrink(cloth, 0.6)), SK_CLOTH_DARK)
    paint_fold(image, lay, fold, inter(light, fold))
    rope = paint_rope_turns(image, back(lay.rope) - 0.3, front(lay.rope) + 0.3, lay.rope, rw, 0.0, tilt=0.5)

    # The knot stands out in front of the belly; below it, his right side shows the two ends, his left
    # the loop edge-on.
    kx = front(lay.knot) - 0.4 * rw
    if west:
        tie = paint_rope(image, loop_path((kx + 0.1 * rw, lay.knot + 0.4 * rw), 0.62 * lay.fall, 1.1 * rw, 0.25 * rw),
                         0.85 * rw)
    else:
        tie = union(paint_end(image, (kx - 0.7 * rw, lay.knot + 0.7 * rw), hem - 0.6 - lay.knot - 0.7 * rw, -0.25 * rw,
                              0.9 * rw),
                    paint_end(image, (kx - 0.1 * rw, lay.knot + 0.8 * rw), hem + 0.8 - lay.knot - 0.8 * rw, 0.15 * rw,
                              0.95 * rw))
    tie = union(tie, paint_knot(image, kx, lay.knot, rw, squeeze=0.8))

    finish(image, union(shirt, collar, fold, cloth, legs, rope, tie), name)


def sasuke_outfit():
    for body in ("Thin", "Male", "Female", "Fat", "Hulk"):
        sasuke_front(Body(BODIES[(body, "south")]), f"SasukeOutfit_{body}_south.png")
        sasuke_side(Body(BODIES[(body, "east")]), f"SasukeOutfit_{body}_east.png", west=False)
        sasuke_side(Body(BODIES[(body, "east")]), f"SasukeOutfit_{body}_west.png", west=True)
        sasuke_back(Body(BODIES[(body, "north")]), f"SasukeOutfit_{body}_north.png")


def main():
    for body in ("Thin", "Male", "Female", "Fat", "Hulk"):
        vergil_front(Body(BODIES[(body, "south")]), f"VergilCoat_{body}_south.png")
        vergil_side(Body(BODIES[(body, "east")]), f"VergilCoat_{body}_east.png")
        vergil_back(Body(BODIES[(body, "north")]), f"VergilCoat_{body}_north.png")
    # Hero form makes adult human Hosts Thin, so only the Thin kneel is made; other bodies keep the squash.
    vergil_kneel_front(Body(BODIES[("Thin", "south")]), "VergilCoatKneel_Thin.png")
    for body in ("Thin", "Male", "Female", "Fat", "Hulk"):
        akatsuki_front(Body(BODIES[(body, "south")]), f"AkatsukiCloak_{body}_south.png")
        akatsuki_side(Body(BODIES[(body, "east")]), f"AkatsukiCloak_{body}_east.png")
        akatsuki_back(Body(BODIES[(body, "north")]), f"AkatsukiCloak_{body}_north.png")
    collar_straight("AkatsukiCollar_south.png", 77.5, 82.0, front=True)
    collar_side("AkatsukiCollar_east.png")
    # From behind, the rim's near side is the back of the collar, over the nape.
    collar_straight("AkatsukiCollar_north.png", 76.5, 79.5, front=False)
    mask_front("ObitoMask_south.png")
    mask_side("ObitoMask_east.png", west=False)
    mask_side("ObitoMask_west.png", west=True)
    mask_back("ObitoMask_north.png")
    piercings_front("PainPiercings_south.png")
    piercings_side("PainPiercings_east.png")
    piercings_back("PainPiercings_north.png")
    for body in ("Thin", "Male", "Female", "Fat", "Hulk"):
        minato_front(Body(BODIES[(body, "south")]), f"MinatoHaori_{body}_south.png")
        minato_side(Body(BODIES[(body, "east")]), f"MinatoHaori_{body}_east.png")
        minato_back(Body(BODIES[(body, "north")]), f"MinatoHaori_{body}_north.png")
    headband_front("MinatoHeadband_south.png")
    headband_side("MinatoHeadband_east.png")
    headband_back("MinatoHeadband_north.png")
    sasuke_outfit()


if __name__ == "__main__":
    main()
