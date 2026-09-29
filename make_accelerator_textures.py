#!/usr/bin/env python3
"""Accelerator's ability icons. Requires Pillow.

Run from any directory: python3 make_accelerator_textures.py

Icon*.png   the five ability icons, 128 px placeholders drawn in the kit's palette
            (Tools/VfxLab/web/sketches/lib/accelerator.js): white with black edges for what he
            controls, a pale blue only for the plasma, a grey stone for the pebble and the chunk.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Accelerator"
SIZE = 128
S = 4  # drawn at 4x, scaled down for smooth edges

WHITE, EDGE, AIR = (240, 242, 246, 255), (12, 12, 16, 255), (186, 199, 219, 255)
PLASMA, PLASMA_DEEP = (224, 237, 255, 255), (140, 178, 242, 255)
STONE, STONE_LIT = (92, 87, 84, 255), (150, 144, 138, 255)


def canvas():
    return Image.new("RGBA", (SIZE * S, SIZE * S), (0, 0, 0, 0))


def save(image, name):
    OUT.mkdir(parents=True, exist_ok=True)
    image = image.resize((SIZE, SIZE), Image.LANCZOS)
    image.save(OUT / name)
    print(f"{OUT / name}  {SIZE}x{SIZE}")


def p(x, y):
    """Icon coordinates 0..1 to pixels."""
    return (x * SIZE * S, y * SIZE * S)


def w(v):
    """A width in icon units to pixels."""
    return max(1, int(v * SIZE * S))


def stroke(draw, pts, width, colour=WHITE, edge=EDGE):
    """A white line with a black edge through points in icon units."""
    px = [p(*q) for q in pts]
    draw.line(px, fill=edge, width=w(width * 1.8), joint="curve")
    for q in (pts[0], pts[-1]):
        r = width * 0.9
        draw.ellipse([*p(q[0] - r, q[1] - r), *p(q[0] + r, q[1] + r)], fill=edge)
    draw.line(px, fill=colour, width=w(width), joint="curve")
    for q in (pts[0], pts[-1]):
        r = width * 0.5
        draw.ellipse([*p(q[0] - r, q[1] - r), *p(q[0] + r, q[1] + r)], fill=colour)


def arrow_head(draw, tip, degrees, size, colour=WHITE):
    a = math.radians(degrees)
    back = (tip[0] - math.cos(a) * size, tip[1] + math.sin(a) * size)
    side = (math.sin(a) * size * 0.6, math.cos(a) * size * 0.6)
    tri = [p(*tip), p(back[0] + side[0], back[1] + side[1]), p(back[0] - side[0], back[1] - side[1])]
    draw.polygon(tri, fill=EDGE)
    grow = 0.55
    cx, cy = sum(t[0] for t in tri) / 3, sum(t[1] for t in tri) / 3
    draw.polygon([(cx + (x - cx) * grow, cy + (y - cy) * grow) for x, y in tri], fill=colour)


def star(draw, cx, cy, r, points=8, seed=0):
    pts = []
    for i in range(points * 2):
        a = math.pi * i / points + seed
        rr = r if i % 2 == 0 else r * 0.35
        pts.append(p(cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    draw.polygon(pts, fill=EDGE)
    cx2, cy2 = p(cx, cy)
    draw.polygon([(cx2 + (x - cx2) * 0.7, cy2 + (y - cy2) * 0.7) for x, y in pts], fill=WHITE)


def rock(draw, cx, cy, r, seed=1):
    pts = []
    for i in range(9):
        a = i / 9 * math.tau
        rr = r * (0.75 + 0.25 * math.sin(i * 2.3 + seed))
        pts.append(p(cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    draw.polygon(pts, fill=STONE)
    draw.polygon([(x - r * 0.08 * SIZE * S + (x - p(cx, cy)[0]) * -0.35, y - r * 0.1 * SIZE * S + (y - p(cx, cy)[1]) * -0.35) for x, y in pts], fill=STONE_LIT)


def icon_manipulation():
    """Three bullets coming in from the right, each bent back at a white corner with a star."""
    image = canvas()
    draw = ImageDraw.Draw(image)
    for k, (y, turn) in enumerate(((0.3, 150), (0.52, 180), (0.74, -150))):
        corner = (0.42, y)
        stroke(draw, [(0.92, y), corner], 0.035, AIR)
        a = math.radians(turn)
        end = (corner[0] + math.cos(a) * -0.32, corner[1] - math.sin(a) * -0.32)
        stroke(draw, [corner, end], 0.045)
        arrow_head(draw, end, math.degrees(math.atan2(-(end[1] - corner[1]), end[0] - corner[0])), 0.09)
        star(draw, corner[0], corner[1], 0.07, 6, k)
    save(image, "IconManipulation.png")


def icon_surge():
    """A clock face whose hands crawl: a white dial with a black edge and four slowed arcs."""
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.ellipse([*p(0.16, 0.16), *p(0.84, 0.84)], fill=EDGE)
    draw.ellipse([*p(0.2, 0.2), *p(0.8, 0.8)], fill=WHITE)
    for i in range(12):
        a = i / 12 * math.tau
        draw.line([p(0.5 + math.cos(a) * 0.25, 0.5 + math.sin(a) * 0.25), p(0.5 + math.cos(a) * 0.29, 0.5 + math.sin(a) * 0.29)], fill=EDGE, width=w(0.015))
    draw.line([p(0.5, 0.5), p(0.5, 0.29)], fill=EDGE, width=w(0.035))
    draw.line([p(0.5, 0.5), p(0.64, 0.58)], fill=EDGE, width=w(0.03))
    for r in (0.38, 0.44):
        draw.arc([*p(0.5 - r, 0.5 - r), *p(0.5 + r, 0.5 + r)], 200, 260, fill=AIR, width=w(0.02))
    save(image, "IconSurge.png")


def icon_shove():
    """A hand's touch flash on the left and a long arrow thrown to the right with speed lines."""
    image = canvas()
    draw = ImageDraw.Draw(image)
    for i, y in enumerate((0.36, 0.5, 0.64)):
        draw.line([p(0.3, y), p(0.62 - 0.08 * (i % 2), y)], fill=EDGE, width=w(0.025))
    stroke(draw, [(0.22, 0.5), (0.8, 0.5)], 0.06)
    arrow_head(draw, (0.93, 0.5), 0, 0.17)
    draw.ellipse([*p(0.1, 0.38), *p(0.34, 0.62)], fill=EDGE)
    draw.ellipse([*p(0.13, 0.41), *p(0.31, 0.59)], fill=WHITE)
    save(image, "IconShove.png")


