#!/usr/bin/env python3
"""
Draws the art this mod could not borrow: Origin: Blade's arsenal, and the dispersal
plexus's crows.

Most of these genes ship no art at all, and for most of them that is the right call - they
dress themselves in a Core texture that already means the right thing. Two could not.

A planted blade has to read as standing *in* the ground, and every weapon texture in the
game is a sword photographed from directly above while lying flat. No rotation of a top-down
sprite produces a side-on one.

A crow is the stranger case, because the game does contain exactly the right picture: Odyssey
ships Things/Pawn/Animal/Crow/Crow_Flying_1..8 in three facings, drawn from above with the
wings mid-beat. It is not used here for two reasons. It is DLC, and nothing else in this mod
resolves outside Core and Biotech. And it is the wrong crow - a naturalistic brown-black bird
with rim light on the primaries, where what this gene wants is a flat cutout, because the
thing being drawn is not a bird. It is a person who is currently not there.

Everything is drawn at 4x and downsampled; that is where the soft edges come from, as PIL
does not anti-alias a polygon fill.

Run:  python3 make_textures.py
"""
from PIL import Image, ImageDraw, ImageFilter

SS = 4                    # supersample factor
SIZE = 128                # final texture size
S = SIZE * SS

STEEL_LIGHT = (208, 216, 226)
STEEL       = (150, 159, 172)
STEEL_DARK  = (92, 99, 112)
EDGE        = (40, 43, 51)

GUARD       = (128, 112, 78)
GUARD_DARK  = (82, 70, 47)
GRIP        = (74, 52, 38)
GRIP_LIGHT  = (100, 72, 51)

DIRT        = (66, 51, 38)
DIRT_LIGHT  = (104, 84, 63)


def px(v):
    """A measurement in final-texture pixels, in supersampled space."""
    return int(round(v * SS))


