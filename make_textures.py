#!/usr/bin/env python3
"""
Draws the panoply organ's two blade sprites.

The rest of this mod ships no art, and for twelve genes that was the right call - each of
them dresses itself in a Core texture that already means the right thing. This gene cannot.
A planted blade has to read as standing *in* the ground, and every weapon texture in the
game is a sword photographed from directly above while lying flat. No rotation of a top-down
sprite produces a side-on one, so these two are drawn rather than borrowed.

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
    finish(img, "Textures/AbilityGenes/Panoply/Blade.png")


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

    finish(img, "Textures/AbilityGenes/Panoply/BladePlanted.png")


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
    finish(img, "Textures/AbilityGenes/Panoply/IconRain.png")

    # LOOSE - a volley, all of it going the same way, tips leading.
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img = place(img, sword, angle=64, scale=0.60, dx=-px(14), dy=-px(30))
    img = place(img, sword, angle=78, scale=0.60, dx=-px(24), dy=px(4))
    img = place(img, sword, angle=71, scale=0.72, dx=-px(4), dy=-px(6))
    finish(img, "Textures/AbilityGenes/Panoply/IconLoose.png")

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
    finish(img, "Textures/AbilityGenes/Panoply/IconGrasp.png")

    # GENE - a panoply: what the carrier is, rather than any one thing they do.
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img = place(img, sword, angle=-34, scale=0.80, dx=-px(20), dy=px(4))
    img = place(img, sword, angle=34, scale=0.80, dx=px(20), dy=px(4))
    img = place(img, sword, angle=0, scale=0.92, dx=0, dy=0)
    finish(img, "Textures/AbilityGenes/Panoply/IconGene.png")


if __name__ == "__main__":
    make_flying()
    make_planted()
    make_icons()
