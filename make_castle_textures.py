#!/usr/bin/env python3
"""The Infinity Castle's textures. Requires Pillow.

Run from any directory: python3 make_castle_textures.py

Void.png    the void terrain: flat VoidDeep, opaque. The mod draws the rooms at other depths and
            the void's fog just above it.
Floor.png   the castle floor terrain: flat WoodFloor. The mod draws every room's floor over it.
Wall.png    the castle wall: flat WallWood. The mod draws the walls over it.
Blank.png   the lantern: nothing. Its light is a glower; the mod draws the lantern itself.

All four are fallbacks under the mod's own drawing (Source/RimArt/InfinityCastle), in the colours of
the lab's lib/infinity-castle.js, so a spot the mod does not draw still reads as the castle.

Biwa.png    Nakime's hero weapon, 128x128: the lab's biwa (lib/infinity-castle.js nakime()) laid
            diagonally, pear-shaped body at the lower left, neck and bent pegbox to the upper right,
            four strings.
Icon*.png   the ability and its six commands, placeholders drawn from flat shapes in the castle's
            colours: Infinity Castle (a shoji door in a pale ring), Shift (a room and an arrow), Drop
            (a floor door and a down arrow), Seal (a doorway barred), Crush (walls closing in), Summon
            (a floor door and an up arrow), Release (the strum's rings).
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/InfinityCastle"
SIZE = 64
ICON = 128
S = ICON * 4

# lib/infinity-castle.js: VoidDeep, WoodFloor, WallWood.
COLOURS = {
    "Void": (0.010, 0.008, 0.016, 1.0),
    "Floor": (0.40, 0.25, 0.14, 1.0),
    "Wall": (0.14, 0.08, 0.045, 1.0),
    "Blank": (0.0, 0.0, 0.0, 0.0),
}


def rgb(r, g, b, a=1.0):
    return (round(r * 255), round(g * 255), round(b * 255), round(a * 255))


# The lab's palette.
WALL = rgb(0.14, 0.08, 0.045)
WALL_TOP = rgb(0.30, 0.18, 0.10)
LACQUER = rgb(0.52, 0.12, 0.07)
PAPER = rgb(0.88, 0.82, 0.68)
LATTICE = rgb(0.22, 0.13, 0.07)
STRUM = rgb(1.0, 0.93, 0.78)
VOID = rgb(0.010, 0.008, 0.016)
BIWA_WOOD = rgb(0.46, 0.25, 0.11)
BIWA_FACE = rgb(0.66, 0.44, 0.23)
BIWA_DARK = rgb(0.17, 0.09, 0.04)
STRING = rgb(0.85, 0.80, 0.66)


def write(name, rgba):
    OUT.mkdir(parents=True, exist_ok=True)
    pixel = tuple(round(v * 255) for v in rgba)
    image = Image.new("RGBA", (SIZE, SIZE), pixel)
    path = OUT / f"{name}.png"
    image.save(path)
    print(f"{path} {SIZE}x{SIZE} {pixel}")


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def save(image, name):
    OUT.mkdir(parents=True, exist_ok=True)
    image = image.resize((ICON, ICON), Image.LANCZOS)
    image.save(OUT / name)
    print(f"{OUT / name} {ICON}x{ICON}")


def p(x, y):
    """A point from 0..1 fractions of the canvas, y down."""
    return (x * S, y * S)


def biwa():
    """The body along an axis 45 degrees up to the right; the lab's outline: half-width 0.15 * sqrt(sin) tapering 45 %."""
    image = canvas()
    draw = ImageDraw.Draw(image)
    ax, ay = math.cos(math.radians(45)), -math.sin(math.radians(45))
    nx, ny = -ay, ax
    b0 = (0.2, 0.8)
    k = 1.55

    def along(t, w=0.0):
        return p(b0[0] + (ax * t + nx * w) * k, b0[1] + (ay * t + ny * w) * k)

    def outline(side, inset=0.0):
        pts = []
        for i in range(25):
            t = i / 24
            hw = 0.15 * math.sqrt(max(0.0, math.sin(t * math.pi * 0.92 + 0.05))) * (1 - 0.45 * t)
            pts.append(along(t * 0.42, side * hw * (1 - inset)))
        return pts

    back = outline(-1) + outline(1)[::-1]
    draw.polygon(back, fill=BIWA_DARK)
    mid = [along(i / 24 * 0.42) for i in range(25)]
    draw.polygon(outline(-1, 0.12) + mid[::-1], fill=BIWA_FACE)
    draw.polygon(mid + outline(1, 0.12)[::-1], fill=BIWA_WOOD)
    draw.polygon([along(0.19, -0.12), along(0.19, 0.12), along(0.26, 0.1), along(0.26, -0.1)], fill=rgb(0.13, 0.09, 0.08))
    draw.polygon([along(0.07, -0.07), along(0.07, 0.07), along(0.095, 0.07), along(0.095, -0.07)], fill=BIWA_DARK)
    # Neck, then the pegbox bent back.
    draw.polygon([along(0.38, -0.03), along(0.66, -0.025), along(0.66, 0.025), along(0.38, 0.03)], fill=BIWA_DARK)
    n1 = along(0.66)
    bend = math.radians(45 + 63)
    bent = (n1[0] + math.cos(bend) * 0.11 * k * S, n1[1] - math.sin(bend) * 0.11 * k * S)
    wx, wy = nx * 0.03 * k * S, ny * 0.03 * k * S
    draw.polygon([(n1[0] - wx, n1[1] - wy), (bent[0] - wx * 0.7, bent[1] - wy * 0.7), (bent[0] + wx * 0.7, bent[1] + wy * 0.7), (n1[0] + wx, n1[1] + wy)], fill=BIWA_DARK)
    for side in (-1, 1):
        for t in (0.3, 0.7):
            px, py = n1[0] + (bent[0] - n1[0]) * t, n1[1] + (bent[1] - n1[1]) * t
            draw.line([(px, py), (px + side * nx * 0.06 * k * S, py + side * ny * 0.06 * k * S)], fill=WALL_TOP, width=int(S * 0.012))
    for i in range(4):
        w = (i - 1.5) * 0.018
        draw.line([along(0.08, w), along(0.66, w * 0.6)], fill=STRING, width=max(2, int(S * 0.006)))
    save(image, "Biwa.png")


