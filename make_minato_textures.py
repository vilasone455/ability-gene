#!/usr/bin/env python3
"""The four ability icons of Minato's kit, placeholders drawn from the pictures' own shapes. Requires Pillow.

Run from any directory: python3 make_minato_textures.py

IconThunderGodJump.png   his three-pronged kunai and the star glint of the arrival
IconThunderGodChain.png  three glints joined by the route line
IconGuidingThunder.png   the ring of script with a shot going in and a gold line coming out at a kunai
IconRasengan.png         the ball: blue shell, white orbit rings and core

Colours are Source/RimArt/ThunderGod/ThunderGodGraphics.cs's Gold and Pale, and RasenganGraphics.cs's blues.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent / "Textures/RimArt/ThunderGod"
SIZE = 128

GOLD, PALE, WHITE = (255, 199, 46), (255, 242, 173), (255, 255, 255)
STEEL, HANDLE, INK = (196, 206, 222), (232, 226, 206), (20, 18, 16)
BLUE, DEEP, SKY, ICE = (77, 158, 255), (26, 82, 230), (128, 199, 255), (199, 232, 255)
TRACER = (242, 230, 204)


def canvas():
    return Image.new("RGBA", (SIZE * 4, SIZE * 4), (0, 0, 0, 0))


def glowing(image, radius):
    halo = image.filter(ImageFilter.GaussianBlur(radius))
    return Image.alpha_composite(halo, image)


def save(image, name):
    image = image.resize((SIZE, SIZE), Image.LANCZOS)
    OUT.mkdir(parents=True, exist_ok=True)
    image.save(OUT / name)
    print(f"{OUT / name}  {SIZE}x{SIZE}")


def star(draw, c, r, alpha=255):
    """The arrival glint: a core, 2 long rays and 2 short diagonal ones."""
    x, y = c
    for angle, reach, width in ((0, 1.25, 0.1), (90, 1.0, 0.1), (45, 0.5, 0.06), (135, 0.5, 0.06)):
        dx, dy = math.cos(math.radians(angle)) * r * reach, math.sin(math.radians(angle)) * r * reach
        draw.line([(x - dx, y - dy), (x + dx, y + dy)], fill=GOLD + (alpha,), width=max(2, int(r * width * 2.6)))
        draw.line([(x - dx, y - dy), (x + dx, y + dy)], fill=PALE + (alpha,), width=max(1, int(r * width)))
    k = r * 0.28
    draw.ellipse([x - k, y - k, x + k, y + k], fill=WHITE + (alpha,))


def minato_kunai(draw, ring, point, s):
    """His kunai from the ring to the point: long pale handle, a narrow blade and one crescent of prongs."""
    dx, dy = point[0] - ring[0], point[1] - ring[1]
    length = math.hypot(dx, dy)
    ux, uy = dx / length, dy / length
    nx, ny = -uy, ux

    def at(u, side=0.0):
        return (ring[0] + dx * u + nx * side, ring[1] + dy * u + ny * side)

    r = s * 0.05
    draw.ellipse([ring[0] - r, ring[1] - r, ring[0] + r, ring[1] + r], outline=STEEL + (255,), width=int(s * 0.014))
    draw.line([at(0.08), at(0.45)], fill=HANDLE + (255,), width=int(s * 0.04))
    for k in range(4):
        draw.line([at(0.14 + 0.08 * k, -s * 0.012), at(0.14 + 0.08 * k, s * 0.012)], fill=INK + (255,), width=int(s * 0.008))
    # The crescent: a curve of prongs across the base of the blade, tips forward.
    crescent = [at(0.5 + 0.1 * math.cos(math.radians(a)) ** 2, s * 0.13 * math.sin(math.radians(a))) for a in range(-90, 91, 10)]
    draw.line(crescent, fill=STEEL + (255,), width=int(s * 0.026))
    blade = [at(0.46, s * 0.03), at(0.9, s * 0.02), at(1.0), at(0.9, -s * 0.02), at(0.46, -s * 0.03)]
    draw.polygon(blade, fill=STEEL + (255,))
    draw.line([at(0.48), at(0.98)], fill=WHITE + (255,), width=max(1, int(s * 0.006)))


def icon_jump():
    image = canvas()
    draw = ImageDraw.Draw(image)
    s = SIZE * 4
    draw.line([(s * 0.14, s * 0.84), (s * 0.66, s * 0.34)], fill=GOLD + (120,), width=int(s * 0.03))
    minato_kunai(draw, (s * 0.2, s * 0.8), (s * 0.58, s * 0.42), s)
    star(draw, (s * 0.7, s * 0.3), s * 0.22)
    save(glowing(image, 6), "IconThunderGodJump.png")


def icon_chain():
    image = canvas()
    draw = ImageDraw.Draw(image)
    s = SIZE * 4
    points = [(0.14, 0.78), (0.4, 0.3), (0.62, 0.7), (0.86, 0.24)]
    points = [(s * x, s * y) for x, y in points]
    draw.line(points, fill=GOLD + (200,), width=int(s * 0.03), joint="curve")
    draw.line(points, fill=PALE + (255,), width=int(s * 0.012), joint="curve")
    for i, p in enumerate(points[1:]):
        star(draw, p, s * (0.11 + 0.02 * i))
    save(glowing(image, 6), "IconThunderGodChain.png")


def icon_guiding():
    image = canvas()
    draw = ImageDraw.Draw(image)
    s = SIZE * 4
    c, r = (s * 0.42, s * 0.56), s * 0.26
    draw.ellipse([c[0] - r * 1.12, c[1] - r * 1.12, c[0] + r * 1.12, c[1] + r * 1.12], outline=GOLD + (220,), width=int(s * 0.01))
    draw.ellipse([c[0] - r * 0.88, c[1] - r * 0.88, c[0] + r * 0.88, c[1] + r * 0.88], outline=GOLD + (170,), width=int(s * 0.008))
    for k in range(20):
        a = math.radians(k * 18)
        x, y = c[0] + math.cos(a) * r, c[1] + math.sin(a) * r
        tx, ty = -math.sin(a) * s * 0.02, math.cos(a) * s * 0.02
        draw.line([(x - math.cos(a) * s * 0.025, y - math.sin(a) * s * 0.025), (x + math.cos(a) * s * 0.025, y + math.sin(a) * s * 0.025)],
                  fill=GOLD + (255,), width=int(s * 0.009))
        if k % 2:
            draw.line([(x, y), (x + tx, y + ty)], fill=GOLD + (255,), width=int(s * 0.008))
    # A shot comes in from the lower left and is taken at the ring...
    entry = (c[0] - r * 0.7, c[1] + r * 0.7)
    draw.line([(s * 0.02, s * 0.98), entry], fill=TRACER + (200,), width=int(s * 0.016))
    star(draw, entry, s * 0.07)
    # ...and comes out at the kunai, up to the right.
    out = (s * 0.84, s * 0.18)
    draw.line([entry, out], fill=GOLD + (170,), width=int(s * 0.022))
    draw.line([entry, out], fill=PALE + (255,), width=int(s * 0.008))
    star(draw, out, s * 0.12)
    save(glowing(image, 6), "IconGuidingThunder.png")


def icon_rasengan():
    image = canvas()
    draw = ImageDraw.Draw(image)
    s = SIZE * 4
    c, r = s / 2, s * 0.3
    halo = Image.new("RGBA", image.size, (0, 0, 0, 0))
    ImageDraw.Draw(halo).ellipse([c - r * 1.5, c - r * 1.5, c + r * 1.5, c + r * 1.5], fill=BLUE + (150,))
    image = Image.alpha_composite(image, halo.filter(ImageFilter.GaussianBlur(s * 0.06)))
    draw = ImageDraw.Draw(image)
    draw.ellipse([c - r, c - r, c + r, c + r], fill=BLUE + (255,), outline=DEEP + (255,), width=int(s * 0.016))
    draw.ellipse([c - r * 0.84, c - r * 0.84, c + r * 0.84, c + r * 0.84], fill=SKY + (230,))
    # Orbit rings: ellipses turned round the centre, drawn on their own layer and rotated.
    for k, (flat, turn) in enumerate(((0.35, 20), (0.5, 80), (0.25, 140), (0.6, 170))):
        layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).ellipse([c - r * 0.95, c - r * 0.95 * flat, c + r * 0.95, c + r * 0.95 * flat],
                                      outline=WHITE + (220,), width=int(s * 0.012))
        image = Image.alpha_composite(image, layer.rotate(turn, center=(c, c), resample=Image.BICUBIC))
    draw = ImageDraw.Draw(image)
    k = r * 0.3
    draw.ellipse([c - k, c - k, c + k, c + k], fill=WHITE + (255,))
    save(glowing(image, 5), "IconRasengan.png")


icon_jump()
icon_chain()
icon_guiding()
icon_rasengan()