def draw_sword(d, cx, tip_y, blade_len, half_w, grip_len, guard_half):
    """
    One sword, point up, hilt below the blade. Every sprite here is this shape - the planted
    one is this drawing turned over, which is the only difference between a sword falling and
    a sword that has arrived.
    """
    base_y = tip_y + blade_len
    shoulder = tip_y + blade_len * 0.14

    body = [
        (cx, tip_y),
        (cx + half_w, shoulder),
        (cx + half_w, base_y),
        (cx - half_w, base_y),
        (cx - half_w, shoulder),
    ]
    d.polygon(body, fill=STEEL)

    # One light source, top-left, which is what every Core texture assumes.
    d.polygon([(cx - half_w, shoulder), (cx, tip_y), (cx - half_w * 0.3, shoulder),
               (cx - half_w * 0.3, base_y), (cx - half_w, base_y)], fill=STEEL_LIGHT)
    d.polygon([(cx + half_w * 0.5, shoulder), (cx + half_w, shoulder),
               (cx + half_w, base_y), (cx + half_w * 0.5, base_y)], fill=STEEL_DARK)

    # The fuller. Without this groove the blade is a grey stick at any distance.
    d.polygon([(cx - half_w * 0.2, shoulder + half_w * 1.4), (cx + half_w * 0.2, shoulder + half_w * 1.4),
               (cx + half_w * 0.2, base_y - half_w), (cx - half_w * 0.2, base_y - half_w)],
              fill=STEEL_DARK)

    d.line(body + [body[0]], fill=EDGE, width=px(1.8))

    guard_h = px(6)
    d.rounded_rectangle([cx - guard_half, base_y - guard_h // 2,
                         cx + guard_half, base_y + guard_h // 2],
                        radius=px(2.5), fill=GUARD, outline=EDGE, width=px(1.8))

    grip_half = int(half_w * 0.62)
    grip_top = base_y + guard_h // 2
    grip_bottom = grip_top + grip_len
    d.rounded_rectangle([cx - grip_half, grip_top, cx + grip_half, grip_bottom],
                        radius=px(2), fill=GRIP, outline=EDGE, width=px(1.8))
    for i in range(4):
        y = grip_top + int(grip_len * (0.22 + i * 0.19))
        d.line([(cx - grip_half + px(1), y), (cx + grip_half - px(1), y)],
               fill=GRIP_LIGHT, width=px(1.4))

    r = int(half_w * 0.9)
    d.ellipse([cx - r, grip_bottom - px(1), cx + r, grip_bottom + r * 2 - px(1)],
              fill=GUARD, outline=EDGE, width=px(1.8))
    d.ellipse([cx - r + px(2), grip_bottom + px(1), cx - px(1), grip_bottom + r],
              fill=(158, 140, 100))


def finish(img, path):
    img.resize((SIZE, SIZE), Image.LANCZOS).save(path)
    print("wrote", path)


def make_flying():
    """A blade in the air, point up: falling, loosed, or on its way to a hand."""
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    draw_sword(d, cx=S // 2, tip_y=px(6), blade_len=px(82), half_w=px(9),
               grip_len=px(20), guard_half=px(23))
    finish(img, "Textures/RimArt/Panoply/Blade.png")


# Where the ground crosses the planted sprite, as a fraction from the top. The drawing code
# needs the same number to pivot a leaning blade about the hole rather than about the middle
# of the picture, so it is stated here and repeated in PanoplyDefaults.PlantedGroundFraction.
GROUND_FRACTION = 0.78


def make_planted():
    """
    A blade standing in the ground, seen from the side: pommel up, point buried.

    Built by drawing the same sword and turning it over, so a planted blade and a falling one
    are recognisably the same object - which matters, because the player watches one become
    the other.

    The point is driven well below the ground line and the near lip of the hole is drawn over
    it afterwards. That overlap is the entire illusion: a blade that stops at the surface
    reads as one lying on it.
    """
    ground_y = int(S * GROUND_FRACTION)
    tip_y = px(5)

    sword = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sword)
    draw_sword(sd, cx=S // 2, tip_y=tip_y, blade_len=px(60), half_w=px(8),
               grip_len=px(15), guard_half=px(19))

    # Turned over: the point is now at the bottom, where the ground is.
    sword = sword.transpose(Image.FLIP_TOP_BOTTOM)

    # After the flip the point sits at S - tip_y. Move it to a little under the ground line.
    dy = (ground_y + px(7)) - (S - tip_y)
    sword = sword.transform(sword.size, Image.AFFINE, (1, 0, 0, 0, 1, -dy), resample=Image.BICUBIC)

    top = sword.getbbox()[1]
    if top < 0:
        raise SystemExit("planted sword is taller than the sprite: shorten the blade")
    print("  planted sword occupies y", top // SS, "to", sword.getbbox()[3] // SS)

    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # The hole itself, and the far lip behind the steel.
    d.ellipse([S // 2 - px(20), ground_y - px(6), S // 2 + px(20), ground_y + px(8)], fill=DIRT)
    d.ellipse([S // 2 - px(11), ground_y - px(4), S // 2 + px(11), ground_y + px(3)],
              fill=(30, 24, 19))

    img = Image.alpha_composite(img, sword)

    # The near lip, drawn over the blade, high enough to swallow the point.
    near = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    nd = ImageDraw.Draw(near)
    nd.ellipse([S // 2 - px(18), ground_y - px(3), S // 2 + px(18), ground_y + px(9)],
               fill=DIRT_LIGHT)
    nd.ellipse([S // 2 - px(13), ground_y - px(5), S // 2 + px(13), ground_y + px(2)],
               fill=DIRT)
    # A couple of clods thrown up by the arrival, so the hole is not a clean ellipse.
    for cx_off, cy_off, r in ((-px(19), px(2), px(4)), (px(16), px(4), px(3)), (px(6), -px(4), px(2))):
        nd.ellipse([S // 2 + cx_off - r, ground_y + cy_off - r,
                    S // 2 + cx_off + r, ground_y + cy_off + r], fill=DIRT_LIGHT)
    near = near.filter(ImageFilter.GaussianBlur(px(0.9)))
    img = Image.alpha_composite(img, near)

    finish(img, "Textures/RimArt/Panoply/BladePlanted.png")


# ---------------------------------------------------------------- icons


def sword_layer(blade_len, half_w, grip_len, guard_half, tip_y=None):
    """One sword on its own transparent layer, point up, so it can be turned and placed."""
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    draw_sword(d, cx=S // 2, tip_y=tip_y if tip_y is not None else px(8),
               blade_len=blade_len, half_w=half_w, grip_len=grip_len, guard_half=guard_half)
    return layer


def place(base, layer, angle=0.0, scale=1.0, dx=0, dy=0):
    """Turn a sword, resize it and drop it on the icon. Angles are clockwise, like the game's."""
    img = layer
    if scale != 1.0:
        w = max(1, int(S * scale))
        img = img.resize((w, w), Image.LANCZOS)
        pad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        pad.alpha_composite(img, ((S - w) // 2, (S - w) // 2))
        img = pad
    if angle:
        img = img.rotate(-angle, resample=Image.BICUBIC, center=(S // 2, S // 2))
    if dx or dy:
        img = img.transform(img.size, Image.AFFINE, (1, 0, -dx, 0, 1, -dy), resample=Image.BICUBIC)
    return Image.alpha_composite(base, img)


def make_icons():
    """
    Four icons, built out of the same sword the gene throws.

    A gizmo is read at 24 pixels while somebody is being shot at, so each of these says one
    thing with its silhouette: rain is blades coming down, loose is blades going one way, and
    grasp is a blade leaving the ground. Borrowing Core's spine icon said none of them.
    """
    sword = sword_layer(blade_len=px(70), half_w=px(8), grip_len=px(18), guard_half=px(20))

    # RAIN - three blades on their way down, one further along than the others.
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img = place(img, sword, angle=170, scale=0.62, dx=-px(30), dy=-px(14))
    img = place(img, sword, angle=186, scale=0.62, dx=px(28), dy=-px(22))
    img = place(img, sword, angle=178, scale=0.74, dx=-px(1), dy=px(16))
    finish(img, "Textures/RimArt/Panoply/IconRain.png")

    # LOOSE - a volley, all of it going the same way, tips leading.
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img = place(img, sword, angle=64, scale=0.60, dx=-px(14), dy=-px(30))
    img = place(img, sword, angle=78, scale=0.60, dx=-px(24), dy=px(4))
    img = place(img, sword, angle=71, scale=0.72, dx=-px(4), dy=-px(6))
    finish(img, "Textures/RimArt/Panoply/IconLoose.png")

    # GRASP - one blade coming up out of the ground, and the earth it leaves.
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([S // 2 - px(30), px(100), S // 2 + px(26), px(118)], fill=DIRT)
    d.ellipse([S // 2 - px(24), px(102), S // 2 + px(20), px(113)], fill=DIRT_LIGHT)
    # Coming out of the hole rather than hovering over it: the streaks are the distance it
    # has already travelled, which is what makes this read as motion at gizmo size.
    for x, y0, y1 in ((-px(14), px(74), px(100)), (px(12), px(68), px(96)), (px(1), px(60), px(92))):
        d.line([(S // 2 + x, y0), (S // 2 + x + px(4), y1)], fill=(150, 140, 120, 150), width=px(2))
    img = place(img, sword, angle=20, scale=0.88, dx=px(4), dy=-px(6))
    finish(img, "Textures/RimArt/Panoply/IconGrasp.png")

    # GENE - a panoply: what the carrier is, rather than any one thing they do.
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img = place(img, sword, angle=-34, scale=0.80, dx=-px(20), dy=px(4))
    img = place(img, sword, angle=34, scale=0.80, dx=px(20), dy=px(4))
    img = place(img, sword, angle=0, scale=0.92, dx=0, dy=0)
    finish(img, "Textures/RimArt/Panoply/IconGene.png")



# ---------------------------------------------------------------------------------------
# The dispersal plexus: crows, a feather, and the gene icon.
#
# Every crow here points north, because that is what RimWorld assumes an unrotated sprite
# does, and the flock is drawn by rotating the quad to the heading of the flight. Drawing
# them pointing east and rotating by AngleFlat - which is the mistake Origin: Blade made
# twice - would put every bird ninety degrees off its own path.
# ---------------------------------------------------------------------------------------

CROW      = (16, 16, 22)
CROW_LIT  = (54, 56, 70)

# Crows are drawn smaller than the blades. A bird on this map is under a cell across, and
# a 128px sprite scaled down to that is four times the texture anybody sees.
CROW_SIZE = 64


def finish_at(img, path, size):
    img.resize((size, size), Image.LANCZOS).save(path)
    print("wrote", path)


def crow_wing(extension, sweepback, mirror):
    """Jointed wing with separated primaries, projected onto the map plane."""
    side = -1 if mirror else 1
    # Fold at the wrist, keeping a substantial upper wing next to the shoulder.
    # The feather notches remain visible after reduction to the in-game size.
    span = 0.10 + 0.34 * extension
    wrist_y = 0.35 + sweepback
    outline = [
        (0.032, 0.365), (span * 0.40, 0.315 + sweepback * 0.30),
        (span * 0.72, wrist_y),
        (span * 0.96, wrist_y - 0.025),
        (span, wrist_y + 0.020),
        (span * 0.81, wrist_y + 0.070),
        (span * 0.98, wrist_y + 0.088),
        (span * 0.94, wrist_y + 0.125),
        (span * 0.76, wrist_y + 0.130),
        (span * 0.87, wrist_y + 0.166),
        (span * 0.81, wrist_y + 0.194),
        (span * 0.67, wrist_y + 0.181),
        (span * 0.71, wrist_y + 0.216),
        (span * 0.64, wrist_y + 0.237),
        (span * 0.49, wrist_y + 0.211),
        (span * 0.28, 0.540 + sweepback * 0.35),
        (0.030, 0.545),
    ]
    return [(S * (0.5 + side * x), S * y) for x, y in outline]


def draw_crow(d, extension, sweepback):
    """North-facing crow: broad feathered wings, stout bill and rounded fan tail."""
    cx = S / 2
    for mirror in (False, True):
        wing = crow_wing(extension, sweepback, mirror)
        d.polygon(wing, fill=CROW)
        # A muted shoulder plane describes the joint without outlining every feather.
        shoulder = [wing[0], wing[1], wing[2], wing[-3], wing[-1]]
        d.polygon(shoulder, fill=(29, 30, 39))
        d.line(wing[:3], fill=CROW_LIT, width=px(0.65))

    d.polygon([
        (cx - S * 0.030, S * 0.565), (cx + S * 0.030, S * 0.565),
        (cx + S * 0.070, S * 0.770), (cx + S * 0.041, S * 0.795),
        (cx, S * 0.805), (cx - S * 0.041, S * 0.795),
        (cx - S * 0.070, S * 0.770),
    ], fill=CROW)
    for offset in (-0.025, 0.025):
        d.line([(cx + S * offset * 0.4, S * 0.64),
                (cx + S * offset, S * 0.775)], fill=(35, 36, 46), width=px(0.5))

    d.ellipse([cx - S * 0.056, S * 0.335, cx + S * 0.056, S * 0.635], fill=CROW)
    d.ellipse([cx - S * 0.043, S * 0.265, cx + S * 0.043, S * 0.385], fill=CROW)
    d.ellipse([cx - S * 0.025, S * 0.345, cx + S * 0.023, S * 0.525],
              fill=(31, 32, 42))
    d.polygon([
        (cx - S * 0.025, S * 0.290), (cx + S * 0.025, S * 0.290),
        (cx + S * 0.012, S * 0.235), (cx, S * 0.215),
        (cx - S * 0.017, S * 0.247),
    ], fill=CROW)


# Each pose has its own wrist sweep. A sinusoidal scale repeats three poses in an
# eight-frame loop and looks like breathing. Here the open power stroke sweeps
# forward, then the wrist folds back during recovery before opening again.
CROW_POSES = (
    (0.48, -0.12),   # raised, opening
    (0.82, -0.07),   # start of power stroke
    (1.00,  0.00),   # full span
    (0.84,  0.09),   # pressing down and back
    (0.52,  0.18),   # bottom of stroke
    (0.22,  0.22),   # folded wrist on recovery
    (0.10,  0.12),   # narrow, rising
    (0.24, -0.02),   # lifting forward to reopen
)


def crow_beat(t):
    """Interpolate cyclic poses for sprites and the smaller gene-icon birds."""
    phase = (t % 1.0) * len(CROW_POSES)
    index = int(phase)
    blend = phase - index
    start = CROW_POSES[index]
    end = CROW_POSES[(index + 1) % len(CROW_POSES)]
    return tuple(a + (b - a) * blend for a, b in zip(start, end))


CROW_FRAME_COUNT = 8


def make_crows():
    for i in range(CROW_FRAME_COUNT):
        extension, sweepback = crow_beat(i / float(CROW_FRAME_COUNT))
        img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        draw_crow(ImageDraw.Draw(img), extension, sweepback)
        finish_at(img, "Textures/RimArt/Dispersal/Crow" + str(i) + ".png", CROW_SIZE)


def make_feeding_crows():
    """Folded wings and four head reaches: standing, dipping, pecking, lifting."""
    for frame, reach in enumerate((0.0, 0.045, 0.09, 0.035)):
        img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        # North-facing bird. Wing feathers lie along the back, never spread in flight.
        d.polygon([(S*.45,S*.59),(S*.55,S*.59),(S*.565,S*.83),
                   (S*.50,S*.85),(S*.435,S*.83)], fill=CROW)
        for side in (-1, 1):
            x=S*(.5+side*.055)
            d.line([(x,S*.60),(x+side*S*.035,S*.65)], fill=CROW_LIT, width=px(1))
        d.ellipse((S*.40,S*.35,S*.60,S*.70), fill=CROW)
        for side in (-1, 1):
            d.polygon([(S*(.5+side*.025),S*.38),(S*(.5+side*.088),S*.43),
                       (S*(.5+side*.055),S*.65),(S*.5,S*.69)], fill=(30,31,41))
        head=.32-reach
        d.ellipse((S*.455,S*(head-.055),S*.545,S*(head+.07)), fill=CROW)
        d.polygon([(S*.48,S*(head-.03)),(S*.52,S*(head-.03)),
                   (S*.5,S*(head-.115))], fill=CROW)
        finish_at(img, "Textures/RimArt/Dispersal/CrowFeed"+str(frame)+".png", CROW_SIZE)

    # Feeding birds surrounding a bone distinguish Carrion from the flying Murder icon.
    icon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon)
    bone = (178, 170, 152)
    d.line([(S*.43,S*.43),(S*.57,S*.57)], fill=bone, width=px(5))
    for x, y in ((.42,.42),(.45,.40),(.55,.60),(.59,.57)):
        d.ellipse((S*(x-.025),S*(y-.025),S*(x+.025),S*(y+.025)), fill=bone)
    bird = img.resize((int(S*.62),int(S*.62)), Image.LANCZOS)
    for angle, x, y in ((-90,.24,.49),(90,.76,.49),(0,.50,.79)):
        layer = bird.rotate(angle, resample=Image.BICUBIC)
        icon.alpha_composite(layer,(int(S*x-layer.width/2),int(S*y-layer.height/2)))
    finish(icon, "Textures/RimArt/Dispersal/IconCarrion.png")


def make_feather():
    """
    One feather, pointing north, for the ground the carrier is no longer standing on.

    Small enough that it is a mark rather than an object: at the size this is thrown it is
    two dozen pixels of something dark that was not there a second ago.
    """
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = S / 2

    d.polygon([
        (cx, S * 0.16),
        (cx + S * 0.11, S * 0.46),
        (cx + S * 0.06, S * 0.74),
        (cx, S * 0.84),
        (cx - S * 0.06, S * 0.74),
        (cx - S * 0.11, S * 0.46),
    ], fill=CROW)
    d.line([(cx, S * 0.18), (cx, S * 0.84)], fill=CROW_LIT, width=px(1.4))

    finish_at(img, "Textures/RimArt/Dispersal/Feather.png", 32)


def make_dispersal_icon():
    """
    The gene icon: three crows leaving, at three points in the same beat.

    A gizmo is read at 24 pixels. One bird at that size is a smudge; three at different
    sizes on a diagonal is a flock going somewhere, which is the whole gene.
    """
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    for phase, scale, dx, dy in (
        (0.25, 0.52, -px(30),  px(26)),
        (0.55, 0.44,  px(30),  px(6)),
        (0.80, 0.66,  -px(2), -px(20)),
    ):
        extension, sweepback = crow_beat(phase)
        layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        draw_crow(ImageDraw.Draw(layer), extension, sweepback)
        layer = layer.resize((int(S * scale), int(S * scale)), Image.LANCZOS)
        img.alpha_composite(layer, (int(S / 2 - layer.width / 2 + dx),
                                    int(S / 2 - layer.height / 2 + dy)))

    finish(img, "Textures/RimArt/Dispersal/IconGene.png")




# ---------------------------------------------------------------------------------------
# The stasis belt and its field icon.
#
# The gene grows an archite lattice through the chest. The belt is the same field in a
# box you can take off and hand to somebody else, which is the whole reason it exists -
# a panic button welded to one colonist is worth less than one you can move to whoever
# is about to be swarmed.
#
# Both are drawn from the dome's own colour, TimeBubbleDefaults.DomeColor (0.75, 0.90,
# 1.00) - off-white with a blue bias, ice rather than another bullet shield. Matching it
# here means the icon on the gizmo and the dome that appears on the map are recognisably
# the same object, which is the only thing stopping a stasis field being mistaken for a
# shield at a glance.
# ---------------------------------------------------------------------------------------

ICE_LIGHT = (233, 247, 255)
ICE       = (191, 230, 255)
ICE_MID   = (138, 191, 232)
ICE_DEEP  = (74, 126, 176)

STRAP      = (56, 49, 44)
STRAP_LIT  = (88, 77, 68)


def ring(d, cx, cy, r, colour, width):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=colour, width=width)


def stasis_core(img, cx, cy, r):
    """
    The emitter: a lens with the field already standing in it.

    Drawn as a filled sphere rather than an outline because a gizmo is read at 24 pixels -
    at that size concentric rings alone collapse into a grey smudge, and what has to
    survive the downsample is one bright disc.
    """
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=ICE_DEEP)
    d.ellipse([cx - int(r * .86), cy - int(r * .86),
               cx + int(r * .86), cy + int(r * .86)], fill=ICE_MID)
    d.ellipse([cx - int(r * .62), cy - int(r * .62),
               cx + int(r * .62), cy + int(r * .62)], fill=ICE)
    # Off-centre highlight: a sphere, not a disc.
    d.ellipse([cx - int(r * .40) - int(r * .22), cy - int(r * .40) - int(r * .22),
               cx + int(r * .14) - int(r * .22), cy + int(r * .14) - int(r * .22)],
              fill=ICE_LIGHT)


def make_stasis_belt():
    """
    The worn item. A strap that runs the full width, and a squared housing sitting on it.

    The housing is deliberately not a circle. A round bezel with rings inside it reads as
    a camera lens at any size, which is what the first pass of this looked like; a boxed
    emitter with one round lens in it reads as equipment.
    """
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    cy = S // 2
    half = px(17)

    # Strap, edge to edge, lit along the top so the band reads as round.
    d.rounded_rectangle([0, cy - half, S, cy + half], radius=px(6), fill=STRAP)
    d.rounded_rectangle([px(4), cy - half + px(3), S - px(4), cy - px(4)],
                        radius=px(4), fill=STRAP_LIT)

    # Stitching, which is most of what says "leather" at item size.
    for y in (cy - half + px(5), cy + half - px(5)):
        for x in range(px(10), S - px(10), px(12)):
            d.line([(x, y), (x + px(5), y)], fill=(38, 33, 30, 180), width=px(2))

    # Housing: a boxed emitter, wider than it is tall, bolted across the strap.
    hw, hh = px(34), px(27)
    d.rounded_rectangle([S // 2 - hw, cy - hh, S // 2 + hw, cy + hh], radius=px(7), fill=EDGE)
    d.rounded_rectangle([S // 2 - hw + px(3), cy - hh + px(3),
                         S // 2 + hw - px(3), cy + hh - px(3)], radius=px(6), fill=STEEL_DARK)
    d.rounded_rectangle([S // 2 - hw + px(5), cy - hh + px(5),
                         S // 2 + hw - px(5), cy - px(2)], radius=px(5), fill=STEEL)

    # Vent slots either side of the lens.
    for x in (S // 2 - px(26), S // 2 + px(22)):
        for i in range(3):
            d.rounded_rectangle([x, cy - px(8) + i * px(7), x + px(4), cy - px(5) + i * px(7)],
                                radius=px(1), fill=STEEL_DARK)

    stasis_core(img, S // 2, cy, px(15))

    finish(img, "Textures/RimArt/Stasis/Belt.png")


def make_stasis_icon():
    """
    The gizmo, shared by the gene and the belt.

    Three rings and a core, plus four streaks stopped short of it. The streaks are the
    point: a sphere alone says "shield", and a shield is the one thing this must not be
    mistaken for. Something frozen on its way in says the field stopped it.
    """
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    c = S // 2

    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    for r, a, w in ((px(58), 150, px(4)), (px(46), 200, px(5)), (px(34), 245, px(6))):
        ring(g, c, c, r, ICE_MID + (a,), w)
    glow = glow.filter(ImageFilter.GaussianBlur(px(0.8)))
    img.alpha_composite(glow)

    # Held mid-flight, tips pointing in, none of them touching the core.
    d = ImageDraw.Draw(img)
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        x0, y0 = c + dx * px(52), c + dy * px(52)
        x1, y1 = c + dx * px(34), c + dy * px(34)
        d.line([(x0, y0), (x1, y1)], fill=ICE_DEEP + (255,), width=px(6))

    stasis_core(img, c, c, px(24))
    finish(img, "Textures/RimArt/Stasis/IconStasis.png")


if __name__ == "__main__":
    make_flying()
    make_planted()
    make_icons()
    make_crows()
    make_feeding_crows()
    make_feather()
    make_dispersal_icon()
    make_stasis_belt()
    make_stasis_icon()