def icon_plasma():
    """The plasma ball: a blue halo, a pale shell with a black edge, rays and a white core."""
    image = canvas()
    draw = ImageDraw.Draw(image)
    for r, colour in ((0.44, (140, 178, 242, 70)), (0.38, (140, 178, 242, 120))):
        draw.ellipse([*p(0.5 - r, 0.5 - r), *p(0.5 + r, 0.5 + r)], fill=colour)
    for i in range(8):
        a = i / 8 * math.tau + 0.2
        draw.line([p(0.5 + math.cos(a) * 0.2, 0.5 + math.sin(a) * 0.2), p(0.5 + math.cos(a) * (0.36 + 0.06 * (i % 2)), 0.5 + math.sin(a) * (0.36 + 0.06 * (i % 2)))], fill=WHITE, width=w(0.03))
    draw.ellipse([*p(0.27, 0.27), *p(0.73, 0.73)], fill=EDGE)
    draw.ellipse([*p(0.29, 0.29), *p(0.71, 0.71)], fill=PLASMA)
    draw.ellipse([*p(0.38, 0.38), *p(0.62, 0.62)], fill=WHITE)
    for k in range(3):
        a0 = k * 120 + 20
        draw.arc([*p(0.33, 0.33), *p(0.67, 0.67)], a0, a0 + 70, fill=PLASMA_DEEP, width=w(0.025))
    save(image, "IconPlasma.png")


def icon_flick():
    """A pebble flying right with a white trail edged black and two cone lines at its head."""
    image = canvas()
    draw = ImageDraw.Draw(image)
    stroke(draw, [(0.1, 0.72), (0.68, 0.38)], 0.05)
    for off in (-0.07, 0.07):
        draw.line([p(0.62, 0.38 + off), p(0.78, 0.36 + off * 0.3)], fill=EDGE, width=w(0.02))
    rock(draw, 0.77, 0.33, 0.14)
    save(image, "IconFlick.png")


if __name__ == "__main__":
    icon_manipulation()
    icon_surge()
    icon_shove()
    icon_plasma()
    icon_flick()
