#!/usr/bin/env python3
"""Gojo's ability icons. Requires Pillow.

Run from any directory: python3 make_gojo_icons.py

Icon*.png   128 px placeholders in the sketches' colours (Tools/VfxLab/web/sketches/gojo-*.js):
            Blue a royal-blue ball with a cyan edge and a white point, Red a shaded red ball with
            two white crescents, Purple a plum ball with a white core and lilac bolts, Void the
            black hole (black disc, pale rim, gold ring) on the void's navy.
"""
from pathlib import Path
import math

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "Textures/RimArt/Gojo"
SIZE = 128
S = 4  # drawn at 4x, scaled down for smooth edges


def canvas(background=None):
    return Image.new("RGBA", (SIZE * S, SIZE * S), background or (0, 0, 0, 0))


def save(image, name):
    OUT.mkdir(parents=True, exist_ok=True)
    image = image.resize((SIZE, SIZE), Image.LANCZOS)
    image.save(OUT / name)
    print(f"{OUT / name}  {SIZE}x{SIZE}")


def p(x, y):
    """Icon coordinates 0..1 to pixels."""
    return (x * SIZE * S, y * SIZE * S)


def w(v):
    return max(1, int(v * SIZE * S))


def disc(draw, cx, cy, r, fill, outline=None, width=0.0):
    draw.ellipse([*p(cx - r, cy - r), *p(cx + r, cy + r)], fill=fill, outline=outline, width=w(width) if outline else 0)


def shaded_ball(draw, cx, cy, r, dark, lit, steps=14):
    """A ball lit from the upper left: rings from the dark edge to the lit spot."""
    for i in range(steps):
        f = i / (steps - 1)
        rr = r * (1 - 0.72 * f)
        ox, oy = -0.28 * r * f, -0.28 * r * f
        colour = tuple(int(dark[k] + (lit[k] - dark[k]) * f) for k in range(3)) + (255,)
        disc(draw, cx + ox, cy + oy, rr, colour)


def icon_blue():
    image = canvas()
    draw = ImageDraw.Draw(image)
    for i in range(3):
        disc(draw, 0.5, 0.5, 0.44 - i * 0.03, None, (80, 220, 255, 90 + 60 * i), 0.012)
    shaded_ball(draw, 0.5, 0.5, 0.34, (18, 32, 110), (70, 120, 235))
    for a in range(0, 360, 90):
        pts = [p(0.5 + math.cos(math.radians(a + t * 3)) * (0.08 + t * 0.0045), 0.5 + math.sin(math.radians(a + t * 3)) * (0.08 + t * 0.0045)) for t in range(0, 50)]
        draw.line(pts, fill=(170, 225, 255, 200), width=w(0.018))
    disc(draw, 0.5, 0.5, 0.045, (255, 255, 255, 255))
    save(image, "IconBlue.png")


def icon_red():
    image = canvas()
    draw = ImageDraw.Draw(image)
    disc(draw, 0.5, 0.5, 0.44, (255, 90, 120, 60))
    shaded_ball(draw, 0.5, 0.5, 0.33, (90, 6, 14), (235, 50, 50))
    for a0 in (20, 200):
        box = [*p(0.5 - 0.39, 0.5 - 0.39), *p(0.5 + 0.39, 0.5 + 0.39)]
        draw.arc(box, a0, a0 + 110, fill=(255, 245, 245, 255), width=w(0.035))
    save(image, "IconRed.png")


def icon_purple():
    image = canvas()
    draw = ImageDraw.Draw(image)
    disc(draw, 0.5, 0.5, 0.46, (190, 120, 255, 60))
    shaded_ball(draw, 0.5, 0.5, 0.36, (52, 10, 70), (160, 80, 210))
    for a in (30, 150, 260):
        r0, r1 = 0.12, 0.34
        mid = math.radians(a + 12)
        pts = [p(0.5 + math.cos(math.radians(a)) * r0, 0.5 + math.sin(math.radians(a)) * r0),
               p(0.5 + math.cos(mid) * 0.24, 0.5 + math.sin(mid) * 0.24),
               p(0.5 + math.cos(math.radians(a - 6)) * r1, 0.5 + math.sin(math.radians(a - 6)) * r1)]
        draw.line(pts, fill=(235, 205, 255, 255), width=w(0.022))
    disc(draw, 0.5, 0.5, 0.1, (255, 240, 255, 255))
    save(image, "IconPurple.png")


def icon_void():
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([*p(0.04, 0.04), *p(0.96, 0.96)], radius=w(0.12), fill=(22, 24, 58, 255))
    for i, (x, y) in enumerate([(0.15, 0.2), (0.82, 0.16), (0.2, 0.84), (0.86, 0.78), (0.62, 0.9), (0.1, 0.55)]):
        disc(draw, x, y, 0.012 + 0.006 * (i % 2), (235, 235, 255, 255))
    disc(draw, 0.5, 0.5, 0.36, None, (255, 205, 150, 255), 0.03)
    disc(draw, 0.5, 0.5, 0.29, (200, 215, 235, 255))
    disc(draw, 0.5, 0.5, 0.26, (0, 0, 0, 255))
    draw.arc([*p(0.24, 0.24), *p(0.76, 0.76)], 190, 320, fill=(120, 230, 230, 255), width=w(0.015))
    save(image, "IconVoid.png")


if __name__ == "__main__":
    icon_blue()
    icon_red()
    icon_purple()
    icon_void()