def room(draw, x0, y0, x1, y1, width=0.05):
    draw.rectangle([p(x0, y0), p(x1, y1)], fill=WALL)
    draw.rectangle([p(x0 + width, y0 + width), p(x1 - width, y1 - width)], fill=rgb(0.40, 0.25, 0.14))


def arrow(draw, x, y, dx, dy, length, colour=STRUM, width=0.06):
    ex, ey = x + dx * length, y + dy * length
    draw.line([p(x, y), p(ex, ey)], fill=colour, width=int(S * width))
    nx, ny = -dy, dx
    head = 0.12
    draw.polygon([p(ex + dx * head, ey + dy * head), p(ex + nx * head * 0.9, ey + ny * head * 0.9), p(ex - nx * head * 0.9, ey - ny * head * 0.9)], fill=colour)


def floor_door(draw, cx, cy, r):
    draw.rectangle([p(cx - r, cy - r), p(cx + r, cy + r)], fill=WALL)
    draw.rectangle([p(cx - r * 0.78, cy - r * 0.78), p(cx + r * 0.78, cy + r * 0.78)], fill=VOID)
    draw.rectangle([p(cx - r * 0.78, cy - r * 0.78), p(cx - r * 0.5, cy + r * 0.78)], fill=PAPER)
    draw.rectangle([p(cx + r * 0.5, cy - r * 0.78), p(cx + r * 0.78, cy + r * 0.78)], fill=PAPER)


def icon_castle():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.ellipse([p(0.05, 0.05), p(0.95, 0.95)], outline=STRUM, width=int(S * 0.04))
    draw.rectangle([p(0.24, 0.2), p(0.76, 0.8)], fill=WALL)
    for i in range(2):
        x0 = 0.28 + i * 0.25
        draw.rectangle([p(x0, 0.24), p(x0 + 0.2, 0.76)], fill=PAPER)
        for k in range(1, 4):
            draw.line([p(x0, 0.24 + k * 0.13), p(x0 + 0.2, 0.24 + k * 0.13)], fill=LATTICE, width=int(S * 0.012))
        draw.line([p(x0 + 0.1, 0.24), p(x0 + 0.1, 0.76)], fill=LATTICE, width=int(S * 0.012))
    draw.rectangle([p(0.24, 0.8), p(0.76, 0.84)], fill=LACQUER)
    save(image, "IconInfinityCastle.png")


def icon_shift():
    image = canvas()
    draw = ImageDraw.Draw(image)
    room(draw, 0.08, 0.3, 0.48, 0.7)
    arrow(draw, 0.52, 0.5, 1, 0, 0.28)
    save(image, "IconShift.png")


def icon_drop():
    image = canvas()
    draw = ImageDraw.Draw(image)
    floor_door(draw, 0.5, 0.66, 0.26)
    arrow(draw, 0.5, 0.06, 0, 1, 0.22)
    save(image, "IconDrop.png")


def icon_seal():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.rectangle([p(0.1, 0.12), p(0.3, 0.88)], fill=WALL)
    draw.rectangle([p(0.7, 0.12), p(0.9, 0.88)], fill=WALL)
    draw.rectangle([p(0.3, 0.18), p(0.7, 0.82)], fill=PAPER)
    draw.line([p(0.5, 0.18), p(0.5, 0.82)], fill=LATTICE, width=int(S * 0.02))
    draw.rectangle([p(0.08, 0.44), p(0.92, 0.56)], fill=LACQUER)
    save(image, "IconSeal.png")


def icon_crush():
    image = canvas()
    draw = ImageDraw.Draw(image)
    room(draw, 0.12, 0.12, 0.88, 0.88, 0.14)
    for dx, dy, x, y in ((1, 0, 0.18, 0.5), (-1, 0, 0.82, 0.5), (0, 1, 0.5, 0.18), (0, -1, 0.5, 0.82)):
        arrow(draw, x, y, dx, dy, 0.12, LACQUER, 0.05)
    save(image, "IconCrush.png")


def icon_summon():
    image = canvas()
    draw = ImageDraw.Draw(image)
    floor_door(draw, 0.5, 0.66, 0.26)
    arrow(draw, 0.5, 0.36, 0, -1, 0.18)
    save(image, "IconSummon.png")


def icon_release():
    image = canvas()
    draw = ImageDraw.Draw(image)
    for r, a in ((0.42, 0.45), (0.3, 0.7), (0.18, 1.0)):
        colour = STRUM[:3] + (round(255 * a),)
        draw.ellipse([p(0.5 - r, 0.5 - r), p(0.5 + r, 0.5 + r)], outline=colour, width=int(S * 0.04))
    save(image, "IconRelease.png")


if __name__ == "__main__":
    for name, rgba in COLOURS.items():
        write(name, rgba)
    biwa()
    icon_castle()
    icon_shift()
    icon_drop()
    icon_seal()
    icon_crush()
    icon_summon()
    icon_release()
